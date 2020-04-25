using System;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.AwaitHelper;
using static Cesil.DynamicRowTrackingHelper;

namespace Cesil
{
    internal sealed class AsyncDynamicReader :
        AsyncReaderBase<dynamic>,
        IDynamicRowOwner
    {
        private NonNull<string[]> ColumnNames;

        private DynamicRow? NotifyOnDisposeHead;

        Options IDynamicRowOwner.Options => Configuration.Options;

        object? IDynamicRowOwner.Context => Context;

        int IDynamicRowOwner.MinimumExpectedColumns => ColumnCount;

        private int NameLookupReferenceCount;
        private NameLookup NameLookup;

        NameLookup IDynamicRowOwner.AcquireNameLookup()
        {
            Interlocked.Increment(ref NameLookupReferenceCount);
            return NameLookup;
        }

        void IDynamicRowOwner.ReleaseNameLookup()
        {
            var res = Interlocked.Decrement(ref NameLookupReferenceCount);
            if (res == 0)
            {
                NameLookup.Dispose();
            }
        }

        internal AsyncDynamicReader(IAsyncReaderAdapter reader, DynamicBoundConfiguration config, object? context)
            : base(reader, config, context, new DynamicRowConstructor(), config.Options.ExtraColumnTreatment)
        {
            NameLookupReferenceCount = 0;
            NameLookup = NameLookup.Empty;
        }

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

        internal override ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync(bool returnComments, bool pinAcquired, bool checkRecord, ref dynamic record, CancellationToken cancel)
        {
            TryAllocateAndTrack(this, ColumnNames, ref NotifyOnDisposeHead, checkRecord, ref record);

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
                        return TryReadInnerAsync_ContinueAfterReadAsync(this, availableTask, handle, returnComments, cancel);
                    }

                    var available = availableTask.Result;
                    if (available == 0)
                    {
                        var endRes = EndOfData();

                        var advanceRes = HandleAdvanceResult(endRes, returnComments, ending: true);

                        return new ValueTask<ReadWithCommentResult<dynamic>>(advanceRes);
                    }

                    if (!RowBuilder.RowStarted)
                    {
                        StartRow();
                    }

                    var res = AdvanceWork(available);
                    var possibleReturn = HandleAdvanceResult(res, returnComments, ending: false);
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

            // continue after we read a chunk into a buffer
            static async ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncDynamicReader self, ValueTask<int> waitFor, ReaderStateMachine.PinHandle handle, bool returnComments, CancellationToken cancel)
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
                    return Throw.PoisonAndRethrow<ReadWithCommentResult<dynamic>>(self, e);
                }
            }
        }

        protected internal override void EndedWithoutReturningRow()
        => FreePreAllocatedOnEnd(RowBuilder);

        public void Remove(DynamicRow row)
        {
            NotifyOnDisposeHead!.Remove(ref NotifyOnDisposeHead, row);
        }

        private ValueTask HandleHeadersAsync(CancellationToken cancel)
        {
            var options = Configuration.Options;

            ReadHeaders = options.ReadHeader;

            var allowColumnsByName = options.ReadHeader == ReadHeader.Always;

            var reader = new HeadersReader<object>(StateMachine, Configuration, SharedCharacterLookup, Inner, Buffer, RowEndings!.Value);
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

                ColumnCount = res.Headers.Count;

                if (ColumnCount == 0)
                {
                    // rare, but possible if the file is empty or all comments or something like that
                    ColumnNames.Value = Array.Empty<string>();
                }
                else
                {
                    string[] columnNamesValue = Array.Empty<string>();
                    if (allowColumnsByName)
                    {
                        columnNamesValue = new string[ColumnCount];
                        ColumnNames.Value = columnNamesValue;

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

                                ix++;
                            }
                        }

                        Interlocked.Increment(ref NameLookupReferenceCount);
                        NameLookup = NameLookup.Create(columnNamesValue, Configuration.Options.MemoryPool);
                    }

                    RowBuilder.SetColumnOrder(res.Headers);

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
                    var res = await ConfigureCancellableAwait(self, toAwait, cancel);
                    CheckCancellation(self, cancel);

                    self.ColumnCount = res.Headers.Count;
                    if (self.ColumnCount == 0)
                    {
                        // rare, but possible if the file is empty or all comments or something like that
                        self.ColumnNames.Value = Array.Empty<string>();
                    }
                    else
                    {
                        string[] columnNamesValue = Array.Empty<string>();
                        if (allowColumnsByName)
                        {
                            columnNamesValue = new string[self.ColumnCount];
                            self.ColumnNames.Value = columnNamesValue;

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

                                    ix++;
                                }
                            }

                            Interlocked.Increment(ref self.NameLookupReferenceCount);
                            self.NameLookup = NameLookup.Create(columnNamesValue, self.Configuration.Options.MemoryPool);
                        }

                        self.RowBuilder.SetColumnOrder(res.Headers);
                    }

                    self.Buffer.PushBackFromOutsideBuffer(res.PushBack);
                    self.TryMakeStateMachine();
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
                finally
                {
                    reader.Dispose();
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

            var detector = new RowEndingDetector(StateMachine, options, SharedCharacterLookup, Inner);
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
            static async ValueTask HandleLineEndingsAsync_WaitForDetector(AsyncDynamicReader self, ValueTask<(RowEnding Ending, Memory<char> PushBack)?> toAwait, RowEndingDetector detector, CancellationToken cancel)
            {
                try
                {
                    var res = await ConfigureCancellableAwait(self, toAwait, cancel);
                    CheckCancellation(self, cancel);

                    self.HandleLineEndingsDetectionResult(res);
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
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

            IsDisposed = true;

            // only need to do work if the reader is responsible for implicitly disposing
            while (NotifyOnDisposeHead != null)
            {
                NotifyOnDisposeHead.Dispose();
                NotifyOnDisposeHead.Remove(ref NotifyOnDisposeHead, NotifyOnDisposeHead);
            }

            try
            {

                var disposeTask = Inner.DisposeAsync();
                if (!disposeTask.IsCompletedSuccessfully(this))
                {
                    return DisposeAsync_WaitForInnerDispose(this, disposeTask);
                }
            }
            catch (Exception e)
            {
                Cleanup(this);

                return Throw.PoisonAndRethrow<ValueTask>(this, e);
            }

            Cleanup(this);

            return default;

            // handle actual cleanup, a method to DRY things up
            static void Cleanup(AsyncDynamicReader self)
            {
                self.RowBuilder.Dispose();
                self.Buffer.Dispose();
                self.Partial.Dispose();
                self.StateMachine?.Dispose();
                self.SharedCharacterLookup.Dispose();

                // if we never acquired one, this will moved the count to -1
                //   which WON'T actually release NameLookup
                (self as IDynamicRowOwner)?.ReleaseNameLookup();
            }

            // wait for Inner's DisposeAsync call to finish, then finish disposing self
            static async ValueTask DisposeAsync_WaitForInnerDispose(AsyncDynamicReader self, ValueTask toAwait)
            {
                try
                {
                    await ConfigureCancellableAwait(self, toAwait, CancellationToken.None);
                }
                catch (Exception e)
                {
                    Cleanup(self);

                    Throw.PoisonAndRethrow<object>(self, e);
                    return;
                }

                Cleanup(self);

                return;
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncDynamicReader)} with {Configuration}";
        }
    }
}
