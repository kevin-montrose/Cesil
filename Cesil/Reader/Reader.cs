using System;
using System.IO;

namespace Cesil
{
    internal sealed class Reader<T> : SyncReaderBase<T>
    {
        internal Reader(IReaderAdapter inner, ConcreteBoundConfiguration<T> config, object context) : base(inner, config, context) { }

        internal override ReadWithCommentResult<T> TryReadInner(bool returnComments, bool pinAcquired, ref T record)
        {
            if (RowEndings == null)
            {
                HandleLineEndings();
            }

            if (ReadHeaders == null)
            {
                HandleHeaders();
            }

            if (!pinAcquired)
            {
                StateMachine.Pin();
            }

            while (true)
            {
                PreparingToWriteToBuffer();
                var available = Buffer.Read(Inner);
                if (available == 0)
                {
                    var endRes = EndOfData();

                    if (!pinAcquired)
                    {
                        StateMachine.Unpin();
                    }
                    return HandleAdvanceResult(endRes, returnComments);
                }

                if (!Partial.HasPending)
                {
                    if (record == null)
                    {
                        if (!Configuration.NewCons(out record))
                        {
                            if (!pinAcquired)
                            {
                                StateMachine.Unpin();
                            }
                            return Throw.InvalidOperationException<ReadWithCommentResult<T>>($"Failed to construct new instance of {typeof(T)}");
                        }
                    }
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
                    return possibleReturn;
                }
            }
        }

        private void HandleHeaders()
        {
            if (Configuration.ReadHeader == Cesil.ReadHeaders.Never)
            {
                // can just use the discovered copy from source
                ReadHeaders = Cesil.ReadHeaders.Never;
                TryMakeStateMachine();
                Columns = Configuration.DeserializeColumns;

                return;
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

            using (
                var headerReader = new HeadersReader<T>(
                    StateMachine,
                    headerConfig,
                    SharedCharacterLookup,
                    Inner,
                    Buffer
                )
            )
            {
                var headers = headerReader.Read();

                HandleHeadersReaderResult(headers);
            }
        }

        private void HandleLineEndings()
        {
            if (Configuration.RowEnding != Cesil.RowEndings.Detect)
            {
                RowEndings = Configuration.RowEnding;
                TryMakeStateMachine();
                return;
            }

            using (var detector = new RowEndingDetector<T>(StateMachine, Configuration, SharedCharacterLookup, Inner))
            {
                var res = detector.Detect();
                HandleLineEndingsDetectionResult(res);
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                Inner.Dispose();
                Buffer.Dispose();
                Partial.Dispose();
                StateMachine?.Dispose();
                SharedCharacterLookup.Dispose();

                Inner = null;
            }
        }

        public override string ToString()
        {
            return $"{nameof(Reader<T>)} with {Configuration}";
        }
    }
}
