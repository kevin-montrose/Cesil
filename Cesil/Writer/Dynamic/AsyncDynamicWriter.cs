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

        public override ValueTask WriteAsync(dynamic row, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                var rowAsObj = row as object;

                var options = Configuration.Options;
                var typeDescriber = options.TypeDescriber;
                var valueSeparator = Configuration.ValueSeparatorMemory;

                var writeHeadersTask = WriteHeadersAndEndRowIfNeededAsync(rowAsObj, cancel);
                if (!writeHeadersTask.IsCompletedSuccessfully(this))
                {
                    return WriteAsync_ContinueAfterWriteHeadersAsync(this, writeHeadersTask, row, typeDescriber, valueSeparator, cancel);
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
                            var placeValueSepTask = PlaceInStagingAsync(valueSeparator, cancel);
                            if (!placeValueSepTask.IsCompletedSuccessfully(this))
                            {
                                disposeE = false;
                                return WriteAsync_ContinueAfterPlaceCharAsync(this, placeValueSepTask, valueSeparator, cell, i, e, cancel);
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

                        var writeValueTask = WriteValueAsync(res, cancel);
                        if (!writeValueTask.IsCompletedSuccessfully(this))
                        {
                            disposeE = false;
                            return WriteAsync_ContinueAfterWriteValueAsync(this, writeValueTask, valueSeparator, i, e, cancel);
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
            static async ValueTask WriteAsync_ContinueAfterWriteHeadersAsync(AsyncDynamicWriter self, ValueTask waitFor, dynamic row, ITypeDescriber typeDescriber, ReadOnlyMemory<char> valueSeparator, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

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
                            var placeTask = self.PlaceInStagingAsync(valueSeparator, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
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

                        var writeValueTask = self.WriteValueAsync(res, cancel);
                        await ConfigureCancellableAwait(self, writeValueTask, cancel);
                        CheckCancellation(self, cancel);

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
            static async ValueTask WriteAsync_ContinueAfterPlaceCharAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> valueSeparator, DynamicCellValue cell, int i, IEnumerator<DynamicCellValue> e, CancellationToken cancel)
            {
                try
                {

                    try
                    {
                        await ConfigureCancellableAwait(self, waitFor, cancel);
                        CheckCancellation(self, cancel);

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

                            var writeValueTask = self.WriteValueAsync(res, cancel);
                            await ConfigureCancellableAwait(self, writeValueTask, cancel);
                            CheckCancellation(self, cancel);

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
                                var placeTask = self.PlaceInStagingAsync(valueSeparator, cancel);
                                await ConfigureCancellableAwait(self, placeTask, cancel);
                                CheckCancellation(self, cancel);
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

                            var writeValueTask = self.WriteValueAsync(res, cancel);
                            await ConfigureCancellableAwait(self, writeValueTask, cancel);
                            CheckCancellation(self, cancel);

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
            static async ValueTask WriteAsync_ContinueAfterWriteValueAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> valueSeparator, int i, IEnumerator<DynamicCellValue> e, CancellationToken cancel)
            {
                try
                {
                    try
                    {
                        await ConfigureCancellableAwait(self, waitFor, cancel);
                        CheckCancellation(self, cancel);

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
                                var placeTask = self.PlaceInStagingAsync(valueSeparator, cancel);
                                await ConfigureCancellableAwait(self, placeTask, cancel);
                                CheckCancellation(self, cancel);
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

                            var writeValueTask = self.WriteValueAsync(res, cancel);
                            await ConfigureCancellableAwait(self, writeValueTask, cancel);
                            CheckCancellation(self, cancel);

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

        public override ValueTask WriteCommentAsync(ReadOnlyMemory<char> comment, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

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
                        var checkHeadersTask = CheckHeadersAsync(null, cancel);
                        if (!checkHeadersTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterCheckHeadersAsync(this, checkHeadersTask, comment, cancel);
                        }

                        var checkHeadersResult = checkHeadersTask.Result;
                        if (!checkHeadersResult)
                        {
                            shouldEndRecord = false;
                        }
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

                var options = Configuration.Options;
                var commentCharNullable = options.CommentCharacter;

                if (commentCharNullable == null)
                {
                    return Throw.InvalidOperationException<ValueTask>($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line");
                }

                HasWrittenComments = true;

                var commentChar = commentCharNullable.Value;
                var rowEndingMem = Configuration.RowEndingMemory;

                var splitIx = Utils.FindNextIx(0, comment, rowEndingMem);
                if (splitIx == -1)
                {
                    // single segment
                    var placeCharTask = PlaceCharInStagingAsync(commentChar, cancel);
                    if (!placeCharTask.IsCompletedSuccessfully(this))
                    {
                        return WriteCommentAsync_ContinueAfterSingleSegmentPlaceCharInStagingAsync(this, placeCharTask, comment, cancel);
                    }

                    if (comment.Length > 0)
                    {
                        var placeTask = PlaceInStagingAsync(comment, cancel);

                        // doesn't matter if it's completed, since the client will await before making another call
                        return placeTask;
                    }
                }
                else
                {
                    // multi segment
                    var prevIx = 0;

                    var isFirstRow = true;
                    while (splitIx != -1)
                    {
                        if (!isFirstRow)
                        {
                            var endRecordTask = EndRecordAsync(cancel);
                            if (!endRecordTask.IsCompletedSuccessfully(this))
                            {
                                return WriteCommentAsync_ContinueAfterMultiSegementEndRecordAsync(this, endRecordTask, commentChar, comment, prevIx, splitIx, rowEndingMem, cancel);
                            }
                        }

                        var placeCharTask = PlaceCharInStagingAsync(commentChar, cancel);
                        if (!placeCharTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterMultiSegmentPlaceCharInStagingAsync(this, placeCharTask, commentChar, comment, prevIx, splitIx, rowEndingMem, cancel);
                        }

                        var segSpan = comment[prevIx..splitIx];
                        if (segSpan.Length > 0)
                        {
                            var placeInTask = PlaceInStagingAsync(segSpan, cancel);
                            if (!placeInTask.IsCompletedSuccessfully(this))
                            {
                                return WriteCommentAsync_ContinueAfterMultiSegmentPlaceInStagingAsync(this, placeInTask, commentChar, comment, prevIx, splitIx, rowEndingMem, cancel);
                            }
                        }

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);

                        isFirstRow = false;
                    }

                    if (prevIx != comment.Length)
                    {
                        if (!isFirstRow)
                        {
                            var endRecordTask = EndRecordAsync(cancel);
                            if (!endRecordTask.IsCompletedSuccessfully(this))
                            {
                                return WriteCommentAsync_ContinueAfterMultiSegmentFinalCommentEndRecordAsync(this, endRecordTask, commentChar, comment, prevIx, rowEndingMem, cancel);
                            }
                        }

                        var placeCharTask = PlaceCharInStagingAsync(commentChar, cancel);
                        if (!placeCharTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterMultiSegmentFinalCommentPlaceCharInStagingAsync(this, placeCharTask, comment, prevIx, rowEndingMem, cancel);
                        }

                        var segSpan = comment[prevIx..];
                        var placeInTask = PlaceInStagingAsync(segSpan, cancel);

                        // no need to wait, as the client must await before making another call
                        return placeInTask;
                    }
                }
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow<object>(this, e);
            }

            return default;

            // continue after waiting, in multi segment case, for the final comment, for the comment start char to write
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegmentFinalCommentPlaceCharInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> comment, int prevIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    var segSpan = comment[prevIx..];
                    var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting, in multi segment case, for the final comment, for ending the record to finish
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegmentFinalCommentEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int prevIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await ConfigureCancellableAwait(self, placeCharTask, cancel);
                    CheckCancellation(self, cancel);

                    var segSpan = comment[prevIx..];
                    var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting, in the multi segment case, for writing the comment mem to finish
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegmentPlaceInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int prevIx, int splitIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    // finish the loop
                    {
                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    while (splitIx != -1)
                    {
                        // not first row by definition, so no check
                        var endRecordTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endRecordTask, cancel);
                        CheckCancellation(self, cancel);

                        var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeCharTask, cancel);
                        CheckCancellation(self, cancel);

                        var segSpan = comment[prevIx..splitIx];
                        if (segSpan.Length > 0)
                        {
                            var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
                        }

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    if (prevIx != comment.Length)
                    {
                        // not first row by definition, so no check
                        var endRecordTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endRecordTask, cancel);
                        CheckCancellation(self, cancel);

                        var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeCharTask, cancel);
                        CheckCancellation(self, cancel);

                        var segSpan = comment[prevIx..];
                        var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                        await ConfigureCancellableAwait(self, placeTask, cancel);
                        CheckCancellation(self, cancel);
                    }

                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting, in the multi sgement case, for writing the comment char to finish
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegmentPlaceCharInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int prevIx, int splitIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    // finish the loop
                    {
                        var segSpan = comment[prevIx..splitIx];
                        if (segSpan.Length > 0)
                        {
                            var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
                        }

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    while (splitIx != -1)
                    {
                        // not first row by definition, so no check
                        var endRecordTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endRecordTask, cancel);
                        CheckCancellation(self, cancel);

                        var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeCharTask, cancel);
                        CheckCancellation(self, cancel);

                        var segSpan = comment[prevIx..splitIx];
                        if (segSpan.Length > 0)
                        {
                            var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
                        }

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    if (prevIx != comment.Length)
                    {
                        // not first by definition, so no check
                        var endRecordTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endRecordTask, cancel);
                        CheckCancellation(self, cancel);

                        var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeCharTask, cancel);
                        CheckCancellation(self, cancel);

                        var segSpan = comment[prevIx..];
                        var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                        await ConfigureCancellableAwait(self, placeTask, cancel);
                        CheckCancellation(self, cancel);
                    }

                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting, in the multi segment case, for the record to end
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegementEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int prevIx, int splitIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    // finish the loop
                    {
                        var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeCharTask, cancel);
                        CheckCancellation(self, cancel);

                        var segSpan = comment[prevIx..splitIx];
                        if (segSpan.Length > 0)
                        {
                            var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
                        }

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    while (splitIx != -1)
                    {
                        // not first row by definition, so no check
                        var endRecordTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endRecordTask, cancel);
                        CheckCancellation(self, cancel);

                        var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeCharTask, cancel);
                        CheckCancellation(self, cancel);

                        var segSpan = comment[prevIx..splitIx];
                        if (segSpan.Length > 0)
                        {
                            var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
                        }

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    if (prevIx != comment.Length)
                    {
                        // not first row, by definition
                        var endRecordTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endRecordTask, cancel);
                        CheckCancellation(self, cancel);

                        var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeCharTask, cancel);
                        CheckCancellation(self, cancel);

                        var segSpan = comment[prevIx..];
                        var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                        await ConfigureCancellableAwait(self, placeTask, cancel);
                        CheckCancellation(self, cancel);
                    }

                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting, in the single segment, for the comment char to finish writing
            static async ValueTask WriteCommentAsync_ContinueAfterSingleSegmentPlaceCharInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> comment, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    if (comment.Length > 0)
                    {
                        var placeTask = self.PlaceInStagingAsync(comment, cancel);
                        await ConfigureCancellableAwait(self, placeTask, cancel);
                        CheckCancellation(self, cancel);
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting for the record to end
            static async ValueTask WriteCommentAsync_ContinueAfterEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> comment, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    var options = self.Configuration.Options;
                    var commentCharNullable = options.CommentCharacter;

                    if (commentCharNullable == null)
                    {
                        Throw.InvalidOperationException<object>($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line");
                        return;
                    }

                    var commentChar = commentCharNullable.Value;

                    self.HasWrittenComments = true;

                    var rowEndingMem = self.Configuration.RowEndingMemory;

                    var splitIx = Utils.FindNextIx(0, comment, rowEndingMem);
                    if (splitIx == -1)
                    {
                        // single segment
                        var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeCharTask, cancel);
                        CheckCancellation(self, cancel);

                        if (comment.Length > 0)
                        {
                            var placeTask = self.PlaceInStagingAsync(comment, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
                        }
                    }
                    else
                    {
                        // multi segment
                        var prevIx = 0;

                        var isFirstRow = true;
                        while (splitIx != -1)
                        {
                            if (!isFirstRow)
                            {
                                var endRecordTask = self.EndRecordAsync(cancel);
                                await ConfigureCancellableAwait(self, endRecordTask, cancel);
                                CheckCancellation(self, cancel);
                            }

                            var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                            await ConfigureCancellableAwait(self, placeCharTask, cancel);
                            CheckCancellation(self, cancel);

                            var segSpan = comment[prevIx..splitIx];
                            if (segSpan.Length > 0)
                            {
                                var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                                await ConfigureCancellableAwait(self, placeTask, cancel);
                                CheckCancellation(self, cancel);
                            }

                            prevIx = splitIx + rowEndingMem.Length;
                            splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);

                            isFirstRow = false;
                        }

                        if (prevIx != comment.Length)
                        {
                            if (!isFirstRow)
                            {
                                var endRecordTask = self.EndRecordAsync(cancel);
                                await ConfigureCancellableAwait(self, endRecordTask, cancel);
                                CheckCancellation(self, cancel);
                            }

                            var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                            await ConfigureCancellableAwait(self, placeCharTask, cancel);
                            CheckCancellation(self, cancel);

                            var segSpan = comment[prevIx..];
                            var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
                        }
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting for the headers check to complete
            static async ValueTask WriteCommentAsync_ContinueAfterCheckHeadersAsync(AsyncDynamicWriter self, ValueTask<bool> waitFor, ReadOnlyMemory<char> comment, CancellationToken cancel)
            {
                try
                {
                    var shouldEndRecord = await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    if (shouldEndRecord)
                    {
                        var endRecordTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endRecordTask, cancel);
                        CheckCancellation(self, cancel);
                    }

                    var options = self.Configuration.Options;
                    var commentCharNullable = options.CommentCharacter;

                    if (commentCharNullable == null)
                    {
                        Throw.InvalidOperationException<object>($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line");
                        return;
                    }

                    var commentChar = commentCharNullable.Value;

                    self.HasWrittenComments = true;

                    var rowEndingMem = self.Configuration.RowEndingMemory;

                    var splitIx = Utils.FindNextIx(0, comment, rowEndingMem);
                    if (splitIx == -1)
                    {
                        // single segment
                        var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeCharTask, cancel);
                        CheckCancellation(self, cancel);

                        if (comment.Length > 0)
                        {
                            var placeTask = self.PlaceInStagingAsync(comment, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
                        }
                    }
                    else
                    {
                        // multi segment
                        var prevIx = 0;

                        var isFirstRow = true;
                        while (splitIx != -1)
                        {
                            if (!isFirstRow)
                            {
                                var endRecordTask = self.EndRecordAsync(cancel);
                                await ConfigureCancellableAwait(self, endRecordTask, cancel);
                                CheckCancellation(self, cancel);
                            }

                            var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                            await ConfigureCancellableAwait(self, placeCharTask, cancel);
                            CheckCancellation(self, cancel);

                            var segSpan = comment[prevIx..splitIx];
                            if (segSpan.Length > 0)
                            {
                                var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                                await ConfigureCancellableAwait(self, placeTask, cancel);
                                CheckCancellation(self, cancel);
                            }

                            prevIx = splitIx + rowEndingMem.Length;
                            splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);

                            isFirstRow = false;
                        }

                        if (prevIx != comment.Length)
                        {
                            if (!isFirstRow)
                            {
                                var endRecordTask = self.EndRecordAsync(cancel);
                                await ConfigureCancellableAwait(self, endRecordTask, cancel);
                                CheckCancellation(self, cancel);
                            }

                            var placeCharTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                            await ConfigureCancellableAwait(self, placeCharTask, cancel);
                            CheckCancellation(self, cancel);

                            var segSpan = comment[prevIx..];
                            var placeTask = self.PlaceInStagingAsync(segSpan, cancel);
                            await ConfigureCancellableAwait(self, placeTask, cancel);
                            CheckCancellation(self, cancel);
                        }
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
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

                var res = await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                if (!res)
                {
                    shouldEndRecord = false;
                }

                if (shouldEndRecord)
                {
                    var endTask = self.EndRecordAsync(cancel);
                    await ConfigureCancellableAwait(self, endTask, cancel);
                    CheckCancellation(self, cancel);
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
        private ValueTask<bool> CheckHeadersAsync(dynamic? firstRow, CancellationToken cancel)
        {
            if (Configuration.Options.WriteHeader == WriteHeader.Never)
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
                return CheckHeadersAsync_ContinueAfterWriteHeadersAsync(this, writeHeadersTask, cancel);
            }

            return new ValueTask<bool>(true);

            // continue after WriteHeadersAsync() completes
            static async ValueTask<bool> CheckHeadersAsync_ContinueAfterWriteHeadersAsync(AsyncDynamicWriter self, ValueTask waitFor, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

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

        private ValueTask WriteHeadersAsync(CancellationToken cancel)
        {
            var valueSeparator = Configuration.ValueSeparatorMemory;

            var columnNamesValue = ColumnNames.Value;
            for (var i = 0; i < columnNamesValue.Length; i++)
            {
                if (i != 0)
                {
                    // first value doesn't get a separator
                    var placeCharTask = PlaceInStagingAsync(valueSeparator, cancel);
                    if (!placeCharTask.IsCompletedSuccessfully(this))
                    {
                        return WriteHeadersAsync_ContinueAfterStartOfForAsync(this, placeCharTask, valueSeparator, i, cancel);
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
                        var endRecordTask = EndRecordAsync(cancel);
                        if (!endRecordTask.IsCompletedSuccessfully(this))
                        {
                            return WriteHeadersAsync_ContinueAfterStartOfForAsync(this, endRecordTask, valueSeparator, i, cancel);
                        }
                    }
                }

                var colName = columnNamesValue[i].EncodedName;

                // can colName is always gonna be encoded correctly, because we just discovered them
                //   (ie. they're always correct for this config)
                var placeInStagingTask = PlaceInStagingAsync(colName.AsMemory(), cancel);
                if (!placeInStagingTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAsync_ContinueAfterPlaceInStagingAsync(this, placeInStagingTask, valueSeparator, i, cancel);
                }
            }

            return default;

            // continue after a PlaceCharInStagingAsync or EndRecordAsync call
            //   we can share this because both branches go into the same next step
            static async ValueTask WriteHeadersAsync_ContinueAfterStartOfForAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> valueSeparator, int i, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                var selfColumnNamesValue = self.ColumnNames.Value;

                // finish the loop
                {
                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var placeTask = self.PlaceInStagingAsync(colName.AsMemory(), cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);

                    i++;
                }

                for (; i < selfColumnNamesValue.Length; i++)
                {
                    // by definition i != 0, so no need for the if
                    var secondPlaceTask = self.PlaceInStagingAsync(valueSeparator, cancel);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancel);
                    CheckCancellation(self, cancel);

                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var thirdPlaceTask = self.PlaceInStagingAsync(colName.AsMemory(), cancel);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancel);
                    CheckCancellation(self, cancel);
                }
            }

            static async ValueTask WriteHeadersAsync_ContinueAfterPlaceInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> valueSeparator, int i, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                var selfColumnNamesValue = self.ColumnNames.Value;

                i++;

                for (; i < selfColumnNamesValue.Length; i++)
                {
                    // by definition i != 0, so no need for the if
                    var placeTask = self.PlaceInStagingAsync(valueSeparator, cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);

                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var secondPlaceTask = self.PlaceInStagingAsync(colName.AsMemory(), cancel);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancel);
                    CheckCancellation(self, cancel);
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
