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

        public int WriteAll(IEnumerable<T> rows)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            var oldRowNumber = RowNumber;

            Utils.CheckArgumentNull(rows, nameof(rows));

            foreach (var row in rows)
            {
                WriteInner(row);
            }

            var ret = RowNumber - oldRowNumber;
            return ret;
        }

        public void Write(T row)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            WriteInner(row);
        }

        internal abstract void WriteInner(T row);

        public abstract void WriteComment(string comment);

        internal void EndRecord()
        {
            PlaceAllInStaging(Configuration.RowEndingMemory.Span);
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
                var options = Configuration.Options;

                CheckCanEncode(charSpan, options);

                var escapedValueStartAndStop = Utils.NonNullValue(options.EscapedValueStartAndEnd);

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
                CheckCanEncode(head, Configuration.Options);

                // we have to encode this value, but let's try to do it in only a couple of
                //    write calls

                WriteEncoded(head);
            }
        }

        internal void WriteEncoded(ReadOnlySequence<char> head)
        {
            var escapedValueStartAndStop = Utils.NonNullValue(Configuration.Options.EscapedValueEscapeCharacter);

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
            var escapedValueStartAndStop = Utils.NonNullValue(Configuration.Options.EscapedValueStartAndEnd);

            // try and blit things in big chunks
            var start = 0;
            var end = Utils.FindChar(charSpan, start, escapedValueStartAndStop);

            while (end != -1)
            {
                var escapeValueEscapeChar = Utils.NonNullValue(Configuration.Options.EscapedValueEscapeCharacter);

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
            if (!HasStaging)
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
            if (!HasStaging)
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

        // returns true if we need to flush staging, sets remaining to what wasn't placed in staging
        internal bool PlaceInStaging(ReadOnlySpan<char> c, out ReadOnlySpan<char> remaining)
        {
            var stagingSpan = StagingMemory.Span;
            var stagingLen = stagingSpan.Length;

            var left = Math.Min(c.Length, stagingLen - InStaging);

            var subC = c[0..left];
            var subStaging = stagingSpan[InStaging..];

            subC.CopyTo(subStaging);

            InStaging += left;

            remaining = c[left..];
            return InStaging == stagingLen;
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
            var span = StagingMemory.Span;

            Inner.Write(span[0..InStaging]);

            InStaging = 0;
        }

        public abstract void Dispose();
    }
}
