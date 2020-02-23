using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using static Cesil.AwaitHelper;
using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class AsyncWriter<T> :
        AsyncWriterBase<T>
    {
        internal AsyncWriter(ConcreteBoundConfiguration<T> config, IAsyncWriterAdapter inner, object? context) : base(config, inner, context) { }

        public override ValueTask WriteAsync(T row, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned();

            try
            {

                var checkHeadersAndEndRowTask = WriteHeadersAndEndRowIfNeededAsync(cancel);
                if (!checkHeadersAndEndRowTask.IsCompletedSuccessfully(this))
                {
                    return WriteAsync_ContinueAfterHeadersAndEndRecordAsync(this, checkHeadersAndEndRowTask, row, cancel);
                }

                var columnsValue = Columns;
                for (var i = 0; i < columnsValue.Length; i++)
                {
                    var needsSeparator = i != 0;
                    var col = columnsValue[i];

                    var writeColumnTask = WriteColumnAsync(row, i, col, needsSeparator, cancel);
                    if (!writeColumnTask.IsCompletedSuccessfully(this))
                    {
                        return WriteAsync_ContinueAfterWriteColumnAsync(this, writeColumnTask, row, i, cancel);
                    }
                }

                RowNumber++;

                return default;
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask>(this, e);
            }

            // wait for the record to end, then continue async
            static async ValueTask WriteAsync_ContinueAfterHeadersAndEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor, T row, CancellationToken cancel)
            {
                try
                {

                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    var selfColumnsValue = self.Columns;
                    for (var i = 0; i < selfColumnsValue.Length; i++)
                    {
                        var needsSeparator = i != 0;
                        var col = selfColumnsValue[i];

                        var writeTask = self.WriteColumnAsync(row, i, col, needsSeparator, cancel);
                        await ConfigureCancellableAwait(self, writeTask, cancel);
                        CheckCancellation(self, cancel);
                    }

                    self.RowNumber++;
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // wait for the column to be written, then continue with the loop
            static async ValueTask WriteAsync_ContinueAfterWriteColumnAsync(AsyncWriter<T> self, ValueTask waitFor, T row, int i, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    // the implicit increment at the end of the loop
                    i++;

                    var selfColumnsValue = self.Columns;
                    for (; i < selfColumnsValue.Length; i++)
                    {
                        const bool needsSeparator = true;                  // by definition, this isn't the first loop
                        var col = selfColumnsValue[i];

                        var writeTask = self.WriteColumnAsync(row, i, col, needsSeparator, cancel);
                        await ConfigureCancellableAwait(self, writeTask, cancel);
                        CheckCancellation(self, cancel);
                    }

                    self.RowNumber++;
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }
        }

        public override ValueTask WriteCommentAsync(string comment, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned();

            Utils.CheckArgumentNull(comment, nameof(comment));

            try
            {

                var writeHeadersTask = WriteHeadersAndEndRowIfNeededAsync(cancel);
                if (!writeHeadersTask.IsCompletedSuccessfully(this))
                {
                    return WriteCommentAsync_ContinueAfterWriteHeadersAndEndRowIfNeededAsync(this, writeHeadersTask, comment, cancel);
                }

                var (commentChar, segments) = SplitCommentIntoLines(comment);

                if (segments.IsSingleSegment)
                {
                    var seg = segments.First;

                    var placeCharInStagingTask = PlaceCharInStagingAsync(commentChar, cancel);
                    if (!placeCharInStagingTask.IsCompletedSuccessfully(this))
                    {
                        if (seg.Length > 0)
                        {
                            return WriteCommentAsync_ContinueAfterPlaceCharInStagingSingleSegmentAsync(this, placeCharInStagingTask, seg, cancel);
                        }

                        return placeCharInStagingTask;
                    }

                    if (seg.Length > 0)
                    {
                        return PlaceInStagingAsync(seg, cancel);
                    }

                    return default;
                }

                // we know we can write directly now
                var e = segments.GetEnumerator();
                var isFirstRow = true;
                while (e.MoveNext())
                {
                    var seg = e.Current;
                    if (!isFirstRow)
                    {
                        var endRecordTask = EndRecordAsync(cancel);
                        if (!endRecordTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterEndRecordMultiSegmentAsync(this, endRecordTask, commentChar, seg, e, cancel);
                        }
                    }

                    var placeCharTask = PlaceCharInStagingAsync(commentChar, cancel);
                    if (!placeCharTask.IsCompletedSuccessfully(this))
                    {
                        return WriteCommentAsync_ContinueAfterPlaceCharMultiSegmentAsync(this, placeCharTask, commentChar, seg, e, cancel);
                    }

                    if (seg.Length > 0)
                    {
                        var placeSegTask = PlaceInStagingAsync(seg, cancel);
                        if (!placeSegTask.IsCompletedSuccessfully(this))
                        {
                            return WriteCommentAsync_ContinueAfterPlaceSegmentMultiSegmentAsync(this, placeSegTask, commentChar, e, cancel);
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

            // continue after checking for writing headers (and ending the last row, if needed)
            static async ValueTask WriteCommentAsync_ContinueAfterWriteHeadersAndEndRowIfNeededAsync(AsyncWriter<T> self, ValueTask waitFor, string comment, CancellationToken cancel)
            {
                try
                {

                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    var (commentChar, segments) = self.SplitCommentIntoLines(comment);

                    if (segments.IsSingleSegment)
                    {
                        var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeTask, cancel);
                        CheckCancellation(self, cancel);

                        var seg = segments.First;
                        if (seg.Length > 0)
                        {
                            var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                            await ConfigureCancellableAwait(self, secondPlaceTask, cancel);
                            CheckCancellation(self, cancel);
                        }
                    }
                    else
                    {
                        // we know we can write directly now
                        var isFirstRow = true;
                        foreach (var seg in segments)
                        {
                            if (!isFirstRow)
                            {
                                var endTask = self.EndRecordAsync(cancel);
                                await ConfigureCancellableAwait(self, endTask, cancel);
                                CheckCancellation(self, cancel);
                            }

                            var thirdPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                            await ConfigureCancellableAwait(self, thirdPlaceTask, cancel);
                            CheckCancellation(self, cancel);

                            if (seg.Length > 0)
                            {
                                var fourthPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                                await ConfigureCancellableAwait(self, fourthPlaceTask, cancel);
                                CheckCancellation(self, cancel);
                            }

                            isFirstRow = false;
                        }
                    }
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after writing the # (or whatever) before the rest of the single segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharInStagingSingleSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlyMemory<char> seg, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    var placeTask = self.PlaceInStagingAsync(seg, cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);
                }
                catch (Exception e)
                {
                    Throw.PoisonAndRethrow<object>(self, e);
                }
            }

            // continue after writing a row ender in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterEndRecordMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);

                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await ConfigureCancellableAwait(self, secondPlaceTask, cancel);
                        CheckCancellation(self, cancel);
                    }

                    while (e.MoveNext())
                    {
                        // no need to check is first, we know it's not
                        seg = e.Current;

                        var endTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endTask, cancel);
                        CheckCancellation(self, cancel);

                        var thirdPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, thirdPlaceTask, cancel);
                        CheckCancellation(self, cancel);

                        if (seg.Length > 0)
                        {
                            var fourthPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                            await ConfigureCancellableAwait(self, fourthPlaceTask, cancel);
                            CheckCancellation(self, cancel);
                        }
                    }
                }
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }

            // continue after writing a # (or whatever) in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    if (seg.Length > 0)
                    {
                        var placeTask = self.PlaceInStagingAsync(seg, cancel);
                        await ConfigureCancellableAwait(self, placeTask, cancel);
                        CheckCancellation(self, cancel);
                    }

                    while (e.MoveNext())
                    {
                        // no need to check is first, we know it's not
                        seg = e.Current;
                        var endTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endTask, cancel);
                        CheckCancellation(self, cancel);

                        var secondPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, secondPlaceTask, cancel);
                        CheckCancellation(self, cancel);

                        if (seg.Length > 0)
                        {
                            var thirdPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                            await ConfigureCancellableAwait(self, thirdPlaceTask, cancel);
                            CheckCancellation(self, cancel);
                        }
                    }
                }
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }

            // continue after writing a segment, in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceSegmentMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    while (e.MoveNext())
                    {
                        // no need to check is first, we know it's not
                        var seg = e.Current;
                        var endTask = self.EndRecordAsync(cancel);
                        await ConfigureCancellableAwait(self, endTask, cancel);
                        CheckCancellation(self, cancel);

                        var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await ConfigureCancellableAwait(self, placeTask, cancel);
                        CheckCancellation(self, cancel);

                        if (seg.Length > 0)
                        {
                            var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                            await ConfigureCancellableAwait(self, secondPlaceTask, cancel);
                            CheckCancellation(self, cancel);
                        }
                    }
                }
                catch (Exception exc)
                {
                    Throw.PoisonAndRethrow<object>(self, exc);
                }
            }
        }

        private ValueTask WriteHeadersAndEndRowIfNeededAsync(CancellationToken cancel)
        {
            var shouldEndRecord = true;
            if (IsFirstRow)
            {
                var headersTask = CheckHeadersAsync(cancel);
                if (!headersTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAndEndRecordIfNeededAsync_ContinueAfterHeadersAsync(this, headersTask, cancel);
                }

                if (!headersTask.Result)
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                var endRecordTask = EndRecordAsync(cancel);
                return endRecordTask;
            }

            return default;

            static async ValueTask WriteHeadersAndEndRecordIfNeededAsync_ContinueAfterHeadersAsync(AsyncWriter<T> self, ValueTask<bool> waitFor, CancellationToken cancel)
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

        private ValueTask WriteColumnAsync(T row, int colIx, Column col, bool needsSeparator, CancellationToken cancel)
        {
            if (needsSeparator)
            {
                var sepTask = PlaceCharInStagingAsync(Configuration.Options.ValueSeparator, cancel);
                if (!sepTask.IsCompletedSuccessfully(this))
                {
                    return WriteColumnAsync_ContinueAfterSeparatorAsync(this, sepTask, row, colIx, col, cancel);
                }
            }

            var ctx = WriteContexts[colIx].SetRowNumberForWriteColumn(RowNumber);
            if (!col.Write(row, ctx, Buffer))
            {
                return Throw.SerializationException<ValueTask>($"Could not write column {col.Name}, formatter returned false");
            }

            ReadOnlySequence<char> res = default;
            if (!Buffer.MakeSequence(ref res))
            {
                // nothing was written, so just move on
                return default;
            }

            var writeTask = WriteValueAsync(res, cancel);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return WriteColumnAsync_ContinueAfterWriteAsync(this, writeTask, cancel);
            }

            Buffer.Reset();

            return default;

            // wait for the separator to be written, then continue async
            static async ValueTask WriteColumnAsync_ContinueAfterSeparatorAsync(AsyncWriter<T> self, ValueTask waitFor, T row, int colIx, Column col, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                var ctx = self.WriteContexts[colIx].SetRowNumberForWriteColumn(self.RowNumber);

                if (!col.Write(row, ctx, self.Buffer))
                {
                    Throw.SerializationException<object>($"Could not write column {col.Name}, formatter returned false");
                }

                ReadOnlySequence<char> res = default;
                if (!self.Buffer.MakeSequence(ref res))
                {
                    // nothing was written, so just move on
                    return;
                }

                var writeTask = self.WriteValueAsync(res, cancel);
                await ConfigureCancellableAwait(self, writeTask, cancel);
                CheckCancellation(self, cancel);

                self.Buffer.Reset();
            }

            // wait for the write to finish, then continue async
            static async ValueTask WriteColumnAsync_ContinueAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                self.Buffer.Reset();
            }
        }

        // returns true if it did write out headers,
        //   so we need to end a record before
        //   writing the next one
        private ValueTask<bool> CheckHeadersAsync(CancellationToken cancel)
        {
            // make a note of what the columns to write actually are
            Columns = Configuration.SerializeColumns;
            CreateWriteContexts();

            IsFirstRow = false;

            if (Configuration.Options.WriteHeader == WriteHeader.Never)
            {
                // nothing to write, so bail
                return new ValueTask<bool>(false);
            }

            var writeTask = WriteHeadersAsync(cancel);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return CheckHeadersAsync_CompleteAsync(this, writeTask, cancel);
            }

            return new ValueTask<bool>(true);

            // wait for the write to complete, then return true
            static async ValueTask<bool> CheckHeadersAsync_CompleteAsync(AsyncWriter<T> self, ValueTask waitFor, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                return true;
            }
        }

        private ValueTask WriteHeadersAsync(CancellationToken cancel)
        {
            var needsEscape = Configuration.SerializeColumnsNeedEscape;

            var columnsValue = Columns;
            var valueSeparator = Configuration.Options.ValueSeparator;

            for (var i = 0; i < columnsValue.Length; i++)
            {
                // for the separator
                if (i != 0)
                {
                    var sepTask = PlaceCharInStagingAsync(valueSeparator, cancel);
                    if (!sepTask.IsCompletedSuccessfully(this))
                    {
                        return WriteHeadersAsync_CompleteAfterFlushAsync(this, sepTask, needsEscape, valueSeparator, i, cancel);
                    }
                }

                var writeTask = WriteSingleHeaderAsync(columnsValue[i], needsEscape[i], cancel);
                if (!writeTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAsync_CompleteAfterHeaderWriteAsync(this, writeTask, needsEscape, valueSeparator, i, cancel);
                }
            }

            return default;

            // waits for a flush to finish, then proceeds with writing headers
            static async ValueTask WriteHeadersAsync_CompleteAfterFlushAsync(AsyncWriter<T> self, ValueTask waitFor, bool[] needsEscape, char valueSeparator, int i, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                var selfColumnsValue = self.Columns;
                var headerTask = self.WriteSingleHeaderAsync(selfColumnsValue[i], needsEscape[i], cancel);
                await ConfigureCancellableAwait(self, headerTask, cancel);
                CheckCancellation(self, cancel);

                // implicit increment at the end of the calling loop
                i++;

                for (; i < selfColumnsValue.Length; i++)
                {
                    // by definition we've always wrote at least one column here
                    var placeTask = self.PlaceCharInStagingAsync(valueSeparator, cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);

                    var writeTask = self.WriteSingleHeaderAsync(selfColumnsValue[i], needsEscape[i], cancel);
                    await ConfigureCancellableAwait(self, writeTask, cancel);
                    CheckCancellation(self, cancel);
                }
            }

            // waits for a header write to finish, then proceeds with the rest
            static async ValueTask WriteHeadersAsync_CompleteAfterHeaderWriteAsync(AsyncWriter<T> self, ValueTask waitFor, bool[] needsEscape, char valueSeparator, int i, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                // implicit increment at the end of the calling loop
                i++;

                var selfColumnsValue = self.Columns;
                for (; i < selfColumnsValue.Length; i++)
                {
                    // by definition we've always wrote at least one column here
                    var placeTask = self.PlaceCharInStagingAsync(valueSeparator, cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);

                    var writeTask = self.WriteSingleHeaderAsync(selfColumnsValue[i], needsEscape[i], cancel);
                    await ConfigureCancellableAwait(self, writeTask, cancel);
                    CheckCancellation(self, cancel);
                }
            }
        }

        private ValueTask WriteSingleHeaderAsync(Column column, bool escape, CancellationToken cancel)
        {
            var colName = column.Name;

            if (!escape)
            {
                var write = colName.AsMemory();
                return PlaceInStagingAsync(write, cancel);
            }
            else
            {
                var options = Configuration.Options;

                // try and blit everything in relatively few calls
                var escapedValueStartAndStop = options.EscapedValueStartAndEnd!.Value;
                var escapeValueEscapeChar = options.EscapedValueEscapeCharacter!.Value;

                var colMem = colName.AsMemory();

                // start with the escape char
                var startEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                if (!startEscapeTask.IsCompletedSuccessfully(this))
                {
                    return WriteSingleHeaderAsync_CompleteAfterFirstCharAsync(this, startEscapeTask, escapedValueStartAndStop, escapeValueEscapeChar, colMem, cancel);
                }

                var start = 0;
                var end = Utils.FindChar(colMem, start, escapedValueStartAndStop);
                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var writeTask = PlaceInStagingAsync(toWrite, cancel);
                    if (!writeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteSingleHeaderAsync_CompleteAfterWriteAsync(this, writeTask, escapedValueStartAndStop, escapeValueEscapeChar, colMem, end, cancel);
                    }

                    // place the escape char
                    var escapeTask = PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                    if (!escapeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteSingleHeaderAsync_CompleteAfterEscapeAsync(this, escapeTask, escapedValueStartAndStop, escapeValueEscapeChar, colMem, end, cancel);
                    }

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var writeTask = PlaceInStagingAsync(toWrite, cancel);
                    if (!writeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteSingleHeaderAsync_CompleteAfterLastWriteAsync(this, writeTask, escapedValueStartAndStop, cancel);
                    }
                }

                // end with the escape char
                var endEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                if (!endEscapeTask.IsCompletedSuccessfully(this))
                {
                    return endEscapeTask;
                }

                return default;
            }

            // waits for the first char to write, then does the rest asynchronously
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterFirstCharAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> colMem, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                var start = 0;
                var end = Utils.FindChar(colMem, start, escapedValueStartAndStop);
                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var placeTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);

                    // place the escape char
                    var secondPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancel);
                    CheckCancellation(self, cancel);

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var thirdPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancel);
                    CheckCancellation(self, cancel);
                }

                // end with the escape char
                var fourthPlaceTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await ConfigureCancellableAwait(self, fourthPlaceTask, cancel);
                CheckCancellation(self, cancel);
            }

            // waits for a write to finish, then complete the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> colMem, int end, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await ConfigureCancellableAwait(self, placeTask, cancel);
                CheckCancellation(self, cancel);

                var start = end;
                end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var secondPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancel);
                    CheckCancellation(self, cancel);

                    // place the escape char
                    var thirdPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancel);
                    CheckCancellation(self, cancel);

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var fourthPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await ConfigureCancellableAwait(self, fourthPlaceTask, cancel);
                    CheckCancellation(self, cancel);
                }

                // end with the escape char
                var fifthPlaceTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await ConfigureCancellableAwait(self, fifthPlaceTask, cancel);
                CheckCancellation(self, cancel);
            }

            // waits for an escape to finish, then completes the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterEscapeAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> colMem, int end, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                var start = end;
                end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var placeTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await ConfigureCancellableAwait(self, placeTask, cancel);
                    CheckCancellation(self, cancel);

                    // place the escape char
                    var secondPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancel);
                    CheckCancellation(self, cancel);

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var thirdPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancel);
                    CheckCancellation(self, cancel);
                }

                // end with the escape char
                var fourthPlaceTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await ConfigureCancellableAwait(self, fourthPlaceTask, cancel);
                CheckCancellation(self, cancel);
            }

            // waits for a write to finish, then writes out the final char and maybe flushes async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterLastWriteAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, CancellationToken cancel)
            {
                await ConfigureCancellableAwait(self, waitFor, cancel);
                CheckCancellation(self, cancel);

                // end with the escape char
                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await ConfigureCancellableAwait(self, placeTask, cancel);
                CheckCancellation(self, cancel);
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
                        var headersTask = CheckHeadersAsync(CancellationToken.None);
                        if (!headersTask.IsCompletedSuccessfully(this))
                        {
                            return DisposeAsync_ContinueAfterHeadersAsync(this, headersTask, writeTrailingNewLine);
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
                            var flushTask = FlushStagingAsync(CancellationToken.None);
                            if (!flushTask.IsCompletedSuccessfully(this))
                            {
                                return DisposeAsync_ContinueAfterFlushAsync(this, flushTask);
                            }
                        }

                        Staging.Dispose();
                        Staging = EmptyMemoryOwner.Singleton;
                        StagingMemory = Memory<char>.Empty;
                    }

                    var ret = Inner.DisposeAsync();
                    if (!ret.IsCompletedSuccessfully(this))
                    {
                        return DisposeAsync_ContinueAfterInnerDisposedAsync(this, ret);
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

            // wait on headers, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterHeadersAsync(AsyncWriter<T> self, ValueTask<bool> waitFor, WriteTrailingRowEnding writeTrailingNewLine)
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

            // wait on end record, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor)
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

            // wait on flush, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterFlushAsync(AsyncWriter<T> self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    if (self.HasStaging)
                    {
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

            // wait on Inner.DisposeAsync
            static async ValueTask DisposeAsync_ContinueAfterInnerDisposedAsync(AsyncWriter<T> self, ValueTask waitFor)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, CancellationToken.None);

                    if (self.OneCharOwner.HasValue)
                    {
                        self.OneCharOwner.Value.Dispose();
                        self.OneCharOwner.Clear();
                    }
                    self.Buffer.Dispose();

                    self.IsDisposed = true;
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
            return $"{nameof(AsyncWriter<T>)} with {Configuration}";
        }
    }
}
