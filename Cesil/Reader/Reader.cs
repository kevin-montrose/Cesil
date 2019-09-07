using System;
using System.IO;

namespace Cesil
{
    internal sealed class Reader<T> : SyncReaderBase<T>
    {
        internal Reader(TextReader inner, ConcreteBoundConfiguration<T> config, object context) : base(inner, config, context)
        {
            Inner = inner;
        }

        internal override ReadWithCommentResult<T> TryReadInner(bool returnComments, ref T record)
        {
            if (RowEndings == null)
            {
                HandleLineEndings();
            }

            if (ReadHeaders == null)
            {
                HandleHeaders();
            }

            while (true)
            {
                PreparingToWriteToBuffer();
                var available = Buffer.Read(Inner);
                if (available == 0)
                {
                    EndOfData();

                    if (HasValueToReturn)
                    {
                        record = GetValueForReturn();
                        return new ReadWithCommentResult<T>(record);
                    }

                    if (HasCommentToReturn)
                    {
                        HasCommentToReturn = false;
                        if (returnComments)
                        {
                            var comment = Partial.PendingAsString(Buffer.Buffer);
                            return new ReadWithCommentResult<T>(comment);
                        }
                    }

                    // intentionally _not_ modifying record here
                    return ReadWithCommentResult<T>.Empty;
                }

                if (!HasValueToReturn)
                {
                    if (record == null)
                    {
                        if (!Configuration.NewCons(out record))
                        {
                            Throw.InvalidOperationException($"Failed to construct new instance of {typeof(T)}");
                        }
                    }
                    SetValueToPopulate(record);
                }

                var res = AdvanceWork(available);
                if (res == ReadWithCommentResultType.HasValue)
                {
                    record = GetValueForReturn();
                    return new ReadWithCommentResult<T>(record);
                }
                if (res == ReadWithCommentResultType.HasComment)
                {
                    HasCommentToReturn = false;

                    if (returnComments)
                    {
                        // only actually allocate for the comment if it's been asked for

                        var comment = Partial.PendingAsString(Buffer.Buffer);
                        Partial.ClearValue();
                        Partial.ClearBuffer();
                        return new ReadWithCommentResult<T>(comment);
                    }
                    else
                    {
                        Partial.ClearValue();
                        Partial.ClearBuffer();
                    }
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

            using (var detector = new RowEndingDetector<T>(Configuration, SharedCharacterLookup, Inner))
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
