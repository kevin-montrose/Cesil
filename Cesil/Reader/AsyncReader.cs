using System;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.AwaitHelper;

namespace Cesil
{
    internal sealed class AsyncReader<T> :
        AsyncReaderBase<T>
    {
        internal AsyncReader(IAsyncReaderAdapter inner, ConcreteBoundConfiguration<T> config, object? context, IRowConstructor<T> rowBuilder) : base(inner, config, context, rowBuilder, Utils.EffectiveColumnTreatmentForStatic(config)) { }

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
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    if (self.ReadHeaders == null)
                    {
                        await ConfigureCancellableAwait(self, self.HandleHeadersAsync(cancel), cancel);
                        CheckCancellation(self, cancel);
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }
        }

        internal override ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync(bool returnComments, bool pinAcquired, bool checkRecord, ref T record, CancellationToken cancel)
        {
            ReaderStateMachine.PinHandle handle = default;
            var disposeHandle = true;

            if (!pinAcquired)
            {
                handle = StateMachine.Pin();
            }

            TryPreAllocateRow(checkRecord, ref record);

            try
            {
                while (true)
                {
                    PreparingToWriteToBuffer();
                    var availableTask = Buffer.ReadAsync(Inner, cancel);
                    if (!availableTask.IsCompletedSuccessfully(this))
                    {

                        disposeHandle = false;
                        return TryReadInnerAsync_ContinueAfterReadAsync(this, availableTask, handle, returnComments, cancel);
                    }

                    var available = availableTask.Result;
                    if (available == 0)
                    {
                        var endRes = EndOfData();

                        var advanceRes = HandleAdvanceResult(endRes, returnComments, ending: true);

                        return new ValueTask<ReadWithCommentResult<T>>(advanceRes);
                    }

                    if (!RowBuilder.RowStarted)
                    {
                        StartRow();
                    }

                    var res = AdvanceWork(available);
                    var possibleReturn = HandleAdvanceResult(res, returnComments, ending: false);
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
            static async ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncReader<T> self, ValueTask<int> waitFor, ReaderStateMachine.PinHandle handle, bool returnComments, CancellationToken cancel)
            {
                try
                {
                    using (handle)
                    {
                        // finish this loop up
                        {
                            int available;
                            self.StateMachine.ReleasePinForAsync(waitFor);
                            {
                                available = await ConfigureCancellableAwait(self, waitFor, cancel);
                                CheckCancellation(self, cancel);
                            }
                            if (available == 0)
                            {
                                var endRes = self.EndOfData();

                                return self.HandleAdvanceResult(endRes, returnComments, ending: true);
                            }

                            if (!self.RowBuilder.RowStarted)
                            {
                                self.StartRow();
                            }

                            var res = self.AdvanceWork(available);
                            var possibleReturn = self.HandleAdvanceResult(res, returnComments, ending: false);
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
                            self.StateMachine.ReleasePinForAsync(availableTask);
                            {
                                available = await ConfigureCancellableAwait(self, availableTask, cancel);
                                CheckCancellation(self, cancel);
                            }
                            if (available == 0)
                            {
                                var endRes = self.EndOfData();

                                return self.HandleAdvanceResult(endRes, returnComments, ending: true);
                            }

                            if (!self.RowBuilder.RowStarted)
                            {
                                self.StartRow();
                            }

                            var res = self.AdvanceWork(available);
                            var possibleReturn = self.HandleAdvanceResult(res, returnComments, ending: false);
                            if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                            {
                                return possibleReturn;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadWithCommentResult<T>>(self, e);
                }
            }
        }

        protected internal override void EndedWithoutReturningRow() { }

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
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
                finally
                {
                    needsDispose.Dispose();
                }
            }
        }

        private ValueTask HandleHeadersAsync(CancellationToken cancel)
        {
            var options = Configuration.Options;

            if (options.ReadHeader == ReadHeader.Never)
            {
                // can just use the discovered copy from source
                ReadHeaders = ReadHeader.Never;
                ColumnCount = Configuration.DeserializeColumns.Length;
                TryMakeStateMachine();

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
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
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
                catch (Exception e)
                {
                    Cleanup(this);

                    return Throw.PoisonAndRethrow<ValueTask>(this, e);
                }

                Cleanup(this);
            }

            return default;

            // handle actual cleanup, a method to DRY things up
            static void Cleanup(AsyncReader<T> self)
            {
                self.RowBuilder.Dispose();

                self.Buffer.Dispose();
                self.Partial.Dispose();
                self.StateMachine?.Dispose();
                self.SharedCharacterLookup.Dispose();
            }

            // continue after Inner.DisposeAsync completes
            static async ValueTask DisposeAsync_ContinueAfterInnerDisposedAsync(AsyncReader<T> self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);
                }
                catch (Exception e)
                {
                    Cleanup(self);

                    Throw.PoisonAndRethrow<object>(self, e);

                    return;
                }

                Cleanup(self);
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncReader<T>)} with {Configuration}";
        }
    }
}
