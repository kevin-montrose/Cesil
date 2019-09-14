using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class AsyncDynamicWriter :
        AsyncWriterBase<dynamic>,
        IDelegateCache
    {
        public override bool IsDisposed => Inner == null;

        internal new bool IsFirstRow => ColumnNames == null;

        private Comparison<DynamicCellValue> ColumnNameSorter;
        private (string Name, string EncodedName)[] ColumnNames;

        private readonly object[] DynamicArgumentsBuffer = new object[3];

        private Dictionary<object, Delegate> DelegateCache;

        internal AsyncDynamicWriter(DynamicBoundConfiguration config, IAsyncWriterAdapter inner, object context) : base(config, inner, context) { }

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

            var cellValues = Config.TypeDescriber.GetCellsForDynamicRow(in wholeRowContext, row as object);
            cellValues = ForceInOrder(cellValues);

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

                    var col = i < ColumnNames.Length ? ColumnNames[i].Name : null;

                    var ctx = WriteContext.WritingColumn(RowNumber, ColumnIdentifier.Create(i, col), Context);

                    var formatter = cell.Formatter;
                    var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                    delProvider.Guarantee(this);
                    var del = delProvider.CachedDelegate;

                    var val = cell.Value as object;
                    if (!del(val, in ctx, Buffer))
                    {
                        return Throw.SerializationException<ValueTask>($"Could not write column {col}, formatter {formatter} returned false");
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

                var wholeRowContext = WriteContext.DiscoveringCells(self.RowNumber, self.Context);

                var cellValues = self.Config.TypeDescriber.GetCellsForDynamicRow(in wholeRowContext, row as object);
                cellValues = self.ForceInOrder(cellValues);

                var i = 0;
                foreach (var cell in cellValues)
                {
                    var needsSeparator = i != 0;

                    if (needsSeparator)
                    {
                        var placeTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                        await placeTask;
                    }

                    var col = i < self.ColumnNames.Length ? self.ColumnNames[i].Name : null;

                    var ctx = WriteContext.WritingColumn(self.RowNumber, ColumnIdentifier.Create(i, col), self.Context);

                    var formatter = cell.Formatter;
                    var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                    delProvider.Guarantee(self);
                    var del = delProvider.CachedDelegate;

                    var val = cell.Value as object;
                    if (!del(val, in ctx, self.Buffer))
                    {
                        Throw.SerializationException<object>($"Could not write column {col}, formatter {formatter} returned false");
                    }

                    var res = self.Buffer.Buffer;
                    if (res.IsEmpty)
                    {
                        // nothing was written, so just move on
                        goto end;
                    }
                    
                    var writeValueTask = self.WriteValueAsync(res, cancel);
                    await writeValueTask;
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

                    // finish the loop
                    {
                        var col = i < self.ColumnNames.Length ? self.ColumnNames[i].Name : null;

                        var ctx = WriteContext.WritingColumn(self.RowNumber, ColumnIdentifier.Create(i, col), self.Context);

                        var formatter = cell.Formatter;
                        var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                        delProvider.Guarantee(self);
                        var del = delProvider.CachedDelegate;

                        var val = cell.Value as object;
                        if (!del(val, in ctx, self.Buffer))
                        {
                            Throw.SerializationException<object>($"Could not write column {col}, formatter {formatter} returned false");
                        }

                        var res = self.Buffer.Buffer;
                        if (res.IsEmpty)
                        {
                            // nothing was written, so just move on
                            goto end;
                        }

                        var writeValueTask = self.WriteValueAsync(res, cancel);
                        await writeValueTask;
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
                        }

                        var col = i < self.ColumnNames.Length ? self.ColumnNames[i].Name : null;

                        var ctx = WriteContext.WritingColumn(self.RowNumber, ColumnIdentifier.Create(i, col), self.Context);

                        var formatter = cell.Formatter;
                        var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                        delProvider.Guarantee(self);
                        var del = delProvider.CachedDelegate;

                        var val = cell.Value as object;
                        if (!del(val, in ctx, self.Buffer))
                        {
                            Throw.SerializationException<object>($"Could not write column {col}, formatter {formatter} returned false");
                        }

                        var res = self.Buffer.Buffer;
                        if (res.IsEmpty)
                        {
                            // nothing was written, so just move on
                            goto end;
                        }

                        var writeValueTask = self.WriteValueAsync(res, cancel);
                        await writeValueTask;
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

                    // finish loop
                    {
                        self.Buffer.Reset();

                        i++;
                    }

                    // resume
                    while (e.MoveNext())
                    {
                        var cell = e.Current;
                        var needsSeparator = i != 0;

                        if (needsSeparator)
                        {
                            var placeTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                            await placeTask;
                        }

                        var col = i < self.ColumnNames.Length ? self.ColumnNames[i].Name : null;

                        var ctx = WriteContext.WritingColumn(self.RowNumber, ColumnIdentifier.Create(i, col), self.Context);

                        var formatter = cell.Formatter;
                        var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                        delProvider.Guarantee(self);
                        var del = delProvider.CachedDelegate;

                        var val = cell.Value as object;
                        if (!del(val, in ctx, self.Buffer))
                        {
                            Throw.SerializationException<object>($"Could not write column {col}, formatter {formatter} returned false");
                        }

                        var res = self.Buffer.Buffer;
                        if (res.IsEmpty)
                        {
                            // nothing was written, so just move on
                            goto end;
                        }

                        var writeValueTask = self.WriteValueAsync(res, cancel);
                        await writeValueTask;
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
            if (comment == null)
            {
                return Throw.ArgumentNullException<ValueTask>(nameof(comment));
            }

            AssertNotDisposed(this);

            var shouldEndRecord = true;
            if (IsFirstRow)
            {
                if (Config.WriteHeader == WriteHeaders.Always)
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

            var segments = SplitCommentIntoLines(comment);

            var commentChar = Config.CommentChar.Value;

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
                        return WriteCommentAsync_ContinueAfterEndRowAsync(this, endRowTask, seg, e, cancel);
                    }
                }

                var placeCharTask = PlaceCharInStagingAsync(commentChar, cancel);
                if (!placeCharTask.IsCompletedSuccessfully(this))
                {
                    return WriteCommentAsync_ContinueAfterPlaceCharAsync(this, placeCharTask, seg, e, cancel);
                }

                if (seg.Length > 0)
                {
                    var placeTask = PlaceInStagingAsync(seg, cancel);
                    if (!placeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteCommentAsync_ContinueAfterPlaceSegementAsync(this, placeTask, e, cancel);
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

                if (!res)
                {
                    shouldEndRecord = false;
                }

                if (shouldEndRecord)
                {
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;
                }

                var segments = self.SplitCommentIntoLines(comment);

                var commentChar = self.Config.CommentChar.Value;

                // we know we can write directly now
                var isFirstRow = true;
                foreach (var seg in segments)
                {
                    if (!isFirstRow)
                    {
                        var endTask = self.EndRecordAsync(cancel);
                        await endTask;
                    }

                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
                    }

                    isFirstRow = false;
                }
            }

            // continue after EndRecordAsync completes
            static async ValueTask WriteCommentAsync_ContinueAfterEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor, string comment, CancellationToken cancel)
            {
                await waitFor;

                var segments = self.SplitCommentIntoLines(comment);

                var commentChar = self.Config.CommentChar.Value;

                // we know we can write directly now
                var isFirstRow = true;
                foreach (var seg in segments)
                {
                    if (!isFirstRow)
                    {
                        var endTask = self.EndRecordAsync(cancel);
                        await endTask;
                    }

                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
                    }

                    isFirstRow = false;
                }
            }

            static async ValueTask WriteCommentAsync_ContinueAfterEndRowAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                var commentChar = self.Config.CommentChar.Value;

                // finish loop
                {
                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
                    }
                }

                // resume
                while (e.MoveNext())
                {
                    seg = e.Current;

                    // by definition, not in the first row so we can skip the if
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;

                    var thirdPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await thirdPlaceTask;
                    if (seg.Length > 0)
                    {
                        var fourthPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await fourthPlaceTask;
                    }
                }
            }

            // continue after writing the comment start char completes
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                // finish the loop
                {
                    if (seg.Length > 0)
                    {
                        var placeTask = self.PlaceInStagingAsync(seg, cancel);
                        await placeTask;
                    }
                }

                var commentChar = self.Config.CommentChar.Value;

                // resume
                while (e.MoveNext())
                {
                    seg = e.Current;

                    // by definition, not in the first row so we can skip the if
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;

                    var secondPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await secondPlaceTask;
                    if (seg.Length > 0)
                    {
                        var thirdPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await thirdPlaceTask;
                    }
                }
            }

            // continue after writing a chunk of the comment completes
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceSegementAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                var commentChar = self.Config.CommentChar.Value;

                while (e.MoveNext())
                {
                    var seg = e.Current;

                    // by definition, not in the first row so we can skip the if
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;

                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
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
                if (!res)
                {
                    shouldEndRecord = false;
                }

                if (shouldEndRecord)
                {
                    var endTask = self.EndRecordAsync(cancel);
                    await endTask;
                }
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
        private ValueTask<bool> CheckHeadersAsync(dynamic firstRow, CancellationToken cancel)
        {
            if (Config.WriteHeader == WriteHeaders.Never)
            {
                // nothing to write, so bail
                ColumnNames = Array.Empty<(string, string)>();
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

                return true;
            }
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

        private ValueTask WriteHeadersAsync(CancellationToken cancel)
        {
            for (var i = 0; i < ColumnNames.Length; i++)
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

                var colName = ColumnNames[i].EncodedName;

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

                // finish the loop
                {
                    var colName = self.ColumnNames[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var placeTask = self.PlaceInStagingAsync(colName.AsMemory(), cancel);
                    await placeTask;

                    i++;
                }

                for (; i < self.ColumnNames.Length; i++)
                {
                    // by defintion i != 0, so no need for the if
                    var secondPlaceTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                    await secondPlaceTask;

                    var colName = self.ColumnNames[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var thirdPlaceTask = self.PlaceInStagingAsync(colName.AsMemory(), cancel);
                    await thirdPlaceTask;
                }
            }

            static async ValueTask WriteHeadersAsync_ContinueAfterPlaceInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, int i, CancellationToken cancel)
            {
                await waitFor;

                i++;

                for (; i < self.ColumnNames.Length; i++)
                {
                    // by defintion i != 0, so no need for the if
                    var placeTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                    await placeTask;

                    var colName = self.ColumnNames[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var secondPlaceTask = self.PlaceInStagingAsync(colName.AsMemory(), cancel);
                    await secondPlaceTask;
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

                if (Config.WriteTrailingNewLine == WriteTrailingNewLines.Always)
                {
                    var endRecordTask = EndRecordAsync(CancellationToken.None);
                    if (!endRecordTask.IsCompletedSuccessfully(this))
                    {
                        return DisposeAsync_ContinueAfterEndRecordAsync(this, endRecordTask);
                    }
                }

                if (HasBuffer)
                {
                    if (InStaging > 0)
                    {
                        var flushStagingTask = FlushStagingAsync(CancellationToken.None);
                        if (!flushStagingTask.IsCompletedSuccessfully(this))
                        {
                            return DisposeAsync_ContinueAfterFlushStagingAsync(this, flushStagingTask);
                        }
                    }

                    Staging.Dispose();
                }

                var innerDisposeTask = Inner.DisposeAsync();
                if (!innerDisposeTask.IsCompletedSuccessfully(this))
                {
                    return DisposeAsync_ContinueAfterDisposeAsync(this, innerDisposeTask);
                }

                OneCharOwner?.Dispose();
                Buffer.Dispose();
                Inner = null;
            }

            return default;

            // continue after CheckHeadersAsync completes
            static async ValueTask DisposeAsync_ContinueAfterCheckHeadersAsync(AsyncDynamicWriter self, ValueTask<bool> waitFor)
            {
                await waitFor;

                if (self.Config.WriteTrailingNewLine == WriteTrailingNewLines.Always)
                {
                    var endTask = self.EndRecordAsync(CancellationToken.None);
                    await endTask;
                }

                if (self.HasBuffer)
                {
                    if (self.InStaging > 0)
                    {
                        var flushTask = self.FlushStagingAsync(CancellationToken.None);
                        await flushTask;
                    }

                    self.Staging.Dispose();
                }

                var disposeTask = self.Inner.DisposeAsync();
                await disposeTask;

                self.OneCharOwner?.Dispose();
                self.Buffer.Dispose();
                self.Inner = null;
            }

            // continue after EndRecordAsync completes
            static async ValueTask DisposeAsync_ContinueAfterEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor)
            {
                await waitFor;

                if (self.HasBuffer)
                {
                    if (self.InStaging > 0)
                    {
                        var flushTask = self.FlushStagingAsync(CancellationToken.None);
                        await flushTask;
                    }

                    self.Staging.Dispose();
                }

                var disposeTask = self.Inner.DisposeAsync();
                await disposeTask;

                self.OneCharOwner?.Dispose();
                self.Buffer.Dispose();
                self.Inner = null;
            }

            // continue after FlushStagingAsync completes
            static async ValueTask DisposeAsync_ContinueAfterFlushStagingAsync(AsyncDynamicWriter self, ValueTask waitFor)
            {
                await waitFor;

                self.Staging.Dispose();

                var disposeTask = self.Inner.DisposeAsync();
                await disposeTask;

                self.OneCharOwner?.Dispose();
                self.Buffer.Dispose();
                self.Inner = null;
            }

            // continue after Inner.DisposeAsync() completes
            static async ValueTask DisposeAsync_ContinueAfterDisposeAsync(AsyncDynamicWriter self, ValueTask waitFor)
            {
                await waitFor;

                self.OneCharOwner?.Dispose();
                self.Buffer.Dispose();
                self.Inner = null;
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncDynamicWriter)} with {Config}";
        }
    }
}
