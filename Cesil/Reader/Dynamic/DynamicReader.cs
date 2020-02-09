using System;

using static Cesil.DynamicRowTrackingHelper;

namespace Cesil
{
    internal sealed class DynamicReader :
        SyncReaderBase<dynamic>,
        IDynamicRowOwner
    {
        private int ColumnCount;
        private NonNull<string[]> ColumnNames;

        private DynamicRow? NotifyOnDisposeHead;

        Options IDynamicRowOwner.Options => Configuration.Options;

        object? IDynamicRowOwner.Context => Context;

        internal DynamicReader(IReaderAdapter reader, DynamicBoundConfiguration config, object? context) : base(reader, config, context, new DynamicRowConstructor()) { }

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

        internal override ReadWithCommentResult<dynamic> TryReadInner(bool returnComments, bool pinAcquired, ref dynamic row)
        {
            ReaderStateMachine.PinHandle handle = default;

            if (!pinAcquired)
            {
                handle = StateMachine.Pin();
            }

            TryAllocateAndTrack(this, ColumnCount, ColumnNames, ref NotifyOnDisposeHead, ref row);

            using (handle)
            {

                while (true)
                {
                    PreparingToWriteToBuffer();
                    var available = Buffer.Read(Inner);
                    if (available == 0)
                    {
                        var endRes = EndOfData();

                        return HandleAdvanceResult(endRes, returnComments);
                    }

                    if (!RowBuilder.RowStarted)
                    {
                        StartRow();
                    }

                    var res = AdvanceWork(available);
                    var possibleReturn = HandleAdvanceResult(res, returnComments);
                    if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                    {
                        return possibleReturn;
                    }
                }
            }
        }

        public void Remove(DynamicRow row)
        {
            NotifyOnDisposeHead.Remove(ref NotifyOnDisposeHead, row);
        }

        private void HandleHeaders()
        {
            var options = Configuration.Options;

            ReadHeaders = options.ReadHeader;

            var allowColumnsByName = options.ReadHeader == ReadHeader.Always;

            using (var reader = new HeadersReader<object>(StateMachine, Configuration, SharedCharacterLookup, Inner, Buffer, RowEndings!.Value))
            {
                var res = reader.Read();
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

                            ix++;
                        }
                    }

                    RowBuilder.SetColumnOrder(res.Headers);
                }

                Buffer.PushBackFromOutsideBuffer(res.PushBack);
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

            using (var detector = new RowEndingDetector(StateMachine, options, SharedCharacterLookup, Inner))
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
            }
        }

        public override string ToString()
        {
            return $"{nameof(DynamicReader)} with {Configuration}";
        }
    }
}
