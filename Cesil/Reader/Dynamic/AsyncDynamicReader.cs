using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class AsyncDynamicReader :
        ReaderBase<dynamic>,
        IAsyncReader<object>,
        IDynamicRowOwner,
        ITestableAsyncDisposable
    {
        public bool IsDisposed => Inner == null;

        private TextReader Inner;

        private string[] ColumnNames;

        private DynamicRow NotifyOnDisposeHead;
        public IIntrusiveLinkedList<DynamicRow> NotifyOnDispose => NotifyOnDisposeHead;

        public new object Context => base.Context;

        internal AsyncDynamicReader(TextReader reader, DynamicBoundConfiguration config, object context) : base(config, context)
        {
            Inner = reader;
        }

        public IAsyncEnumerable<dynamic> EnumerateAllAsync()
        {
            AssertNotDisposed();

            return new AsyncEnumerable<dynamic>(this);
        }

        public ValueTask<List<dynamic>> ReadAllAsync(CancellationToken cancel = default)
        => ReadAllAsync(new List<dynamic>());

        public ValueTask<List<dynamic>> ReadAllAsync(List<dynamic> into, CancellationToken cancel = default)
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
                    return ReadAllAsync_WaitForRead(this, resTask, into, cancel);
                }
            }

            return new ValueTask<List<dynamic>>(into);

            // wait for a TryReadAsync to finish, then continue reading
            static async ValueTask<List<dynamic>> ReadAllAsync_WaitForRead(AsyncDynamicReader self, ValueTask<ReadResult<dynamic>> toAwait, List<dynamic> into, CancellationToken cancel)
            {
                var res = await toAwait;

                if (res.HasValue)
                {
                    into.Add(res.Value);

                    while (true)
                    {
                        res = await self.TryReadAsync(cancel);
                        if (res.HasValue)
                        {
                            into.Add(res.Value);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                return into;
            }
        }

        public ValueTask<ReadResult<dynamic>> TryReadAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed();

            dynamic row = null;
            return TryReadWithReuseAsync(ref row);
        }

        public ValueTask<ReadResult<dynamic>> TryReadWithReuseAsync(ref dynamic row, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            var tryReadTask = TryReadInnerAsync(false, ref row, cancel);
            if (!tryReadTask.IsCompletedSuccessfully)
            {
                return TryReadWithReuseAsync_ContinueAfterTryRead(this, tryReadTask, cancel);
            }

            var res = tryReadTask.Result;
            switch (res.ResultType)
            {
                case ReadWithCommentResultType.HasValue:
                    return new ValueTask<ReadResult<dynamic>>(new ReadResult<dynamic>(res.Value));
                case ReadWithCommentResultType.NoValue:
                    return new ValueTask<ReadResult<dynamic>>(ReadResult<dynamic>.Empty);
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}");
                    // just for control flow
                    return default;
            }

            // continue after waiting for TryReadInnerAsync to complete
            static async ValueTask<ReadResult<dynamic>> TryReadWithReuseAsync_ContinueAfterTryRead(AsyncDynamicReader self, ValueTask<ReadWithCommentResult<dynamic>> waitFor, CancellationToken cancel)
            {
                var res = await waitFor;
                switch (res.ResultType)
                {
                    case ReadWithCommentResultType.HasValue:
                        return new ReadResult<dynamic>(res.Value);
                    case ReadWithCommentResultType.NoValue:
                        return ReadResult<dynamic>.Empty;
                    default:
                        Throw.InvalidOperationException($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}");
                        // just for control flow
                        return default;
                }
            }
        }


        public ValueTask<ReadWithCommentResult<dynamic>> TryReadWithCommentAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed();

            dynamic row = null;
            return TryReadWithCommentReuseAsync(ref row, cancel);
        }

        public ValueTask<ReadWithCommentResult<dynamic>> TryReadWithCommentReuseAsync(ref dynamic row, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            return TryReadInnerAsync(true, ref row, cancel);
        }

        private ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync(bool returnComments, ref dynamic record, CancellationToken cancel)
        {
            if (RowEndings == null)
            {
                var handleLineEndingsTask = HandleLineEndingsAsync(cancel);
                if (!handleLineEndingsTask.IsCompletedSuccessfully)
                {
                    DynamicRow dynRow;
                    record = dynRow = GuaranteeUninitializedDynamicRow(this, ref record);
                    return TryReadInnerAsync_ContinueAfterHandleLineEndingsAsync(this, handleLineEndingsTask, returnComments, dynRow, cancel);
                }
            }

            if (ReadHeaders == null)
            {
                var handleHeadersTask = HandleHeadersAsync(cancel);
                if (!handleHeadersTask.IsCompletedSuccessfully)
                {
                    DynamicRow dynRow;
                    record = dynRow = GuaranteeUninitializedDynamicRow(this, ref record);
                    return TryReadInnerAsync_ContinueAfterHandleHeadersAsync(this, handleHeadersTask, returnComments, dynRow, cancel);
                }
            }


            var row = GuaranteeUninitializedDynamicRow(this, ref record);
            var needsInit = true;

            while (true)
            {
                PreparingToWriteToBuffer();
                var availableTask = Buffer.ReadAsync(Inner, cancel);
                if (!availableTask.IsCompletedSuccessfully)
                {
                    return TryReadInnerAsync_ContinueAfterReadAsync(this, availableTask, returnComments, row, needsInit, cancel);
                }

                var available = availableTask.Result;
                if (available == 0)
                {
                    EndOfData();

                    if (HasValueToReturn)
                    {
                        var ret = GetValueForReturn();
                        return new ValueTask<ReadWithCommentResult<dynamic>>(new ReadWithCommentResult<dynamic>(ret));
                    }

                    if (HasCommentToReturn)
                    {
                        HasCommentToReturn = false;
                        if (returnComments)
                        {
                            var comment = Partial.PendingAsString(Buffer.Buffer);
                            return new ValueTask<ReadWithCommentResult<dynamic>>(new ReadWithCommentResult<dynamic>(comment));
                        }
                    }

                    // intentionally _not_ modifying record here
                    return new ValueTask<ReadWithCommentResult<dynamic>>(ReadWithCommentResult<dynamic>.Empty);
                }

                if (!HasValueToReturn)
                {
                    if (needsInit)
                    {
                        GuaranteeInitializedRow(this, row);
                        needsInit = false;
                    }
                    SetValueToPopulate(row);
                }

                var res = AdvanceWork(available);
                if (res == ReadWithCommentResultType.HasValue)
                {
                    var ret = GetValueForReturn();
                    return new ValueTask<ReadWithCommentResult<dynamic>>(new ReadWithCommentResult<dynamic>(ret));
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
                        return new ValueTask<ReadWithCommentResult<dynamic>>(new ReadWithCommentResult<dynamic>(comment));
                    }
                    else
                    {
                        Partial.ClearValue();
                        Partial.ClearBuffer();
                    }
                }
            }

            // make sure we've got a row to work with
            static DynamicRow GuaranteeUninitializedDynamicRow(AsyncDynamicReader self, ref dynamic row)
            {
                DynamicRow dynRow;
                var rowAsObj = row as object;

                if (rowAsObj == null || !(row is DynamicRow))
                {
                    row = dynRow = new DynamicRow();
                }
                else
                {
                    // clear it, if we're reusing
                    dynRow = (row as DynamicRow);

                    if (!dynRow.IsDisposed)
                    {
                        dynRow.Dispose();

                        if (dynRow.Owner != null)
                        {
                            dynRow.Owner.Remove(dynRow);
                        }
                    }
                }

                return dynRow;
            }

            static DynamicRow GuaranteeInitializedRow(AsyncDynamicReader self, DynamicRow dynRow)
            {
                self.MonitorForDispose(dynRow);
                dynRow.Init(self, self.RowNumber, self.Columns.Length, self.Context, self.Configuration.TypeDescriber, self.ColumnNames, self.Configuration.MemoryPool);

                return dynRow;
            }

            // continue after we handle detecting line endings
            static async ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync_ContinueAfterHandleLineEndingsAsync(AsyncDynamicReader self, ValueTask waitFor, bool returnComments, DynamicRow record, CancellationToken cancel)
            {
                var needsInit = true;
                
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
                            var ret = self.GetValueForReturn();
                            return new ReadWithCommentResult<dynamic>(ret);
                        }

                        if (self.HasCommentToReturn)
                        {
                            self.HasCommentToReturn = false;
                            if (returnComments)
                            {
                                var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                                return new ReadWithCommentResult<dynamic>(comment);
                            }
                        }

                        return ReadWithCommentResult<dynamic>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        if (needsInit)
                        {
                            GuaranteeInitializedRow(self, record);
                            needsInit = false;
                        }
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    if (res == ReadWithCommentResultType.HasValue)
                    {
                        var ret = self.GetValueForReturn();
                        return new ReadWithCommentResult<dynamic>(ret);
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
                            return new ReadWithCommentResult<dynamic>(comment);
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
            static async ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync_ContinueAfterHandleHeadersAsync(AsyncDynamicReader self, ValueTask waitFor, bool returnComments, DynamicRow record, CancellationToken cancel)
            {
                var needsInit = true;

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
                            var ret = self.GetValueForReturn();
                            return new ReadWithCommentResult<dynamic>(ret);
                        }

                        if (self.HasCommentToReturn)
                        {
                            self.HasCommentToReturn = false;
                            if (returnComments)
                            {
                                var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                                return new ReadWithCommentResult<dynamic>(comment);
                            }
                        }

                        return ReadWithCommentResult<dynamic>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        if (needsInit)
                        {
                            GuaranteeInitializedRow(self, record);
                            needsInit = false;
                        }
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    if (res == ReadWithCommentResultType.HasValue)
                    {
                        var ret = self.GetValueForReturn();
                        return new ReadWithCommentResult<dynamic>(ret);
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
                            return new ReadWithCommentResult<dynamic>(comment);
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
            static async ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncDynamicReader self, ValueTask<int> waitFor, bool returnComments, DynamicRow record, bool needsInit, CancellationToken cancel)
            {
                // finish this loop up
                {
                    var available = await waitFor;
                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            var ret = self.GetValueForReturn();
                            return new ReadWithCommentResult<dynamic>(ret);
                        }

                        if (self.HasCommentToReturn)
                        {
                            self.HasCommentToReturn = false;
                            if (returnComments)
                            {
                                var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                                return new ReadWithCommentResult<dynamic>(comment);
                            }
                        }

                        // intentionally _not_ modifying record here
                        return ReadWithCommentResult<dynamic>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        if (needsInit)
                        {
                            GuaranteeInitializedRow(self, record);
                            needsInit = false;
                        }
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    if (res == ReadWithCommentResultType.HasValue)
                    {
                        var ret = self.GetValueForReturn();
                        return new ReadWithCommentResult<dynamic>(ret);
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
                            return new ReadWithCommentResult<dynamic>(comment);
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
                            var ret = self.GetValueForReturn();
                            return new ReadWithCommentResult<dynamic>(ret);
                        }

                        if (self.HasCommentToReturn)
                        {
                            self.HasCommentToReturn = false;
                            if (returnComments)
                            {
                                var comment = self.Partial.PendingAsString(self.Buffer.Buffer);
                                return new ReadWithCommentResult<dynamic>(comment);
                            }
                        }

                        // intentionally _not_ modifying record here
                        return ReadWithCommentResult<dynamic>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        if (needsInit)
                        {
                            GuaranteeInitializedRow(self, record);
                            needsInit = false;
                        }
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    if (res == ReadWithCommentResultType.HasValue)
                    {
                        var ret = self.GetValueForReturn();
                        return new ReadWithCommentResult<dynamic>(ret);
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
                            return new ReadWithCommentResult<dynamic>(comment);
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

        private void MonitorForDispose(DynamicRow dynRow)
        {
            if (Configuration.DynamicRowDisposal == DynamicRowDisposal.OnReaderDispose)
            {
                NotifyOnDisposeHead.AddHead(ref NotifyOnDisposeHead, dynRow);
            }
        }

        public void Remove(DynamicRow row)
        {
            NotifyOnDisposeHead.Remove(ref NotifyOnDisposeHead, row);
        }

        private ValueTask HandleHeadersAsync(CancellationToken cancel)
        {
            ReadHeaders = Configuration.ReadHeader;
            TryMakeStateMachine();

            var headerConfig =
                new DynamicBoundConfiguration(
                    Configuration.TypeDescriber,
                    Configuration.ValueSeparator,
                    Configuration.EscapedValueStartAndStop,
                    Configuration.EscapeValueEscapeChar,
                    RowEndings.Value,
                    Configuration.ReadHeader,
                    Configuration.WriteHeader,
                    Configuration.WriteTrailingNewLine,
                    Configuration.MemoryPool,
                    Configuration.CommentChar,
                    Configuration.WriteBufferSizeHint,
                    Configuration.ReadBufferSizeHint,
                    Configuration.DynamicRowDisposal
                );

            var allowColumnsByName = Configuration.ReadHeader == Cesil.ReadHeaders.Always;

            var reader = new HeadersReader<object>(headerConfig, SharedCharacterLookup, Inner, Buffer);
            var disposeReader = true;
            try
            {
                var resTask = reader.ReadAsync(cancel);
                if (resTask.IsCompletedSuccessfully)
                {
                    var res = resTask.Result;

                    var foundHeaders = res.Headers.Count;
                    if (foundHeaders == 0)
                    {
                        // rare, but possible if the file is empty or all comments or something like that
                        Columns = Array.Empty<Column>();
                        ColumnNames = Array.Empty<string>();
                    }
                    else
                    {
                        Columns = new Column[foundHeaders];
                        if (allowColumnsByName)
                        {
                            ColumnNames = new string[foundHeaders];
                        }

                        using (var e = res.Headers)
                        {
                            var ix = 0;
                            while (e.MoveNext())
                            {
                                var name = allowColumnsByName ? new string(e.Current.Span) : null;
                                if (name != null)
                                {
                                    ColumnNames[ix] = name;
                                }
                                var col = new Column(name, Column.MakeDynamicSetter(name, ix), null, false);
                                Columns[ix] = col;

                                ix++;
                            }
                        }
                    }

                    Buffer.PushBackFromOutsideBuffer(res.PushBack);

                    return default;
                }
                else
                {
                    disposeReader = false;
                    return HandleHeadersAsync_WaitForRead(this, resTask, allowColumnsByName, reader);
                }
            }
            finally
            {
                if (disposeReader)
                {
                    reader.Dispose();
                }
            }

            // wait for a call to ReadAsync to finish, then continue
            static async ValueTask HandleHeadersAsync_WaitForRead(AsyncDynamicReader self, ValueTask<(HeadersReader<object>.HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> toAwait, bool allowColumnsByName, HeadersReader<object> reader)
            {
                try
                {
                    var res = await toAwait;

                    var foundHeaders = res.Headers.Count;
                    if (foundHeaders == 0)
                    {
                        // rare, but possible if the file is empty or all comments or something like that
                        self.Columns = Array.Empty<Column>();
                        self.ColumnNames = Array.Empty<string>();
                    }
                    else
                    {
                        self.Columns = new Column[foundHeaders];
                        if (allowColumnsByName)
                        {
                            self.ColumnNames = new string[foundHeaders];
                        }

                        using (var e = res.Headers)
                        {
                            var ix = 0;
                            while (e.MoveNext())
                            {
                                var name = allowColumnsByName ? new string(e.Current.Span) : null;
                                if (name != null)
                                {
                                    self.ColumnNames[ix] = name;
                                }
                                var col = new Column(name, Column.MakeDynamicSetter(name, ix), null, false);
                                self.Columns[ix] = col;

                                ix++;
                            }
                        }
                    }

                    self.Buffer.PushBackFromOutsideBuffer(res.PushBack);
                }
                finally
                {
                    reader.Dispose();
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

            var detector = new RowEndingDetector<object>(Configuration, SharedCharacterLookup, Inner);
            var disposeDetector = true;
            try
            {
                var resTask = detector.DetectAsync(cancel);

                if (resTask.IsCompletedSuccessfully)
                {
                    HandleLineEndingsDetectionResult(resTask.Result);
                    return default;
                }

                disposeDetector = false;
                return HandleLineEndingsAsync_WaitForDetector(this, resTask, detector);
            }
            finally
            {
                if (disposeDetector)
                {
                    detector.Dispose();
                }
            }

            // wait for the call to DetectAsync to complete
            static async ValueTask HandleLineEndingsAsync_WaitForDetector(AsyncDynamicReader self, ValueTask<(RowEndings Ending, Memory<char> PushBack)?> toAwait, RowEndingDetector<object> detector)
            {
                try
                {
                    var res = await toAwait;
                    self.HandleLineEndingsDetectionResult(res);
                }
                finally
                {
                    detector.Dispose();
                }
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(DynamicReader));
            }
        }

        public ValueTask DisposeAsync()
        {
            if (IsDisposed)
            {
                return default;
            }

            // only need to do work if the reader is responsbile for implicitly disposing
            while (NotifyOnDispose != null)
            {
                NotifyOnDisposeHead.Dispose();
                NotifyOnDisposeHead.Remove(ref NotifyOnDisposeHead, NotifyOnDisposeHead);
            }

            if (Inner is IAsyncDisposable iad)
            {
                var disposeTask = iad.DisposeAsync();
                if (disposeTask.IsCompletedSuccessfully)
                {
                    return DisposeAsync_WaitForInnerDispose(this, disposeTask);
                }

                Inner = null;
                return default;
            }
            else
            {
                Inner.Dispose();
                Inner = null;

                return default;
            }

            // wait for Inner's DisposeAsync call to finish, then finish disposing self
            static async ValueTask DisposeAsync_WaitForInnerDispose(AsyncDynamicReader self, ValueTask toAwait)
            {
                await toAwait;

                self.Inner = null;
                return;
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncDynamicReader)} with {Configuration}";
        }
    }
}
