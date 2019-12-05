using System;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicWriter :
        SyncWriterBase<dynamic>,
        IDelegateCache
    {
        internal new bool IsFirstRow => !ColumnNames.HasValue;

        private NonNull<Comparison<DynamicCellValue>> ColumnNameSorter;

        private NonNull<(string Name, string EncodedName)[]> ColumnNames;

        private readonly object[] DynamicArgumentsBuffer = new object[3];

        private Dictionary<object, Delegate>? DelegateCache;

        internal DynamicWriter(DynamicBoundConfiguration config, IWriterAdapter inner, object? context) : base(config, inner, context) { }

        CachedDelegate<V> IDelegateCache.TryGet<T, V>(T key)
            where V : class
        {
            if (DelegateCache == null)
            {
                return CachedDelegate<V>.Empty;
            }

            if (DelegateCache.TryGetValue(key, out var cached))
            {
                return new CachedDelegate<V>(cached as V);
            }

            return CachedDelegate<V>.Empty;
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

            var wholeRowContext = WriteContext.DiscoveringCells(Configuration.Options, RowNumber, Context);

            var options = Configuration.Options;

            var cellValues = options.TypeDescriber.GetCellsForDynamicRow(in wholeRowContext, row as object);
            cellValues = ForceInOrder(cellValues);

            var columnNamesValue = ColumnNames.Value;

            var i = 0;
            foreach (var cell in cellValues)
            {
                var needsSeparator = i != 0;

                if (needsSeparator)
                {
                    PlaceCharInStaging(options.ValueSeparator);
                }

                ColumnIdentifier ci;
                if (i < columnNamesValue.Length)
                {
                    ci = ColumnIdentifier.Create(i, columnNamesValue[i].Name);
                }
                else
                {
                    ci = ColumnIdentifier.Create(i);
                }

                var ctx = WriteContext.WritingColumn(Configuration.Options, RowNumber, ci, Context);

                var formatter = cell.Formatter;
                var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                delProvider.Guarantee(this);
                var del = delProvider.CachedDelegate.Value;

                var val = cell.Value as object;
                if (!del(val, in ctx, Buffer))
                {
                    Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
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
            AssertNotDisposed(this);

            Utils.CheckArgumentNull(comment, nameof(comment));

            var shouldEndRecord = true;
            if (IsFirstRow)
            {
                // todo: I feel like this can be made to work?
                // it's basically just a write line
                if (Configuration.Options.WriteHeader == WriteHeader.Always)
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

            var (commentChar, segments) = SplitCommentIntoLines(comment);

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
            var columnNamesValue = ColumnNames.Value;

            // no headers mean we write whatever we're given!
            if (columnNamesValue.Length == 0) return raw;

            var inOrder = true;

            var i = 0;
            foreach (var x in raw)
            {
                if (i == columnNamesValue.Length)
                {
                    return Throw.InvalidOperationException<IEnumerable<DynamicCellValue>>("Too many cells returned, could not place in desired order");
                }

                var expectedName = columnNamesValue[i];
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
            ret.Sort(ColumnNameSorter.Value);

            return ret;
        }

        // returns true if it did write out headers,
        //   so we need to end a record before
        //   writing the next one
        private bool CheckHeaders(dynamic? firstRow)
        {
            if (Configuration.Options.WriteHeader == WriteHeader.Never)
            {
                // nothing to write, so bail
                ColumnNames.Value = Array.Empty<(string, string)>();
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

            var ctx = WriteContext.DiscoveringColumns(Configuration.Options, Context);

            var options = Configuration.Options;
            
            var colIx = 0;
            foreach (var c in options.TypeDescriber.GetCellsForDynamicRow(in ctx, o as object))
            {
                var colName = c.Name;

                if (colName == null)
                {
                    Throw.InvalidOperationException<object>($"No column name found at index {colIx} when {nameof(Cesil.WriteHeader)} = {options.WriteHeader}");
                    return;
                }

                var encodedColName = colName;

                // encode it, if it needs encoding
                if (NeedsEncode(encodedColName))
                {
                    encodedColName = Utils.Encode(encodedColName, options);
                }

                cols.Add((colName, encodedColName));
            }

            ColumnNames.Value = cols.ToArray();

            ColumnNameSorter.Value =
                (a, b) =>
                {
                    var columnNamesValue = ColumnNames.Value;

                    int aIx = -1, bIx = -1;
                    for (var i = 0; i < columnNamesValue.Length; i++)
                    {
                        var colName = columnNamesValue[i].Name;
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
            var columnNamesValue = ColumnNames.Value;
            for (var i = 0; i < columnNamesValue.Length; i++)
            {
                // for the separator
                if (i != 0)
                {
                    PlaceCharInStaging(Configuration.Options.ValueSeparator);
                }

                var colName = columnNamesValue[i].EncodedName;

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

                if (Configuration.Options.WriteTrailingNewLine == WriteTrailingNewLine.Always)
                {
                    EndRecord();
                }

                if (Staging.HasValue)
                {
                    if (InStaging > 0)
                    {
                        FlushStaging();
                    }

                    Staging.Value.Dispose();
                }

                Inner.Dispose();
                Buffer.Dispose();
                IsDisposed = true;
            }
        }

        public override string ToString()
        => $"{nameof(DynamicWriter)} with {Configuration}";
    }
}
