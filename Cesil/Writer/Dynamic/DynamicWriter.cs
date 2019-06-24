using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

namespace Cesil
{
    internal sealed class DynamicWriter :
        WriterBase<dynamic>,
        IWriter<object>,
        IDelegateCache,
        ITestableDisposable
    {
        private TextWriter Inner;

        internal new bool IsFirstRow => ColumnNames == null;

        private Comparison<DynamicCellValue> ColumnNameSorter;
        private (string Name, string EncodedName)[] ColumnNames;

        public bool IsDisposed => Inner == null;

        private readonly object[] DynamicArgumentsBuffer = new object[3];

        private Dictionary<object, Delegate> DelegateCache;

        internal DynamicWriter(DynamicBoundConfiguration config, TextWriter inner, object context) : base(config, context)
        {
            Inner = inner;
        }

        bool IDelegateCache.TryGet<T, V>(T key, out V del)
        {
            if (DelegateCache == null)
            {
                del = default;
                return false;
            }

            if (DelegateCache.TryGetValue(key, out var cached))
            {
                del = (V)cached;
                return true;
            }

            del = default;
            return false;
        }

        void IDelegateCache.Add<T, V>(T key, V cached)
        {
            if (DelegateCache == null)
            {
                DelegateCache = new Dictionary<object, Delegate>();
            }

            DelegateCache.Add(key, cached);
        }

        public void WriteAll(IEnumerable<dynamic> rows)
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

        public void Write(dynamic row)
        {
            AssertNotDisposed();

            WriteHeadersAndEndRowIfNeeded(row);

            var wholeRowContext = WriteContext.DiscoveringCells(RowNumber, Context);

            var cellValues = Config.TypeDescriber.GetCellsForDynamicRow(in wholeRowContext, row as object);
            cellValues = ForceInOrder(cellValues);

            var i = 0;
            foreach (var cell in cellValues)
            {
                var needsSeparator = i != 0;

                if (needsSeparator)
                {
                    PlaceCharInStaging(Config.ValueSeparator);
                }

                var col = i < ColumnNames.Length ? ColumnNames[i].Name : null;

                var ctx = WriteContext.WritingColumn(RowNumber, ColumnIdentifier.Create(i, col), Context);

                var formatter = cell.Formatter;
                formatter.PrimeDynamicDelegate(this);
                var del = formatter.DynamicDelegate;

                var val = cell.Value as object;
                if (!del(val, in ctx, Buffer))
                {
                    Throw.SerializationException($"Could not write column {col}, formatter {formatter} returned false");
                }

                var res = Buffer.Buffer;
                if (res.IsEmpty)
                {
                    // nothing was written, so just move on
                    goto end;
                }

                WriteValue(res);
                Buffer.Reset();

end:
                i++;
            }

            RowNumber++;
        }

        public void WriteComment(string comment)
        {
            if (comment == null)
            {
                Throw.ArgumentNullException(nameof(comment));
            }

            AssertNotDisposed();

            var shouldEndRecord = true;
            if (IsFirstRow)
            {
                if (Config.WriteHeader == Cesil.WriteHeaders.Always)
                {
                    Throw.InvalidOperationException($"First operation on a dynamic writer cannot be {nameof(WriteComment)} if configured to write headers, headers cannot be inferred");
                }

                if (!CheckHeaders(null))
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                EndRecord();
            }

            var segments = SplitCommentIntoLines(comment);

            var commentChar = Config.CommentChar.Value;

            // we know we can write directly now
            var isFirstRow = true;
            foreach (var seg in segments)
            {
                if (!isFirstRow)
                {
                    EndRecord();
                }

                PlaceCharInStaging(commentChar);
                if (seg.Span.Length > 0)
                {
                    PlaceAllInStaging(seg.Span);
                }

                isFirstRow = false;
            }
        }

        private void WriteHeadersAndEndRowIfNeeded(dynamic row)
        {
            var shouldEndRecord = true;

            if (IsFirstRow)
            {
                if (!CheckHeaders(row))
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                EndRecord();
            }
        }

        private IEnumerable<DynamicCellValue> ForceInOrder(IEnumerable<DynamicCellValue> raw)
        {
            // no headers mean we write whatever we're given!
            if (ColumnNames.Length == 0) return raw;

            var inOrder = true;

            var i = 0;
            foreach (var x in raw)
            {
                if (i == ColumnNames.Length)
                {
                    Throw.InvalidOperationException("Too many cells returned, could not place in desired order");
                }

                var expectedName = ColumnNames[i];
                if (!expectedName.Name.Equals(x.Name))
                {
                    inOrder = false;
                    break;
                }

                i++;
            }

            // already in order, 
            if (inOrder) return raw;

            var ret = new List<DynamicCellValue>(raw);
            ret.Sort(ColumnNameSorter);

            return ret;
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
        private bool CheckHeaders(dynamic firstRow)
        {
            if (Config.WriteHeader == Cesil.WriteHeaders.Never)
            {
                // nothing to write, so bail
                ColumnNames = Array.Empty<(string, string)>();
                return false;
            }

            // init columns
            DiscoverColumns(firstRow);

            WriteHeaders();

            return true;
        }

        private void DiscoverColumns(dynamic o)
        {
            var cols = new List<(string TrueName, string EncodedName)>();

            var ctx = WriteContext.DiscoveringColumns(Context);

            var colIx = 0;
            foreach (var c in Config.TypeDescriber.GetCellsForDynamicRow(in ctx, o as object))
            {
                var colName = c.Name;

                if (colName == null)
                {
                    Throw.InvalidOperationException($"No column name found at index {colIx} when {nameof(Cesil.WriteHeaders)} = {Config.WriteHeader}");
                }

                var encodedColName = colName;

                // encode it, if it needs encoding
                if (NeedsEncode(encodedColName))
                {
                    encodedColName = Utils.Encode(encodedColName, Config);
                }

                cols.Add((colName, encodedColName));
            }

            ColumnNames = cols.ToArray();

            ColumnNameSorter =
                (a, b) =>
                {
                    int aIx = -1, bIx = -1;
                    for (var i = 0; i < ColumnNames.Length; i++)
                    {
                        var colName = ColumnNames[i].Name;
                        if (colName.Equals(a.Name))
                        {
                            aIx = i;
                            if (bIx != -1) break;
                        }

                        if (colName.Equals(b.Name))
                        {
                            bIx = i;
                            if (aIx != -1) break;
                        }
                    }

                    return aIx.CompareTo(bIx);
                };
        }

        private void WriteHeaders()
        {
            for (var i = 0; i < ColumnNames.Length; i++)
            {
                // for the separator
                if (i != 0)
                {
                    PlaceCharInStaging(Config.ValueSeparator);
                }

                var colName = ColumnNames[i].EncodedName;

                // can colName is always gonna be encoded correctly, because we just discovered them
                //   (ie. they're always correct for this config)
                PlaceAllInStaging(colName.AsSpan());
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
                    CheckHeaders(null);
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
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(DynamicWriter));
            }
        }

        public override string ToString()
        => $"{nameof(DynamicWriter)} with {Config}";
    }
}
