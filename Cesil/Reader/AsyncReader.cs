using System;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.AwaitHelper;

namespace Cesil
{
    internal sealed class AsyncReader<T> :
        AsyncReaderBase<T>
    {
        internal AsyncReader(IAsyncReaderAdapter inner, ConcreteBoundConfiguration<T> config, object? context) : base(inner, config, context) { }

        internal override ValueTask HandleRowEndingsAndHeadersAsync(CancellationToken cancel)
        {
            if (RowEndings == null)
            {
                var handleLineEndingsTask = HandleLineEndingsAsync(cancel);
                if (!handleLineEndingsTask.IsCompletedSuccessfully(this))
                {
                    return HandleRowEndingsAndHeadersAsync_ContinueAfterRowEndingsAsync(this, handleLineEndingsTask, cancel);
                }
            }

            if (ReadHeaders == null)
            {
                var handleHeadersTask = HandleHeadersAsync(cancel);
                return handleHeadersTask;
            }

            return default;

            // continue after HandleLineEndingsAsync
            static async ValueTask HandleRowEndingsAndHeadersAsync_ContinueAfterRowEndingsAsync(AsyncReader<T> self, ValueTask waitFor, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                if (self.ReadHeaders == null)
                {
                    await ConfigureCancellableAwait(self, self.HandleHeadersAsync(cancel), cancel);
                    CheckCancellation(self, cancel);
                }
            }
        }

        internal override ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync(bool returnComments, bool pinAcquired, ref T record, CancellationToken cancel)
        {
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
                        var row = GuaranteeRow(ref record);
                        disposeHandle = false;
                        return TryReadInnerAsync_ContinueAfterReadAsync(this, availableTask, handle, returnComments, row, cancel);
                    }

                    var available = availableTask.Result;
                    if (available == 0)
                    {
                        var endRes = EndOfData();

                        return new ValueTask<ReadWithCommentResult<T>>(HandleAdvanceResult(endRes, returnComments));
                    }

                    if (!Partial.HasPending)
                    {
                        record = GuaranteeRow(ref record);
                        SetValueToPopulate(record);
                    }

                    var res = AdvanceWork(available);
                    var possibleReturn = HandleAdvanceResult(res, returnComments);
                    if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                    {
                        return new ValueTask<ReadWithCommentResult<T>>(possibleReturn);
                    }
                }
            }
            finally
            {
                if (disposeHandle)
                {
                    handle.Dispose();
                }
            }

            // continue after we read a chunk into a buffer
            static async ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncReader<T> self, ValueTask<int> waitFor, ReaderStateMachine.PinHandle handle, bool returnComments, T record, CancellationToken cancel)
            {
                using (handle)
                {
                    // finish this loop up
                    {
                        int available;
                        using (self.StateMachine.ReleaseAndRePinForAsync(waitFor))
                        {
                            available = await ConfigureCancellableAwait(self, waitFor, cancel);
                            CheckCancellation(self, cancel);
                        }
                        if (available == 0)
                        {
                            var endRes = self.EndOfData();

                            return self.HandleAdvanceResult(endRes, returnComments);
                        }

                        if (!self.Partial.HasPending)
                        {
                            record = self.GuaranteeRow(ref record);
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
                            available = await ConfigureCancellableAwait(self, availableTask, cancel);
                            CheckCancellation(self, cancel);
                        }
                        if (available == 0)
                        {
                            var endRes = self.EndOfData();

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
                            return possibleReturn;
                        }
                    }
                }
            }
        }

        private ValueTask HandleLineEndingsAsync(CancellationToken cancel)
        {
            var options = Configuration.Options;

            if (options.RowEnding != RowEnding.Detect)
            {
                RowEndings = options.RowEnding;
                TryMakeStateMachine();
                return default;
            }

            var disposeDetector = true;
            var detector = new RowEndingDetector(StateMachine, Configuration.Options, SharedCharacterLookup, Inner);
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
            static async ValueTask HandleLineEndingsAsync_ContinueAfterDetectAsync(AsyncReader<T> self, ValueTask<(RowEnding Ending, Memory<char> PushBack)?> waitFor, RowEndingDetector needsDispose, CancellationToken cancel)
            {
                try
                {
                    var res = await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    self.HandleLineEndingsDetectionResult(res);
                }
                finally
                {
                    needsDispose.Dispose();
                }
            }
        }

        // make sure we've got a row to work with
        internal override T GuaranteeRow(ref T preallocd)
        {
            if (preallocd != null)
            {
                return preallocd;
            }

            var ctx = ReadContext.ReadingRow(Configuration.Options, RowNumber, Context);
            if (!Configuration.NewCons.Value(in ctx, out preallocd))
            {
                return Throw.InvalidOperationException<T>($"Failed to construct new instance of {typeof(T)}");
            }

            return preallocd;
        }

        private ValueTask HandleHeadersAsync(CancellationToken cancel)
        {
            var options = Configuration.Options;

            if (options.ReadHeader == ReadHeader.Never)
            {
                // can just use the discovered copy from source
                ReadHeaders = ReadHeader.Never;
                TryMakeStateMachine();
                Columns.Value = Configuration.DeserializeColumns;

                return default;
            }

            var disposeReader = true;
            var headerReader =
                new HeadersReader<T>(
                    StateMachine,
                    Configuration,
                    SharedCharacterLookup,
                    Inner,
                    Buffer, 
                    RowEndings!.Value
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
                    var res = await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

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
                IsDisposed = true;

                try
                {
                    var disposeTask = Inner.DisposeAsync();
                    if (!disposeTask.IsCompletedSuccessfully(this))
                    {
                        return DisposeAsync_ContinueAfterInnerDisposedAsync(this, disposeTask);
                    }
                }
                catch(Exception e)
                {
                    Buffer.Dispose();
                    Partial.Dispose();
                    StateMachine?.Dispose();
                    SharedCharacterLookup.Dispose();

                    return Throw.PoisonAndRethrow<ValueTask>(this, e);
                }

                Buffer.Dispose();
                Partial.Dispose();
                StateMachine?.Dispose();
                SharedCharacterLookup.Dispose();
            }

            return default;

            // continue after Inner.DisposeAsync completes
            static async ValueTask DisposeAsync_ContinueAfterInnerDisposedAsync(AsyncReader<T> self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);
                }
                catch (Exception e)
                {
                    self.Buffer.Dispose();
                    self.Partial.Dispose();
                    self.StateMachine?.Dispose();
                    self.SharedCharacterLookup.Dispose();

                    Throw.PoisonAndRethrow<object>(self, e);

                    return;
                }

                self.Buffer.Dispose();
                self.Partial.Dispose();
                self.StateMachine?.Dispose();
                self.SharedCharacterLookup.Dispose();
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncReader<T>)} with {Configuration}";
        }
    }
}
