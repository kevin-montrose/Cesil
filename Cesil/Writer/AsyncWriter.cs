using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class AsyncWriter<T> :
        WriterBase<T>,
        IAsyncWriter<T>,
        ITestableAsyncDisposable
    {
        public bool IsDisposed => Inner == null;

        private TextWriter Inner;

        private IMemoryOwner<char> OneCharOwner;
        private Memory<char> OneCharMemory;

        internal AsyncWriter(ConcreteBoundConfiguration<T> config, TextWriter inner, object context) : base(config, context)
        {
            Inner = inner;
        }

        public ValueTask WriteAllAsync(IEnumerable<T> rows, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            if (rows == null)
            {
                Throw.ArgumentNullException(nameof(rows));
            }

            var e = rows.GetEnumerator();
            var disposeE = true;
            try
            {
                while(e.MoveNext())
                {
                    var row = e.Current;
                    var writeTask = WriteAsync(row, cancel);

                    if(!writeTask.IsCompletedSuccessfully)
                    {
                        disposeE = false;
                        return WriteAllAsync_CompleteAsync(this, writeTask, e, cancel);
                    }
                }
            }
            finally
            {
                if (disposeE)
                {
                    e.Dispose();
                }
            }

            return default;

            // waits for write to finish, then completes asynchronously
            static async ValueTask WriteAllAsync_CompleteAsync(AsyncWriter<T> self, ValueTask waitFor, IEnumerator<T> e, CancellationToken cancel)
            {
                try
                {
                    await waitFor;

                    while (e.MoveNext())
                    {
                        var row = e.Current;
                        await self.WriteAsync(row, cancel);
                    }
                }
                finally
                {
                    e.Dispose();
                }
            }
        }

        public ValueTask WriteAllAsync(IAsyncEnumerable<T> rows, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            if (rows == null)
            {
                Throw.ArgumentNullException(nameof(rows));
            }

            ValueTask ret;

            var e = rows.GetAsyncEnumerator(cancel);
            var disposeE = true;
            try
            {
                while (true)
                {
                    var nextTask = e.MoveNextAsync();
                    if (!nextTask.IsCompletedSuccessfully)
                    {
                        disposeE = false;
                        return WriteAllAsync_ContinueAfterNextAsync(this, nextTask, e, cancel);
                    }

                    var res = nextTask.Result;
                    if (!res)
                    {
                        // end the loop
                        break;
                    }

                    var row = e.Current;
                    var writeTask = WriteAsync(row, cancel);
                    if (!writeTask.IsCompletedSuccessfully)
                    {
                        disposeE = false;
                        return WriteAllAsync_ContinueAfterWriteAsync(this, writeTask, e, cancel);
                    }
                }
            }
            finally
            {
                if (disposeE)
                {
                    var disposeTask = e.DisposeAsync();
                    if (!disposeTask.IsCompletedSuccessfully)
                    {
                        ret = disposeTask;
                    }
                    else
                    {
                        ret = default;
                    }
                }
                else
                {
                    ret = default;
                }
            }

            return ret;

            // wait for a move next to complete, then continue asynchronously
            static async ValueTask WriteAllAsync_ContinueAfterNextAsync(AsyncWriter<T> self, ValueTask<bool> waitFor, IAsyncEnumerator<T> e, CancellationToken cancel)
            {
                try
                {
                    var res = await waitFor;
                    if (!res) return;

                    var row = e.Current;
                    await self.WriteAsync(row, cancel);

                    while(await e.MoveNextAsync())
                    {
                        row = e.Current;
                        await self.WriteAsync(row, cancel);
                    }
                }
                finally
                {
                    await e.DisposeAsync();
                }
            }

            // wait for a write to complete, then continue asynchronously
            static async ValueTask WriteAllAsync_ContinueAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, IAsyncEnumerator<T> e, CancellationToken cancel)
            {
                try
                {
                    await waitFor;

                    while(await e.MoveNextAsync())
                    {
                        var row = e.Current;
                        await self.WriteAsync(row, cancel);
                    }
                }
                finally
                {
                    await e.DisposeAsync();
                }
            }
        }

        public ValueTask WriteAsync(T row, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            var shouldEndRecord = true;

            if (IsFirstRow)
            {
                var headersTask = CheckHeadersAsync(cancel);
                if (!headersTask.IsCompletedSuccessfully)
                {
                    return WriteAsync_ContinueAfterHeadersAsync(this, headersTask, row, cancel);
                }

                if (!headersTask.Result)
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                var endRecordTask = EndRecordAsync(cancel);
                if (!endRecordTask.IsCompletedSuccessfully)
                {
                    return WriteAsync_ContinueAfterEndRecordAsync(this, endRecordTask, row, cancel);
                }
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

            // wait for the headers to write, then continue async
            static async ValueTask WriteAsync_ContinueAfterHeadersAsync(AsyncWriter<T> self, ValueTask<bool> waitFor, T row, CancellationToken cancel)
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

                for (var i = 0; i < self.Columns.Length; i++)
                {
                    var needsSeparator = i != 0;
                    var col = self.Columns[i];

                    await self.WriteColumnAsync(row, i, col, needsSeparator, cancel);
                }

                self.RowNumber++;
            }

            // wait for the record to end, then continue async
            static async ValueTask WriteAsync_ContinueAfterEndRecordAsync(AsyncWriter<T> self, ValueTask waitFor, T row, CancellationToken cancel)
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

            var ctx = new WriteContext(RowNumber, colIx, col.Name, Context);

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

                var ctx = new WriteContext(self.RowNumber, colIx, col.Name, self.Context);

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

        private ValueTask EndRecordAsync(CancellationToken cancel)
        {
            return PlaceInStagingAsync(Config.RowEndingMemory, cancel);
        }

        private ValueTask WriteValueAsync(ReadOnlySequence<char> buffer, CancellationToken cancel)
        {
            if (buffer.IsSingleSegment)
            {
                return WriteSingleSegmentAsync(buffer.First, cancel);
            }
            else
            {
                return WriteMultiSegmentAsync(buffer, cancel);
            }
        }

        private ValueTask WriteSingleSegmentAsync(ReadOnlyMemory<char> charMem, CancellationToken cancel)
        {
            if (!NeedsEncode(charMem))
            {
                return PlaceInStagingAsync(charMem, cancel);
            }
            else
            {
                var startEscapeTask = PlaceCharInStagingAsync(Config.EscapedValueStartAndStop, cancel);
                if (!startEscapeTask.IsCompletedSuccessfully)
                {
                    return WriteSingleSegmentAsync_CompleteAfterFirstFlushAsync(this, startEscapeTask, charMem, cancel);
                }

                var writeTask = WriteEncodedAsync(charMem, cancel);
                if (!writeTask.IsCompleted)
                {
                    return WriteSingleSegmentAsync_CompleteAfterWriteAsync(this, writeTask, cancel);
                }

                var endEscapeTask = PlaceCharInStagingAsync(Config.EscapedValueStartAndStop, cancel);
                if (!endEscapeTask.IsCompletedSuccessfully)
                {
                    return new ValueTask(endEscapeTask);
                }

                return default;
            }

            // complete async after trying to write the first escaped character
            static async ValueTask WriteSingleSegmentAsync_CompleteAfterFirstFlushAsync(AsyncWriter<T> self, Task waitFor, ReadOnlyMemory<char> charMem, CancellationToken cancel)
            {
                await waitFor;

                await self.WriteEncodedAsync(charMem, cancel);

                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }

            // complete async after writing the encoded value
            static async ValueTask WriteSingleSegmentAsync_CompleteAfterWriteAsync(AsyncWriter<T> self, ValueTask waitFor, CancellationToken cancel)
            {
                await waitFor;

                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }
        }

        private ValueTask WriteMultiSegmentAsync(ReadOnlySequence<char> head, CancellationToken cancel)
        {
            if (!NeedsEncode(head))
            {
                // no encoding, so just blit each segment into the writer

                var e = head.GetEnumerator();

                while (e.MoveNext())
                {
                    var charMem = e.Current;

                    var write = charMem;

                    var placeTask = PlaceInStagingAsync(write, cancel);

                    if (!placeTask.IsCompletedSuccessfully)
                    {
                        return WriteMultiSegmentAsync_CompleteAsync(this, placeTask, e, cancel);
                    }
                }

                return default;
            }
            else
            {
                // we have to encode this value, but let's try to do it in only a couple of
                //    write calls
                return WriteEncodedAsync(head, cancel);
            }

            // waits for the flush task, then continues placing everythign into staging
            static async ValueTask WriteMultiSegmentAsync_CompleteAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                while (e.MoveNext())
                {
                    var c = e.Current;

                    await self.PlaceInStagingAsync(c, cancel);
                }
            }
        }

        private ValueTask WriteEncodedAsync(ReadOnlySequence<char> head, CancellationToken cancel)
        {
            // start with whatever the escape is
            var startEscapeTask = PlaceCharInStagingAsync(Config.EscapedValueStartAndStop, cancel);
            if (!startEscapeTask.IsCompletedSuccessfully)
            {
                return WriteEncodedAsync_CompleteAfterFirstAsync(this, startEscapeTask, head, cancel);
            }

            var e = head.GetEnumerator();
            while (e.MoveNext())
            {
                var cur = e.Current;
                var writeTask = WriteEncodedAsync(cur, cancel);

                if (!writeTask.IsCompletedSuccessfully)
                {
                    return WriteEncodedAsync_CompleteEnumerating(this, writeTask, e, cancel);
                }
            }

            // end with the escape
            var endEscapeTask = PlaceCharInStagingAsync(Config.EscapedValueStartAndStop, cancel);
            if(!endEscapeTask.IsCompletedSuccessfully)
            {
                return new ValueTask(endEscapeTask);
            }

            return default;

            // wait for the flush, then proceed for after the first char
            static async ValueTask WriteEncodedAsync_CompleteAfterFirstAsync(AsyncWriter<T> self, Task waitFor, ReadOnlySequence<char> head, CancellationToken cancel)
            {
                await waitFor;

                foreach (var cur in head)
                {
                    await self.WriteEncodedAsync(cur, cancel);
                }

                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }

            // wait for the encoded to finish, then proceed with the remaining
            static async ValueTask WriteEncodedAsync_CompleteEnumerating(AsyncWriter<T> self, ValueTask waitFor, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                while (e.MoveNext())
                {
                    var c = e.Current;

                    await self.WriteEncodedAsync(c, cancel);
                }

                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }
        }

        private ValueTask WriteEncodedAsync(ReadOnlyMemory<char> charMem, CancellationToken cancel)
        {
            // try and blit things in in big chunks
            var start = 0;
            var end = Utils.FindChar(charMem, start, Config.EscapedValueStartAndStop);

            while (end != -1)
            {
                var len = end - start;
                var toWrite = charMem.Slice(start, len);

                var writeTask = PlaceInStagingAsync(toWrite, cancel);

                if (!writeTask.IsCompletedSuccessfully)
                {
                    return WriteEncodedAsync_CompleteWritesBeforeFlushAsync(this, writeTask, charMem, start, len, cancel);
                }

                var escapeCharTask = PlaceCharInStagingAsync(Config.EscapeValueEscapeChar, cancel);
                if (!escapeCharTask.IsCompletedSuccessfully)
                {
                    return WriteEncodedAsync_CompleteWritesAfterFlushAsync(this, escapeCharTask, charMem, start, len, cancel);
                }

                start += len;
                end = Utils.FindChar(charMem, start + 1, Config.EscapedValueStartAndStop);
            }

            if (start != charMem.Length)
            {
                var toWrite = charMem.Slice(start);

                return PlaceInStagingAsync(toWrite, cancel);
            }

            return default;

            // wait for the previous write, then continue the while loop
            static async ValueTask WriteEncodedAsync_CompleteWritesBeforeFlushAsync(AsyncWriter<T> self, ValueTask waitFor, ReadOnlyMemory<char> charMem, int start, int len, CancellationToken cancel)
            {
                await waitFor;

                await self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);

                start += len;
                var end = Utils.FindChar(charMem, start + 1, self.Config.EscapedValueStartAndStop);

                while (end != -1)
                {
                    len = end - start;
                    var toWrite = charMem.Slice(start, len);

                    await self.PlaceInStagingAsync(toWrite, cancel);

                    await self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);

                    start += len;
                    end = Utils.FindChar(charMem, start + 1, self.Config.EscapedValueStartAndStop);
                }

                if (start != charMem.Length)
                {
                    var toWrite = charMem.Slice(start);

                    await self.PlaceInStagingAsync(toWrite, cancel);
                }
            }

            // wait for a flush, then continue the while loop
            static async ValueTask WriteEncodedAsync_CompleteWritesAfterFlushAsync(AsyncWriter<T> self, Task waitFor, ReadOnlyMemory<char> charMem, int start, int len, CancellationToken cancel)
            {
                await waitFor;

                start += len;
                var end = Utils.FindChar(charMem, start + 1, self.Config.EscapedValueStartAndStop);

                while (end != -1)
                {
                    len = end - start;
                    var toWrite = charMem.Slice(start, len);

                    await self.PlaceInStagingAsync(toWrite, cancel);

                    await self.PlaceCharInStagingAsync(self.Config.EscapeValueEscapeChar, cancel);

                    start += len;
                    end = Utils.FindChar(charMem, start + 1, self.Config.EscapedValueStartAndStop);
                }

                if (start != charMem.Length)
                {
                    var toWrite = charMem.Slice(start);

                    await self.PlaceInStagingAsync(toWrite, cancel);
                }
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

        private ValueTask PlaceInStagingAsync(ReadOnlyMemory<char> chars, CancellationToken cancel)
        {
            if(!HasBuffer)
            {
                return WriteDirectlyAsync(chars, cancel);
            }

            // try and keep this sync, if we can
            var toWrite = chars;
            while (PlaceInStaging(toWrite, out toWrite))
            {
                var flushTask = FlushStagingAsync(cancel);
                if (!flushTask.IsCompletedSuccessfully)
                {
                    return PlaceInStagingAsync_FinishAsync(this, flushTask, toWrite, cancel);
                }
            }

            return default;

            // Finish TryPlaceInStagingSync asynchrounsly
            static async ValueTask PlaceInStagingAsync_FinishAsync(AsyncWriter<T> self, Task waitFor, ReadOnlyMemory<char> remainingWork, CancellationToken subCancel)
            {
                await waitFor;

                var nextWrite = remainingWork;
                while (self.PlaceInStaging(nextWrite, out nextWrite))
                {
                    await self.FlushStagingAsync(subCancel);
                }
            }
        }

        private ValueTask WriteDirectlyAsync(ReadOnlyMemory<char> chars, CancellationToken cancel)
        {
            var writeTask = Inner.WriteAsync(chars, cancel);
            if (!writeTask.IsCompletedSuccessfully)
            {
                return new ValueTask(writeTask);
            }

            return default;
        }

        private Task FlushStagingAsync(CancellationToken cancel)
        {
            var toWrite = Staging.Memory.Slice(0, InStaging);
            InStaging = 0;

            return Inner.WriteAsync(toWrite, cancel);
        }

        // returns true if we need to flush stating, sets remaing to what wasn't placed in staging
        private bool PlaceInStaging(ReadOnlyMemory<char> c, out ReadOnlyMemory<char> remaining)
        {
            var stagingMem = Staging.Memory;

            var ix = 0;
            while (ix < c.Length)
            {
                var leftInC = c.Length - ix;

                var left = Math.Min(leftInC, stagingMem.Length - InStaging);

                var subC = c.Slice(ix, left);
                var subStaging = stagingMem.Slice(InStaging);

                subC.CopyTo(subStaging);

                ix += left;
                InStaging += left;

                if (InStaging == stagingMem.Length)
                {
                    remaining = c.Slice(ix);
                    return true;
                }
            }

            remaining = default;
            return false;
        }

        private Task PlaceCharInStagingAsync(char c, CancellationToken cancel)
        {
            if(!HasBuffer)
            {
                return WriteCharDirectlyAsync(c, cancel);
            }

            if(PlaceInStaging(c))
            {
                return FlushStagingAsync(cancel);
            }

            return Task.CompletedTask;
        }

        private Task WriteCharDirectlyAsync(char c, CancellationToken cancel)
        {
            if (OneCharOwner == null)
            {
                OneCharOwner = Config.MemoryPool.Rent(1);
                OneCharMemory = OneCharOwner.Memory.Slice(0, 1);
            }

            OneCharMemory.Span[0] = c;
            var writeTask = Inner.WriteAsync(OneCharMemory, cancel);
            if (!writeTask.IsCompletedSuccessfully)
            {
                return writeTask;
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
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

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(AsyncWriter<T>));
            }
        }
    }
}
