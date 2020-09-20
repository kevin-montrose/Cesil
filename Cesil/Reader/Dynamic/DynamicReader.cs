using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using static Cesil.DynamicRowTrackingHelper;

namespace Cesil
{
    internal sealed class DynamicReader :
        SyncReaderBase<dynamic>,
        IDynamicRowOwner
    {
        private NonNull<string[]> ColumnNames;

        private DynamicRow? NotifyOnDisposeHead;

        Options IDynamicRowOwner.Options => Configuration.Options;

        object? IDynamicRowOwner.Context => Context;

        private int NameLookupReferenceCount;
        private NameLookup NameLookup;

        private readonly ConcurrentDictionary<object, Delegate> DelegateCache;

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

        internal DynamicReader(IReaderAdapter reader, DynamicBoundConfiguration config, object? context)
            : base(reader, config, context, new DynamicRowConstructor(), config.Options.ExtraColumnTreatment)
        {
            NameLookupReferenceCount = 0;
            NameLookup = NameLookup.Empty;
            DelegateCache = new ConcurrentDictionary<object, Delegate>();
        }

        internal override void HandleRowEndingsAndHeaders()
        {
            if (RowEndings == null)
            {
                HandleLineEndings();
            }

            if (ReadHeaders == null)
            {
                HandleHeaders();
            }
        }

        internal override ReadWithCommentResult<dynamic> TryReadInner(bool returnComments, bool pinAcquired, bool checkRow, ref dynamic row)
        {
            ReaderStateMachine.PinHandle handle = default;

            if (!pinAcquired)
            {
                handle = StateMachine.Pin();
            }

            TryAllocateAndTrack(this, ColumnNames, ref NotifyOnDisposeHead, checkRow, ref row);

            using (handle)
            {
                var madeProgress = true;
                while (true)
                {
                    PreparingToWriteToBuffer();
                    var available = Buffer.Read(Inner, madeProgress);
                    if (available == 0)
                    {
                        var endRes = EndOfData();

                        return HandleAdvanceResult(endRes, returnComments, ending: true);
                    }

                    if (!RowBuilder.RowStarted)
                    {
                        StartRow();
                    }

                    var res = AdvanceWork(available, out madeProgress);
                    var possibleReturn = HandleAdvanceResult(res, returnComments, ending: false);
                    if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                    {
                        return possibleReturn;
                    }
                }
            }
        }

        protected internal override void EndedWithoutReturningRow()
        => FreePreAllocatedOnEnd(RowBuilder);

        public void Remove(DynamicRow row)
        {
            NotifyOnDisposeHead.Remove(ref NotifyOnDisposeHead, row);
        }

        private void HandleHeaders()
        {
            var options = Configuration.Options;

            ReadHeaders = options.ReadHeader;

            var allowColumnsByName = options.ReadHeader == ReadHeader.Always;

            using (var reader = new HeadersReader<object>(StateMachine, Configuration, SharedCharacterLookup, Inner, Buffer, Utils.NonNullValue(RowEndings)))
            {
                var (headers, isHeader, pushBack) = reader.Read();
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

                    RowBuilder.SetColumnOrder(headers);
                }

                Buffer.PushBackFromOutsideBuffer(pushBack);
            }

            TryMakeStateMachine();
        }

        private void HandleLineEndings()
        {
            var options = Configuration.Options;

            if (options.RowEnding != RowEnding.Detect)
            {
                RowEndings = options.RowEnding;
                TryMakeStateMachine();
                return;
            }

            using (var detector = new RowEndingDetector(StateMachine, options, Configuration.MemoryPool, SharedCharacterLookup, Inner, Configuration.ValueSeparatorMemory))
            {
                var res = detector.Detect();
                HandleLineEndingsDetectionResult(res);
            }
        }

        public override void Dispose()
        {
            if (IsDisposed) return;

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
                Inner.Dispose();
            }
            catch (Exception e)
            {
                Cleanup(this);

                Throw.PoisonAndRethrow<object>(this, e);
                return;
            }

            Cleanup(this);

            // handle actual cleanup, a method to DRY things up
            static void Cleanup(DynamicReader self)
            {
                self.RowBuilder.Dispose();
                self.Buffer.Dispose();
                self.Partial.Dispose();
                self.StateMachine.Dispose();
                self.SharedCharacterLookup.Dispose();

                // if we never acquired one, this will moved the count to -1
                //   which WON'T actually release NameLookup
                (self as IDynamicRowOwner)?.ReleaseNameLookup();
            }
        }

        public override string ToString()
        {
            return $"{nameof(DynamicReader)} with {Configuration}";
        }
    }
}
