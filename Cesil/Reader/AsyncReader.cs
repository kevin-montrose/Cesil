using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class AsyncReader<T> :
        AsyncReaderBase<T>
    {
        internal AsyncReader(IAsyncReaderAdapter inner, ConcreteBoundConfiguration<T> config, object context) : base(inner, config, context) { }

        internal override ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync(bool returnComments, bool pinAcquired, ref T record, CancellationToken cancel)
        {
            if (RowEndings == null)
            {
                var handleLineEndingsTask = HandleLineEndingsAsync(cancel);
                if (!handleLineEndingsTask.IsCompletedSuccessfully(this))
                {
                    var row = GuaranteeRecord(this, pinAcquired, ref record);
                    return TryReadInnerAsync_ContinueAfterHandleLineEndingsAsync(this, handleLineEndingsTask, pinAcquired, returnComments, row, cancel);
                }
            }

            if (ReadHeaders == null)
            {
                var handleHeadersTask = HandleHeadersAsync(cancel);
                if (!handleHeadersTask.IsCompletedSuccessfully(this))
                {
                    var row = GuaranteeRecord(this, pinAcquired, ref record);
                    return TryReadInnerAsync_ContinueAfterHandleHeadersAsync(this, handleHeadersTask, pinAcquired, returnComments, row, cancel);
                }
            }

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
                    var row = GuaranteeRecord(this, pinAcquired, ref record);
                    return TryReadInnerAsync_ContinueAfterReadAsync(this, availableTask, pinAcquired, returnComments, row, cancel);
                }

                var available = availableTask.Result;
                if (available == 0)
                {
                    var endRes = EndOfData();

                    if (!pinAcquired)
                    {
                        StateMachine.Unpin();
                    }
                    return new ValueTask<ReadWithCommentResult<T>>(HandleAdvanceResult(endRes, returnComments));
                }

                if (!Partial.HasPending)
                {
                    record = GuaranteeRecord(this, pinAcquired, ref record);
                    SetValueToPopulate(record);
                }

                var res = AdvanceWork(available);
                var possibleReturn = HandleAdvanceResult(res, returnComments);
                if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                {
                    if (!pinAcquired)
                    {
                        StateMachine.Unpin();
                    }
                    return new ValueTask<ReadWithCommentResult<T>>(possibleReturn);
                }
            }

            // make sure we've got a row to work with
            static T GuaranteeRecord(AsyncReader<T> self, bool pinAcquired, ref T preallocd)
            {
                if (preallocd != null)
                {
                    return preallocd;
                }

                if (!self.Configuration.NewCons(out preallocd))
                {
                    if (!pinAcquired)
                    {
                        self.StateMachine.Unpin();
                    }
                    return Throw.InvalidOperationException<T>($"Failed to construct new instance of {typeof(T)}");
                }

                return preallocd;
            }

            // continue after we handle detecting line endings
            static async ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync_ContinueAfterHandleLineEndingsAsync(AsyncReader<T> self, ValueTask waitFor, bool pinAcquired, bool returnComments, T record, CancellationToken cancel)
            {
                await waitFor;

                if (self.ReadHeaders == null)
                {
                    var handleTask = self.HandleHeadersAsync(cancel);
                    await handleTask;
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
            static async ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync_ContinueAfterHandleHeadersAsync(AsyncReader<T> self, ValueTask waitFor, bool pinAcquired, bool returnComments, T record, CancellationToken cancel)
            {
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
            static async ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncReader<T> self, ValueTask<int> waitFor, bool pinAcquired, bool returnComments, T record, CancellationToken cancel)
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
                        record = GuaranteeRecord(self, pinAcquired, ref record);
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
                        self.SetValueToPopulate(record);
                    }

                    var res = self.AdvanceWork(available);
                    var possibleReturn = self.HandleAdvanceResult(res, returnComments);
                    if(possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
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

        private ValueTask HandleLineEndingsAsync(CancellationToken cancel)
        {
            if (Configuration.RowEnding != Cesil.RowEndings.Detect)
            {
                RowEndings = Configuration.RowEnding;
                TryMakeStateMachine();
                return default;
            }

            var disposeDetector = true;
            var detector = new RowEndingDetector<T>(StateMachine, Configuration, SharedCharacterLookup, Inner);
            try
            {
                var resTask = detector.DetectAsync(cancel);
                if (!resTask.IsCompletedSuccessfully(this))
                {
                    // whelp, async time!
                    disposeDetector = false;
                    return HandleLineEndingsAsync_ContinueAfterDetectAsync(this, resTask, detector, cancel);
                }

                var res = resTask.Result;
                HandleLineEndingsDetectionResult(res);
                return default;
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
                    StateMachine,
                    headerConfig,
                    SharedCharacterLookup,
                    Inner,
                    Buffer
                );
            try
            {
                var headersTask = headerReader.ReadAsync(cancel);
                if (!headersTask.IsCompletedSuccessfully(this))
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

        public override ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                var disposeTask = Inner.DisposeAsync();
                if (!disposeTask.IsCompletedSuccessfully(this))
                {
                    return DisposeAsync_ContinueAfterInnerDisposedAsync(this, disposeTask);
                }
                
                Buffer.Dispose();
                Partial.Dispose();
                StateMachine?.Dispose();
                SharedCharacterLookup.Dispose();

                Inner = null;
            }

            return default;

            // continue after Inner.DisposeAsync completes
            static async ValueTask DisposeAsync_ContinueAfterInnerDisposedAsync(AsyncReader<T> self, ValueTask waitFor)
            {
                await waitFor;

                self.Buffer.Dispose();
                self.Partial.Dispose();
                self.StateMachine?.Dispose();
                self.SharedCharacterLookup.Dispose();

                self.Inner = null;
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncReader<T>)} with {Configuration}";
        }
    }
}
