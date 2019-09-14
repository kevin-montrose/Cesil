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
        public override bool IsDisposed => Inner == null;

        internal AsyncWriter(ConcreteBoundConfiguration<T> config, IAsyncWriterAdapter inner, object context) : base(config, inner, context) { }

        public override ValueTask WriteAsync(T row, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);

            var checkHeadersAndEndRowTask = WriteHeadersAndEndRowIfNeededAsync(cancel);
            if (!checkHeadersAndEndRowTask.IsCompletedSuccessfully(this))
            {
                return WriteAsync_ContinueAfterHeadersAndEndRecordAsync(this, checkHeadersAndEndRowTask, row, cancel);
            }

            for (var i = 0; i < Columns.Length; i++)
            {
                var needsSeparator = i != 0;
                var col = Columns[i];

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

                for (var i = 0; i < self.Columns.Length; i++)
                {
                    var needsSeparator = i != 0;
                    var col = self.Columns[i];

                    var writeTask = self.WriteColumnAsync(row, i, col, needsSeparator, cancel);
                    await writeTask;
                }

                self.RowNumber++;
            }

            // wait for the column to be written, then continue with the loop
            static async ValueTask WriteAsync_ContinueAfterWriteColumnAsync(AsyncWriter<T> self, ValueTask waitFor, T row, int i, CancellationToken cancel)
            {
                await waitFor;

                // the implict increment at the end of the loop
                i++;

                for (; i < self.Columns.Length; i++)
                {
                    const bool needsSeparator = true;                  // by definition, this isn't the first loop
                    var col = self.Columns[i];

                    var writeTask = self.WriteColumnAsync(row, i, col, needsSeparator, cancel);
                    await writeTask;
                }

                self.RowNumber++;
            }
        }

        public override ValueTask WriteCommentAsync(string comment, CancellationToken cancel = default)
        {
            if (comment == null)
            {
                return Throw.ArgumentNullException<ValueTask>(nameof(comment));
            }

            AssertNotDisposed(this);

            var writeHeadersTask = WriteHeadersAndEndRowIfNeededAsync(cancel);
            if (!writeHeadersTask.IsCompletedSuccessfully(this))
            {
                return WriteCommentAsync_ContinueAfterWriteHeadersAndEndRowIfNeededAsync(this, writeHeadersTask, comment, cancel);
            }

            var segments = SplitCommentIntoLines(comment);

            var commentChar = Config.CommentChar.Value;

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

                var segments = self.SplitCommentIntoLines(comment);

                var commentChar = self.Config.CommentChar.Value;

                if (segments.IsSingleSegment)
                {
                    var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                    await placeTask;
                    var seg = segments.First;
                    if (seg.Length > 0)
                    {
                        var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                        await secondPlaceTask;
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
                        }

                        var thirdPlaceTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                        await thirdPlaceTask;
                        if (seg.Length > 0)
                        {
                            var fourthPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                            await fourthPlaceTask;
                        }

                        isFirstRow = false;
                    }
                }
            }

            // continue after writing the # (or whatever) before the rest of the single segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharInStagingSingleSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlyMemory<char> seg, CancellationToken cancel)
            {
                await waitFor;

                var placeTask = self.PlaceInStagingAsync(seg, cancel); ;
                await placeTask;
            }

            // continue after writing a row ender in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterEndRecordMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                var placeTask = self.PlaceCharInStagingAsync(commentChar, cancel);
                await placeTask;
                if (seg.Length > 0)
                {
                    var secondPlaceTask = self.PlaceInStagingAsync(seg, cancel);
                    await secondPlaceTask;
                }

                while (e.MoveNext())
                {
                    // no need to check is first, we know it's not
                    seg = e.Current;

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

            // continue aftering writing a # (or whatever) in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                if (seg.Length > 0)
                {
                    var placeTask = self.PlaceInStagingAsync(seg, cancel);
                    await placeTask;
                }

                while (e.MoveNext())
                {
                    // no need to check is first, we know it's not
                    seg = e.Current;
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

            // continue after writing a segment, in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceSegmentMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                while (e.MoveNext())
                {
                    // no need to check is first, we know it's not
                    var seg = e.Current;
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

        private ValueTask WriteColumnAsync(T row, int colIx, Column col, bool needsSeparator, CancellationToken cancel)
        {
            if (needsSeparator)
            {
                var sepTask = PlaceCharInStagingAsync(Config.ValueSeparator, cancel);
                if (!sepTask.IsCompletedSuccessfully(this))
                {
                    return WriteColumnAsync_ContinueAfterSeparatorAsync(this, sepTask, row, colIx, col, cancel);
                }
            }

            var ctx = WriteContext.WritingColumn(RowNumber, ColumnIdentifier.Create(colIx, col.Name), Context);

            if (!col.Write(row, ctx, Buffer))
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

                var ctx = WriteContext.WritingColumn(self.RowNumber, ColumnIdentifier.Create(colIx, col.Name), self.Context);

                if (!col.Write(row, ctx, self.Buffer))
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

                self.Buffer.Reset();
            }

            // wait for the write to finish, then continue async
            static async ValueTask WriteColumnAsync_ContinueAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, CancellationToken cancel)
            {
                await waitFor;

                self.Buffer.Reset();
            }
        }

        // returns true if it did write out headers,
        //   so we need to end a record before
        //   writing the next one
        private ValueTask<bool> CheckHeadersAsync(CancellationToken cancel)
        {
            // make a note of what the columns to write actually are
            Columns = Config.SerializeColumns;

            if (Config.WriteHeader == WriteHeaders.Never)
            {
                // nothing to write, so bail
                return new ValueTask<bool>(false);
            }

            var writeTask = WriteHeadersAsync(cancel);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return CheckHeadersAsync_CompleteAsync(writeTask);
            }

            return new ValueTask<bool>(true);

            // wait for the write to complete, then return true
            static async ValueTask<bool> CheckHeadersAsync_CompleteAsync(ValueTask waitFor)
            {
                await waitFor;

                return true;
            }
        }

        private ValueTask WriteHeadersAsync(CancellationToken cancel)
        {
            var needsEscape = Config.SerializeColumnsNeedEscape;

            for (var i = 0; i < Columns.Length; i++)
            {
                // for the separator
                if (i != 0)
                {
                    var sepTask = PlaceCharInStagingAsync(Config.ValueSeparator, cancel);
                    if (!sepTask.IsCompletedSuccessfully(this))
                    {
                        return WriteHeadersAsync_CompleteAfterFlushAsync(this, sepTask, needsEscape, i, cancel);
                    }
                }

                var writeTask = WriteSingleHeaderAsync(Columns[i], needsEscape[i], cancel);
                if (!writeTask.IsCompletedSuccessfully(this))
                {
                    return WriteHeadersAsync_CompleteAfterHeaderWriteAsync(this, writeTask, needsEscape, i, cancel);
                }
            }

            return default;

            // waits for a flush to finish, then proceeds with writing headers
            static async ValueTask WriteHeadersAsync_CompleteAfterFlushAsync(AsyncWriter<T> self, ValueTask waitFor, bool[] needsEscape, int i, CancellationToken cancel)
            {
                await waitFor;

                var headerTask = self.WriteSingleHeaderAsync(self.Columns[i], needsEscape[i], cancel);
                await headerTask;

                // implicit increment at the end of the calling loop
                i++;

                for (; i < self.Columns.Length; i++)
                {
                    // by definition we've always wrote at least one column here
                    var placeTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                    await placeTask;

                    var writeTask = self.WriteSingleHeaderAsync(self.Columns[i], needsEscape[i], cancel);
                    await writeTask;
                }
            }

            // waits for a header write to finish, then proceeds with the rest
            static async ValueTask WriteHeadersAsync_CompleteAfterHeaderWriteAsync(AsyncWriter<T> self, ValueTask waitFor, bool[] needsEscape, int i, CancellationToken cancel)
            {
                await waitFor;

                // implicit increment at the end of the calling loop
                i++;

                for (; i < self.Columns.Length; i++)
                {
                    // by definition we've always wrote at least one column here
                    var placeTask = self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);
                    await placeTask;

                    var writeTask = self.WriteSingleHeaderAsync(self.Columns[i], needsEscape[i], cancel);
                    await writeTask;
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
                // try and blit everything in relatively few calls

                var colMem = colName.AsMemory();

                // start with the escape char
                var startEscapeTask = PlaceCharInStagingAsync(Config.EscapedValueStartAndStop, cancel);
                if (!startEscapeTask.IsCompletedSuccessfully(this))
                {
                    return WriteSingleHeaderAsync_CompleteAfterFirstCharAsync(this, startEscapeTask, colMem, cancel);
                }

                var start = 0;
                var end = Utils.FindChar(colMem, start, Config.EscapedValueStartAndStop);
                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var writeTask = PlaceInStagingAsync(toWrite, cancel);
                    if (!writeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteSingleHeaderAsync_CompleteAfterWriteAsync(this, writeTask, colMem, end, cancel);
                    }

                    // place the escape char
                    var escapeTask = PlaceCharInStagingAsync(Config.EscapeValueEscapeChar, cancel);
                    if (!escapeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteSingleHeaderAsync_CompleteAfterEscapeAsync(this, escapeTask, colMem, end, cancel);
                    }

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, Config.EscapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var writeTask = PlaceInStagingAsync(toWrite, cancel);
                    if (!writeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteSingleHeaderAsync_CompleteAfterLastWriteAsync(this, writeTask, cancel);
                    }
                }

                // end with the escape char
                var endEscapeTask = PlaceCharInStagingAsync(Config.EscapedValueStartAndStop, cancel);
                if (!endEscapeTask.IsCompletedSuccessfully(this))
                {
                    return endEscapeTask;
                }

                return default;
            }

            // waits for the first char to write, then does the rest asynchronously
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterFirstCharAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlyMemory<char> colMem, CancellationToken cancel)
            {
                await waitFor;

                var start = 0;
                var end = Utils.FindChar(colMem, start, self.Config.EscapedValueStartAndStop);
                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var placeTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await placeTask;

                    // place the escape char
                    var secondPlaceTask = self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);
                    await secondPlaceTask;

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var thirdPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await thirdPlaceTask;
                }

                // end with the escape char
                var fourthPlaceTask = self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
                await fourthPlaceTask;
            }

            // waits for a write to finish, then complete the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlyMemory<char> colMem, int end, CancellationToken cancel)
            {
                await waitFor;

                var placeTask = self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);
                await placeTask;

                var start = end;
                end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var secondPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await secondPlaceTask;

                    // place the escape char
                    var thirdPlaceTask = self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);
                    await thirdPlaceTask;

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var fourthPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await fourthPlaceTask;
                }

                // end with the escape char
                var fifthPlaceTask = self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
                await fifthPlaceTask;
            }

            // waits for an escape to finish, then completes the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterEscapeAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlyMemory<char> colMem, int end, CancellationToken cancel)
            {
                await waitFor;

                var start = end;
                end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    var placeTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await placeTask;

                    // place the escape char
                    var secondPlaceTask = self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);
                    await secondPlaceTask;

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    var thirdPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await thirdPlaceTask;
                }

                // end with the escape char
                var fourthPlaceTask = self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
                await fourthPlaceTask;
            }

            // waits for a write to finish, then writes out the final char and maybe flushes async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterLastWriteAsync(AsyncWriter<T> self, ValueTask waitFor, CancellationToken cancel)
            {
                await waitFor;

                // end with the escape char
                var placeTask = self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
                await placeTask;
            }
        }

        public override ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                if (IsFirstRow)
                {
                    var headersTask = CheckHeadersAsync(CancellationToken.None);
                    if (!headersTask.IsCompletedSuccessfully(this))
                    {
                        return DisposeAsync_ContinueAfterHeadersAsync(this, headersTask);
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
                        var flushTask = FlushStagingAsync(CancellationToken.None);
                        if (!flushTask.IsCompletedSuccessfully(this))
                        {
                            return DisposeAsync_ContinueAfterFlushAsync(this, flushTask);
                        }
                    }

                    Staging.Dispose();
                }

                var ret = Inner.DisposeAsync();
                if (!ret.IsCompletedSuccessfully(this))
                {
                    return DisposeAsync_ContinueAfterInnerDisposedAsync(this, ret);
                }

                OneCharOwner?.Dispose();
                Buffer.Dispose();
                Inner = null;
            }

            return default;

            // wait on headers, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterHeadersAsync(AsyncWriter<T> self, ValueTask<bool> waitFor)
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

            // wait on end record, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor)
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

            // wait on flush, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterFlushAsync(AsyncWriter<T> self, ValueTask waitFor)
            {
                await waitFor;

                self.Staging?.Dispose();

                var disposeTask = self.Inner.DisposeAsync();
                await disposeTask;
                self.OneCharOwner?.Dispose();
                self.Buffer.Dispose();

                self.Inner = null;
            }

            // wait on Inner.DisposeAsync
            static async ValueTask DisposeAsync_ContinueAfterInnerDisposedAsync(AsyncWriter<T> self, ValueTask waitFor)
            {
                await waitFor;

                self.OneCharOwner?.Dispose();
                self.Buffer.Dispose();

                self.Inner = null;
            }
        }

        public override string ToString()
        {
            return $"{nameof(AsyncWriter<T>)} with {Config}";
        }
    }
}
