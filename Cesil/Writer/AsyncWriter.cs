using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

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

            var checkHeadersAndEndRowTask = WriteHeadersAndEndRowIfNeededAsync(cancel);
            if (!checkHeadersAndEndRowTask.IsCompletedSuccessfully(this))
            {
                return WriteAsync_ContinueAfterHeadersAndEndRecordAsync(this, checkHeadersAndEndRowTask, row, cancel);
            }

            var columnsValue = Columns.Value;
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

            // wait for the record to end, then continue async
            static async ValueTask WriteAsync_ContinueAfterHeadersAndEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor, T row, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var selfColumnsValue = self.Columns.Value;
                for (var i = 0; i < selfColumnsValue.Length; i++)
                {
                    var needsSeparator = i != 0;
                    var col = selfColumnsValue[i];

                    var writeTask = self.WriteColumnAsync(row, i, col, needsSeparator, cancel);
                    await writeTask;
                    cancel.ThrowIfCancellationRequested();
                }

                self.RowNumber++;
            }

            // wait for the column to be written, then continue with the loop
            static async ValueTask WriteAsync_ContinueAfterWriteColumnAsync(AsyncWriter<T> self, ValueTask waitFor, T row, int i, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                // the implict increment at the end of the loop
                i++;

                var selfColumnsValue = self.Columns.Value;
                for (; i < selfColumnsValue.Length; i++)
                {
                    const bool needsSeparator = true;                  // by definition, this isn't the first loop
                    var col = selfColumnsValue[i];

                    var writeTask = self.WriteColumnAsync(row, i, col, needsSeparator, cancel);
                    await writeTask;
                    cancel.ThrowIfCancellationRequested();
                }

                self.RowNumber++;
            }
        }

        public override ValueTask WriteCommentAsync(string comment, CancellationToken cancel = default)
        {
            Utils.CheckArgumentNull(comment, nameof(comment));

            AssertNotDisposed(this);

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

            // continue after checking for writing headers (and ending the last row, if needed)
            static async ValueTask WriteCommentAsync_ContinueAfterWriteHeadersAndEndRowIfNeededAsync(AsyncWriter<T> self, ValueTask waitFor, string comment, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var (commentChar, segments) = self.SplitCommentIntoLines(comment);

                if (segments.IsSingleSegment)
                {
                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    var seg = segments.First;
                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
                        cancel.ThrowIfCancellationRequested();
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
                            await endTask;
                            cancel.ThrowIfCancellationRequested();
                        }

                        var thirdPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await thirdPlaceTask;
                        cancel.ThrowIfCancellationRequested();

                        if (seg.Length > 0)
                        {
                            var fourthPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                            await fourthPlaceTask;
                            cancel.ThrowIfCancellationRequested();
                        }

                        isFirstRow = false;
                    }
                }
            }

            // continue after writing the # (or whatever) before the rest of the single segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharInStagingSingleSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlyMemory<char> seg, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var placeTask = self.PlaceInStagingAsync(seg, cancel); ;
                await placeTask;
                cancel.ThrowIfCancellationRequested();
            }

            // continue after writing a row ender in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterEndRecordMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;
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

                while (e.MoveNext())
                {
                    // no need to check is first, we know it's not
                    seg = e.Current;

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

            // continue aftering writing a # (or whatever) in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                if (seg.Length > 0)
                {
                    var placeTask = self.PlaceInStagingAsync(seg, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();
                }

                while (e.MoveNext())
                {
                    // no need to check is first, we know it's not
                    seg = e.Current;
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

            // continue after writing a segment, in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceSegmentMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                while (e.MoveNext())
                {
                    // no need to check is first, we know it's not
                    var seg = e.Current;
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

            var ctx = WriteContext.WritingColumn(RowNumber, ColumnIdentifier.Create(colIx, col.Name), Context);

            if (!col.Write.Value(row, ctx, Buffer))
            {
                return Throw.SerializationException<ValueTask>($"Could not write column {col.Name}, formatter returned false");
            }

            var res = Buffer.Buffer;
            if (res.IsEmpty)
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
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var ctx = WriteContext.WritingColumn(self.RowNumber, ColumnIdentifier.Create(colIx, col.Name), self.Context);

                if (!col.Write.Value(row, ctx, self.Buffer))
                {
                    Throw.SerializationException<object>($"Could not write column {col.Name}, formatter returned false");
                }

                var res = self.Buffer.Buffer;
                if (res.IsEmpty)
                {
                    // nothing was written, so just move on
                    return;
                }

                var writeTask = self.WriteValueAsync(res, cancel);
                await writeTask;
                cancel.ThrowIfCancellationRequested();

                self.Buffer.Reset();
            }

            // wait for the write to finish, then continue async
            static async ValueTask WriteColumnAsync_ContinueAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                self.Buffer.Reset();
            }
        }

        // returns true if it did write out headers,
        //   so we need to end a record before
        //   writing the next one
        private ValueTask<bool> CheckHeadersAsync(CancellationToken cancel)
        {
            // make a note of what the columns to write actually are
            Columns.Value = Configuration.SerializeColumns;

            if (Configuration.Options.WriteHeader == WriteHeader.Never)
            {
                // nothing to write, so bail
                return new ValueTask<bool>(false);
            }

            var writeTask = WriteHeadersAsync(cancel);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return CheckHeadersAsync_CompleteAsync(writeTask, cancel);
            }

            return new ValueTask<bool>(true);

            // wait for the write to complete, then return true
            static async ValueTask<bool> CheckHeadersAsync_CompleteAsync(ValueTask waitFor, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                return true;
            }
        }

        private ValueTask WriteHeadersAsync(CancellationToken cancel)
        {
            var needsEscape = Configuration.SerializeColumnsNeedEscape;

            var columnsValue = Columns.Value;
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
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var selfColumnsValue = self.Columns.Value;
                var headerTask = self.WriteSingleHeaderAsync(selfColumnsValue[i], needsEscape[i], cancel);
                await headerTask;
                cancel.ThrowIfCancellationRequested();

                // implicit increment at the end of the calling loop
                i++;

                for (; i < selfColumnsValue.Length; i++)
                {
                    // by definition we've always wrote at least one column here
                    var placeTask = self.PlaceCharInStagingAsync(valueSeparator, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    var writeTask = self.WriteSingleHeaderAsync(selfColumnsValue[i], needsEscape[i], cancel);
                    await writeTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }

            // waits for a header write to finish, then proceeds with the rest
            static async ValueTask WriteHeadersAsync_CompleteAfterHeaderWriteAsync(AsyncWriter<T> self, ValueTask waitFor, bool[] needsEscape, char valueSeparator, int i, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                // implicit increment at the end of the calling loop
                i++;

                var selfColumnsValue = self.Columns.Value;
                for (; i < selfColumnsValue.Length; i++)
                {
                    // by definition we've always wrote at least one column here
                    var placeTask = self.PlaceCharInStagingAsync(valueSeparator, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    var writeTask = self.WriteSingleHeaderAsync(selfColumnsValue[i], needsEscape[i], cancel);
                    await writeTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }
        }

        private ValueTask WriteSingleHeaderAsync(Column column, bool escape, CancellationToken cancel)
        {
            var colName = column.Name.Value;

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
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var start = 0;
                var end = Utils.FindChar(colMem, start, escapedValueStartAndStop);
                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var placeTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    // place the escape char
                    var secondPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                    await secondPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var thirdPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await thirdPlaceTask;
                    cancel.ThrowIfCancellationRequested();
                }

                // end with the escape char
                var fourthPlaceTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await fourthPlaceTask;
                cancel.ThrowIfCancellationRequested();
            }

            // waits for a write to finish, then complete the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> colMem, int end, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await placeTask;
                cancel.ThrowIfCancellationRequested();

                var start = end;
                end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var secondPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await secondPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    // place the escape char
                    var thirdPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                    await thirdPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var fourthPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await fourthPlaceTask;
                    cancel.ThrowIfCancellationRequested();
                }

                // end with the escape char
                var fifthPlaceTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await fifthPlaceTask;
                cancel.ThrowIfCancellationRequested();
            }

            // waits for an escape to finish, then completes the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterEscapeAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> colMem, int end, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var start = end;
                end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var placeTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    // place the escape char
                    var secondPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                    await secondPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, escapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var thirdPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await thirdPlaceTask;
                    cancel.ThrowIfCancellationRequested();
                }

                // end with the escape char
                var fourthPlaceTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await fourthPlaceTask;
                cancel.ThrowIfCancellationRequested();
            }

            // waits for a write to finish, then writes out the final char and maybe flushes async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterLastWriteAsync(AsyncWriter<T> self, ValueTask waitFor, char escapedValueStartAndStop, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                // end with the escape char
                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await placeTask;
                cancel.ThrowIfCancellationRequested();
            }
        }

        public override ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                var writeTrailingNewLine = Configuration.Options.WriteTrailingNewLine;

                if (IsFirstRow)
                {
                    var headersTask = CheckHeadersAsync(CancellationToken.None);
                    if (!headersTask.IsCompletedSuccessfully(this))
                    {
                        return DisposeAsync_ContinueAfterHeadersAsync(this, headersTask, writeTrailingNewLine);
                    }
                }

                if (writeTrailingNewLine == WriteTrailingNewLine.Always)
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
                        var flushTask = FlushStagingAsync(CancellationToken.None);
                        if (!flushTask.IsCompletedSuccessfully(this))
                        {
                            return DisposeAsync_ContinueAfterFlushAsync(this, flushTask);
                        }
                    }

                    Staging.Value.Dispose();
                }

                var ret = Inner.DisposeAsync();
                if (!ret.IsCompletedSuccessfully(this))
                {
                    return DisposeAsync_ContinueAfterInnerDisposedAsync(this, ret);
                }

                if (OneCharOwner.HasValue)
                {
                    OneCharOwner.Value.Dispose();
                }
                Buffer.Dispose();
                IsDisposed = true;
            }

            return default;

            // wait on headers, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterHeadersAsync(AsyncWriter<T> self, ValueTask<bool> waitFor, WriteTrailingNewLine writeTrailingNewLine)
            {
                await waitFor;

                if (writeTrailingNewLine == WriteTrailingNewLine.Always)
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

            // wait on end record, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor)
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

            // wait on flush, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterFlushAsync(AsyncWriter<T> self, ValueTask waitFor)
            {
                await waitFor;

                if (self.Staging.HasValue)
                {
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

            // wait on Inner.DisposeAsync
            static async ValueTask DisposeAsync_ContinueAfterInnerDisposedAsync(AsyncWriter<T> self, ValueTask waitFor)
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
            return $"{nameof(AsyncWriter<T>)} with {Configuration}";
        }
    }
}
