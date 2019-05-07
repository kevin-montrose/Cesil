using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

namespace Cesil
{
    internal sealed class Writer<T> : WriterBase<T>, IWriter<T>, ITestableDisposable
    {
        private TextWriter Inner;

        public bool IsDisposed => Inner == null;

        internal Writer(ConcreteBoundConfiguration<T> config, TextWriter inner, object context) : base(config, context)
        {
            Inner = inner;
        }

        public void WriteAll(IEnumerable<T> rows)
        {
            AssertNotDisposed();

            if (rows == null)
            {
                Throw.ArgumentNullException(nameof(rows));
            }

            foreach (var row in rows)
            {
                Write(row);
            }
        }

        public void Write(T row)
        {
            AssertNotDisposed();

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

            for (var i = 0; i < Columns.Length; i++)
            {
                var needsSeparator = i != 0;

                if (needsSeparator)
                {
                    PlaceCharInStaging(Config.ValueSeparator);
                }

                var col = Columns[i];

                var ctx = new WriteContext(RowNumber, i, col.Name, Context);

                if (!col.Write(row, ctx, Buffer))
                {
                    Throw.SerializationException($"Could not write column {col.Name}, formatter returned false");
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

        private void EndRecord()
        {
            PlaceAllInStaging(Config.RowEndingMemory.Span);
        }

        private void WriteValue(ReadOnlySequence<char> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                WriteSingleSegment(buffer.First.Span);
            }
            else
            {
                WriteMultiSegment(buffer);
            }
        }

        private void WriteSingleSegment(ReadOnlySpan<char> charSpan)
        {
            if (!NeedsEncode(charSpan))
            {
                // most of the time we have no need to encode
                //   so just blit this write into the stream

                PlaceAllInStaging(charSpan);
            }
            else
            {
                PlaceCharInStaging(Config.EscapedValueStartAndStop);

                WriteEncoded(charSpan);

                PlaceCharInStaging(Config.EscapedValueStartAndStop);
            }
        }

        private void WriteMultiSegment(ReadOnlySequence<char> head)
        {
            if (!NeedsEncode(head))
            {
                // no encoding, so just blit each segment into the writer

                foreach (var cur in head)
                {
                    var charSpan = cur.Span;

                    PlaceAllInStaging(charSpan);
                }
            }
            else
            {
                // we have to encode this value, but let's try to do it in only a couple of
                //    write calls

                WriteEncoded(head);
            }
        }

        private void WriteEncoded(ReadOnlySequence<char> head)
        {
            // start with whatever the escape is
            PlaceCharInStaging(Config.EscapedValueStartAndStop);

            foreach (var cur in head)
            {
                WriteEncoded(cur.Span);
            }

            // end with the escape
            PlaceCharInStaging(Config.EscapedValueStartAndStop);
        }

        private void WriteEncoded(ReadOnlySpan<char> charSpan)
        {
            // try and blit things in in big chunks
            var start = 0;
            var end = Utils.FindChar(charSpan, start, Config.EscapedValueStartAndStop);

            while (end != -1)
            {
                var len = end - start;
                var toWrite = charSpan.Slice(start, len);

                PlaceAllInStaging(toWrite);

                PlaceCharInStaging(Config.EscapeValueEscapeChar);

                start += len;
                end = Utils.FindChar(charSpan, start + 1, Config.EscapedValueStartAndStop);
            }

            if (start != charSpan.Length)
            {
                var toWrite = charSpan.Slice(start);

                PlaceAllInStaging(toWrite);
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

        private void PlaceCharInStaging(char c)
        {
            // if we can't buffer, just go straight to the underlying stream
            if (!HasBuffer)
            {
                WriteCharDirectly(c);
                return;
            }

            if (PlaceInStaging(c))
            {
                FlushStaging();
            }
        }

        private void PlaceAllInStaging(ReadOnlySpan<char> charSpan)
        {
            // if we can't buffer, just go straight to the underlying stream
            if (!HasBuffer)
            {
                WriteAllDirectly(charSpan);
                return;
            }

            var write = charSpan;
            while (PlaceInStaging(write, out write))
            {
                FlushStaging();
            }
        }

        // returns true if we need to flush stating, sets remaing to what wasn't placed in staging
        private bool PlaceInStaging(ReadOnlySpan<char> c, out ReadOnlySpan<char> remaining)
        {
            var stagingSpan = Staging.Memory.Span;

            var ix = 0;
            while (ix < c.Length)
            {
                var leftInC = c.Length - ix;

                var left = Math.Min(leftInC, stagingSpan.Length - InStaging);

                var subC = c.Slice(ix, left);
                var subStaging = stagingSpan.Slice(InStaging);

                subC.CopyTo(subStaging);

                ix += left;
                InStaging += left;

                if (InStaging == stagingSpan.Length)
                {
                    remaining = c.Slice(ix);
                    return true;
                }
            }

            remaining = default;
            return false;
        }

        private void WriteCharDirectly(char c)
        {
            Inner.Write(c);
        }

        private void WriteAllDirectly(ReadOnlySpan<char> c)
        {
            Inner.Write(c);
        }

        private void FlushStaging()
        {
            var span = Staging.Memory.Span;

            Inner.Write(span.Slice(0, InStaging));

            InStaging = 0;
        }

        public void Dispose()
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

        public void AssertNotDisposed()
        {
            if(IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(Writer<T>));
            }
        }
    }
}