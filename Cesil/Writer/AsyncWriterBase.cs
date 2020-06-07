using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.AwaitHelper;
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

        public ValueTask<int> WriteAllAsync(IEnumerable<T> rows, CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);

            Utils.CheckArgumentNull(rows, nameof(rows));

            var oldRowNumber = RowNumber;

            var e = rows.GetEnumerator();
            var disposeE = true;
            try
            {
                while (e.MoveNext())
                {
                    var row = e.Current;
                    var writeTask = WriteAsync(row, cancellationToken);
                    if (!writeTask.IsCompletedSuccessfully(this))
                    {
                        disposeE = false;
                        return WriteAllAsync_CompleteAsync(this, writeTask, oldRowNumber, e, cancellationToken);
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

            var ret = RowNumber - oldRowNumber;
            return new ValueTask<int>(ret);

            // waits for write to finish, then completes asynchronously
            static async ValueTask<int> WriteAllAsync_CompleteAsync(AsyncWriterBase<T> self, ValueTask waitFor, int oldRowNumber, IEnumerator<T> e, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    while (e.MoveNext())
                    {
                        var row = e.Current;
                        var writeAsyncTask = self.WriteAsync(row, cancellationToken);

                        await ConfigureCancellableAwait(self, writeAsyncTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);
                    }
                }
                finally
                {
                    e.Dispose();
                }

                var ret = self.RowNumber - oldRowNumber;

                return ret;
            }
        }

        public ValueTask<int> WriteAllAsync(IAsyncEnumerable<T> rows, CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);

            Utils.CheckArgumentNull(rows, nameof(rows));

            ValueTask cleanupTask = default;

            var oldRowNumber = RowNumber;

            var e = rows.GetAsyncEnumerator(cancellationToken);
            var disposeE = true;
            try
            {
                while (true)
                {
                    var nextTask = e.MoveNextAsync();
                    if (!nextTask.IsCompletedSuccessfully(this))
                    {
                        disposeE = false;
                        return WriteAllAsync_ContinueAfterNextAsync(this, nextTask, oldRowNumber, e, cancellationToken);
                    }

                    var res = nextTask.Result;
                    if (!res)
                    {
                        // end the loop
                        break;
                    }

                    var row = e.Current;
                    var writeTask = WriteAsync(row, cancellationToken);
                    if (!writeTask.IsCompletedSuccessfully(this))
                    {
                        disposeE = false;
                        return WriteAllAsync_ContinueAfterWriteAsync(this, writeTask, oldRowNumber, e, cancellationToken);
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
#pragma warning disable IDE0059 // This actually matters, but the compiler likes to say it's unnecessary
                        cleanupTask = disposeTask;
#pragma warning restore IDE0059
                    }
                }
            }

            if (!cleanupTask.IsCompletedSuccessfully(this))
            {
                return WriteAllAsync_ContinueAfterCleanupAsync(this, cleanupTask, oldRowNumber, cancellationToken);
            }

            var ret = RowNumber - oldRowNumber;

            return new ValueTask<int>(ret);

            // wait for a move next to complete, then continue asynchronously
            static async ValueTask<int> WriteAllAsync_ContinueAfterNextAsync(AsyncWriterBase<T> self, ValueTask<bool> waitFor, int oldRowNumber, IAsyncEnumerator<T> e, CancellationToken cancellationToken)
            {
                try
                {
                    var res = await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    if (!res)
                    {
                        var innerRet = self.RowNumber - oldRowNumber;
                        return innerRet;
                    }

                    var row = e.Current;
                    var writeAsyncTask = self.WriteAsync(row, cancellationToken);
                    await ConfigureCancellableAwait(self, writeAsyncTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    while (await ConfigureCancellableAwait(self, e.MoveNextAsync(), cancellationToken))
                    {
                        row = e.Current;
                        var secondWriteTask = self.WriteAsync(row, cancellationToken);
                        await ConfigureCancellableAwait(self, secondWriteTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);
                    }
                }
                finally
                {
                    var disposeTask = e.DisposeAsync();
                    await ConfigureCancellableAwait(self, disposeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }

                var ret = self.RowNumber - oldRowNumber;

                return ret;
            }

            // wait for a write to complete, then continue asynchronously
            static async ValueTask<int> WriteAllAsync_ContinueAfterWriteAsync(AsyncWriterBase<T> self, ValueTask waitFor, int oldRowNumber, IAsyncEnumerator<T> e, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    while (await ConfigureCancellableAwait(self, e.MoveNextAsync(), cancellationToken))
                    {
                        var row = e.Current;
                        var writeTask = self.WriteAsync(row, cancellationToken);
                        await ConfigureCancellableAwait(self, writeTask, cancellationToken);
                        CheckCancellation(self, cancellationToken);
                    }
                }
                finally
                {
                    var disposeTask = e.DisposeAsync();
                    await ConfigureCancellableAwait(self, disposeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }

                var ret = self.RowNumber - oldRowNumber;

                return ret;
            }

            // wait for the given task to finish, then return a row count
            static async ValueTask<int> WriteAllAsync_ContinueAfterCleanupAsync(AsyncWriterBase<T> self, ValueTask waitFor, int oldRowNumber, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                var ret = self.RowNumber - oldRowNumber;

                return ret;
            }
        }

        internal ValueTask EndRecordAsync(CancellationToken cancellationToken)
        {
            return PlaceInStagingAsync(Configuration.RowEndingMemory, cancellationToken);
        }

        internal ValueTask PlaceInStagingAsync(ReadOnlyMemory<char> chars, CancellationToken cancellationToken)
        {
            if (!HasStaging)
            {
                return WriteDirectlyAsync(chars, cancellationToken);
            }

            // try and keep this sync, if we can
            var toWrite = chars;
            while (PlaceInStaging(toWrite, out toWrite))
            {
                var flushTask = FlushStagingAsync(cancellationToken);
                if (!flushTask.IsCompletedSuccessfully(this))
                {
                    return PlaceInStagingAsync_FinishAsync(this, flushTask, toWrite, cancellationToken);
                }
            }

            return default;

            // Finish TryPlaceInStagingSync asynchronously
            static async ValueTask PlaceInStagingAsync_FinishAsync(AsyncWriterBase<T> self, ValueTask waitFor, ReadOnlyMemory<char> remainingWork, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                var nextWrite = remainingWork;
                while (self.PlaceInStaging(nextWrite, out nextWrite))
                {
                    var flushTask = self.FlushStagingAsync(cancellationToken);
                    await ConfigureCancellableAwait(self, flushTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }
            }
        }

        internal ValueTask WriteDirectlyAsync(ReadOnlyMemory<char> chars, CancellationToken cancellationToken)
        {
            var writeTask = Inner.WriteAsync(chars, cancellationToken);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return writeTask;
            }

            return default;
        }

        internal ValueTask FlushStagingAsync(CancellationToken cancellationToken)
        {
            var toWrite = StagingMemory[0..InStaging];
            InStaging = 0;

            return Inner.WriteAsync(toWrite, cancellationToken);
        }

        internal ValueTask WriteValueAsync(ReadOnlySequence<char> buffer, CancellationToken cancellationToken)
        {
            if (buffer.IsSingleSegment)
            {
                return WriteSingleSegmentAsync(buffer.First, cancellationToken);
            }
            else
            {
                return WriteMultiSegmentAsync(buffer, cancellationToken);
            }
        }

        internal ValueTask WriteSingleSegmentAsync(ReadOnlyMemory<char> charMem, CancellationToken cancellationToken)
        {
            if (!NeedsEncode(charMem))
            {
                return PlaceInStagingAsync(charMem, cancellationToken);
            }
            else
            {
                var options = Configuration.Options;

                CheckCanEncode(charMem.Span, options);

                var escapedValueStartAndStop = Utils.NonNullValue(options.EscapedValueStartAndEnd);

                var startEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                if (!startEscapeTask.IsCompletedSuccessfully(this))
                {
                    return WriteSingleSegmentAsync_CompleteAfterFirstFlushAsync(this, startEscapeTask, escapedValueStartAndStop, charMem, cancellationToken);
                }

                var writeTask = WriteEncodedAsync(charMem, cancellationToken);
                if (!writeTask.IsCompletedSuccessfully(this))
                {
                    return WriteSingleSegmentAsync_CompleteAfterWriteAsync(this, writeTask, escapedValueStartAndStop, cancellationToken);
                }

                var endEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                if (!endEscapeTask.IsCompletedSuccessfully(this))
                {
                    return endEscapeTask;
                }

                return default;
            }

            // complete async after trying to write the first escaped character
            static async ValueTask WriteSingleSegmentAsync_CompleteAfterFirstFlushAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, ReadOnlyMemory<char> charMem, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                var writeEncodedTask = self.WriteEncodedAsync(charMem, cancellationToken);
                await ConfigureCancellableAwait(self, writeEncodedTask, cancellationToken);
                CheckCancellation(self, cancellationToken);

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                CheckCancellation(self, cancellationToken);
            }

            // complete async after writing the encoded value
            static async ValueTask WriteSingleSegmentAsync_CompleteAfterWriteAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                CheckCancellation(self, cancellationToken);
            }
        }

        internal ValueTask WriteMultiSegmentAsync(ReadOnlySequence<char> head, CancellationToken cancellationToken)
        {
            if (!NeedsEncode(head))
            {
                // no encoding, so just blit each segment into the writer

                var e = head.GetEnumerator();

                while (e.MoveNext())
                {
                    var charMem = e.Current;

                    var write = charMem;

                    var placeTask = PlaceInStagingAsync(write, cancellationToken);
                    if (!placeTask.IsCompletedSuccessfully(this))
                    {
                        return WriteMultiSegmentAsync_CompleteAsync(this, placeTask, e, cancellationToken);
                    }
                }

                return default;
            }
            else
            {
                CheckCanEncode(head, Configuration.Options);

                // we have to encode this value, but let's try to do it in only a couple of
                //    write calls
                return WriteEncodedAsync(head, cancellationToken);
            }

            // waits for the flush task, then continues placing everything into staging
            static async ValueTask WriteMultiSegmentAsync_CompleteAsync(AsyncWriterBase<T> self, ValueTask waitFor, ReadOnlySequence<char>.Enumerator e, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                while (e.MoveNext())
                {
                    var c = e.Current;

                    var placeTask = self.PlaceInStagingAsync(c, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }
            }
        }

        internal ValueTask WriteEncodedAsync(ReadOnlySequence<char> head, CancellationToken cancellationToken)
        {
            var escapedValueStartAndStop = Utils.NonNullValue(Configuration.Options.EscapedValueStartAndEnd);

            // start with whatever the escape is
            var startEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
            if (!startEscapeTask.IsCompletedSuccessfully(this))
            {
                return WriteEncodedAsync_CompleteAfterFirstAsync(this, startEscapeTask, escapedValueStartAndStop, head, cancellationToken);
            }

            var e = head.GetEnumerator();
            while (e.MoveNext())
            {
                var cur = e.Current;
                var writeTask = WriteEncodedAsync(cur, cancellationToken);
                if (!writeTask.IsCompletedSuccessfully(this))
                {
                    return WriteEncodedAsync_CompleteEnumerating(this, writeTask, escapedValueStartAndStop, e, cancellationToken);
                }
            }

            // end with the escape
            var endEscapeTask = PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
            if (!endEscapeTask.IsCompletedSuccessfully(this))
            {
                return endEscapeTask;
            }

            return default;

            // wait for the flush, then proceed for after the first char
            static async ValueTask WriteEncodedAsync_CompleteAfterFirstAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, ReadOnlySequence<char> head, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                foreach (var cur in head)
                {
                    var writeTask = self.WriteEncodedAsync(cur, cancellationToken);
                    await ConfigureCancellableAwait(self, writeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                CheckCancellation(self, cancellationToken);
            }

            // wait for the encoded to finish, then proceed with the remaining
            static async ValueTask WriteEncodedAsync_CompleteEnumerating(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, ReadOnlySequence<char>.Enumerator e, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                while (e.MoveNext())
                {
                    var c = e.Current;

                    var writeTask = self.WriteEncodedAsync(c, cancellationToken);
                    await ConfigureCancellableAwait(self, writeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }

                var placeTask = self.PlaceCharInStagingAsync(escapedValueStartAndStop, cancellationToken);
                await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                CheckCancellation(self, cancellationToken);
            }
        }

        internal ValueTask WriteEncodedAsync(ReadOnlyMemory<char> charMem, CancellationToken cancellationToken)
        {
            var escapedValueStartAndStop = Utils.NonNullValue(Configuration.Options.EscapedValueStartAndEnd);


            // try and blit things in big chunks
            var start = 0;
            var end = Utils.FindChar(charMem, start, escapedValueStartAndStop);

            while (end != -1)
            {
                var escapeValueEscapeChar = Utils.NonNullValue(Configuration.Options.EscapedValueEscapeCharacter);

                var len = end - start;
                var toWrite = charMem.Slice(start, len);

                var writeTask = PlaceInStagingAsync(toWrite, cancellationToken);
                if (!writeTask.IsCompletedSuccessfully(this))
                {
                    return WriteEncodedAsync_CompleteWritesBeforeFlushAsync(this, writeTask, escapedValueStartAndStop, escapeValueEscapeChar, charMem, start, len, cancellationToken);
                }

                var escapeCharTask = PlaceCharInStagingAsync(escapeValueEscapeChar, cancellationToken);
                if (!escapeCharTask.IsCompletedSuccessfully(this))
                {
                    return WriteEncodedAsync_CompleteWritesAfterFlushAsync(this, escapeCharTask, escapedValueStartAndStop, escapeValueEscapeChar, charMem, start, len, cancellationToken);
                }

                start += len;
                end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);
            }

            if (start != charMem.Length)
            {
                var toWrite = charMem.Slice(start);

                return PlaceInStagingAsync(toWrite, cancellationToken);
            }

            return default;

            // wait for the previous write, then continue the while loop
            static async ValueTask WriteEncodedAsync_CompleteWritesBeforeFlushAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> charMem, int start, int len, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                var placeTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancellationToken);
                await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                CheckCancellation(self, cancellationToken);

                start += len;
                var end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    len = end - start;
                    var toWrite = charMem.Slice(start, len);

                    var secondPlaceTask = self.PlaceInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    var thirdPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancellationToken);
                    await ConfigureCancellableAwait(self, thirdPlaceTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    start += len;
                    end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);
                }

                if (start != charMem.Length)
                {
                    var toWrite = charMem.Slice(start);

                    var writeTask = self.PlaceInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, writeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }
            }

            // wait for a flush, then continue the while loop
            static async ValueTask WriteEncodedAsync_CompleteWritesAfterFlushAsync(AsyncWriterBase<T> self, ValueTask waitFor, char escapedValueStartAndStop, char escapeValueEscapeChar, ReadOnlyMemory<char> charMem, int start, int len, CancellationToken cancellationToken)
            {
                await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                CheckCancellation(self, cancellationToken);

                start += len;
                var end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);

                while (end != -1)
                {
                    len = end - start;
                    var toWrite = charMem.Slice(start, len);

                    var placeTask = self.PlaceInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, placeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    var secondPlaceTask = self.PlaceCharInStagingAsync(escapeValueEscapeChar, cancellationToken);
                    await ConfigureCancellableAwait(self, secondPlaceTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);

                    start += len;
                    end = Utils.FindChar(charMem, start + 1, escapedValueStartAndStop);
                }

                if (start != charMem.Length)
                {
                    var toWrite = charMem.Slice(start);

                    var writeTask = self.PlaceInStagingAsync(toWrite, cancellationToken);
                    await ConfigureCancellableAwait(self, writeTask, cancellationToken);
                    CheckCancellation(self, cancellationToken);
                }
            }
        }

        // returns true if we need to flush staging, sets remaining to what wasn't placed in staging
        internal bool PlaceInStaging(ReadOnlyMemory<char> c, out ReadOnlyMemory<char> remaining)
        {
            var stagingMem = StagingMemory;
            var stagingLen = stagingMem.Length;

            var left = Math.Min(c.Length, stagingLen - InStaging);

            var subC = c[0..left];
            var subStaging = stagingMem[InStaging..];

            subC.CopyTo(subStaging);

            InStaging += left;

            remaining = c[left..];
            return InStaging == stagingLen;
        }

        internal ValueTask PlaceCharInStagingAsync(char c, CancellationToken cancellationToken)
        {
            if (!HasStaging)
            {
                return WriteCharDirectlyAsync(c, cancellationToken);
            }

            if (PlaceInStaging(c))
            {
                return FlushStagingAsync(cancellationToken);
            }

            return default;
        }

        internal ValueTask WriteCharDirectlyAsync(char c, CancellationToken cancellationToken)
        {
            if (!OneCharOwner.HasValue)
            {
                OneCharOwner.Value = Configuration.Options.MemoryPool.Rent(1);
                OneCharMemory = OneCharOwner.Value.Memory.Slice(0, 1);
            }

            OneCharMemory.Span[0] = c;
            var writeTask = Inner.WriteAsync(OneCharMemory, cancellationToken);
            if (!writeTask.IsCompletedSuccessfully(this))
            {
                return writeTask;
            }

            return default;
        }

        public abstract ValueTask WriteAsync(T row, CancellationToken cancellationToken = default);

        public ValueTask WriteCommentAsync(string comment, CancellationToken cancellationToken = default)
        {
            Utils.CheckArgumentNull(comment, nameof(comment));

            return WriteCommentAsync(comment.AsMemory(), cancellationToken);
        }

        public abstract ValueTask WriteCommentAsync(ReadOnlyMemory<char> comment, CancellationToken cancellationToken = default);

        public abstract ValueTask DisposeAsync();
    }

#if DEBUG
    internal abstract partial class AsyncWriterBase<T> : ITestableCancellableProvider
    {
        int? ITestableCancellableProvider.CancelAfter { get; set; }
        int ITestableCancellableProvider.CancelCounter { get; set; }
    }

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