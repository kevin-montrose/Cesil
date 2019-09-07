using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal abstract class AsyncWriterBase<T> :
        WriterBase<T>,
        IAsyncWriter<T>,
        ITestableAsyncDisposable
    {
        public abstract bool IsDisposed { get; }

        internal TextWriter Inner;
        internal IMemoryOwner<char> OneCharOwner;
        private Memory<char> OneCharMemory;

        internal AsyncWriterBase(BoundConfigurationBase<T> config, TextWriter inner, object context) : base(config, context)
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
                while (e.MoveNext())
                {
                    var row = e.Current;
                    var writeTask = WriteAsync(row, cancel);

                    if (!writeTask.IsCompletedSuccessfully)
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
            static async ValueTask WriteAllAsync_CompleteAsync(AsyncWriterBase<T> self, ValueTask waitFor, IEnumerator<T> e, CancellationToken cancel)
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
            static async ValueTask WriteAllAsync_ContinueAfterNextAsync(AsyncWriterBase<T> self, ValueTask<bool> waitFor, IAsyncEnumerator<T> e, CancellationToken cancel)
            {
                try
                {
                    var res = await waitFor;
                    if (!res) return;

                    var row = e.Current;
                    await self.WriteAsync(row, cancel);

                    while (await e.MoveNextAsync())
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
            static async ValueTask WriteAllAsync_ContinueAfterWriteAsync(AsyncWriterBase<T> self, ValueTask waitFor, IAsyncEnumerator<T> e, CancellationToken cancel)
            {
                try
                {
                    await waitFor;

                    while (await e.MoveNextAsync())
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

        internal ValueTask EndRecordAsync(CancellationToken cancel)
        {
            return PlaceInStagingAsync(Config.RowEndingMemory, cancel);
        }

        internal ValueTask PlaceInStagingAsync(ReadOnlyMemory<char> chars, CancellationToken cancel)
        {
            if (!HasBuffer)
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
            static async ValueTask PlaceInStagingAsync_FinishAsync(AsyncWriterBase<T> self, Task waitFor, ReadOnlyMemory<char> remainingWork, CancellationToken subCancel)
            {
                await waitFor;

                var nextWrite = remainingWork;
                while (self.PlaceInStaging(nextWrite, out nextWrite))
                {
                    await self.FlushStagingAsync(subCancel);
                }
            }
        }

        internal ValueTask WriteDirectlyAsync(ReadOnlyMemory<char> chars, CancellationToken cancel)
        {
            var writeTask = Inner.WriteAsync(chars, cancel);
            if (!writeTask.IsCompletedSuccessfully)
            {
                return new ValueTask(writeTask);
            }

            return default;
        }

        internal Task FlushStagingAsync(CancellationToken cancel)
        {
            var toWrite = Staging.Memory.Slice(0, InStaging);
            InStaging = 0;

            return Inner.WriteAsync(toWrite, cancel);
        }

        internal ValueTask WriteValueAsync(ReadOnlySequence<char> buffer, CancellationToken cancel)
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

        internal ValueTask WriteSingleSegmentAsync(ReadOnlyMemory<char> charMem, CancellationToken cancel)
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
            static async ValueTask WriteSingleSegmentAsync_CompleteAfterFirstFlushAsync(AsyncWriterBase<T> self, Task waitFor, ReadOnlyMemory<char> charMem, CancellationToken cancel)
            {
                await waitFor;

                await self.WriteEncodedAsync(charMem, cancel);

                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }

            // complete async after writing the encoded value
            static async ValueTask WriteSingleSegmentAsync_CompleteAfterWriteAsync(AsyncWriterBase<T> self, ValueTask waitFor, CancellationToken cancel)
            {
                await waitFor;

                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }
        }

        internal ValueTask WriteMultiSegmentAsync(ReadOnlySequence<char> head, CancellationToken cancel)
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
            static async ValueTask WriteMultiSegmentAsync_CompleteAsync(AsyncWriterBase<T> self, ValueTask waitFor, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;

                while (e.MoveNext())
                {
                    var c = e.Current;

                    await self.PlaceInStagingAsync(c, cancel);
                }
            }
        }

        internal ValueTask WriteEncodedAsync(ReadOnlySequence<char> head, CancellationToken cancel)
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
            if (!endEscapeTask.IsCompletedSuccessfully)
            {
                return new ValueTask(endEscapeTask);
            }

            return default;

            // wait for the flush, then proceed for after the first char
            static async ValueTask WriteEncodedAsync_CompleteAfterFirstAsync(AsyncWriterBase<T> self, Task waitFor, ReadOnlySequence<char> head, CancellationToken cancel)
            {
                await waitFor;

                foreach (var cur in head)
                {
                    await self.WriteEncodedAsync(cur, cancel);
                }

                await self.PlaceCharInStagingAsync(self.Config.EscapedValueStartAndStop, cancel);
            }

            // wait for the encoded to finish, then proceed with the remaining
            static async ValueTask WriteEncodedAsync_CompleteEnumerating(AsyncWriterBase<T> self, ValueTask waitFor, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
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

        internal ValueTask WriteEncodedAsync(ReadOnlyMemory<char> charMem, CancellationToken cancel)
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
            static async ValueTask WriteEncodedAsync_CompleteWritesBeforeFlushAsync(AsyncWriterBase<T> self, ValueTask waitFor, ReadOnlyMemory<char> charMem, int start, int len, CancellationToken cancel)
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
            static async ValueTask WriteEncodedAsync_CompleteWritesAfterFlushAsync(AsyncWriterBase<T> self, Task waitFor, ReadOnlyMemory<char> charMem, int start, int len, CancellationToken cancel)
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

        // returns true if we need to flush stating, sets remaing to what wasn't placed in staging
        internal bool PlaceInStaging(ReadOnlyMemory<char> c, out ReadOnlyMemory<char> remaining)
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

        internal Task PlaceCharInStagingAsync(char c, CancellationToken cancel)
        {
            if (!HasBuffer)
            {
                return WriteCharDirectlyAsync(c, cancel);
            }

            if (PlaceInStaging(c))
            {
                return FlushStagingAsync(cancel);
            }

            return Task.CompletedTask;
        }

        internal Task WriteCharDirectlyAsync(char c, CancellationToken cancel)
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

        public abstract ValueTask WriteAsync(T row, CancellationToken cancel = default);

        public abstract ValueTask WriteCommentAsync(string comment, CancellationToken cancel = default);

        public abstract ValueTask DisposeAsync();

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                var name = this.GetType().Name;

                Throw.ObjectDisposedException(name);
            }
        }
    }
}
