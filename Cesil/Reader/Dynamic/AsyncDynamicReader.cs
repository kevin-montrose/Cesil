using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class AsyncDynamicReader :
        AsyncReaderBase<dynamic>,
        IDynamicRowOwner
    {
        private NonNull<string[]> ColumnNames;

        private DynamicRow? NotifyOnDisposeHead;
        public IIntrusiveLinkedList<DynamicRow>? NotifyOnDispose => NotifyOnDisposeHead;

        object? IDynamicRowOwner.Context => base.Context;

        internal AsyncDynamicReader(IAsyncReaderAdapter reader, DynamicBoundConfiguration config, object? context) : base(reader, config, context) { }

        internal override ValueTask HandleRowEndingsAndHeadersAsync(CancellationToken cancel)
        {
            if (RowEndings == null)
            {
                var handleLineEndingsTask = HandleLineEndingsAsync(cancel);
                if (!handleLineEndingsTask.IsCompletedSuccessfully(this))
                {
                    return HandleRowEndingsAndHeadersAsync_ContinueAFterHandleLineEndingsAsync(this, handleLineEndingsTask, cancel);
                }
            }

            if (ReadHeaders == null)
            {
                return HandleHeadersAsync(cancel);
            }

            return default;

            // continue after waiting for HandleLineEndings to finish
            static async ValueTask HandleRowEndingsAndHeadersAsync_ContinueAFterHandleLineEndingsAsync(AsyncDynamicReader self, ValueTask waitFor, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                if (self.ReadHeaders == null)
                {
                    await self.HandleHeadersAsync(cancel);
                    cancel.ThrowIfCancellationRequested();
                }
            }
        }

        internal override ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync(bool returnComments, bool pinAcquired, ref dynamic record, CancellationToken cancel)
        {
            var row = Utils.NonNull(GuaranteeRow(ref record) as DynamicRow);  // this `as` is absurdly important, don't remove it
            var needsInit = true;

            ReaderStateMachine.PinHandle handle = default;
            var disposeHandle = true;

            if (!pinAcquired)
            {
                handle = StateMachine.Pin();
            }

            try
            {
                while (true)
                {
                    PreparingToWriteToBuffer();
                    var availableTask = Buffer.ReadAsync(Inner, cancel);
                    if (!availableTask.IsCompletedSuccessfully(this))
                    {
                        disposeHandle = false;
                        return TryReadInnerAsync_ContinueAfterReadAsync(this, availableTask, handle, returnComments, row, needsInit, cancel);
                    }

                    var available = availableTask.Result;
                    if (available == 0)
                    {
                        var endRes = EndOfData();

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
                        return new ValueTask<ReadWithCommentResult<dynamic>>(possibleReturn);
                    }
                }
            }
            finally
            {
                if (!disposeHandle)
                {
                    handle.Dispose();
                }
            }

            // make sure our row is ready to go
            static DynamicRow GuaranteeInitializedRow(AsyncDynamicReader self, DynamicRow dynRow)
            {
                self.MonitorForDispose(dynRow);
                dynRow.Init(self, self.RowNumber, self.Columns.Value.Length, self.Context, self.Configuration.TypeDescriber.Value, self.ColumnNames, self.Configuration.MemoryPool);

                return dynRow;
            }

            // continue after we read a chunk into a buffer
            static async ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncDynamicReader self, ValueTask<int> waitFor, ReaderStateMachine.PinHandle handle, bool returnComments, DynamicRow record, bool needsInit, CancellationToken cancel)
            {
                using (handle)
                {
                    // finish this loop up
                    {
                        int available;
                        using (self.StateMachine.ReleaseAndRePinForAsync(waitFor))
                        {
                            available = await waitFor;
                            cancel.ThrowIfCancellationRequested();
                        }
                        if (available == 0)
                        {
                            var endRes = self.EndOfData();

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
                            cancel.ThrowIfCancellationRequested();
                        }
                        if (available == 0)
                        {
                            var endRes = self.EndOfData();

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
                            return possibleReturn;
                        }
                    }
                }
            }
        }

        internal override dynamic GuaranteeRow(ref dynamic row)
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
                dynRow = Utils.NonNull(row as DynamicRow);

                if (!dynRow.IsDisposed)
                {
                    dynRow.Dispose();

                    if (dynRow.Owner.HasValue)
                    {
                        dynRow.Owner.Value.Remove(dynRow);
                    }
                }
            }

            return dynRow;
        }

        private void MonitorForDispose(DynamicRow dynRow)
        {
            if (Configuration.DynamicRowDisposal == DynamicRowDisposal.OnReaderDispose)
            {
                NotifyOnDisposeHead!.AddHead(ref NotifyOnDisposeHead, dynRow);
            }
        }

        public void Remove(DynamicRow row)
        {
            NotifyOnDisposeHead!.Remove(ref NotifyOnDisposeHead, row);
        }

        private ValueTask HandleHeadersAsync(CancellationToken cancel)
        {
            ReadHeaders = Configuration.ReadHeader;

            var headerConfig =
                new DynamicBoundConfiguration(
                    Configuration.TypeDescriber.Value,
                    Configuration.ValueSeparator,
                    Configuration.EscapedValueStartAndStop,
                    Configuration.EscapeValueEscapeChar,
                    RowEndings!.Value,
                    Configuration.ReadHeader,
                    Configuration.WriteHeader,
                    Configuration.WriteTrailingNewLine,
                    Configuration.MemoryPool,
                    Configuration.CommentChar,
                    Configuration.WriteBufferSizeHint,
                    Configuration.ReadBufferSizeHint,
                    Configuration.DynamicRowDisposal
                );

            var allowColumnsByName = Configuration.ReadHeader == Cesil.ReadHeader.Always;

            var reader = new HeadersReader<object>(StateMachine, headerConfig, SharedCharacterLookup, Inner, Buffer);
            var disposeReader = true;
            try
            {
                var resTask = reader.ReadAsync(cancel);
                if (!resTask.IsCompletedSuccessfully(this))
                {
                    disposeReader = false;
                    return HandleHeadersAsync_WaitForRead(this, resTask, allowColumnsByName, reader, cancel);
                }

                var res = resTask.Result;

                var foundHeaders = res.Headers.Count;
                if (foundHeaders == 0)
                {
                    // rare, but possible if the file is empty or all comments or something like that
                    Columns.Value = Array.Empty<Column>();
                    ColumnNames.Value = Array.Empty<string>();
                }
                else
                {
                    var columnsValue = new Column[foundHeaders];
                    Columns.Value = columnsValue;

                    string[] columnNamesValue = Array.Empty<string>();
                    if (allowColumnsByName)
                    {
                        columnNamesValue = new string[foundHeaders];
                        ColumnNames.Value = columnNamesValue;
                    }

                    using (var e = res.Headers)
                    {
                        var ix = 0;
                        while (e.MoveNext())
                        {
                            var name = allowColumnsByName ? new string(e.Current.Span) : null;
                            if (name != null)
                            {
                                columnNamesValue[ix] = name;
                            }
                            var col = new Column(name, ColumnSetter.CreateDynamic(name, ix), null, false);
                            columnsValue[ix] = col;

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
            static async ValueTask HandleHeadersAsync_WaitForRead(AsyncDynamicReader self, ValueTask<(HeadersReader<object>.HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> toAwait, bool allowColumnsByName, HeadersReader<object> reader, CancellationToken cancel)
            {
                try
                {
                    var res = await toAwait;
                    cancel.ThrowIfCancellationRequested();

                    var foundHeaders = res.Headers.Count;
                    if (foundHeaders == 0)
                    {
                        // rare, but possible if the file is empty or all comments or something like that
                        self.Columns.Value = Array.Empty<Column>();
                        self.ColumnNames.Value = Array.Empty<string>();
                    }
                    else
                    {
                        var selfColumnsValue = new Column[foundHeaders];
                        self.Columns.Value = selfColumnsValue;

                        string[] selfColumnNamesValue = Array.Empty<string>();
                        if (allowColumnsByName)
                        {
                            selfColumnNamesValue = new string[foundHeaders];
                            self.ColumnNames.Value = selfColumnNamesValue;
                        }

                        using (var e = res.Headers)
                        {
                            var ix = 0;
                            while (e.MoveNext())
                            {
                                var name = allowColumnsByName ? new string(e.Current.Span) : null;
                                if (name != null)
                                {
                                    selfColumnNamesValue[ix] = name;
                                }
                                var col = new Column(name, ColumnSetter.CreateDynamic(name, ix), null, false);
                                selfColumnsValue[ix] = col;

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
            if (Configuration.RowEnding != Cesil.RowEnding.Detect)
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
                    return HandleLineEndingsAsync_WaitForDetector(this, resTask, detector, cancel);
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
            static async ValueTask HandleLineEndingsAsync_WaitForDetector(AsyncDynamicReader self, ValueTask<(RowEnding Ending, Memory<char> PushBack)?> toAwait, RowEndingDetector<object> detector, CancellationToken cancel)
            {
                try
                {
                    var res = await toAwait;
                    cancel.ThrowIfCancellationRequested();

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
            while (NotifyOnDisposeHead != null)
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

            IsDisposed = true;
            return default;

            // wait for Inner's DisposeAsync call to finish, then finish disposing self
            static async ValueTask DisposeAsync_WaitForInnerDispose(AsyncDynamicReader self, ValueTask toAwait)
            {
                await toAwait;

                self.Buffer.Dispose();
                self.Partial.Dispose();
                self.StateMachine?.Dispose();
                self.SharedCharacterLookup.Dispose();

                self.IsDisposed = true;
                return;
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncDynamicReader)} with {Configuration}";
        }
    }
}
