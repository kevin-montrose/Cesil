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

        private IMemoryOwner<DynamicCellValue>? CellBuffer;

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
                var valueSeparator = Configuration.ValueSeparatorMemory;

                var writeHeadersTask = WriteHeadersAndEndRowIfNeededAsync(rowAsObj, cancellationToken);
                if (!writeHeadersTask.IsCompletedSuccessfully(this))
                {
                    return WriteAsync_ContinueAfterWriteHeadersAsync(this, writeHeadersTask, row, typeDescriber, valueSeparator, cancellationToken);
                }

                var wholeRowContext = WriteContext.DiscoveringCells(Configuration.Options, RowNumber, Context);

                var cellValues = Utils.GetCells(Configuration.DynamicMemoryPool, ref CellBuffer, typeDescriber, in wholeRowContext, row as object);

                Utils.ForceInOrder(ColumnNames.Value, ColumnNameSorter, cellValues);
                var cellValuesInOrderSpan = cellValues.Span;

                var columnNamesValue = ColumnNames.Value;

                for (var i = 0; i < cellValuesInOrderSpan.Length; i++)
                {
                    var cell = cellValuesInOrderSpan[i];

                    var needsSeparator = i != 0;

                    if (needsSeparator)
                    {
                        var placeValueSepTask = PlaceAllInStagingAsync(valueSeparator, cancellationToken);
                        if (!placeValueSepTask.IsCompletedSuccessfully(this))
                        {
                            return WriteAsync_ContinueAfterPlaceCharAsync(this, placeValueSepTask, valueSeparator, cell, i, cellValues, cancellationToken);
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
                        return WriteAsync_ContinueAfterWriteValueAsync(this, writeValueTask, valueSeparator, i, cellValues, cancellationToken);
                    }
                    Buffer.Reset();

end:
// no-op for label
                    { }
                }
                RowNumber++;

                return default;
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask>(this, e);
            }

            // continue after WriteHeadersAndRowIfNeededAsync completes
            static async ValueTask WriteAsync_ContinueAfterWriteHeadersAsync(AsyncDynamicWriter self, ValueTask waitFor, dynamic row, ITypeDescriber typeDescriber, ReadOnlyMemory<char> valueSeparator, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    var options = self.Configuration.Options;

                    var wholeRowContext = WriteContext.DiscoveringCells(options, self.RowNumber, self.Context);

                    var cellValues = Utils.GetCells(self.Configuration.DynamicMemoryPool, ref self.CellBuffer, typeDescriber, in wholeRowContext, row as object);

                    Utils.ForceInOrder(self.ColumnNames.Value, self.ColumnNameSorter, cellValues);

                    var selfColumnNamesValue = self.ColumnNames.Value;

                    for (var i = 0; i < cellValues.Length; i++)
                    {
                        var cell = cellValues.Span[i];

                        var needsSeparator = i != 0;

                        if (needsSeparator)
                        {
                            var placeTask = self.PlaceAllInStagingAsync(valueSeparator, cancellationToken);
                            await ConfigureCancellableAwait(self, placeTask, cancellationToken);
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

                        self.Buffer.Reset();

end:
// no-op for label
                        { }
                    }

                    self.RowNumber++;
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after PlaceCharInStagingAsync completes
            static async ValueTask WriteAsync_ContinueAfterPlaceCharAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> valueSeparator, DynamicCellValue cell, int i, ReadOnlyMemory<DynamicCellValue> e, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

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

                        self.Buffer.Reset();

end:
                        i++;
                    }

                    // resume
                    for (; i < e.Length; i++)
                    {
                        cell = e.Span[i];

                        // always need a separator, since the i always > 0
                        var placeTask = self.PlaceAllInStagingAsync(valueSeparator, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);

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

                        self.Buffer.Reset();

end:
// no-op for label
                        { }
                    }

                    self.RowNumber++;
                }
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }

            // continue after WriteValueAsync completes
            static async ValueTask WriteAsync_ContinueAfterWriteValueAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> valueSeparator, int i, ReadOnlyMemory<DynamicCellValue> e, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    // finish loop
                    {
                        self.Buffer.Reset();

                        i++;
                    }

                    var selfColumnNamesValue = self.ColumnNames.Value;

                    // resume
                    for (; i < e.Length; i++)
                    {
                        var cell = e.Span[i];

                        // always need a separator, i is also > 0
                        var placeTask = self.PlaceAllInStagingAsync(valueSeparator, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);

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

                        self.Buffer.Reset();

end:
// no-op for label
                        { }
                    }

                    self.RowNumber++;
                }
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }
        }

        public override ValueTask WriteCommentAsync(ReadOnlyMemory<char> comment, CancellationToken cancellationToken = default)
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
                        var checkHeadersTask = CheckHeadersAsync(null, cancellationToken);
                        if (!checkHeadersTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterCheckHeadersAsync(this, checkHeadersTask, comment, cancellationToken);
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
                    var endRecordTask = EndRecordAsync(cancellationToken);
                    if (!endRecordTask.IsCompletedSuccessfully(this))
                    {
                        return WriteCommentAsync_ContinueAfterEndRecordAsync(this, endRecordTask, comment, cancellationToken);
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
                    var placeTask = PlaceCharAndSegmentInStagingAsync(commentChar, comment, cancellationToken);

                    // doesn't matter if it's completed, since the client will await before making another call
                    return placeTask;
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
                            var endRecordTask = EndRecordAsync(cancellationToken);
                            if (!endRecordTask.IsCompletedSuccessfully(this))
                            {
                                return WriteCommentAsync_ContinueAfterMultiSegementEndRecordAsync(this, endRecordTask, commentChar, comment, prevIx, splitIx, rowEndingMem, cancellationToken);
                            }
                        }

                        var segSpan = comment[prevIx..splitIx];
                        var placeTask = PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);

                        if (!placeTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterMultiSegmentPlaceCharAndSegmentInStagingAsync(this, placeTask, commentChar, comment, splitIx, rowEndingMem, cancellationToken);
                        }

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);

                        isFirstRow = false;
                    }

                    if (prevIx != comment.Length)
                    {
                        if (!isFirstRow)
                        {
                            var endRecordTask = EndRecordAsync(cancellationToken);
                            if (!endRecordTask.IsCompletedSuccessfully(this))
                            {
                                return WriteCommentAsync_ContinueAfterMultiSegmentFinalCommentEndRecordAsync(this, endRecordTask, commentChar, comment, prevIx, rowEndingMem, cancellationToken);
                            }
                        }

                        var segSpan = comment[prevIx..];
                        var placeTask = PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);

                        // no need to wait, as the client must await before making another call
                        return placeTask;
                    }
                }
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow<object>(this, e);
            }

            return default;

            // continue after waiting, in the multi segment case, for writing a line to finish
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegmentPlaceCharAndSegmentInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int splitIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    // finish the loop
                    int prevIx;
                    {
                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    while (splitIx != -1)
                    {
                        // not first row, by definition
                        var endRecordTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                        var segSpan = comment[prevIx..splitIx];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    if (prevIx != comment.Length)
                    {
                        // not first row, by definition
                        var endRecordTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);

                        var segSpan = comment[prevIx..];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);

                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting, in multi segment case, for the final comment, for ending the record to finish
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegmentFinalCommentEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int prevIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    var segSpan = comment[prevIx..];
                    var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting, in the multi segment case, for the record to end
            static async ValueTask WriteCommentAsync_ContinueAfterMultiSegementEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> comment, int prevIx, int splitIx, ReadOnlyMemory<char> rowEndingMem, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    // finish the loop
                    {
                        var segSpan = comment[prevIx..splitIx];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    while (splitIx != -1)
                    {
                        // not first row by definition, so no check
                        var endRecordTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);

                        var segSpan = comment[prevIx..splitIx];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                        prevIx = splitIx + rowEndingMem.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);
                    }

                    if (prevIx != comment.Length)
                    {
                        // not first row, by definition
                        var endRecordTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);

                        var segSpan = comment[prevIx..];
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting for the record to end
            static async ValueTask WriteCommentAsync_ContinueAfterEndRecordAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> comment, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

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
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, comment, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
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
                                var endRecordTask = self.EndRecordAsync(cancellationToken);
                                await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);
                            }

                            var segSpan = comment[prevIx..splitIx];
                            var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                            await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                            prevIx = splitIx + rowEndingMem.Length;
                            splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);

                            isFirstRow = false;
                        }

                        if (prevIx != comment.Length)
                        {
                            if (!isFirstRow)
                            {
                                var endRecordTask = self.EndRecordAsync(cancellationToken);
                                await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);
                            }

                            var segSpan = comment[prevIx..];
                            var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                            await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                        }
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after waiting for the headers check to complete
            static async ValueTask WriteCommentAsync_ContinueAfterCheckHeadersAsync(AsyncDynamicWriter self, ValueTask<bool> waitFor, ReadOnlyMemory<char> comment, CancellationToken cancellationToken)
            {
                try
                {
                    var shouldEndRecord = await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    if (shouldEndRecord)
                    {
                        var endRecordTask = self.EndRecordAsync(cancellationToken);
                        await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);
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
                        var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, comment, cancellationToken);
                        await ConfigureCancellableAwait(self, placeTask, cancellationToken);
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
                                var endRecordTask = self.EndRecordAsync(cancellationToken);
                                await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);
                            }

                            var segSpan = comment[prevIx..splitIx];
                            var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                            await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                            prevIx = splitIx + rowEndingMem.Length;
                            splitIx = Utils.FindNextIx(prevIx, comment, rowEndingMem);

                            isFirstRow = false;
                        }

                        if (prevIx != comment.Length)
                        {
                            if (!isFirstRow)
                            {
                                var endRecordTask = self.EndRecordAsync(cancellationToken);
                                await ConfigureCancellableAwait(self, endRecordTask, cancellationToken);
                            }

                            var segSpan = comment[prevIx..];
                            var placeTask = self.PlaceCharAndSegmentInStagingAsync(commentChar, segSpan, cancellationToken);
                            await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                        }
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
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

                if (!res)
                {
                    shouldEndRecord = false;
                }

                if (shouldEndRecord)
                {
                    var endTask = self.EndRecordAsync(cancellationToken);
                    await ConfigureCancellableAwait(self, endTask, cancellationToken);
                }
            }
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

                return true;
            }
        }

        private void DiscoverColumns(dynamic o)
        {
            // todo: remove this allocation
            var cols = new List<(string TrueName, string EncodedName)>();

            var ctx = WriteContext.DiscoveringColumns(Configuration.Options, Context);

            var options = Configuration.Options;

            var colIx = 0;
            var cellsMem = Utils.GetCells(Configuration.DynamicMemoryPool, ref CellBuffer, options.TypeDescriber, in ctx, o as object);
            var cells = cellsMem.Span;
            foreach (var c in cells)
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
                    encodedColName = Utils.Encode(encodedColName, options, Configuration.MemoryPool);
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
            var valueSeparator = Configuration.ValueSeparatorMemory;

            var columnNamesValue = ColumnNames.Value;
            for (var i = 0; i < columnNamesValue.Length; i++)
            {
                if (i != 0)
                {
                    // first value doesn't get a separator
                    var placeCharTask = PlaceAllInStagingAsync(valueSeparator, cancellationToken);
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
                var placeInStagingTask = PlaceAllInStagingAsync(colName.AsMemory(), cancellationToken);
                if (!placeInStagingTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAsync_ContinueAfterPlaceInStagingAsync(this, placeInStagingTask, valueSeparator, i, cancellationToken);
                }
            }

            return default;

            // continue after a PlaceCharInStagingAsync or EndRecordAsync call
            //   we can share this because both branches go into the same next step
            static async ValueTask WriteHeadersAsync_ContinueAfterStartOfForAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> valueSeparator, int i, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                var selfColumnNamesValue = self.ColumnNames.Value;

                // finish the loop
                {
                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var placeTask = self.PlaceAllInStagingAsync(colName.AsMemory(), cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                    i++;
                }

                for (; i < selfColumnNamesValue.Length; i++)
                {
                    // by definition i != 0, so no need for the if
                    var secondPlaceTask = self.PlaceAllInStagingAsync(valueSeparator, cancellationToken);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);

                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var thirdPlaceTask = self.PlaceAllInStagingAsync(colName.AsMemory(), cancellationToken);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancellationToken);
                }
            }

            static async ValueTask WriteHeadersAsync_ContinueAfterPlaceInStagingAsync(AsyncDynamicWriter self, ValueTask waitFor, ReadOnlyMemory<char> valueSeparator, int i, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                var selfColumnNamesValue = self.ColumnNames.Value;

                i++;

                for (; i < selfColumnNamesValue.Length; i++)
                {
                    // by definition i != 0, so no need for the if
                    var placeTask = self.PlaceAllInStagingAsync(valueSeparator, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);

                    var colName = selfColumnNamesValue[i].EncodedName;

                    // can colName is always gonna be encoded correctly, because we just discovered them
                    //   (ie. they're always correct for this config)
                    var secondPlaceTask = self.PlaceAllInStagingAsync(colName.AsMemory(), cancellationToken);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
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

                    CellBuffer?.Dispose();
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
                    CellBuffer?.Dispose();

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

                    self.CellBuffer?.Dispose();
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
                    self.CellBuffer?.Dispose();

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
                    self.CellBuffer?.Dispose();
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
                    self.CellBuffer?.Dispose();

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
                    self.CellBuffer?.Dispose();
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
                    self.CellBuffer?.Dispose();

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
                    self.CellBuffer?.Dispose();
                }
                catch (Exception e)
                {
                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }

                    self.Buffer.Dispose();
                    self.CellBuffer?.Dispose();

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