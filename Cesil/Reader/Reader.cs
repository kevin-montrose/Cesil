using System;

namespace Cesil
{
    internal sealed class Reader<T> : SyncReaderBase<T>
    {
        internal Reader(IReaderAdapter inner, ConcreteBoundConfiguration<T> config, object? context, IRowConstructor<T> rowBuilder) : base(inner, config, context, rowBuilder, Utils.EffectiveColumnTreatmentForStatic(config)) { }

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

        internal override ReadWithCommentResult<T> TryReadInner(bool returnComments, bool pinAcquired, bool checkRecord, ref T record)
        {
            ReaderStateMachine.PinHandle handle = default;

            if (!pinAcquired)
            {
                handle = StateMachine.Pin();
            }

            TryPreAllocateRow(checkRecord, ref record);

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

        protected internal override void EndedWithoutReturningRow() { }

        private void HandleHeaders()
        {
            var options = Configuration.Options;

            if (options.ReadHeader == ReadHeader.Never)
            {
                // can just use the discovered copy from source
                ReadHeaders = ReadHeader.Never;
                ColumnCount = Configuration.DeserializeColumns.Length;
                TryMakeStateMachine();

                return;
            }

            // should always have been initialized by now
            var rowEndings = Utils.NonNullValue(RowEndings);

            using (
                var headerReader = new HeadersReader<T>(
                    StateMachine,
                    Configuration,
                    SharedCharacterLookup,
                    Inner,
                    Buffer,
                    rowEndings
                )
            )
            {
                var headers = headerReader.Read();

                HandleHeadersReaderResult(headers);
            }
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

            using (var detector = new RowEndingDetector(StateMachine, options, SharedCharacterLookup, Inner, Configuration.ValueSeparatorMemory))
            {
                var res = detector.Detect();
                HandleLineEndingsDetectionResult(res);
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

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
            }

            // handle actual cleanup, a method to DRY things up
            static void Cleanup(Reader<T> self)
            {
                self.RowBuilder.Dispose();
                self.Buffer.Dispose();
                self.Partial.Dispose();
                self.StateMachine?.Dispose();
                self.SharedCharacterLookup.Dispose();
            }
        }

        public override string ToString()
        {
            return $"{nameof(Reader<T>)} with {Configuration}";
        }
    }
}
