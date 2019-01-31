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
        where T : new()
    {
        public bool IsDisposed => Inner == null;
        private TextReader Inner;

        internal AsyncReader(TextReader inner, BoundConfiguration<T> config) : base(config)
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

            if (RowEndings == null)
            {
                var handleLineEndingsTask = HandleLineEndingsAsync(cancel);
                if (!handleLineEndingsTask.IsCompletedSuccessfully)
                {
                    // gotta check this now, because we're about to go async and ref is a no-go
                    if(row == null)
                    {
                        row = new T();
                    }
                    return TryReadAsync_ContinueAfterRowEndingsAsync(this, handleLineEndingsTask, row, cancel);
                }
            }

            if (ReadHeaders == null)
            {
                var handleHeadersTask = HandleHeadersAsync(cancel);
                if (!handleHeadersTask.IsCompletedSuccessfully)
                {
                    // gotta check this now, because we're about to go async and ref is a no-go
                    if (row == null)
                    {
                        row = new T();
                    }
                    return TryReadAsync_ContinueAfterHeadersAsync(this, handleHeadersTask, row, cancel);
                }
            }

            while (true)
            {
                PreparingToWriteToBuffer();
                var inBuffer = Buffer.ReadAsync(Inner, cancel);
                // can we complete sync?
                if (inBuffer.IsCompletedSuccessfully)
                {
                    var available = inBuffer.Result;
                    if (available == 0)
                    {
                        EndOfData();

                        if (HasValueToReturn)
                        {
                            var ret = new ValueTask<ReadResult<T>>(new ReadResult<T>(GetValueForReturn()));
                            return ret;
                        }

                        return new ValueTask<ReadResult<T>>(ReadResult<T>.Empty);
                    }

                    if (!HasValueToReturn)
                    {
                        if(row == null)
                        {
                            row = new T();
                        }

                        SetValueToPopulate(row);
                    }

                    var res = AdvanceWork(available);
                    if (res)
                    {
                        var ret = new ValueTask<ReadResult<T>>(new ReadResult<T>(GetValueForReturn()));
                        return ret;
                    }
                }
                else
                {
                    // if ever we need to actually await, bail out to a different method
                    return TryReadAsync_ContinueAfterReadAsync(this, inBuffer, cancel);
                }
            }

            // wait for row endings discovery to finish, then continue async
            static async ValueTask<ReadResult<T>> TryReadAsync_ContinueAfterRowEndingsAsync(AsyncReader<T> self, ValueTask waitFor, T row, CancellationToken cancel)
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
                            return new ReadResult<T>(self.GetValueForReturn());
                        }

                        return ReadResult<T>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        self.SetValueToPopulate(row);
                    }

                    var res = self.AdvanceWork(available);
                    if (res)
                    {
                        return new ReadResult<T>(self.GetValueForReturn());
                    }
                }
            }

            // wait for headers to be handled, then continue async
            static async ValueTask<ReadResult<T>> TryReadAsync_ContinueAfterHeadersAsync(AsyncReader<T> self, ValueTask waitFor, T row, CancellationToken cancel)
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
                            return new ReadResult<T>(self.GetValueForReturn());
                        }

                        return ReadResult<T>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        self.SetValueToPopulate(row);
                    }

                    var res = self.AdvanceWork(available);
                    if (res)
                    {
                        return new ReadResult<T>(self.GetValueForReturn());
                    }
                }
            }

            // wait for a read to finish, then continue async
            static async ValueTask<ReadResult<T>> TryReadAsync_ContinueAfterReadAsync(AsyncReader<T> self, ValueTask<int> waitFor, CancellationToken cancel)
            {
                var available = await waitFor;

                // handle the one read in flight

                if (available == 0)
                {
                    self.EndOfData();

                    if (self.HasValueToReturn)
                    {
                        return new ReadResult<T>(self.GetValueForReturn());
                    }

                    return ReadResult<T>.Empty;
                }

                if (!self.HasValueToReturn)
                {
                    self.SetValueToPopulate(new T());
                }

                var res = self.AdvanceWork(available);
                if (res)
                {
                    return new ReadResult<T>(self.GetValueForReturn());
                }

                // back into the loop

                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    available = await self.Buffer.ReadAsync(self.Inner, cancel);

                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            return new ReadResult<T>(self.GetValueForReturn());
                        }

                        return ReadResult<T>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        self.SetValueToPopulate(new T());
                    }

                    res = self.AdvanceWork(available);
                    if (res)
                    {
                        return new ReadResult<T>(self.GetValueForReturn());
                    }
                }
            }
        }

        public ValueTask<ReadResult<T>> TryReadAsync(CancellationToken cancel = default)
        {
            var record = new T();
            return TryReadWithReuseAsync(ref record, cancel);
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
                new BoundConfiguration<T>(
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
                Throw.ObjectDisposed(nameof(AsyncReader<T>));
            }
        }
    }
}
