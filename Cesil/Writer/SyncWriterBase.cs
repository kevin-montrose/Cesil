using System;
using System.Buffers;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal abstract class SyncWriterBase<T> :
        WriterBase<T>,
        IWriter<T>,
        ITestableDisposable
    {
        internal readonly IWriterAdapter Inner;

        public bool IsDisposed { get; protected set; }

        internal SyncWriterBase(BoundConfigurationBase<T> config, IWriterAdapter inner, object? context) : base(config, context)
        {
            Inner = inner;
        }

        public void WriteAll(IEnumerable<T> rows)
        {
            AssertNotDisposed(this);

            Utils.CheckArgumentNull(rows, nameof(rows));

            foreach (var row in rows)
            {
                Write(row);
            }
        }

        public abstract void Write(T row);

        public abstract void WriteComment(string comment);

        internal void EndRecord()
        {
            PlaceAllInStaging(Config.RowEndingMemory.Span);
        }

        internal void WriteValue(ReadOnlySequence<char> buffer)
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

        internal void WriteSingleSegment(ReadOnlySpan<char> charSpan)
        {
            if (!NeedsEncode(charSpan))
            {
                // most of the time we have no need to encode
                //   so just blit this write into the stream

                PlaceAllInStaging(charSpan);
            }
            else
            {
                CheckCanEncode(charSpan);

                var escapedValueStartAndStop = Utils.NonNullStruct(Config.EscapedValueStartAndStop);

                PlaceCharInStaging(escapedValueStartAndStop);

                WriteEncoded(charSpan);

                PlaceCharInStaging(escapedValueStartAndStop);
            }
        }

        internal void WriteMultiSegment(ReadOnlySequence<char> head)
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
                CheckCanEncode(head);

                // we have to encode this value, but let's try to do it in only a couple of
                //    write calls

                WriteEncoded(head);
            }
        }

        internal void WriteEncoded(ReadOnlySequence<char> head)
        {
            var escapedValueStartAndStop = Utils.NonNullStruct(Config.EscapedValueStartAndStop);

            // start with whatever the escape is
            PlaceCharInStaging(escapedValueStartAndStop);

            foreach (var cur in head)
            {
                WriteEncoded(cur.Span);
            }

            // end with the escape
            PlaceCharInStaging(escapedValueStartAndStop);
        }

        internal void WriteEncoded(ReadOnlySpan<char> charSpan)
        {
            var escapedValueStartAndStop = Utils.NonNullStruct(Config.EscapedValueStartAndStop);

            // try and blit things in in big chunks
            var start = 0;
            var end = Utils.FindChar(charSpan, start, escapedValueStartAndStop);

            while (end != -1)
            {
                var escapeValueEscapeChar = Utils.NonNullStruct(Config.EscapeValueEscapeChar);

                var len = end - start;
                var toWrite = charSpan.Slice(start, len);

                PlaceAllInStaging(toWrite);

                PlaceCharInStaging(escapeValueEscapeChar);

                start += len;
                end = Utils.FindChar(charSpan, start + 1, escapedValueStartAndStop);
            }

            if (start != charSpan.Length)
            {
                var toWrite = charSpan.Slice(start);

                PlaceAllInStaging(toWrite);
            }
        }

        internal void PlaceCharInStaging(char c)
        {
            // if we can't buffer, just go straight to the underlying stream
            if (!Staging.HasValue)
            {
                WriteCharDirectly(c);
                return;
            }

            if (PlaceInStaging(c))
            {
                FlushStaging();
            }
        }

        internal void PlaceAllInStaging(ReadOnlySpan<char> charSpan)
        {
            // if we can't buffer, just go straight to the underlying stream
            if (!Staging.HasValue)
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
        internal bool PlaceInStaging(ReadOnlySpan<char> c, out ReadOnlySpan<char> remaining)
        {
            var stagingSpan = Staging.Value.Memory.Span;

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

        internal void WriteCharDirectly(char c)
        {
            Inner.Write(c);
        }

        internal void WriteAllDirectly(ReadOnlySpan<char> c)
        {
            Inner.Write(c);
        }

        internal void FlushStaging()
        {
            var span = Staging.Value.Memory.Span;

            Inner.Write(span.Slice(0, InStaging));

            InStaging = 0;
        }

        public abstract void Dispose();
    }
}
