using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class AsyncDynamicWriter :
        AsyncWriterBase<dynamic>,
        IDelegateCache
    {
        internal new bool IsFirstRow => !ColumnNames.HasValue;

        private NonNull<Comparison<DynamicCellValue>> ColumnNameSorter;

        private NonNull<(string Name, string EncodedName)[]> ColumnNames;

        private readonly object[] DynamicArgumentsBuffer = new object[3];

        private Dictionary<object, Delegate>? DelegateCache;

        internal AsyncDynamicWriter(DynamicBoundConfiguration config, IAsyncWriterAdapter inner, object? context) : base(config, inner, context) 
        { 
            
        }

        CachedDelegate<V> IDelegateCache.TryGet<T, V>(T key)
            where V: class
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

        public override ValueTask WriteAsync(dynamic row, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);

            var rowAsObj = row as object;

            var writeHeadersTask = WriteHeadersAndEndRowIfNeededAsync(rowAsObj, cancel);
            if (!writeHeadersTask.IsCompletedSuccessfully(this))
            {
                return WriteAsync_ContinueAfterWriteHeadersAsync(this, writeHeadersTask, row, cancel);
            }

            var wholeRowContext = WriteContext.DiscoveringCells(RowNumber, Context);

            var cellValues = Config.TypeDescriber.Value.GetCellsForDynamicRow(in wholeRowContext, row as object);
            cellValues = ForceInOrder(cellValues);

            var columnNamesValue = ColumnNames.Value;

            var i = 0;
            var e = cellValues.GetEnumerator();
            bool disposeE = true;
            try
            {
                while (e.MoveNext())
                {
                    var cell = e.Current;
                    var needsSeparator = i != 0;

                    if (needsSeparator)
                    {
                        var placeCharTask = PlaceCharInStagingAsync(Config.ValueSeparator, cancel);
                        if (!placeCharTask.IsCompletedSuccessfully(this))
                        {
                            disposeE = false;
                            return WriteAsync_ContinueAfterPlaceCharAsync(this, placeCharTask, cell, i, e, cancel);
                        }
                    }

                    ColumnIdentifier ci;
                    if(i < columnNamesValue.Length)
                    {
                        ci = ColumnIdentifier.Create(i, columnNamesValue[i].Name);
                    }
                    else
                    {
                        ci = ColumnIdentifier.Create(i);
                    }

                    var ctx = WriteContext.WritingColumn(RowNumber, ci, Context);

                    var formatter = cell.Formatter;
                    var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                    delProvider.Guarantee(this);
                    var del = delProvider.CachedDelegate.Value;

                    var val = cell.Value as object;
                    if (!del(val, in ctx, Buffer))
                    {
                        return Throw.SerializationException<ValueTask>($"Could not write column {ci}, formatter {formatter} returned false");
                    }

                    var res = Buffer.Buffer;
                    if (res.IsEmpty)
                    {
                        // nothing was written, so just move on
                        goto end;
                    }

                    var writeValueTask = WriteValueAsync(res, cancel);
                    if (!writeValueTask.IsCompletedSuccessfully(this))
                    {
                        disposeE = false;
                        return WriteAsync_ContinueAfterWriteValueAsync(this, writeValueTask, i, e, cancel);
                    }
                    Buffer.Reset();

end:
                    i++;
                }
            }
            finally
            {
                if (disposeE)
                {
                    e.Dispose();
                }
            }

            RowNumber++;

            return default;

            // continue after WriteHeadersAndRowIfNeededAsync completes
            static async ValueTask WriteAsync_ContinueAfterWriteHeadersAsync(AsyncDynamicWriter self, ValueTask waitFor, dynamic row, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var wholeRowContext = WriteContext.DiscoveringCells(self.RowNumber, self.Context);

                var cellValues = self.Config.TypeDescriber.Value.GetCellsForDynamicRow(in wholeRowContext, row as object);
                cellValues = self.ForceInOrder(cellValues);

                var selfColumnNamesValue = self.ColumnNames.Value;

                var i = 0;
                foreach (var cell in cellValues)
                {
                    var needsSeparator = i != 0;

                    if (needsSeparator)
                    {
                        var placeTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                        await placeTask;
                        cancel.ThrowIfCancellationRequested();
                    }

                    ColumnIdentifier ci;
                    if (i < selfColumnNamesValue.Length)
                    {
                        ci = ColumnIdentifier.Create(i, selfColumnNamesValue[i].Name);
                    }
                    else
                    {
                        ci = ColumnIdentifier.Create(i);
                    }

                    var ctx = WriteContext.WritingColumn(self.RowNumber, ci, self.Context);

                    var formatter = cell.Formatter;
                    var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                    delProvider.Guarantee(self);
                    var del = delProvider.CachedDelegate.Value;

                    var val = cell.Value as object;
                    if (!del(val, in ctx, self.Buffer))
                    {
                        Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
                    }

                    var res = self.Buffer.Buffer;
                    if (res.IsEmpty)
                    {
                        // nothing was written, so just move on
                        goto end;
                    }
                    
                    var writeValueTask = self.WriteValueAsync(res, cancel);
                    await writeValueTask;
                    cancel.ThrowIfCancellationRequested();

                    self.Buffer.Reset();

end:
                    i++;
                }

                self.RowNumber++;
            }

            // continue after PlaceCharInStagingAsync completes
            static async ValueTask WriteAsync_ContinueAfterPlaceCharAsync(AsyncDynamicWriter self, ValueTask waitFor, DynamicCellValue cell, int i, IEnumerator<DynamicCellValue> e, CancellationToken cancel)
            {
                try
                {
                    await waitFor;
                    cancel.ThrowIfCancellationRequested();

                    var selfColumnNamesValue = self.ColumnNames.Value;

                    // finish the loop
                    {
                        ColumnIdentifier ci;
                        if (i < selfColumnNamesValue.Length)
                        {
                            ci = ColumnIdentifier.Create(i, selfColumnNamesValue[i].Name);
                        }
                        else
                        {
                            ci = ColumnIdentifier.Create(i);
                        }

                        var ctx = WriteContext.WritingColumn(self.RowNumber, ci, self.Context);

                        var formatter = cell.Formatter;
                        var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                        delProvider.Guarantee(self);
                        var del = delProvider.CachedDelegate.Value;

                        var val = cell.Value as object;
                        if (!del(val, in ctx, self.Buffer))
                        {
                            Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
                        }

                        var res = self.Buffer.Buffer;
                        if (res.IsEmpty)
                        {
                            // nothing was written, so just move on
                            goto end;
                        }

                        var writeValueTask = self.WriteValueAsync(res, cancel);
                        await writeValueTask;
                        cancel.ThrowIfCancellationRequested();

                        self.Buffer.Reset();

end:
                        i++;
                    }

                    // resume
                    while (e.MoveNext())
                    {
                        cell = e.Current;
                        var needsSeparator = i != 0;

                        if (needsSeparator)
                        {
                            var placeTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                            await placeTask;
                            cancel.ThrowIfCancellationRequested();
                        }

                        ColumnIdentifier ci;
                        if (i < selfColumnNamesValue.Length)
                        {
                            ci = ColumnIdentifier.Create(i, selfColumnNamesValue[i].Name);
                        }
                        else
                        {
                            ci = ColumnIdentifier.Create(i);
                        }

                        var ctx = WriteContext.WritingColumn(self.RowNumber, ci, self.Context);

                        var formatter = cell.Formatter;
                        var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                        delProvider.Guarantee(self);
                        var del = delProvider.CachedDelegate.Value;

                        var val = cell.Value as object;
                        if (!del(val, in ctx, self.Buffer))
                        {
                            Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
                        }

                        var res = self.Buffer.Buffer;
                        if (res.IsEmpty)
                        {
                            // nothing was written, so just move on
                            goto end;
                        }

                        var writeValueTask = self.WriteValueAsync(res, cancel);
                        await writeValueTask;
                        cancel.ThrowIfCancellationRequested();

                        self.Buffer.Reset();

end:
                        i++;
                    }

                    self.RowNumber++;
                }
                finally
                {
                    e.Dispose();
                }
            }

            // continue after WriteValueAsync completes
            static async ValueTask WriteAsync_ContinueAfterWriteValueAsync(AsyncDynamicWriter self, ValueTask waitFor, int i, IEnumerator<DynamicCellValue> e, CancellationToken cancel)
            {
                try
                {
                    await waitFor;
                    cancel.ThrowIfCancellationRequested();

                    // finish loop
                    {
                        self.Buffer.Reset();

                        i++;
                    }

                    var selfColumnNamesValue = self.ColumnNames.Value;

                    // resume
                    while (e.MoveNext())
                    {
                        var cell = e.Current;
                        var needsSeparator = i != 0;

                        if (needsSeparator)
                        {
                            var placeTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                            await placeTask;
                            cancel.ThrowIfCancellationRequested();
                        }

                        ColumnIdentifier ci;
                        if (i < selfColumnNamesValue.Length)
                        {
                            ci = ColumnIdentifier.Create(i, selfColumnNamesValue[i].Name);
                        }
                        else
                        {
                            ci = ColumnIdentifier.Create(i);
                        }

                        var ctx = WriteContext.WritingColumn(self.RowNumber, ci, self.Context);

                        var formatter = cell.Formatter;
                        var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                        delProvider.Guarantee(self);
                        var del = delProvider.CachedDelegate.Value;

                        var val = cell.Value as object;
                        if (!del(val, in ctx, self.Buffer))
                        {
                            Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
                        }

                        var res = self.Buffer.Buffer;
                        if (res.IsEmpty)
                        {
                            // nothing was written, so just move on
                            goto end;
                        }

                        var writeValueTask = self.WriteValueAsync(res, cancel);
                        await writeValueTask;
                        cancel.ThrowIfCancellationRequested();

                        self.Buffer.Reset();

end:
                        i++;
                    }

                    self.RowNumber++;
                }
                finally
                {
                    e.Dispose();
                }
            }
        }

        public override ValueTask WriteCommentAsync(string comment, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);

            Utils.CheckArgumentNull(comment, nameof(comment));

            var shouldEndRecord = true;
            if (IsFirstRow)
            {
                if (Config.WriteHeader == WriteHeader.Always)
                {
                    return Throw.InvalidOperationException<ValueTask>($"First operation on a dynamic writer cannot be {nameof(WriteCommentAsync)} if configured to write headers, headers cannot be inferred");
                }

                var checkHeaders = CheckHeadersAsync(null, cancel);
                if (!checkHeaders.IsCompletedSuccessfully(this))
                {
                    return WriteCommentAsync_ContinueAfterCheckHeadersAsync(this, checkHeaders, comment, cancel);
                }

                if (!checkHeaders.Result)
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                var endRecordTask = EndRecordAsync(cancel);
                if (!endRecordTask.IsCompletedSuccessfully(this))
                {
                    return WriteCommentAsync_ContinueAfterEndRecordAsync(this, endRecordTask, comment, cancel);
                }
            }

            var (commentChar, segments) = SplitCommentIntoLines(comment);
            
            // we know we can write directly now
            var isFirstRow = true;
            var e = segments.GetEnumerator();
            while (e.MoveNext())
            {
                var seg = e.Current;

                if (!isFirstRow)
                {
                    var endRowTask = EndRecordAsync(cancel);
                    if (!endRowTask.IsCompletedSuccessfully(this))
                    {
                        return WriteCommentAsync_ContinueAfterEndRowAsync(this, endRowTask, commentChar, seg, e, cancel);
                    }
                }

                var placeCharTask = PlaceCharInStagingAsync(commentChar, cancel);
                if (!placeCharTask.IsCompletedSuccessfully(this))
                {
                    return WriteCommentAsync_ContinueAfterPlaceCharAsync(this, placeCharTask, commentChar, seg, e, cancel);
                }

                if (seg.Length > 0)
                {
                    var placeTask = PlaceInStagingAsync(seg, cancel);
                    if (!placeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteCommentAsync_ContinueAfterPlaceSegementAsync(this, placeTask, commentChar, e, cancel);
                    }
                }

                isFirstRow = false;
            }

            return default;

            // continue after CheckHeadersAsync completes
            static async ValueTask WriteCommentAsync_ContinueAfterCheckHeadersAsync(AsyncDynamicWriter self, ValueTask<bool> waitFor, string comment, CancellationToken cancel)
            {
                var shouldEndRecord = true;

                var res = await waitFor;
                cancel.ThrowIfCancellationRequested();

                if (!res)
                {
                    shouldEndRecord = false;
                }

                if (shouldEndRecord)
                {
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;
                    cancel.ThrowIfCancellationRequested();
                }

                var (commentChar, segments) = self.SplitCommentIntoLines(comment);

                // we know we can write directly now
                var isFirstRow = true;
                foreach (var seg in segments)
                {
                    if (!isFirstRow)
                    {
                        var endTask = self.EndRecordAsync(cancel);
                        await endTask;
                        cancel.ThrowIfCancellationRequested();
                    }

                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
                        cancel.ThrowIfCancellationRequested();
                    }

                    isFirstRow = false;
                }
            }

            // continue after EndRecordAsync completes
            static async ValueTask WriteCommentAsync_ContinueAfterEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor, string comment, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var (commentChar, segments) = self.SplitCommentIntoLines(comment);

                // we know we can write directly now
                var isFirstRow = true;
                foreach (var seg in segments)
                {
                    if (!isFirstRow)
                    {
                        var endTask = self.EndRecordAsync(cancel);
                        await endTask;
                        cancel.ThrowIfCancellationRequested();
                    }

                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
                        cancel.ThrowIfCancellationRequested();
                    }

                    isFirstRow = false;
                }
            }

            static async ValueTask WriteCommentAsync_ContinueAfterEndRowAsync(
                AsyncDynamicWriter self, 
                ValueTask waitFor, 
                char commentChar,
                ReadOnlyMemory<char> seg, 
                ReadOnlySequence<char>.Enumerator e, 
                CancellationToken cancel
            )
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                // finish loop
                {
                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
                        cancel.ThrowIfCancellationRequested();
                    }
                }

                // resume
                while (e.MoveNext())
                {
                    seg = e.Current;

                    // by definition, not in the first row so we can skip the if
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;
                    cancel.ThrowIfCancellationRequested();

                    var thirdPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await thirdPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    if (seg.Length > 0)
                    {
                        var fourthPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await fourthPlaceTask;
                        cancel.ThrowIfCancellationRequested();
                    }
                }
            }

            // continue after writing the comment start char completes
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharAsync(
                AsyncDynamicWriter self, 
                ValueTask waitFor, 
                char commentChar,
                ReadOnlyMemory<char> seg, 
                ReadOnlySequence<char>.Enumerator e, 
                CancellationToken cancel
            )
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                // finish the loop
                {
                    if (seg.Length > 0)
                    {
                        var placeTask = self.PlaceInStagingAsync(seg, cancel);
                        await placeTask;
                        cancel.ThrowIfCancellationRequested();
                    }
                }

                // resume
                while (e.MoveNext())
                {
                    seg = e.Current;

                    // by definition, not in the first row so we can skip the if
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;
                    cancel.ThrowIfCancellationRequested();

                    var secondPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await secondPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    if (seg.Length > 0)
                    {
                        var thirdPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await thirdPlaceTask;
                        cancel.ThrowIfCancellationRequested();
                    }
                }
            }

            // continue after writing a chunk of the comment completes
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceSegementAsync(
                AsyncDynamicWriter self, 
                ValueTask waitFor, 
                char commentChar,
                ReadOnlySequence<char>.Enumerator e, 
                CancellationToken cancel
            )
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                while (e.MoveNext())
                {
                    var seg = e.Current;

                    // by definition, not in the first row so we can skip the if
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;
                    cancel.ThrowIfCancellationRequested();

                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
                        cancel.ThrowIfCancellationRequested();
                    }
                }
            }
        }

        private ValueTask WriteHeadersAndEndRowIfNeededAsync(dynamic row, CancellationToken cancel)
        {
            var shouldEndRecord = true;

            if (IsFirstRow)
            {
                var rowAsObj = row as object;
                var checkHeadersTask = CheckHeadersAsync(rowAsObj, cancel);
                if (!checkHeadersTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAndEndRowIfNeededAsync_ContinueAfterCheckHeadersAsync(this, checkHeadersTask, cancel);
                }

                if (!checkHeadersTask.Result)
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                var endRecordTask = EndRecordAsync(cancel);
                if (!endRecordTask.IsCompletedSuccessfully(this))
                {
                    return endRecordTask;
                }
            }

            return default;

            // continue after CheckHeadersAsync completes
            static async ValueTask WriteHeadersAndEndRowIfNeededAsync_ContinueAfterCheckHeadersAsync(AsyncDynamicWriter self, ValueTask<bool> waitFor, CancellationToken cancel)
            {
                var shouldEndRecord = true;

                var res = await waitFor;
                cancel.ThrowIfCancellationRequested();

                if (!res)
                {
                    shouldEndRecord = false;
                }

                if (shouldEndRecord)
                {
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;
                    cancel.ThrowIfCancellationRequested();
                }
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
        private ValueTask<bool> CheckHeadersAsync(dynamic? firstRow, CancellationToken cancel)
        {
            if (Config.WriteHeader == WriteHeader.Never)
            {
                // nothing to write, so bail
                ColumnNames.Value = Array.Empty<(string, string)>();
                return new ValueTask<bool>(false);
            }

            // init columns
            DiscoverColumns(firstRow);

            var writeHeadersTask = WriteHeadersAsync(cancel);
            if (!writeHeadersTask.IsCompletedSuccessfully(this))
            {
                return CheckHeadersAsync_ContinueAfterWriteHeadersAsync(writeHeadersTask, cancel);
            }

            return new ValueTask<bool>(true);

            // continue after WriteHeadersAsync() completes
            static async ValueTask<bool> CheckHeadersAsync_ContinueAfterWriteHeadersAsync(ValueTask waitFor, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                return true;
            }
        }

        private void DiscoverColumns(dynamic o)
        {
            var cols = new List<(string TrueName, string EncodedName)>();

            var ctx = WriteContext.DiscoveringColumns(Context);

            var colIx = 0;
            foreach (var c in Config.TypeDescriber.Value.GetCellsForDynamicRow(in ctx, o as object))
            {
                var colName = c.Name;

                if (colName == null)
                {
                    Throw.InvalidOperationException<object>($"No column name found at index {colIx} when {nameof(Cesil.WriteHeader)} = {Config.WriteHeader}");
                    return;
                }

                var encodedColName = colName;

                // encode it, if it needs encoding
                if (NeedsEncode(encodedColName))
                {
                    encodedColName = Utils.Encode(encodedColName, Config);
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

        private ValueTask WriteHeadersAsync(CancellationToken cancel)
        {
            var columnNamesValue = ColumnNames.Value;
            for (var i = 0; i < columnNamesValue.Length; i++)
            {
                // for the separator
                if (i != 0)
                {
                    var placeCharTask = PlaceCharInStagingAsync(Config.ValueSeparator, cancel);
                    if (!placeCharTask.IsCompletedSuccessfully(this))
                    {
                        return WriteHeadersAsync_ContinueAfterPlaceCharAsync(this, placeCharTask, i, cancel);
                    }
                }

                var colName = columnNamesValue[i].EncodedName;

                // can colName is always gonna be encoded correctly, because we just discovered them
                //   (ie. they're always correct for this config)
                var placeInStagingTask = PlaceInStagingAsync(colName.AsMemory(), cancel);
                if (!placeInStagingTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAsync_ContinueAfterPlaceInStagingAsync(this, placeInStagingTask, i, cancel);
                }
            }

            return default;

            // continue after a PlaceCharInStagingAsync call
            static async ValueTask WriteHeadersAsync_ContinueAfterPlaceCharAsync(AsyncDynamicWriter self, ValueTask waitFor, int i, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var selfColumnNamesValue = self.ColumnNames.Value;

                // finish the loop
                {
                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var placeTask = self.PlaceInStagingAsync(colName.AsMemory(), cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    i++;
                }

                for (; i < selfColumnNamesValue.Length; i++)
                {
                    // by defintion i != 0, so no need for the if
                    var secondPlaceTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                    await secondPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var thirdPlaceTask = self.PlaceInStagingAsync(colName.AsMemory(), cancel);
                    await thirdPlaceTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }

            static async ValueTask WriteHeadersAsync_ContinueAfterPlaceInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, int i, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var selfColumnNamesValue = self.ColumnNames.Value;

                i++;

                for (; i < selfColumnNamesValue.Length; i++)
                {
                    // by defintion i != 0, so no need for the if
                    var placeTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var secondPlaceTask = self.PlaceInStagingAsync(colName.AsMemory(), cancel);
                    await secondPlaceTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }
        }

        public override ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                if (IsFirstRow)
                {
                    var checkHeadersTask = CheckHeadersAsync(null, CancellationToken.None);
                    if (!checkHeadersTask.IsCompletedSuccessfully(this))
                    {
                        return DisposeAsync_ContinueAfterCheckHeadersAsync(this, checkHeadersTask);
                    }
                }

                if (Config.WriteTrailingNewLine == WriteTrailingNewLine.Always)
                {
                    var endRecordTask = EndRecordAsync(CancellationToken.None);
                    if (!endRecordTask.IsCompletedSuccessfully(this))
                    {
                        return DisposeAsync_ContinueAfterEndRecordAsync(this, endRecordTask);
                    }
                }

                if (Staging.HasValue)
                {
                    if (InStaging > 0)
                    {
                        var flushStagingTask = FlushStagingAsync(CancellationToken.None);
                        if (!flushStagingTask.IsCompletedSuccessfully(this))
                        {
                            return DisposeAsync_ContinueAfterFlushStagingAsync(this, flushStagingTask);
                        }
                    }

                    Staging.Value.Dispose();
                }

                var innerDisposeTask = Inner.DisposeAsync();
                if (!innerDisposeTask.IsCompletedSuccessfully(this))
                {
                    return DisposeAsync_ContinueAfterDisposeAsync(this, innerDisposeTask);
                }

                if (OneCharOwner.HasValue)
                {
                    OneCharOwner.Value.Dispose();
                }
                Buffer.Dispose();
                IsDisposed = true;
            }

            return default;

            // continue after CheckHeadersAsync completes
            static async ValueTask DisposeAsync_ContinueAfterCheckHeadersAsync(AsyncDynamicWriter self, ValueTask<bool> waitFor)
            {
                await waitFor;

                if (self.Config.WriteTrailingNewLine == WriteTrailingNewLine.Always)
                {
                    var endTask = self.EndRecordAsync(CancellationToken.None);
                    await endTask;
                }

                if (self.Staging.HasValue)
                {
                    if (self.InStaging > 0)
                    {
                        var flushTask = self.FlushStagingAsync(CancellationToken.None);
                        await flushTask;
                    }

                    self.Staging.Value.Dispose();
                }

                var disposeTask = self.Inner.DisposeAsync();
                await disposeTask;

                if (self.OneCharOwner.HasValue)
                {
                    self.OneCharOwner.Value.Dispose();
                }
                self.Buffer.Dispose();
                self.IsDisposed = true;
            }

            // continue after EndRecordAsync completes
            static async ValueTask DisposeAsync_ContinueAfterEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor)
            {
                await waitFor;

                if (self.Staging.HasValue)
                {
                    if (self.InStaging > 0)
                    {
                        var flushTask = self.FlushStagingAsync(CancellationToken.None);
                        await flushTask;
                    }

                    self.Staging.Value.Dispose();
                }

                var disposeTask = self.Inner.DisposeAsync();
                await disposeTask;

                if (self.OneCharOwner.HasValue)
                {
                    self.OneCharOwner.Value.Dispose();
                }
                self.Buffer.Dispose();
                self.IsDisposed = true;
            }

            // continue after FlushStagingAsync completes
            static async ValueTask DisposeAsync_ContinueAfterFlushStagingAsync(AsyncDynamicWriter self, ValueTask waitFor)
            {
                await waitFor;

                self.Staging.Value.Dispose();

                var disposeTask = self.Inner.DisposeAsync();
                await disposeTask;

                if (self.OneCharOwner.HasValue)
                {
                    self.OneCharOwner.Value.Dispose();
                }
                self.Buffer.Dispose();
                self.IsDisposed = true;
            }

            // continue after Inner.DisposeAsync() completes
            static async ValueTask DisposeAsync_ContinueAfterDisposeAsync(AsyncDynamicWriter self, ValueTask waitFor)
            {
                await waitFor;

                if (self.OneCharOwner.HasValue)
                {
                    self.OneCharOwner.Value.Dispose();
                }
                self.Buffer.Dispose();
                self.IsDisposed = true;
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncDynamicWriter)} with {Config}";
        }
    }
}
