using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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

        private ConcurrentDictionary<object, Delegate> DelegateCache;

        bool IDelegateCache.TryGetDelegate<TKey, TDelegate>(TKey key, [MaybeNullWhen(returnValue: false)] out TDelegate del)
        {
            if (!DelegateCache.TryGetValue(key, out var untyped))
            {
                del = default;
                return false;
            }

            del = (TDelegate)untyped;
            return true;
        }

        void IDelegateCache.AddDelegate<TKey, TDelegate>(TKey key, TDelegate cached)
        => DelegateCache.TryAdd(key, cached);

        internal AsyncDynamicReader(IAsyncReaderAdapter reader, DynamicBoundConfiguration config, object? context)
            : base(reader, config, context, new DynamicRowConstructor(), config.Options.ExtraColumnTreatment)
        {
            NameLookupReferenceCount = 0;
            NameLookup = NameLookup.Empty;
            DelegateCache = new ConcurrentDictionary<object, Delegate>();
        }

        internal override ValueTask HandleRowEndingsAndHeadersAsync(CancellationToken cancellationToken)
        {
            if (RowEndings == null)
            {
                var handleLineEndingsTask = HandleLineEndingsAsync(cancellationToken);
                if (!handleLineEndingsTask.IsCompletedSuccessfully(this))
                {
                    return HandleRowEndingsAndHeadersAsync_ContinueAFterHandleLineEndingsAsync(this, handleLineEndingsTask, cancellationToken);
                }
            }

            if (ReadHeaders == null)
            {
                return HandleHeadersAsync(cancellationToken);
            }

            return default;

            // continue after waiting for HandleLineEndings to finish
            static async ValueTask HandleRowEndingsAndHeadersAsync_ContinueAFterHandleLineEndingsAsync(AsyncDynamicReader self, ValueTask waitFor, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    if (self.ReadHeaders == null)
                    {
                        var headersTask = self.HandleHeadersAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, headersTask, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }
        }

        internal override ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync(bool returnComments, bool pinAcquired, bool checkRecord, ref dynamic record, CancellationToken cancellationToken)
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

                        return new ValueTask<ReadWithCommentResult<dynamic>>(advanceRes);
                    }

                    if (!RowBuilder.RowStarted)
                    {
                        StartRow();
                    }

                    var res = AdvanceWork(available, out madeProgress);
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
            static async ValueTask<ReadWithCommentResult<dynamic>> TryReadInnerAsync_ContinueAfterReadAsync(AsyncDynamicReader self, ValueTask<int> waitFor, ReaderStateMachine.PinHandle handle, bool returnComments, CancellationToken cancellationToken)
            {
                try
                {
                    using (handle)
                    {
                        var madeProgress = true;

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
                    return Throw.PoisonAndRethrow<ReadWithCommentResult<dynamic>>(self, e);
                }
            }
        }

        protected internal override void EndedWithoutReturningRow()
        => FreePreAllocatedOnEnd(RowBuilder);

        public void Remove(DynamicRow row)
        {
            NotifyOnDisposeHead.Remove(ref NotifyOnDisposeHead, row);
        }

        private ValueTask HandleHeadersAsync(CancellationToken cancellationToken)
        {
            var options = Configuration.Options;

            ReadHeaders = options.ReadHeader;

            var allowColumnsByName = options.ReadHeader == ReadHeader.Always;

            var reader = new HeadersReader<object>(StateMachine, Configuration, SharedCharacterLookup, Inner, Buffer, Utils.NonNullValue(RowEndings));
            var disposeReader = true;
            try
            {
                var resTask = reader.ReadAsync(cancellationToken);
                if (!resTask.IsCompletedSuccessfully(this))
                {
                    disposeReader = false;
                    return HandleHeadersAsync_WaitForRead(this, resTask, allowColumnsByName, reader, cancellationToken);
                }

                var (headers, isHeader, pushBack) = resTask.Result;

                ColumnCount = headers.Count;

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

                        using (var e = headers)
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
                        NameLookup = NameLookup.Create(columnNamesValue, Configuration.MemoryPool);
                    }

                    RowBuilder.SetColumnOrder(Configuration.Options, headers);

                }

                Buffer.PushBackFromOutsideBuffer(pushBack);

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
            static async ValueTask HandleHeadersAsync_WaitForRead(AsyncDynamicReader self, ValueTask<(HeadersReader<object>.HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> toAwait, bool allowColumnsByName, HeadersReader<object> reader, CancellationToken cancellationToken)
            {
                try
                {
                    var (headers, isHeader, pushBack) = await ConfigureCancellableAwait(self, toAwait, cancellationToken);

                    self.ColumnCount = headers.Count;
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

                            using (var e = headers)
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
                            self.NameLookup = NameLookup.Create(columnNamesValue, self.Configuration.MemoryPool);
                        }

                        self.RowBuilder.SetColumnOrder(self.Configuration.Options, headers);
                    }

                    self.Buffer.PushBackFromOutsideBuffer(pushBack);
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

        private ValueTask HandleLineEndingsAsync(CancellationToken cancellationToken)
        {
            var options = Configuration.Options;

            if (options.RowEnding != RowEnding.Detect)
            {
                RowEndings = options.RowEnding;
                TryMakeStateMachine();
                return default;
            }

            var detector = new RowEndingDetector(StateMachine, options, Configuration.MemoryPool, SharedCharacterLookup, Inner, Configuration.ValueSeparatorMemory);
            var disposeDetector = true;
            try
            {
                var resTask = detector.DetectAsync(cancellationToken);
                if (!resTask.IsCompletedSuccessfully(this))
                {
                    disposeDetector = false;
                    return HandleLineEndingsAsync_WaitForDetector(this, resTask, detector, cancellationToken);
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
            static async ValueTask HandleLineEndingsAsync_WaitForDetector(AsyncDynamicReader self, ValueTask<(RowEnding Ending, Memory<char> PushBack)?> toAwait, RowEndingDetector detector, CancellationToken cancellationToken)
            {
                try
                {
                    var res = await ConfigureCancellableAwait(self, toAwait, cancellationToken);

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
                NotifyOnDisposeHead.TryDataDispose(force: true);
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
