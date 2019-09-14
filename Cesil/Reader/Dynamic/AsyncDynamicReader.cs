using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class AsyncDynamicReader :
        AsyncReaderBase<dynamic>,
        IDynamicRowOwner
    {
        private string[] ColumnNames;

        private DynamicRow NotifyOnDisposeHead;
        public IIntrusiveLinkedList<DynamicRow> NotifyOnDispose => NotifyOnDisposeHead;

        public new object Context => base.Context;

        internal AsyncDynamicReader(IAsyncReaderAdapter reader, DynamicBoundConfiguration config, object context) : base(reader, config, context) { }

        internal override ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync(bool returnComments, bool pinAcquired, ref dynamic record, CancellationToken cancel)
        {
            if (RowEndings == null)
            {
                var handleLineEndingsTask = HandleLineEndingsAsync(cancel);
                if (!handleLineEndingsTask.IsCompletedSuccessfully(this))
                {
                    DynamicRow dynRow;
                    record = dynRow = GuaranteeUninitializedDynamicRow(this, ref record);
                    return TryReadInnerAsync_ContinueAfterHandleLineEndingsAsync(this, handleLineEndingsTask, pinAcquired, returnComments, dynRow, cancel);
                }
            }

            if (ReadHeaders == null)
            {
                var handleHeadersTask = HandleHeadersAsync(cancel);
                if (!handleHeadersTask.IsCompletedSuccessfully(this))
                {
                    DynamicRow dynRow;
                    record = dynRow = GuaranteeUninitializedDynamicRow(this, ref record);
                    return TryReadInnerAsync_ContinueAfterHandleHeadersAsync(this, handleHeadersTask, pinAcquired, returnComments, dynRow, cancel);
                }
            }


            var row = GuaranteeUninitializedDynamicRow(this, ref record);
            var needsInit = true;

            if (!pinAcquired)
            {
                StateMachine.Pin();
            }

            while (true)
            {
                PreparingToWriteToBuffer();
                var availableTask = Buffer.ReadAsync(Inner, cancel);
                if (!availableTask.IsCompletedSuccessfully(this))
                {
                    return TryReadInnerAsync_ContinueAfterReadAsync(this, availableTask, pinAcquired, returnComments, row, needsInit, cancel);
                }

                var available = availableTask.Result;
                if (available == 0)
                {
                    var endRes = EndOfData();

                    if (!pinAcquired)
                    {
                        StateMachine.Unpin();
                    }
                    return new ValueTask<ReadWithCommentResult<dynamic>>(HandleAdvanceResult(endRes, returnComments));
                }

                if (!Partial.HasPending)
                {
                    if (needsInit)
                    {
                        GuaranteeInitializedRow(this, row);
                        needsInit = false;
                    }
                    SetValueToPopulate(row);
                }

                var res = AdvanceWork(available);
                var possibleReturn = HandleAdvanceResult(res, returnComments);
                if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                {
                    if (!pinAcquired)
                    {
                        StateMachine.Unpin();
                    }
                    return new ValueTask<ReadWithCommentResult<dynamic>>(possibleReturn);
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
            static async ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync_ContinueAfterHandleLineEndingsAsync(AsyncDynamicReader self, ValueTask waitFor, bool pinAcquired, bool returnComments, DynamicRow record, CancellationToken cancel)
            {
                var needsInit = true;

                await waitFor;

                if (self.ReadHeaders == null)
                {
                    var handleHeadersTask = self.HandleHeadersAsync(cancel);
                    await handleHeadersTask;
                }

                if (!pinAcquired)
                {
                    self.StateMachine.Pin();
                }

                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    var availableTask = self.Buffer.ReadAsync(self.Inner, cancel);
                    int available;
                    using(self.StateMachine.ReleaseAndRePinForAsync(availableTask))
                    {
                        available = await availableTask;
                    }
                    if (available == 0)
                    {
                        var endRes = self.EndOfData();

                        if (!pinAcquired)
                        {
                            self.StateMachine.Unpin();
                        }
                        return self.HandleAdvanceResult(endRes, returnComments);
                    }

                    if (!self.Partial.HasPending)
                    {
                        if (needsInit)
                        {
                            GuaranteeInitializedRow(self, record);
                            needsInit = false;
                        }
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    var possibleReturn = self.HandleAdvanceResult(res, returnComments);
                    if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                    {
                        if (!pinAcquired)
                        {
                            self.StateMachine.Unpin();
                        }
                        return possibleReturn;
                    }
                }
            }

            // continue after we handle detecting headers
            static async ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync_ContinueAfterHandleHeadersAsync(AsyncDynamicReader self, ValueTask waitFor, bool pinAcquired, bool returnComments, DynamicRow record, CancellationToken cancel)
            {
                var needsInit = true;

                await waitFor;

                if (!pinAcquired)
                {
                    self.StateMachine.Pin();
                }

                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    var availableTask = self.Buffer.ReadAsync(self.Inner, cancel);
                    int available;
                    using (self.StateMachine.ReleaseAndRePinForAsync(availableTask))
                    {
                        available = await availableTask;
                    }
                    if (available == 0)
                    {
                        var endRes = self.EndOfData();

                        if (!pinAcquired)
                        {
                            self.StateMachine.Unpin();
                        }
                        return self.HandleAdvanceResult(endRes, returnComments);
                    }

                    if (!self.Partial.HasPending)
                    {
                        if (needsInit)
                        {
                            GuaranteeInitializedRow(self, record);
                            needsInit = false;
                        }
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    var possibleReturn = self.HandleAdvanceResult(res, returnComments);
                    if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                    {
                        if (!pinAcquired)
                        {
                            self.StateMachine.Unpin();
                        }
                        return possibleReturn;
                    }
                }
            }

            // continue after we read a chunk into a buffer
            static async ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncDynamicReader self, ValueTask<int> waitFor, bool pinAcquired, bool returnComments, DynamicRow record, bool needsInit, CancellationToken cancel)
            {
                // finish this loop up
                {
                    int available;
                    using (self.StateMachine.ReleaseAndRePinForAsync(waitFor))
                    {
                        available = await waitFor;
                    }
                    if (available == 0)
                    {
                        var endRes = self.EndOfData();

                        if (!pinAcquired)
                        {
                            self.StateMachine.Unpin();
                        }
                        return self.HandleAdvanceResult(endRes, returnComments);
                    }

                    if (!self.Partial.HasPending)
                    {
                        if (needsInit)
                        {
                            GuaranteeInitializedRow(self, record);
                            needsInit = false;
                        }
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    var possibleReturn = self.HandleAdvanceResult(res, returnComments);
                    if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                    {
                        if (!pinAcquired)
                        {
                            self.StateMachine.Unpin();
                        }
                        return possibleReturn;
                    }
                }

                // back into the loop
                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    var availableTask = self.Buffer.ReadAsync(self.Inner, cancel);
                    int available;
                    using (self.StateMachine.ReleaseAndRePinForAsync(availableTask))
                    {
                        available = await availableTask;
                    }
                    if (available == 0)
                    {
                        var endRes = self.EndOfData();

                        if (!pinAcquired)
                        {
                            self.StateMachine.Unpin();
                        }
                        return self.HandleAdvanceResult(endRes, returnComments);
                    }

                    if (!self.Partial.HasPending)
                    {
                        if (needsInit)
                        {
                            GuaranteeInitializedRow(self, record);
                            needsInit = false;
                        }
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    var possibleReturn = self.HandleAdvanceResult(res, returnComments);
                    if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                    {
                        if (!pinAcquired)
                        {
                            self.StateMachine.Unpin();
                        }
                        return possibleReturn;
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

            var reader = new HeadersReader<object>(StateMachine, headerConfig, SharedCharacterLookup, Inner, Buffer);
            var disposeReader = true;
            try
            {
                var resTask = reader.ReadAsync(cancel);
                if (!resTask.IsCompletedSuccessfully(this))
                {
                    disposeReader = false;
                    return HandleHeadersAsync_WaitForRead(this, resTask, allowColumnsByName, reader);
                }

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
                            var col = new Column(name, ColumnSetter.CreateDynamic(name, ix), null, false);
                            Columns[ix] = col;

                            ix++;
                        }
                    }
                }

                Buffer.PushBackFromOutsideBuffer(res.PushBack);

                TryMakeStateMachine();

                return default;
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
                                var col = new Column(name, ColumnSetter.CreateDynamic(name, ix), null, false);
                                self.Columns[ix] = col;

                                ix++;
                            }
                        }
                    }

                    self.Buffer.PushBackFromOutsideBuffer(res.PushBack);
                    self.TryMakeStateMachine();
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

            var detector = new RowEndingDetector<object>(StateMachine, Configuration, SharedCharacterLookup, Inner);
            var disposeDetector = true;
            try
            {
                var resTask = detector.DetectAsync(cancel);
                if (!resTask.IsCompletedSuccessfully(this))
                {
                    disposeDetector = false;
                    return HandleLineEndingsAsync_WaitForDetector(this, resTask, detector);
                }

                HandleLineEndingsDetectionResult(resTask.Result);
                return default;
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

        public override ValueTask DisposeAsync()
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

            var disposeTask = Inner.DisposeAsync();
            if (!disposeTask.IsCompletedSuccessfully(this))
            {
                return DisposeAsync_WaitForInnerDispose(this, disposeTask);
            }

            Buffer.Dispose();
            Partial.Dispose();
            StateMachine?.Dispose();
            SharedCharacterLookup.Dispose();

            Inner = null;
            return default;

            // wait for Inner's DisposeAsync call to finish, then finish disposing self
            static async ValueTask DisposeAsync_WaitForInnerDispose(AsyncDynamicReader self, ValueTask toAwait)
            {
                await toAwait;

                self.Buffer.Dispose();
                self.Partial.Dispose();
                self.StateMachine?.Dispose();
                self.SharedCharacterLookup.Dispose();

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
