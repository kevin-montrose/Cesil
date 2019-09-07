using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class AsyncWriter<T> :
        AsyncWriterBase<T>
    {
        public override bool IsDisposed => Inner == null;

        internal AsyncWriter(ConcreteBoundConfiguration<T> config, TextWriter inner, object context) : base(config, inner, context) { }

        public override ValueTask WriteAsync(T row, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            var checkHeadersAndEndRowTask = WriteHeadersAndEndRowIfNeededAsync(cancel);
            if (!checkHeadersAndEndRowTask.IsCompletedSuccessfully)
            {
                return WriteAsync_ContinueAfterHeadersAndEndRecordAsync(this, checkHeadersAndEndRowTask, row, cancel);
            }

            for (var i = 0; i < Columns.Length; i++)
            {
                var needsSeparator = i != 0;
                var col = Columns[i];

                var writeColumnTask = WriteColumnAsync(row, i, col, needsSeparator, cancel);
                if (!writeColumnTask.IsCompletedSuccessfully)
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

                    await self.WriteColumnAsync(row, i, col, needsSeparator, cancel);
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

                    await self.WriteColumnAsync(row, i, col, needsSeparator, cancel);
                }

                self.RowNumber++;
            }
        }

        public override ValueTask WriteCommentAsync(string comment, CancellationToken cancel = default)
        {
            if (comment == null)
            {
                Throw.ArgumentNullException(nameof(comment));
            }

            AssertNotDisposed();

            var writeHeadersTask = WriteHeadersAndEndRowIfNeededAsync(cancel);
            if (!writeHeadersTask.IsCompletedSuccessfully)
            {
                return WriteCommentAsync_ContinueAfterWriteHeadersAndEndRowIfNeededAsync(this, writeHeadersTask, comment, cancel);
            }

            var segments = SplitCommentIntoLines(comment);

            var commentChar = Config.CommentChar.Value;

            if (segments.IsSingleSegment)
            {
                var seg = segments.First;

                var placeCharInStagingTask = PlaceCharInStagingAsync(commentChar, cancel);
                if (!placeCharInStagingTask.IsCompletedSuccessfully)
                {
                    if (seg.Length > 0)
                    {
                        return WriteCommentAsync_ContinueAfterPlaceCharInStagingSingleSegmentAsync(this, placeCharInStagingTask, seg, cancel);
                    }

                    return new ValueTask(placeCharInStagingTask);
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
                    if (!endRecordTask.IsCompletedSuccessfully)
                    {
                        return WriteCommentAsync_ContinueAfterEndRecordMultiSegmentAsync(this, endRecordTask, commentChar, seg, e, cancel);
                    }
                }

                var placeCharTask = PlaceCharInStagingAsync(commentChar, cancel);
                if (!placeCharTask.IsCompletedSuccessfully)
                {
                    return WriteCommentAsync_ContinueAfterPlaceCharMultiSegmentAsync(this, placeCharTask, commentChar, seg, e, cancel);
                }

                if (seg.Length > 0)
                {
                    var placeSegTask = PlaceInStagingAsync(seg, cancel);
                    if (!placeSegTask.IsCompletedSuccessfully)
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
                    await self.PlaceCharInStagingAsync(commentChar, cancel);
                    var seg = segments.First;
                    if (seg.Length > 0)
                    {
                        await self.PlaceInStagingAsync(seg, cancel);
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
                            await self.EndRecordAsync(cancel);
                        }

                        await self.PlaceCharInStagingAsync(commentChar, cancel);
                        if (seg.Length > 0)
                        {
                            await self.PlaceInStagingAsync(seg, cancel);
                        }

                        isFirstRow = false;
                    }
                }
            }

            // continue after writing the # (or whatever) before the rest of the single segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharInStagingSingleSegmentAsync(AsyncWriter<T> self, Task waitFor, ReadOnlyMemory<char> seg, CancellationToken cancel)
            {
                await waitFor;

                await self.PlaceInStagingAsync(seg, cancel);
            }

            // continue after writing a row ender in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterEndRecordMultiSegmentAsync(AsyncWriter<T> self, ValueTask waitFor, char commentChar, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                await self.PlaceCharInStagingAsync(commentChar, cancel);
                if (seg.Length > 0)
                {
                    await self.PlaceInStagingAsync(seg, cancel);
                }

                while (e.MoveNext())
                {
                    // no need to check is first, we know it's not
                    seg = e.Current;
                    await self.EndRecordAsync(cancel);

                    await self.PlaceCharInStagingAsync(commentChar, cancel);
                    if (seg.Length > 0)
                    {
                        await self.PlaceInStagingAsync(seg, cancel);
                    }
                }
            }

            // continue aftering writing a # (or whatever) in the multi-segment case
            static async ValueTask WriteCommentAsync_ContinueAfterPlaceCharMultiSegmentAsync(AsyncWriter<T> self, Task waitFor, char commentChar, ReadOnlyMemory<char> seg, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                if (seg.Length > 0)
                {
                    await self.PlaceInStagingAsync(seg, cancel);
                }

                while (e.MoveNext())
                {
                    // no need to check is first, we know it's not
                    seg = e.Current;
                    await self.EndRecordAsync(cancel);

                    await self.PlaceCharInStagingAsync(commentChar, cancel);
                    if (seg.Length > 0)
                    {
                        await self.PlaceInStagingAsync(seg, cancel);
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
                    await self.EndRecordAsync(cancel);

                    await self.PlaceCharInStagingAsync(commentChar, cancel);
                    if (seg.Length > 0)
                    {
                        await self.PlaceInStagingAsync(seg, cancel);
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
                if (!headersTask.IsCompletedSuccessfully)
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
                    await self.EndRecordAsync(cancel);
                }
            }
        }

        private ValueTask WriteColumnAsync(T row, int colIx, Column col, bool needsSeparator, CancellationToken cancel)
        {
            if (needsSeparator)
            {
                var sepTask = PlaceCharInStagingAsync(Config.ValueSeparator, cancel);
                if (!sepTask.IsCompletedSuccessfully)
                {
                    return WriteColumnAsync_ContinueAfterSeparatorAsync(this, sepTask, row, colIx, col, cancel);
                }
            }

            var ctx = WriteContext.WritingColumn(RowNumber, ColumnIdentifier.Create(colIx, col.Name), Context);

            if (!col.Write(row, ctx, Buffer))
            {
                Throw.SerializationException($"Could not write column {col.Name}, formatter returned false");
            }

            var res = Buffer.Buffer;
            if (res.IsEmpty)
            {
                // nothing was written, so just move on
                return default;
            }

            var writeTask = WriteValueAsync(res, cancel);
            if (!writeTask.IsCompletedSuccessfully)
            {
                return WriteColumnAsync_ContinueAfterWriteAsync(this, writeTask, cancel);
            }

            Buffer.Reset();

            return default;

            // wait for the separator to be written, then continue async
            static async ValueTask WriteColumnAsync_ContinueAfterSeparatorAsync(AsyncWriter<T> self, Task waitFor, T row, int colIx, Column col, CancellationToken cancel)
            {
                await waitFor;

                var ctx = WriteContext.WritingColumn(self.RowNumber, ColumnIdentifier.Create(colIx, col.Name), self.Context);

                if (!col.Write(row, ctx, self.Buffer))
                {
                    Throw.SerializationException($"Could not write column {col.Name}, formatter returned false");
                }

                var res = self.Buffer.Buffer;
                if (res.IsEmpty)
                {
                    // nothing was written, so just move on
                    return;
                }

                await self.WriteValueAsync(res, cancel);

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
            if (!writeTask.IsCompletedSuccessfully)
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
                    if (!sepTask.IsCompletedSuccessfully)
                    {
                        return WriteHeadersAsync_CompleteAfterFlushAsync(this, sepTask, needsEscape, i, cancel);
                    }
                }

                var writeTask = WriteSingleHeaderAsync(Columns[i], needsEscape[i], cancel);
                if (!writeTask.IsCompletedSuccessfully)
                {
                    return WriteHeadersAsync_CompleteAfterHeaderWriteAsync(this, writeTask, needsEscape, i, cancel);
                }
            }

            return default;

            // waits for a flush to finish, then proceeds with writing headers
            static async ValueTask WriteHeadersAsync_CompleteAfterFlushAsync(AsyncWriter<T> self, Task waitFor, bool[] needsEscape, int i, CancellationToken cancel)
            {
                await waitFor;

                await self.WriteSingleHeaderAsync(self.Columns[i], needsEscape[i], cancel);

                // implicit increment at the end of the calling loop
                i++;

                for (; i < self.Columns.Length; i++)
                {
                    // by definition we've always wrote at least one column here
                    await self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);

                    await self.WriteSingleHeaderAsync(self.Columns[i], needsEscape[i], cancel);
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
                    await self.PlaceCharInStagingAsync(self.Config.ValueSeparator, cancel);

                    await self.WriteSingleHeaderAsync(self.Columns[i], needsEscape[i], cancel);
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
                if (!startEscapeTask.IsCompletedSuccessfully)
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
                    if (!writeTask.IsCompletedSuccessfully)
                    {
                        return WriteSingleHeaderAsync_CompleteAfterWriteAsync(this, writeTask, colMem, end, cancel);
                    }

                    // place the escape char
                    var escapeTask = PlaceCharInStagingAsync(Config.EscapeValueEscapeChar, cancel);
                    if (!escapeTask.IsCompletedSuccessfully)
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
                    if (!writeTask.IsCompletedSuccessfully)
                    {
                        return WriteSingleHeaderAsync_CompleteAfterLastWriteAsync(this, writeTask, cancel);
                    }
                }

                // end with the escape char
                var endEscapeTask = PlaceCharInStagingAsync(Config.EscapedValueStartAndStop, cancel);
                if (!endEscapeTask.IsCompletedSuccessfully)
                {
                    return new ValueTask(endEscapeTask);
                }

                return default;
            }

            // waits for the first char to write, then does the rest asynchronously
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterFirstCharAsync(AsyncWriter<T> self, Task waitFor, ReadOnlyMemory<char> colMem, CancellationToken cancel)
            {
                await waitFor;

                var start = 0;
                var end = Utils.FindChar(colMem, start, self.Config.EscapedValueStartAndStop);
                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    await self.PlaceInStagingAsync(toWrite, cancel);

                    // place the escape char
                    await self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    await self.PlaceInStagingAsync(toWrite, cancel);
                }

                // end with the escape char
                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }

            // waits for a write to finish, then complete the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlyMemory<char> colMem, int end, CancellationToken cancel)
            {
                await waitFor;

                await self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);

                var start = end;
                end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    await self.PlaceInStagingAsync(toWrite, cancel);

                    // place the escape char
                    await self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    await self.PlaceInStagingAsync(toWrite, cancel);
                }

                // end with the escape char
                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }

            // waits for an escape to finish, then completes the rest of the while loop and method async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterEscapeAsync(AsyncWriter<T> self, Task waitFor, ReadOnlyMemory<char> colMem, int end, CancellationToken cancel)
            {
                await waitFor;

                var start = end;
                end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);

                while (end != -1)
                {
                    var len = end - start;
                    var toWrite = colMem.Slice(start, len);

                    await self.PlaceInStagingAsync(toWrite, cancel);

                    // place the escape char
                    await self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);

                    start = end;
                    end = Utils.FindChar(colMem, start + 1, self.Config.EscapedValueStartAndStop);
                }

                // copy the last bit
                if (start != colMem.Length)
                {
                    var toWrite = colMem.Slice(start);

                    await self.PlaceInStagingAsync(toWrite, cancel);
                }

                // end with the escape char
                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }

            // waits for a write to finish, then writes out the final char and maybe flushes async
            static async ValueTask WriteSingleHeaderAsync_CompleteAfterLastWriteAsync(AsyncWriter<T> self, ValueTask waitFor, CancellationToken cancel)
            {
                await waitFor;

                // end with the escape char
                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }
        }

        public override ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                if (IsFirstRow)
                {
                    var headersTask = CheckHeadersAsync(CancellationToken.None);
                    if (!headersTask.IsCompletedSuccessfully)
                    {
                        return DisposeAsync_ContinueAfterHeadersAsync(this, headersTask);
                    }
                }

                if (Config.WriteTrailingNewLine == WriteTrailingNewLines.Always)
                {
                    var endRecordTask = EndRecordAsync(CancellationToken.None);
                    if (!endRecordTask.IsCompletedSuccessfully)
                    {
                        return DisposeAsync_ContinueAfterEndRecordAsync(this, endRecordTask);
                    }
                }

                if (HasBuffer)
                {
                    if (InStaging > 0)
                    {
                        var flushTask = FlushStagingAsync(CancellationToken.None);
                        if (!flushTask.IsCompletedSuccessfully)
                        {
                            return DisposeAsync_ContinueAfterFlushAsync(this, flushTask);
                        }
                    }

                    Staging.Dispose();
                }

                var ret = Inner.DisposeAsync();

                OneCharOwner?.Dispose();
                Buffer.Dispose();
                Inner = null;

                return ret;
            }

            return default;

            // wait on headers, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterHeadersAsync(AsyncWriter<T> self, ValueTask<bool> waitFor)
            {
                await waitFor;

                if (self.Config.WriteTrailingNewLine == WriteTrailingNewLines.Always)
                {
                    await self.EndRecordAsync(CancellationToken.None);
                }

                if (self.HasBuffer)
                {
                    if (self.InStaging > 0)
                    {
                        await self.FlushStagingAsync(CancellationToken.None);
                    }

                    self.Staging.Dispose();
                }

                await self.Inner.DisposeAsync();
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
                        await self.FlushStagingAsync(CancellationToken.None);
                    }

                    self.Staging.Dispose();
                }

                await self.Inner.DisposeAsync();
                self.OneCharOwner?.Dispose();
                self.Buffer.Dispose();

                self.Inner = null;
            }

            // wait on flush, then continue asynchronously
            static async ValueTask DisposeAsync_ContinueAfterFlushAsync(AsyncWriter<T> self, Task waitFor)
            {
                await waitFor;

                self.Staging?.Dispose();

                await self.Inner.DisposeAsync();
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
