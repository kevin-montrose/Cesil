using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using static Cesil.AwaitHelper;
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

        private bool HasWrittenComments;

        internal AsyncDynamicWriter(DynamicBoundConfiguration config, IAsyncWriterAdapter inner, object? context) : base(config, inner, context) { }

        bool IDelegateCache.TryGetDelegate<T, V>(T key, [MaybeNullWhen(returnValue: false)] out V del)
            where V : class
        {
            if (DelegateCache == null)
            {
                del = default;
                return false;
            }

            if (DelegateCache.TryGetValue(key, out var untypedDel))
            {
                del = (V)untypedDel;
                return true;
            }

            del = default;
            return false;
        }

        void IDelegateCache.AddDelegate<T, V>(T key, V cached)
        {
            if (DelegateCache == null)
            {
                DelegateCache = new Dictionary<object, Delegate>();
            }

            DelegateCache.Add(key, (Delegate)(object)cached);
        }

        public override ValueTask WriteAsync(dynamic row, CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                var rowAsObj = row as object;

                var options = Configuration.Options;
                var typeDescriber = options.TypeDescriber;
                var valueSeparator = options.ValueSeparator;

                var writeHeadersTask = WriteHeadersAndEndRowIfNeededAsync(rowAsObj, cancellationToken);
                if (!writeHeadersTask.IsCompletedSuccessfully(this))
                {
                    return WriteAsync_ContinueAfterWriteHeadersAsync(this, writeHeadersTask, row, typeDescriber, valueSeparator, cancellationToken);
                }

                var wholeRowContext = WriteContext.DiscoveringCells(Configuration.Options, RowNumber, Context);

                var cellValues = typeDescriber.GetCellsForDynamicRow(in wholeRowContext, row as object);
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
                            var placeCharTask = PlaceCharInStagingAsync(valueSeparator, cancellationToken);
                            if (!placeCharTask.IsCompletedSuccessfully(this))
                            {
                                disposeE = false;
                                return WriteAsync_ContinueAfterPlaceCharAsync(this, placeCharTask, valueSeparator, cell, i, e, cancellationToken);
                            }
                        }

                        ColumnIdentifier ci;
                        if (i < columnNamesValue.Length)
                        {
                            ci = ColumnIdentifier.CreateInner(i, columnNamesValue[i].Name);
                        }
                        else
                        {
                            ci = ColumnIdentifier.Create(i);
                        }

                        var ctx = WriteContext.WritingColumn(Configuration.Options, RowNumber, ci, Context);

                        var formatter = cell.Formatter;
                        var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                        var del = delProvider.Guarantee(this);

                        var val = cell.Value as object;
                        if (!del(val, in ctx, Buffer))
                        {
                            return Throw.SerializationException<ValueTask>($"Could not write column {ci}, formatter {formatter} returned false");
                        }

                        ReadOnlySequence<char> res = default;
                        if (!Buffer.MakeSequence(ref res))
                        {
                            // nothing was written, so just move on
                            goto end;
                        }

                        var writeValueTask = WriteValueAsync(res, cancellationToken);
                        if (!writeValueTask.IsCompletedSuccessfully(this))
                        {
                            disposeE = false;
                            return WriteAsync_ContinueAfterWriteValueAsync(this, writeValueTask, valueSeparator, i, e, cancellationToken);
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
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask>(this, e);
            }

            // continue after WriteHeadersAndRowIfNeededAsync completes
            static async ValueTask WriteAsync_ContinueAfterWriteHeadersAsync(AsyncDynamicWriter self, ValueTask waitFor, dynamic row, ITypeDescriber typeDescriber, char valueSeparator, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    var wholeRowContext = WriteContext.DiscoveringCells(self.Configuration.Options, self.RowNumber, self.Context);

                    var cellValues = typeDescriber.GetCellsForDynamicRow(in wholeRowContext, row as object);
                    cellValues = self.ForceInOrder(cellValues);

                    var selfColumnNamesValue = self.ColumnNames.Value;

                    var i = 0;
                    foreach (var cell in cellValues)
                    {
                        var needsSeparator = i != 0;

                        if (needsSeparator)
                        {
                            var placeTask = self.PlaceCharInStagingAsync(valueSeparator, cancellationToken);
                            await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }

                        ColumnIdentifier ci;
                        if (i < selfColumnNamesValue.Length)
                        {
                            ci = ColumnIdentifier.CreateInner(i, selfColumnNamesValue[i].Name);
                        }
                        else
                        {
                            ci = ColumnIdentifier.Create(i);
                        }

                        var ctx = WriteContext.WritingColumn(self.Configuration.Options, self.RowNumber, ci, self.Context);

                        var formatter = cell.Formatter;
                        var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                        var del = delProvider.Guarantee(self);

                        var val = cell.Value as object;
                        if (!del(val, in ctx, self.Buffer))
                        {
                            Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
                        }

                        ReadOnlySequence<char> res = default;
                        if (!self.Buffer.MakeSequence(ref res))
                        {
                            // nothing was written, so just move on
                            goto end;
                        }

                        var writeValueTask = self.WriteValueAsync(res, cancellationToken);
                        await ConfigureCancellableAwait(self, writeValueTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        self.Buffer.Reset();

end:
                        i++;
                    }

                    self.RowNumber++;
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after PlaceCharInStagingAsync completes
            static async ValueTask WriteAsync_ContinueAfterPlaceCharAsync(AsyncDynamicWriter self, ValueTask waitFor, char valueSeparator, DynamicCellValue cell, int i, IEnumerator<DynamicCellValue> e, CancellationToken cancellationToken)
            {
                try
                {

                    try
                    {
                        await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        var selfColumnNamesValue = self.ColumnNames.Value;

                        // finish the loop
                        {
                            ColumnIdentifier ci;
                            if (i < selfColumnNamesValue.Length)
                            {
                                ci = ColumnIdentifier.CreateInner(i, selfColumnNamesValue[i].Name);
                            }
                            else
                            {
                                ci = ColumnIdentifier.Create(i);
                            }

                            var ctx = WriteContext.WritingColumn(self.Configuration.Options, self.RowNumber, ci, self.Context);

                            var formatter = cell.Formatter;
                            var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                            var del = delProvider.Guarantee(self);

                            var val = cell.Value as object;
                            if (!del(val, in ctx, self.Buffer))
                            {
                                Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
                            }

                            ReadOnlySequence<char> res = default;
                            if (!self.Buffer.MakeSequence(ref res))
                            {
                                // nothing was written, so just move on
                                goto end;
                            }

                            var writeValueTask = self.WriteValueAsync(res, cancellationToken);
                            await ConfigureCancellableAwait(self, writeValueTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);

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
                                var placeTask = self.PlaceCharInStagingAsync(valueSeparator, cancellationToken);
                                await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                                CheckCancellation(self, cancellationToken);
                            }

                            ColumnIdentifier ci;
                            if (i < selfColumnNamesValue.Length)
                            {
                                ci = ColumnIdentifier.CreateInner(i, selfColumnNamesValue[i].Name);
                            }
                            else
                            {
                                ci = ColumnIdentifier.Create(i);
                            }

                            var ctx = WriteContext.WritingColumn(self.Configuration.Options, self.RowNumber, ci, self.Context);

                            var formatter = cell.Formatter;
                            var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                            var del = delProvider.Guarantee(self);

                            var val = cell.Value as object;
                            if (!del(val, in ctx, self.Buffer))
                            {
                                Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
                            }

                            ReadOnlySequence<char> res = default;
                            if (!self.Buffer.MakeSequence(ref res))
                            {
                                // nothing was written, so just move on
                                goto end;
                            }

                            var writeValueTask = self.WriteValueAsync(res, cancellationToken);
                            await ConfigureCancellableAwait(self, writeValueTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);

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
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }

            // continue after WriteValueAsync completes
            static async ValueTask WriteAsync_ContinueAfterWriteValueAsync(AsyncDynamicWriter self, ValueTask waitFor, char valueSeparator, int i, IEnumerator<DynamicCellValue> e, CancellationToken cancellationToken)
            {
                try
                {

                    try
                    {
                        await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                        CheckCancellation(self, cancellationToken);

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
                                var placeTask = self.PlaceCharInStagingAsync(valueSeparator, cancellationToken);
                                await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                                CheckCancellation(self, cancellationToken);
                            }

                            ColumnIdentifier ci;
                            if (i < selfColumnNamesValue.Length)
                            {
                                ci = ColumnIdentifier.CreateInner(i, selfColumnNamesValue[i].Name);
                            }
                            else
                            {
                                ci = ColumnIdentifier.Create(i);
                            }

                            var ctx = WriteContext.WritingColumn(self.Configuration.Options, self.RowNumber, ci, self.Context);

                            var formatter = cell.Formatter;
                            var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                            var del = delProvider.Guarantee(self);

                            var val = cell.Value as object;
                            if (!del(val, in ctx, self.Buffer))
                            {
                                Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
                            }

                            ReadOnlySequence<char> res = default;
                            if (!self.Buffer.MakeSequence(ref res))
                            {
                                // nothing was written, so just move on
                                goto end;
                            }

                            var writeValueTask = self.WriteValueAsync(res, cancellationToken);
                            await ConfigureCancellableAwait(self, writeValueTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);

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
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }
        }

        public override ValueTask WriteCommentAsync(string comment, CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            Utils.CheckArgumentNull(comment, nameof(comment));

            try
            {
                var shouldEndRecord = true;
                if (IsFirstRow)
                {
                    if (Configuration.Options.WriteHeader == WriteHeader.Always)
                    {
                        if (!HasWrittenComments)
                        {
                            shouldEndRecord = false;
                        }
                    }
                    else
                    {
                        var checkHeaders = CheckHeadersAsync(null, cancellationToken);
                        if (!checkHeaders.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterCheckHeadersAsync(this, checkHeaders, comment, cancellationToken);
                        }

                        if (!checkHeaders.Result)
                        {
                            shouldEndRecord = false;
                        }
                    }
                }

                if (shouldEndRecord)
                {
                    var endRecordTask = EndRecordAsync(cancellationToken);
                    if (!endRecordTask.IsCompletedSuccessfully(this))
                    {
                        return WriteCommentAsync_ContinueAfterEndRecordAsync(this, endRecordTask, comment, cancellationToken);
                    }
                }

                var (commentChar, segments) = SplitCommentIntoLines(comment);

                // we know we can write directly now
                var isFirstRow = true;
                var e = segments.GetEnumerator();
                while (e.MoveNext())
                {
                    HasWrittenComments = true;

                    var seg = e.Current;

                    if (!isFirstRow)
                    {
                        var endRowTask = EndRecordAsync(cancellationToken);
                        if (!endRowTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterEndRowAsync(this, endRowTask, commentChar, seg, e, cancellationToken);
                        }
                    }

                    var placeCharTask = PlaceCharInStagingAsync(commentChar, cancellationToken);
                    if (!placeCharTask.IsCompletedSuccessfully(this))
                    {
                        return WriteCommentAsync_ContinueAfterPlaceCharAsync(this, placeCharTask, commentChar, seg, e, cancellationToken);
                    }

                    if (seg.Length > 0)
                    {
                        var placeTask = PlaceInStagingAsync(seg, cancellationToken);
                        if (!placeTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterPlaceSegementAsync(this, placeTask, commentChar, e, cancellationToken);
                        }
                    }

                    isFirstRow = false;
                }

                return default;
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask>(this, e);
            }

            // continue after CheckHeadersAsync completes
            static async ValueTask WriteCommentAsync_ContinueAfterCheckHeadersAsync(AsyncDynamicWriter self, ValueTask<bool> waitFor, string comment, CancellationToken cancellationToken)
            {
                try
                {

                    var shouldEndRecord = true;

                    var res = await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    if (!res)
                    {
                        shouldEndRecord = false;
                    }

                    if (shouldEndRecord)
                    {
                        var endTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);
                    }

                    var (commentChar, segments) = self.SplitCommentIntoLines(comment);

                    // we know we can write directly now
                    var isFirstRow = true;
                    foreach (var seg in segments)
                    {
                        self.HasWrittenComments = true;

                        if (!isFirstRow)
                        {
                            var endTask = self.EndRecordAsync(cancellationToken);
                            await ConfigureCancellableAwait(self, endTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }

                        var placeTask = self.PlaceCharInStagingAsync(commentChar, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        if (seg.Length > 0)
                        {
                            var secondPlaceTask = self.PlaceInStagingAsync(seg, cancellationToken);
                            await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }

                        isFirstRow = false;
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after EndRecordAsync completes
            static async ValueTask WriteCommentAsync_ContinueAfterEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor, string comment, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    var (commentChar, segments) = self.SplitCommentIntoLines(comment);

                    // we know we can write directly now
                    var isFirstRow = true;
                    foreach (var seg in segments)
                    {
                        self.HasWrittenComments = true;

                        if (!isFirstRow)
                        {
                            var endTask = self.EndRecordAsync(cancellationToken);
                            await ConfigureCancellableAwait(self, endTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }

                        var placeTask = self.PlaceCharInStagingAsync(commentChar, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        if (seg.Length > 0)
                        {
                            var secondPlaceTask = self.PlaceInStagingAsync(seg, cancellationToken);
                            await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }

                        isFirstRow = false;
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            static async ValueTask WriteCommentAsync_ContinueAfterEndRowAsync(
                AsyncDynamicWriter self,
                ValueTask waitFor,
                char commentChar,
                ReadOnlyMemory<char> seg,
                ReadOnlySequence<char>.Enumerator e,
                CancellationToken cancellationToken
            )
            {
                try
                {

                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    // finish loop
                    {
                        var placeTask = self.PlaceCharInStagingAsync(commentChar, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        if (seg.Length > 0)
                        {
                            var secondPlaceTask = self.PlaceInStagingAsync(seg, cancellationToken);
                            await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }
                    }

                    // resume
                    while (e.MoveNext())
                    {
                        seg = e.Current;

                        // by definition, not in the first row so we can skip the if
                        var endTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        var thirdPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancellationToken);
                        await ConfigureCancellableAwait(self, thirdPlaceTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        if (seg.Length > 0)
                        {
                            var fourthPlaceTask = self.PlaceInStagingAsync(seg, cancellationToken);
                            await ConfigureCancellableAwait(self, fourthPlaceTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }
                    }
                }
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }

            // continue after writing the comment start char completes
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharAsync(
                AsyncDynamicWriter self,
                ValueTask waitFor,
                char commentChar,
                ReadOnlyMemory<char> seg,
                ReadOnlySequence<char>.Enumerator e,
                CancellationToken cancellationToken
            )
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    // finish the loop
                    {
                        if (seg.Length > 0)
                        {
                            var placeTask = self.PlaceInStagingAsync(seg, cancellationToken);
                            await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }
                    }

                    // resume
                    while (e.MoveNext())
                    {
                        seg = e.Current;

                        // by definition, not in the first row so we can skip the if
                        var endTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        var secondPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancellationToken);
                        await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        if (seg.Length > 0)
                        {
                            var thirdPlaceTask = self.PlaceInStagingAsync(seg, cancellationToken);
                            await ConfigureCancellableAwait(self, thirdPlaceTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }
                    }
                }
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }

            // continue after writing a chunk of the comment completes
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceSegementAsync(
                AsyncDynamicWriter self,
                ValueTask waitFor,
                char commentChar,
                ReadOnlySequence<char>.Enumerator e,
                CancellationToken cancellationToken
            )
            {
                try
                {

                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    while (e.MoveNext())
                    {
                        var seg = e.Current;

                        // by definition, not in the first row so we can skip the if
                        var endTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        var placeTask = self.PlaceCharInStagingAsync(commentChar, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);

                        if (seg.Length > 0)
                        {
                            var secondPlaceTask = self.PlaceInStagingAsync(seg, cancellationToken);
                            await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
                            CheckCancellation(self, cancellationToken);
                        }
                    }

                }
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }
        }

        private ValueTask WriteHeadersAndEndRowIfNeededAsync(dynamic row, CancellationToken cancellationToken)
        {
            var shouldEndRecord = true;

            if (IsFirstRow)
            {
                var rowAsObj = row as object;
                var checkHeadersTask = CheckHeadersAsync(rowAsObj, cancellationToken);
                if (!checkHeadersTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAndEndRowIfNeededAsync_ContinueAfterCheckHeadersAsync(this, checkHeadersTask, cancellationToken);
                }

                if (!checkHeadersTask.Result)
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                var endRecordTask = EndRecordAsync(cancellationToken);
                if (!endRecordTask.IsCompletedSuccessfully(this))
                {
                    return endRecordTask;
                }
            }

            return default;

            // continue after CheckHeadersAsync completes
            static async ValueTask WriteHeadersAndEndRowIfNeededAsync_ContinueAfterCheckHeadersAsync(AsyncDynamicWriter self, ValueTask<bool> waitFor, CancellationToken cancellationToken)
            {
                var shouldEndRecord = true;

                var res = await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                if (!res)
                {
                    shouldEndRecord = false;
                }

                if (shouldEndRecord)
                {
                    var endTask = self.EndRecordAsync(cancellationToken);
                    await ConfigureCancellableAwait(self, endTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
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

                var (name, _) = columnNamesValue[i];
                if (!name.Equals(x.Name))
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
        private ValueTask<bool> CheckHeadersAsync(dynamic? firstRow, CancellationToken cancellationToken)
        {
            if (Configuration.Options.WriteHeader == WriteHeader.Never)
            {
                // nothing to write, so bail
                ColumnNames.Value = Array.Empty<(string, string)>();
                return new ValueTask<bool>(false);
            }

            // init columns
            DiscoverColumns(firstRow);

            var writeHeadersTask = WriteHeadersAsync(cancellationToken);
            if (!writeHeadersTask.IsCompletedSuccessfully(this))
            {
                return CheckHeadersAsync_ContinueAfterWriteHeadersAsync(this, writeHeadersTask, cancellationToken);
            }

            return new ValueTask<bool>(true);

            // continue after WriteHeadersAsync() completes
            static async ValueTask<bool> CheckHeadersAsync_ContinueAfterWriteHeadersAsync(AsyncDynamicWriter self, ValueTask waitFor, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                return true;
            }
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
                    Throw.InvalidOperationException<object>($"No column name found at index {colIx} when {nameof(WriteHeader)} = {options.WriteHeader}");
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

        private ValueTask WriteHeadersAsync(CancellationToken cancellationToken)
        {
            var valueSeparator = Configuration.Options.ValueSeparator;

            var columnNamesValue = ColumnNames.Value;
            for (var i = 0; i < columnNamesValue.Length; i++)
            {
                if (i != 0)
                {
                    // first value doesn't get a separator
                    var placeCharTask = PlaceCharInStagingAsync(valueSeparator, cancellationToken);
                    if (!placeCharTask.IsCompletedSuccessfully(this))
                    {
                        return WriteHeadersAsync_ContinueAfterStartOfForAsync(this, placeCharTask, valueSeparator, i, cancellationToken);
                    }
                }
                else
                {
                    // if we're going to write any headers... before we 
                    //   write the first one we need to check if
                    //   we need to end the previous record... which only happens
                    //   if we've written comments _before_ the header
                    if (HasWrittenComments)
                    {
                        var endRecordTask = EndRecordAsync(cancellationToken);
                        if (!endRecordTask.IsCompletedSuccessfully(this))
                        {
                            return WriteHeadersAsync_ContinueAfterStartOfForAsync(this, endRecordTask, valueSeparator, i, cancellationToken);
                        }
                    }
                }

                var colName = columnNamesValue[i].EncodedName;

                // can colName is always gonna be encoded correctly, because we just discovered them
                //   (ie. they're always correct for this config)
                var placeInStagingTask = PlaceInStagingAsync(colName.AsMemory(), cancellationToken);
                if (!placeInStagingTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAsync_ContinueAfterPlaceInStagingAsync(this, placeInStagingTask, valueSeparator, i, cancellationToken);
                }
            }

            return default;

            // continue after a PlaceCharInStagingAsync or EndRecordAsync call
            //   we can share this because both branches go into the same next step
            static async ValueTask WriteHeadersAsync_ContinueAfterStartOfForAsync(AsyncDynamicWriter self, ValueTask waitFor, char valueSeparator, int i, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                var selfColumnNamesValue = self.ColumnNames.Value;

                // finish the loop
                {
                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var placeTask = self.PlaceInStagingAsync(colName.AsMemory(), cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    i++;
                }

                for (; i < selfColumnNamesValue.Length; i++)
                {
                    // by definition i != 0, so no need for the if
                    var secondPlaceTask = self.PlaceCharInStagingAsync(valueSeparator, cancellationToken);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var thirdPlaceTask = self.PlaceInStagingAsync(colName.AsMemory(), cancellationToken);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }
            }

            static async ValueTask WriteHeadersAsync_ContinueAfterPlaceInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, char valueSeparator, int i, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                var selfColumnNamesValue = self.ColumnNames.Value;

                i++;

                for (; i < selfColumnNamesValue.Length; i++)
                {
                    // by definition i != 0, so no need for the if
                    var placeTask = self.PlaceCharInStagingAsync(valueSeparator, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var secondPlaceTask = self.PlaceInStagingAsync(colName.AsMemory(), cancellationToken);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }
            }
        }

        public override ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                try
                {
                    var writeTrailingNewLine = Configuration.Options.WriteTrailingRowEnding;

                    if (IsFirstRow)
                    {
                        var checkHeadersTask = CheckHeadersAsync(null, CancellationToken.None);
                        if (!checkHeadersTask.IsCompletedSuccessfully(this))
                        {
                            return DisposeAsync_ContinueAfterCheckHeadersAsync(this, checkHeadersTask, writeTrailingNewLine);
                        }
                    }

                    if (writeTrailingNewLine == WriteTrailingRowEnding.Always)
                    {
                        var endRecordTask = EndRecordAsync(CancellationToken.None);
                        if (!endRecordTask.IsCompletedSuccessfully(this))
                        {
                            return DisposeAsync_ContinueAfterEndRecordAsync(this, endRecordTask);
                        }
                    }

                    if (HasStaging)
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
                        Staging = EmptyMemoryOwner.Singleton;
                        StagingMemory = Memory<char>.Empty;
                    }

                    var innerDisposeTask = Inner.DisposeAsync();
                    if (!innerDisposeTask.IsCompletedSuccessfully(this))
                    {
                        return DisposeAsync_ContinueAfterDisposeAsync(this, innerDisposeTask);
                    }

                    if (OneCharOwner.HasValue)
                    {
                        OneCharOwner.Value.Dispose();
                        OneCharOwner.Clear();
                    }
                    Buffer.Dispose();
                }
                catch (Exception e)
                {
                    if (HasStaging)
                    {
                        Staging.Dispose();
                        Staging = EmptyMemoryOwner.Singleton;
                        StagingMemory = Memory<char>.Empty;
                    }

                    if (OneCharOwner.HasValue)
                    {
                        OneCharOwner.Value.Dispose();
                        OneCharOwner.Clear();
                    }

                    Buffer.Dispose();

                    return Throw.PoisonAndRethrow<ValueTask>(this, e);
                }
            }

            return default;

            // continue after CheckHeadersAsync completes
            static async ValueTask DisposeAsync_ContinueAfterCheckHeadersAsync(AsyncDynamicWriter self, ValueTask<bool> waitFor, WriteTrailingRowEnding writeTrailingNewLine)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    if (writeTrailingNewLine == WriteTrailingRowEnding.Always)
                    {
                        var endTask = self.EndRecordAsync(CancellationToken.None);
                        await ConfigureCancellableAwait(self, endTask, CancellationToken.None);
                    }

                    if (self.HasStaging)
                    {
                        if (self.InStaging > 0)
                        {
                            var flushTask = self.FlushStagingAsync(CancellationToken.None);
                            await ConfigureCancellableAwait(self, flushTask, CancellationToken.None);
                        }

                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    var disposeTask = self.Inner.DisposeAsync();
                    await ConfigureCancellableAwait(self, disposeTask, CancellationToken.None);

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }
                    self.Buffer.Dispose();
                }
                catch (Exception e)
                {
                    if (self.HasStaging)
                    {
                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();

                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after EndRecordAsync completes
            static async ValueTask DisposeAsync_ContinueAfterEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    if (self.HasStaging)
                    {
                        if (self.InStaging > 0)
                        {
                            var flushTask = self.FlushStagingAsync(CancellationToken.None);
                            await ConfigureCancellableAwait(self, flushTask, CancellationToken.None);
                        }

                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    var disposeTask = self.Inner.DisposeAsync();
                    await ConfigureCancellableAwait(self, disposeTask, CancellationToken.None);

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }
                    self.Buffer.Dispose();
                }
                catch (Exception e)
                {
                    if (self.HasStaging)
                    {
                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();

                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after FlushStagingAsync completes
            static async ValueTask DisposeAsync_ContinueAfterFlushStagingAsync(AsyncDynamicWriter self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    self.Staging.Dispose();
                    self.Staging = EmptyMemoryOwner.Singleton;
                    self.StagingMemory = Memory<char>.Empty;

                    var disposeTask = self.Inner.DisposeAsync();
                    await ConfigureCancellableAwait(self, disposeTask, CancellationToken.None);

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                    }
                    self.Buffer.Dispose();
                }
                catch (Exception e)
                {
                    if (self.HasStaging)
                    {
                        self.Staging.Dispose();
                        self.Staging = EmptyMemoryOwner.Singleton;
                        self.StagingMemory = Memory<char>.Empty;
                    }

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();

                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after Inner.DisposeAsync() completes
            static async ValueTask DisposeAsync_ContinueAfterDisposeAsync(AsyncDynamicWriter self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                    }
                    self.Buffer.Dispose();
                }
                catch (Exception e)
                {
                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();

                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncDynamicWriter)} with {Configuration}";
        }
    }
}
