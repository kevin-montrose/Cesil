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

        internal override ValueTask HandleRowEndingsAndHeadersAsync(CancellationToken cancellationToken)
        {
            if (RowEndings == null)
            {
                var handleLineEndingsTask = HandleLineEndingsAsync(cancellationToken);
                if (!handleLineEndingsTask.IsCompletedSuccessfully(this))
                {
                    return HandleRowEndingsAndHeadersAsync_ContinueAfterRowEndingsAsync(this, handleLineEndingsTask, cancellationToken);
                }
            }

            if (ReadHeaders == null)
            {
                var handleHeadersTask = HandleHeadersAsync(cancellationToken);
                return handleHeadersTask;
            }

            return default;

            // continue after HandleLineEndingsAsync
            static async ValueTask HandleRowEndingsAndHeadersAsync_ContinueAfterRowEndingsAsync(AsyncReader<T> self, ValueTask waitFor, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    if (self.ReadHeaders == null)
                    {
                        var handleHeadersTask = self.HandleHeadersAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, handleHeadersTask, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }
        }

        internal override ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync(bool returnComments, bool pinAcquired, bool checkRecord, ref T record, CancellationToken cancellationToken)
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
                var madeProgress = true;
                while (true)
                {
                    PreparingToWriteToBuffer();

                    var availableTask = Buffer.ReadAsync(Inner, madeProgress, cancellationToken);
                    if (!availableTask.IsCompletedSuccessfully(this))
                    {

                        disposeHandle = false;
                        return TryReadInnerAsync_ContinueAfterReadAsync(this, availableTask, handle, returnComments, cancellationToken);
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

                    var res = AdvanceWork(available, out madeProgress);
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
            static async ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncReader<T> self, ValueTask<int> waitFor, ReaderStateMachine.PinHandle handle, bool returnComments, CancellationToken cancellationToken)
            {
                try
                {
                    using (handle)
                    {
                        bool madeProgress;

                        // finish this loop up
                        {
                            int available;
                            self.StateMachine.ReleasePinForAsync(waitFor);
                            {
                                available = await ConfigureCancellableAwait(self, waitFor, cancellationToken);
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

                            var res = self.AdvanceWork(available, out madeProgress);
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

                            var availableTask = self.Buffer.ReadAsync(self.Inner, madeProgress, cancellationToken);
                            int available;
                            self.StateMachine.ReleasePinForAsync(availableTask);
                            {
                                available = await ConfigureCancellableAwait(self, availableTask, cancellationToken);
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

                            var res = self.AdvanceWork(available, out madeProgress);
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

        private ValueTask HandleLineEndingsAsync(CancellationToken cancellationToken)
        {
            var options = Configuration.Options;

            if (options.ReadRowEnding != ReadRowEnding.Detect)
            {
                RowEndings = options.ReadRowEnding;
                TryMakeStateMachine();
                return default;
            }

            var disposeDetector = true;
            var detector = new RowEndingDetector(StateMachine, Configuration.Options, Configuration.MemoryPool, SharedCharacterLookup, Inner, Configuration.ValueSeparatorMemory);
            try
            {
                var resTask = detector.DetectAsync(cancellationToken);
                if (!resTask.IsCompletedSuccessfully(this))
                {
                    // whelp, async time
                    disposeDetector = false;
                    return HandleLineEndingsAsync_ContinueAfterDetectAsync(this, resTask, detector, cancellationToken);
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
            static async ValueTask HandleLineEndingsAsync_ContinueAfterDetectAsync(AsyncReader<T> self, ValueTask<(ReadRowEnding Ending, Memory<char> PushBack)?> waitFor, RowEndingDetector needsDispose, CancellationToken cancellationToken)
            {
                try
                {
                    var res = await ConfigureCancellableAwait(self, waitFor, cancellationToken);

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

        private ValueTask HandleHeadersAsync(CancellationToken cancellationToken)
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
                    Utils.NonNullValue(RowEndings)
                );
            try
            {
                var headersTask = headerReader.ReadAsync(cancellationToken);
                if (!headersTask.IsCompletedSuccessfully(this))
                {
                    // whelp, async time
                    disposeReader = false;
                    return HandleHeadersAsync_ContinueAfterReadAsync(this, headersTask, headerReader, cancellationToken);
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
            static async ValueTask HandleHeadersAsync_ContinueAfterReadAsync(AsyncReader<T> self, ValueTask<(HeadersReader<T>.HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> waitFor, HeadersReader<T> needsDispose, CancellationToken cancellationToken)
            {
                try
                {
                    var res = await ConfigureCancellableAwait(self, waitFor, cancellationToken);

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
