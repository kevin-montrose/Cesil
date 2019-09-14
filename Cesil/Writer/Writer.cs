using System;
using System.IO;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class Writer<T> : SyncWriterBase<T>
    {
        internal Writer(ConcreteBoundConfiguration<T> config, IWriterAdapter inner, object context) : base(config, inner, context) { }

        public override void Write(T row)
        {
            AssertNotDisposed(this);

            WriteHeadersAndEndRowIfNeeded();

            for (var i = 0; i < Columns.Length; i++)
            {
                var needsSeparator = i != 0;

                if (needsSeparator)
                {
                    PlaceCharInStaging(Config.ValueSeparator);
                }

                var col = Columns[i];

                var ctx = WriteContext.WritingColumn(RowNumber, ColumnIdentifier.Create(i, col.Name), Context);

                if (!col.Write(row, ctx, Buffer))
                {
                    Throw.SerializationException<object>($"Could not write column {col.Name}, formatter returned false");
                }

                var res = Buffer.Buffer;
                if (res.IsEmpty)
                {
                    // nothing was written, so just move on
                    continue;
                }

                WriteValue(res);
                Buffer.Reset();
            }

            RowNumber++;
        }

        public override void WriteComment(string comment)
        {
            if (comment == null)
            {
                Throw.ArgumentNullException<object>(nameof(comment));
            }

            AssertNotDisposed(this);

            WriteHeadersAndEndRowIfNeeded();

            var segments = SplitCommentIntoLines(comment);

            var commentChar = Config.CommentChar.Value;
            if (segments.IsSingleSegment)
            {
                PlaceCharInStaging(commentChar);
                var segSpan = segments.First.Span;
                if (segSpan.Length > 0)
                {
                    PlaceAllInStaging(segSpan);
                }
            }
            else
            {
                // we know we can write directly now
                var isFirstRow = true;
                foreach (var seg in segments)
                {
                    if (!isFirstRow)
                    {
                        EndRecord();
                    }

                    PlaceCharInStaging(commentChar);
                    var segSpan = seg.Span;
                    if (segSpan.Length > 0)
                    {
                        PlaceAllInStaging(segSpan);
                    }
                    isFirstRow = false;
                }
            }
        }

        private void WriteHeadersAndEndRowIfNeeded()
        {
            var shouldEndRecord = true;

            if (IsFirstRow)
            {
                if (!CheckHeaders())
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                EndRecord();
            }
        }

        // returns true if it did write out headers,
        //   so we need to end a record before
        //   writing the next one
        private bool CheckHeaders()
        {
            // make a note of what the columns to write actually are
            Columns = Config.SerializeColumns;

            if (Config.WriteHeader == Cesil.WriteHeaders.Never)
            {
                // nothing to write, so bail
                return false;
            }

            WriteHeaders();

            return true;
        }

        private void WriteHeaders()
        {
            var needsEscape = Config.SerializeColumnsNeedEscape;

            for (var i = 0; i < Columns.Length; i++)
            {
                // for the separator
                if (i != 0)
                {
                    PlaceCharInStaging(Config.ValueSeparator);
                }

                var colName = Columns[i].Name;
                var escape = needsEscape[i];

                if (!escape)
                {
                    PlaceAllInStaging(colName.AsSpan());
                }
                else
                {
                    // start with the escape char
                    PlaceCharInStaging(Config.EscapedValueStartAndStop);

                    // try and blit everything in relatively few calls

                    var colSpan = colName.AsSpan();

                    var start = 0;
                    var end = Utils.FindChar(colSpan, start, Config.EscapedValueStartAndStop);
                    while (end != -1)
                    {
                        var len = end - start;
                        var toWrite = colSpan.Slice(start, len);

                        var write = toWrite;
                        PlaceAllInStaging(toWrite);

                        // place the escape char
                        PlaceCharInStaging(Config.EscapeValueEscapeChar);

                        start = end;
                        end = Utils.FindChar(colSpan, start + 1, Config.EscapedValueStartAndStop);
                    }

                    // copy the last bit
                    if (start != colSpan.Length)
                    {
                        var toWrite = colSpan.Slice(start);

                        PlaceAllInStaging(toWrite);
                    }

                    // end with the escape char
                    PlaceCharInStaging(Config.EscapedValueStartAndStop);
                }
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                if (IsFirstRow)
                {
                    CheckHeaders();
                }

                if (Config.WriteTrailingNewLine == WriteTrailingNewLines.Always)
                {
                    EndRecord();
                }

                if (HasBuffer)
                {
                    if (InStaging > 0)
                    {
                        FlushStaging();
                    }

                    Staging.Dispose();
                }

                Inner.Dispose();
                Buffer.Dispose();
                Inner = null;
            }
        }

        public override string ToString()
        {
            return $"{nameof(Writer<T>)} with {Config}";
        }
    }
}