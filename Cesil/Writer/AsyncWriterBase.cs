using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal abstract partial class AsyncWriterBase<T> :
        WriterBase<T>,
        IAsyncWriter<T>,
        ITestableAsyncDisposable
    {
        public bool IsDisposed { get; internal set; }

        internal readonly IAsyncWriterAdapter Inner;

        internal NonNull<IMemoryOwner<char>> OneCharOwner;

        private Memory<char> OneCharMemory;

        internal AsyncWriterBase(BoundConfigurationBase<T> config, IAsyncWriterAdapter inner, object? context) : base(config, context)
        {
            Inner = inner;
        }

        public ValueTask WriteAllAsync(IEnumerable<T> rows, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);

            Utils.CheckArgumentNull(rows, nameof(rows));

            var e = rows.GetEnumerator();
            var disposeE = true;
            try
            {
                while (e.MoveNext())
                {
                    var row = e.Current;
                    var writeTask = WriteAsync(row, cancel);
                    if (!writeTask.IsCompletedSuccessfully(this))
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
                    cancel.ThrowIfCancellationRequested();

                    while (e.MoveNext())
                    {
                        var row = e.Current;
                        var writeAsyncTask = self.WriteAsync(row, cancel);

                        await writeAsyncTask;
                        cancel.ThrowIfCancellationRequested();
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
            AssertNotDisposed(this);

            Utils.CheckArgumentNull(rows, nameof(rows));

            ValueTask ret;

            var e = rows.GetAsyncEnumerator(cancel);
            var disposeE = true;
            try
            {
                while (true)
                {
                    var nextTask = e.MoveNextAsync();
                    if (!nextTask.IsCompletedSuccessfully(this))
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
                    if (!writeTask.IsCompletedSuccessfully(this))
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
                    if (!disposeTask.IsCompletedSuccessfully(this))
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
                    cancel.ThrowIfCancellationRequested();

                    if (!res) return;

                    var row = e.Current;
                    var writeAsyncTask = self.WriteAsync(row, cancel);
                    await writeAsyncTask;
                    cancel.ThrowIfCancellationRequested();

                    while (await e.MoveNextAsync())
                    {
                        row = e.Current;
                        var secondWriteTask = self.WriteAsync(row, cancel);
                        await secondWriteTask;
                        cancel.ThrowIfCancellationRequested();
                    }
                }
                finally
                {
                    var disposeTask = e.DisposeAsync();
                    await disposeTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }

            // wait for a write to complete, then continue asynchronously
            static async ValueTask WriteAllAsync_ContinueAfterWriteAsync(AsyncWriterBase<T> self, ValueTask waitFor, IAsyncEnumerator<T> e, CancellationToken cancel)
            {
                try
                {
                    await waitFor;
                    cancel.ThrowIfCancellationRequested();

                    while (await e.MoveNextAsync())
                    {
                        var row = e.Current;
                        var writeTask = self.WriteAsync(row, cancel);
                        await writeTask;
                        cancel.ThrowIfCancellationRequested();
                    }
                }
                finally
                {
                    var disposeTask = e.DisposeAsync();
                    await disposeTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }
        }

        internal ValueTask EndRecordAsync(CancellationToken cancel)
        {
            return PlaceInStagingAsync(Config.RowEndingMemory, cancel);
        }

        internal ValueTask PlaceInStagingAsync(ReadOnlyMemory<char> chars, CancellationToken cancel)
        {
            if (!Staging.HasValue)
            {
                return WriteDirectlyAsync(chars, cancel);
            }

            // try and keep this sync, if we can
            var toWrite = chars;
            while (PlaceInStaging(toWrite, out toWrite))
            {
                var flushTask = FlushStagingAsync(cancel);
                if (!flushTask.IsCompletedSuccessfully(this))
                {
                    return PlaceInStagingAsync_FinishAsync(this, flushTask, toWrite, cancel);
                }
            }

            return default;

            // Finish TryPlaceInStagingSync asynchrounsly
            static async ValueTask PlaceInStagingAsync_FinishAsync(AsyncWriterBase<T> self, ValueTask waitFor, ReadOnlyMemory<char> remainingWork, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var nextWrite = remainingWork;
                while (self.PlaceInStaging(nextWrite, out nextWrite))
                {
                    var flushTask = self.FlushStagingAsync(cancel);
                    await flushTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }
        }

        internal ValueTask WriteDirectlyAsync(ReadOnlyMemory<char> chars, CancellationToken cancel)
        {
            var writeTask = Inner.WriteAsync(chars, cancel);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return writeTask;
            }

            return default;
        }

        internal ValueTask FlushStagingAsync(CancellationToken cancel)
        {
            var toWrite = Staging.Value.Memory.Slice(0, InStaging);
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
                CheckCanEncode(charMem.Span);

                var escapedValueStartAndStop = Utils.NonNullStruct(Config.EscapedValueStartAndStop);

                var startEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                if (!startEscapeTask.IsCompletedSuccessfully(this))
                {
                    return WriteSingleSegmentAsync_CompleteAfterFirstFlushAsync(this, startEscapeTask, escapedValueStartAndStop, charMem, cancel);
                }

                var writeTask = WriteEncodedAsync(charMem, cancel);
                if (!writeTask.IsCompletedSuccessfully(this))
                {
                    return WriteSingleSegmentAsync_CompleteAfterWriteAsync(this, writeTask, escapedValueStartAndStop, cancel);
                }

                var endEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                if (!endEscapeTask.IsCompletedSuccessfully(this))
                {
                    return endEscapeTask;
                }

                return default;
            }

            // complete async after trying to write the first escaped character
            static async ValueTask WriteSingleSegmentAsync_CompleteAfterFirstFlushAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, ReadOnlyMemory<char> charMem, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var writeEncodedTask = self.WriteEncodedAsync(charMem, cancel);
                await writeEncodedTask;
                cancel.ThrowIfCancellationRequested();

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await placeTask;
                cancel.ThrowIfCancellationRequested();
            }

            // complete async after writing the encoded value
            static async ValueTask WriteSingleSegmentAsync_CompleteAfterWriteAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await placeTask;
                cancel.ThrowIfCancellationRequested();
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
                    if (!placeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteMultiSegmentAsync_CompleteAsync(this, placeTask, e, cancel);
                    }
                }

                return default;
            }
            else
            {
                CheckCanEncode(head);

                // we have to encode this value, but let's try to do it in only a couple of
                //    write calls
                return WriteEncodedAsync(head, cancel);
            }

            // waits for the flush task, then continues placing everythign into staging
            static async ValueTask WriteMultiSegmentAsync_CompleteAsync(AsyncWriterBase<T> self, ValueTask waitFor, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                while (e.MoveNext())
                {
                    var c = e.Current;

                    var placeTask = self.PlaceInStagingAsync(c, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }
        }

        internal ValueTask WriteEncodedAsync(ReadOnlySequence<char> head, CancellationToken cancel)
        {
            var escapedValueStartAndStop = Utils.NonNullStruct(Config.EscapedValueStartAndStop);

            // start with whatever the escape is
            var startEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
            if (!startEscapeTask.IsCompletedSuccessfully(this))
            {
                return WriteEncodedAsync_CompleteAfterFirstAsync(this, startEscapeTask, escapedValueStartAndStop, head, cancel);
            }

            var e = head.GetEnumerator();
            while (e.MoveNext())
            {
                var cur = e.Current;
                var writeTask = WriteEncodedAsync(cur, cancel);
                if (!writeTask.IsCompletedSuccessfully(this))
                {
                    return WriteEncodedAsync_CompleteEnumerating(this, writeTask, escapedValueStartAndStop, e, cancel);
                }
            }

            // end with the escape
            var endEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
            if (!endEscapeTask.IsCompletedSuccessfully(this))
            {
                return endEscapeTask;
            }

            return default;

            // wait for the flush, then proceed for after the first char
            static async ValueTask WriteEncodedAsync_CompleteAfterFirstAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, ReadOnlySequence<char> head, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                foreach (var cur in head)
                {
                    var writeTask = self.WriteEncodedAsync(cur, cancel);
                    await writeTask;
                    cancel.ThrowIfCancellationRequested();
                }

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await placeTask;
                cancel.ThrowIfCancellationRequested();
            }

            // wait for the encoded to finish, then proceed with the remaining
            static async ValueTask WriteEncodedAsync_CompleteEnumerating(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, ReadOnlySequence<char>.Enumerator e, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                while (e.MoveNext())
                {
                    var c = e.Current;

                    var writeTask = self.WriteEncodedAsync(c, cancel);
                    await writeTask;
                    cancel.ThrowIfCancellationRequested();
                }

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancel);
                await placeTask;
                cancel.ThrowIfCancellationRequested();
            }
        }

        internal ValueTask WriteEncodedAsync(ReadOnlyMemory<char> charMem, CancellationToken cancel)
        {
            var escapedValueStartAndStop = Utils.NonNullStruct(Config.EscapedValueStartAndStop);


            // try and blit things in in big chunks
            var start = 0;
            var end = Utils.FindChar(charMem, start, escapedValueStartAndStop);

            while (end != -1)
            {
                var escapeValueEscapeChar = Utils.NonNullStruct(Config.EscapeValueEscapeChar);

                var len = end - start;
                var toWrite = charMem.Slice(start, len);

                var writeTask = PlaceInStagingAsync(toWrite, cancel);
                if (!writeTask.IsCompletedSuccessfully(this))
                {
                    return WriteEncodedAsync_CompleteWritesBeforeFlushAsync(this, writeTask, escapedValueStartAndStop, escapeValueEscapeChar, charMem, start, len, cancel);
                }

                var escapeCharTask = PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                if (!escapeCharTask.IsCompletedSuccessfully(this))
                {
                    return WriteEncodedAsync_CompleteWritesAfterFlushAsync(this, escapeCharTask, escapedValueStartAndStop, escapeValueEscapeChar, charMem, start, len, cancel);
                }

                start += len;
                end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);
            }

            if (start != charMem.Length)
            {
                var toWrite = charMem.Slice(start);

                return PlaceInStagingAsync(toWrite, cancel);
            }

            return default;

            // wait for the previous write, then continue the while loop
            static async ValueTask WriteEncodedAsync_CompleteWritesBeforeFlushAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> charMem, int start, int len, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                var placeTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                await placeTask;
                cancel.ThrowIfCancellationRequested();

                start += len;
                var end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    len = end - start;
                    var toWrite = charMem.Slice(start, len);

                    var secondPlaceTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await secondPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    var thirdPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                    await thirdPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    start += len;
                    end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);
                }

                if (start != charMem.Length)
                {
                    var toWrite = charMem.Slice(start);

                    var writeTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await writeTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }

            // wait for a flush, then continue the while loop
            static async ValueTask WriteEncodedAsync_CompleteWritesAfterFlushAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> charMem, int start, int len, CancellationToken cancel)
            {
                await waitFor;
                cancel.ThrowIfCancellationRequested();

                start += len;
                var end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    len = end - start;
                    var toWrite = charMem.Slice(start, len);

                    var placeTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await placeTask;
                    cancel.ThrowIfCancellationRequested();

                    var secondPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancel);
                    await secondPlaceTask;
                    cancel.ThrowIfCancellationRequested();

                    start += len;
                    end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);
                }

                if (start != charMem.Length)
                {
                    var toWrite = charMem.Slice(start);

                    var writeTask = self.PlaceInStagingAsync(toWrite, cancel);
                    await writeTask;
                    cancel.ThrowIfCancellationRequested();
                }
            }
        }

        // returns true if we need to flush stating, sets remaing to what wasn't placed in staging
        internal bool PlaceInStaging(ReadOnlyMemory<char> c, out ReadOnlyMemory<char> remaining)
        {
            var stagingMem = Staging.Value.Memory;

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

        internal ValueTask PlaceCharInStagingAsync(char c, CancellationToken cancel)
        {
            if (!Staging.HasValue)
            {
                return WriteCharDirectlyAsync(c, cancel);
            }

            if (PlaceInStaging(c))
            {
                return FlushStagingAsync(cancel);
            }

            return default;
        }

        internal ValueTask WriteCharDirectlyAsync(char c, CancellationToken cancel)
        {
            if (!OneCharOwner.HasValue)
            {
                OneCharOwner.Value = Config.MemoryPool.Rent(1);
                OneCharMemory = OneCharOwner.Value.Memory.Slice(0, 1);
            }

            OneCharMemory.Span[0] = c;
            var writeTask = Inner.WriteAsync(OneCharMemory, cancel);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return writeTask;
            }

            return default;
        }

        public abstract ValueTask WriteAsync(T row, CancellationToken cancel = default);

        public abstract ValueTask WriteCommentAsync(string comment, CancellationToken cancel = default);

        public abstract ValueTask DisposeAsync();
    }

#if DEBUG
    // this is only implemented in DEBUG builds, so tests (and only tests) can force
    //    particular async paths
    internal abstract partial class AsyncWriterBase<T> : ITestableAsyncProvider
    {
        private int _GoAsyncAfter;
        int ITestableAsyncProvider.GoAsyncAfter { set { _GoAsyncAfter = value; } }

        private int _AsyncCounter;
        int ITestableAsyncProvider.AsyncCounter => _AsyncCounter;

        bool ITestableAsyncProvider.ShouldGoAsync()
        {
            lock (this)
            {
                _AsyncCounter++;

                var ret = _AsyncCounter >= _GoAsyncAfter;

                return ret;
            }
        }
    }
#endif
}
