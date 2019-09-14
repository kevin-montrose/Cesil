using System;
using System.Collections.Generic;
using System.IO;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicWriter :
        SyncWriterBase<dynamic>,
        IDelegateCache
    {
        internal new bool IsFirstRow => ColumnNames == null;

        private Comparison<DynamicCellValue> ColumnNameSorter;
        private (string Name, string EncodedName)[] ColumnNames;

        private readonly object[] DynamicArgumentsBuffer = new object[3];

        private Dictionary<object, Delegate> DelegateCache;

        internal DynamicWriter(DynamicBoundConfiguration config, IWriterAdapter inner, object context) : base(config, inner, context) { }

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

        public override void Write(dynamic row)
        {
            AssertNotDisposed(this);

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
                var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                delProvider.Guarantee(this);
                var del = delProvider.CachedDelegate;

                var val = cell.Value as object;
                if (!del(val, in ctx, Buffer))
                {
                    Throw.SerializationException<object>($"Could not write column {col}, formatter {formatter} returned false");
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

        public override void WriteComment(string comment)
        {
            if (comment == null)
            {
                Throw.ArgumentNullException<object>(nameof(comment));
            }

            AssertNotDisposed(this);

            var shouldEndRecord = true;
            if (IsFirstRow)
            {
                if (Config.WriteHeader == Cesil.WriteHeaders.Always)
                {
                    Throw.InvalidOperationException<object>($"First operation on a dynamic writer cannot be {nameof(WriteComment)} if configured to write headers, headers cannot be inferred");
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
                    return Throw.InvalidOperationException<IEnumerable<DynamicCellValue>>("Too many cells returned, could not place in desired order");
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
                    Throw.InvalidOperationException<object>($"No column name found at index {colIx} when {nameof(Cesil.WriteHeaders)} = {Config.WriteHeader}");
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

        public override void Dispose()
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

        public override string ToString()
        => $"{nameof(DynamicWriter)} with {Config}";
    }
}
