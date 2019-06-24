using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class AsyncReader<T> :
        ReaderBase<T>,
        IAsyncReader<T>,
        ITestableAsyncDisposable
    {
        public bool IsDisposed => Inner == null;
        private TextReader Inner;

        internal AsyncReader(TextReader inner, ConcreteBoundConfiguration<T> config, object context) : base(config, context)
        {
            Inner = inner;
        }

        public ValueTask<List<T>> ReadAllAsync(List<T> into, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            if (into == null)
            {
                Throw.ArgumentNullException(nameof(into));
            }

            return ReadAllIntoListAsync(into, cancel);
        }

        public ValueTask<List<T>> ReadAllAsync(CancellationToken cancel = default)
        => ReadAllAsync(new List<T>(), cancel);

        private ValueTask<List<T>> ReadAllIntoListAsync(List<T> into, CancellationToken cancel)
        {
            AssertNotDisposed();

            if (into == null)
            {
                Throw.ArgumentNullException(nameof(into));
            }

            while (true)
            {
                var resTask = TryReadAsync(cancel);
                if (resTask.IsCompletedSuccessfully)
                {
                    var res = resTask.Result;

                    if (res.HasValue)
                    {
                        into.Add(res.Value);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    return ReadAllAsync_ContinueAfterTryReadAsync(this, resTask, into, cancel);
                }
            }

            return new ValueTask<List<T>>(into);

            // wait for a tryreadasync to finish, then continue async
            static async ValueTask<List<T>> ReadAllAsync_ContinueAfterTryReadAsync(AsyncReader<T> self, ValueTask<ReadResult<T>> waitFor, List<T> ret, CancellationToken cancel)
            {
                var other = await waitFor;
                if (other.HasValue)
                {
                    ret.Add(other.Value);
                }
                else
                {
                    return ret;
                }

                while (true)
                {
                    var res = await self.TryReadAsync(cancel);
                    if (res.HasValue)
                    {
                        ret.Add(res.Value);
                    }
                    else
                    {
                        break;
                    }
                }

                return ret;
            }
        }

        public IAsyncEnumerable<T> EnumerateAllAsync()
        {
            AssertNotDisposed();

            return new AsyncEnumerable<T>(this);
        }

        public ValueTask<ReadResult<T>> TryReadWithReuseAsync(ref T row, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            var tryReadTask = TryReadInnerAsync(false, ref row, cancel);
            if (!tryReadTask.IsCompletedSuccessfully)
            {
                return TryReadWithReuseAsync_ContinueAfterTryReadInnerAsync(tryReadTask, cancel);
            }

            var res = tryReadTask.Result;
            switch (res.ResultType)
            {
                case ReadWithCommentResultType.HasValue:
                    return new ValueTask<ReadResult<T>>(new ReadResult<T>(res.Value));
                case ReadWithCommentResultType.NoValue:
                    return new ValueTask<ReadResult<T>>(ReadResult<T>.Empty);
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}");
                    // just for control flow
                    return default;
            }

            // wait for the inner call to finish
            static async ValueTask<ReadResult<T>> TryReadWithReuseAsync_ContinueAfterTryReadInnerAsync(ValueTask<ReadWithCommentResult<T>> waitFor, CancellationToken cancel)
            {
                var res = await waitFor;

                switch (res.ResultType)
                {
                    case ReadWithCommentResultType.HasValue:
                        return new ReadResult<T>(res.Value);
                    case ReadWithCommentResultType.NoValue:
                        return ReadResult<T>.Empty;
                    default:
                        Throw.InvalidOperationException($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}");
                        // just for control flow
                        return default;
                }
            }
        }

        public ValueTask<ReadResult<T>> TryReadAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed();

            var record = default(T);
            return TryReadWithReuseAsync(ref record, cancel);
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed();

            var record = default(T);
            return TryReadWithCommentReuseAsync(ref record, cancel);
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentReuseAsync(ref T record, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            return TryReadInnerAsync(true, ref record, cancel);
        }

        private ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync(bool returnComments, ref T record, CancellationToken cancel)
        {
            if (RowEndings == null)
            {
                var handleLineEndingsTask = HandleLineEndingsAsync(cancel);
                if (!handleLineEndingsTask.IsCompletedSuccessfully)
                {
                    var row = GuaranteeRecord(this, ref record);
                    return TryReadInnerAsync_ContinueAfterHandleLineEndingsAsync(this, handleLineEndingsTask, returnComments, row, cancel);
                }
            }

            if (ReadHeaders == null)
            {
                var handleHeadersTask = HandleHeadersAsync(cancel);
                if (!handleHeadersTask.IsCompletedSuccessfully)
                {
                    var row = GuaranteeRecord(this, ref record);
                    return TryReadInnerAsync_ContinueAfterHandleHeadersAsync(this, handleHeadersTask, returnComments, row, cancel);
                }
            }

            while (true)
            {
                PreparingToWriteToBuffer();
                var availableTask = Buffer.ReadAsync(Inner, cancel);
                if (!availableTask.IsCompletedSuccessfully)
                {
                    var row = GuaranteeRecord(this, ref record);
                    return TryReadInnerAsync_ContinueAfterReadAsync(this, availableTask, returnComments, row, cancel);
                }

                var available = availableTask.Result;
                if (available == 0)
                {
                    EndOfData();

                    if (HasValueToReturn)
                    {
                        record = GetValueForReturn();
                        return new ValueTask<ReadWithCommentResult<T>>(new ReadWithCommentResult<T>(record));
                    }

                    if (HasCommentToReturn)
                    {
                        HasCommentToReturn = false;
                        if (returnComments)
                        {
                            var comment = Partial.PendingAsString(Buffer.Buffer);
                            return new ValueTask<ReadWithCommentResult<T>>(new ReadWithCommentResult<T>(comment));
                        }
                    }

                    // intentionally _not_ modifying record here
                    return new ValueTask<ReadWithCommentResult<T>>(ReadWithCommentResult<T>.Empty);
                }

                if (!HasValueToReturn)
                {
                    record = GuaranteeRecord(this, ref record);
                    SetValueToPopulate(record);
                }

                var res = AdvanceWork(available);
                if (res == ReadWithCommentResultType.HasValue)
                {
                    record = GetValueForReturn();
                    return new ValueTask<ReadWithCommentResult<T>>(new ReadWithCommentResult<T>(record));
                }
                if (res == ReadWithCommentResultType.HasComment)
                {
                    HasCommentToReturn = false;

                    if (returnComments)
                    {
                        // only actually allocate for the comment if it's been asked for
                        var comment = Partial.PendingAsString(Buffer.Buffer);
                        Partial.ClearValue();
                        Partial.ClearBuffer();
                        return new ValueTask<ReadWithCommentResult<T>>(new ReadWithCommentResult<T>(comment));
                    }
                    else
                    {
                        Partial.ClearValue();
                        Partial.ClearBuffer();
                    }
                }
            }

            // make sure we've got a row to work with
            static T GuaranteeRecord(AsyncReader<T> self, ref T preallocd)
            {
                if (preallocd != null)
                {
                    return preallocd;
                }

                if (!self.Configuration.NewCons(out preallocd))
                {
                    Throw.InvalidOperationException($"Failed to construct new instance of {typeof(T)}");
                }

                return preallocd;
            }

            // continue after we handle detecting line endings
            static async ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync_ContinueAfterHandleLineEndingsAsync(AsyncReader<T> self, ValueTask waitFor, bool returnComments, T record, CancellationToken cancel)
            {
                await waitFor;

                if (self.ReadHeaders == null)
                {
                    await self.HandleHeadersAsync(cancel);
                }

                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    var available = await self.Buffer.ReadAsync(self.Inner, cancel);
                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            record = self.GetValueForReturn();
                            return new ReadWithCommentResult<T>(record);
                        }

                        if (self.HasCommentToReturn)
                        {
                            self.HasCommentToReturn = false;
                            if (returnComments)
                            {
                                var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                                return new ReadWithCommentResult<T>(comment);
                            }
                        }

                        return ReadWithCommentResult<T>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    if (res == ReadWithCommentResultType.HasValue)
                    {
                        record = self.GetValueForReturn();
                        return new ReadWithCommentResult<T>(record);
                    }
                    if (res == ReadWithCommentResultType.HasComment)
                    {
                        self.HasCommentToReturn = false;

                        if (returnComments)
                        {
                            // only actually allocate for the comment if it's been asked for
                            var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                            self.Partial.ClearValue();
                            self.Partial.ClearBuffer();
                            return new ReadWithCommentResult<T>(comment);
                        }
                        else
                        {
                            self.Partial.ClearValue();
                            self.Partial.ClearBuffer();
                        }
                    }
                }
            }

            // continue after we handle detecting headers
            static async ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync_ContinueAfterHandleHeadersAsync(AsyncReader<T> self, ValueTask waitFor, bool returnComments, T record, CancellationToken cancel)
            {
                await waitFor;

                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    var available = await self.Buffer.ReadAsync(self.Inner, cancel);
                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            record = self.GetValueForReturn();
                            return new ReadWithCommentResult<T>(record);
                        }

                        if (self.HasCommentToReturn)
                        {
                            self.HasCommentToReturn = false;
                            if (returnComments)
                            {
                                var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                                return new ReadWithCommentResult<T>(comment);
                            }
                        }

                        return ReadWithCommentResult<T>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    if (res == ReadWithCommentResultType.HasValue)
                    {
                        record = self.GetValueForReturn();
                        return new ReadWithCommentResult<T>(record);
                    }
                    if (res == ReadWithCommentResultType.HasComment)
                    {
                        self.HasCommentToReturn = false;

                        if (returnComments)
                        {
                            // only actually allocate for the comment if it's been asked for
                            var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                            self.Partial.ClearValue();
                            self.Partial.ClearBuffer();
                            return new ReadWithCommentResult<T>(comment);
                        }
                        else
                        {
                            self.Partial.ClearValue();
                            self.Partial.ClearBuffer();
                        }
                    }
                }
            }

            // continue after we read a chunk into a buffer
            static async ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncReader<T> self, ValueTask<int> waitFor, bool returnComments, T record, CancellationToken cancel)
            {
                // finish this loop up
                {
                    var available = await waitFor;
                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            record = self.GetValueForReturn();
                            return new ReadWithCommentResult<T>(record);
                        }

                        if (self.HasCommentToReturn)
                        {
                            self.HasCommentToReturn = false;
                            if (returnComments)
                            {
                                var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                                return new ReadWithCommentResult<T>(comment);
                            }
                        }

                        // intentionally _not_ modifying record here
                        return ReadWithCommentResult<T>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        record = GuaranteeRecord(self, ref record);
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    if (res == ReadWithCommentResultType.HasValue)
                    {
                        record = self.GetValueForReturn();
                        return new ReadWithCommentResult<T>(record);
                    }
                    if (res == ReadWithCommentResultType.HasComment)
                    {
                        self.HasCommentToReturn = false;

                        if (returnComments)
                        {
                            // only actually allocate for the comment if it's been asked for
                            var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                            self.Partial.ClearValue();
                            self.Partial.ClearBuffer();
                            return new ReadWithCommentResult<T>(comment);
                        }
                        else
                        {
                            self.Partial.ClearValue();
                            self.Partial.ClearBuffer();
                        }
                    }
                }

                // back into the loop
                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    var available = await self.Buffer.ReadAsync(self.Inner, cancel);
                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            record = self.GetValueForReturn();
                            return new ReadWithCommentResult<T>(record);
                        }

                        if (self.HasCommentToReturn)
                        {
                            self.HasCommentToReturn = false;
                            if (returnComments)
                            {
                                var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                                return new ReadWithCommentResult<T>(comment);
                            }
                        }

                        // intentionally _not_ modifying record here
                        return ReadWithCommentResult<T>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    if (res == ReadWithCommentResultType.HasValue)
                    {
                        record = self.GetValueForReturn();
                        return new ReadWithCommentResult<T>(record);
                    }
                    if (res == ReadWithCommentResultType.HasComment)
                    {
                        self.HasCommentToReturn = false;

                        if (returnComments)
                        {
                            // only actually allocate for the comment if it's been asked for
                            var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                            self.Partial.ClearValue();
                            self.Partial.ClearBuffer();
                            return new ReadWithCommentResult<T>(comment);
                        }
                        else
                        {
                            self.Partial.ClearValue();
                            self.Partial.ClearBuffer();
                        }
                    }
                }
            }
        }

        private ValueTask HandleLineEndingsAsync(CancellationToken cancel)
        {
            if (Configuration.RowEnding != Cesil.RowEndings.Detect)
            {
                RowEndings = Configuration.RowEnding;
                TryMakeStateMachine();
                return default;
            }

            var disposeDetector = true;
            var detector = new RowEndingDetector<T>(Configuration, SharedCharacterLookup, Inner);
            try
            {
                var resTask = detector.DetectAsync(cancel);
                if (resTask.IsCompletedSuccessfully)
                {
                    var res = resTask.Result;
                    HandleLineEndingsDetectionResult(res);
                    return default;
                }

                // whelp, async time!
                disposeDetector = false;
                return HandleLineEndingsAsync_ContinueAfterDetectAsync(this, resTask, detector, cancel);
            }
            finally
            {
                if (disposeDetector)
                {
                    detector.Dispose();
                }
            }


            // wait for header detection to finish, then continue async
            static async ValueTask HandleLineEndingsAsync_ContinueAfterDetectAsync(AsyncReader<T> self, ValueTask<(RowEndings Ending, Memory<char> PushBack)?> waitFor, RowEndingDetector<T> needsDispose, CancellationToken cancel)
            {
                try
                {
                    var res = await waitFor;
                    self.HandleLineEndingsDetectionResult(res);
                }
                finally
                {
                    needsDispose.Dispose();
                }
            }
        }

        private ValueTask HandleHeadersAsync(CancellationToken cancel)
        {
            if (Configuration.ReadHeader == Cesil.ReadHeaders.Never)
            {
                // can just use the discovered copy from source
                ReadHeaders = Cesil.ReadHeaders.Never;
                TryMakeStateMachine();
                Columns = Configuration.DeserializeColumns;

                return default;
            }

            var headerConfig =
                new ConcreteBoundConfiguration<T>(
                    Configuration.NewCons,
                    Configuration.DeserializeColumns,
                    Array.Empty<Column>(),
                    Array.Empty<bool>(),
                    Configuration.ValueSeparator,
                    Configuration.EscapedValueStartAndStop,
                    Configuration.EscapeValueEscapeChar,
                    RowEndings.Value,
                    Configuration.ReadHeader,
                    Configuration.WriteHeader,
                    Configuration.WriteTrailingNewLine,
                    Configuration.MemoryPool,
                    Configuration.CommentChar,
                    null,
                    Configuration.ReadBufferSizeHint
                );

            var disposeReader = true;
            var headerReader =
                new HeadersReader<T>(
                    headerConfig,
                    SharedCharacterLookup,
                    Inner,
                    Buffer
                );
            try
            {
                var headersTask = headerReader.ReadAsync(cancel);

                if (!headersTask.IsCompletedSuccessfully)
                {
                    // whelp, async time!
                    disposeReader = false;
                    return HandleHeadersAsync_ContinueAfterReadAsync(this, headersTask, headerReader, cancel);
                }

                var res = headersTask.Result;
                HandleHeadersReaderResult(res);
                return default;
            }
            finally
            {
                if (disposeReader)
                {
                    headerReader.Dispose();
                }
            }

            // wait for header reading to finish, then continue async
            static async ValueTask HandleHeadersAsync_ContinueAfterReadAsync(AsyncReader<T> self, ValueTask<(HeadersReader<T>.HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> waitFor, HeadersReader<T> needsDispose, CancellationToken cancel)
            {
                try
                {
                    var res = await waitFor;
                    self.HandleHeadersReaderResult(res);
                }
                finally
                {
                    needsDispose.Dispose();
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                Inner.Dispose();
                Buffer.Dispose();
                Partial.Dispose();
                StateMachine?.Dispose();
                SharedCharacterLookup.Dispose();

                Inner = null;
            }

            return default;
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(AsyncReader<T>));
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncReader<T>)} with {Configuration}";
        }
    }
}
