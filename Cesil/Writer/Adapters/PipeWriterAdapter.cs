using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed partial class PipeWriterAdapter : IAsyncWriterAdapter
    {
        public bool IsDisposed => Writer == null;

        private Encoding Encoding;
        private PipeWriter Writer;
        private MemoryPool<char> MemoryPool;

        private IMemoryOwner<char> BufferOwner;

        public PipeWriterAdapter(PipeWriter writer, Encoding encoding, MemoryPool<char> memoryPool)
        {
            Writer = writer;
            Encoding = encoding;
            MemoryPool = memoryPool;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<char> chars, CancellationToken cancel)
        {
            AssertNotDisposed(this);

            if (chars.IsEmpty)
            {
                return default;
            }

            // using Encoding directly because the built in Latin1 Encod_er_ is unreliable
            //   in the face of weird input
            var neededBytes = Encoding.GetByteCount(chars.Span);
            var neededChars = neededBytes / sizeof(char) + neededBytes % sizeof(char);

            if (BufferOwner == null || BufferOwner.Memory.Length < neededChars)
            {
                BufferOwner?.Dispose();
                BufferOwner = MemoryPool.Rent(neededChars);
            }

            // we know this will handle everything in chars, because we called GetByteCount()
            Encoding.GetBytes(chars.Span, MemoryMarshal.AsBytes(BufferOwner.Memory.Span));

            var copyTo = Writer.GetMemory(neededBytes);
            MemoryMarshal.AsBytes(BufferOwner.Memory.Span).Slice(0, neededBytes).CopyTo(copyTo.Span);

            Writer.Advance(neededBytes);
            var flushTask = Writer.FlushAsync();

            if (!flushTask.IsCompletedSuccessfully(this))
            {
                return WriteAsync_ContinueAfterFlushAsync(flushTask);
            }

            return default;

            static async ValueTask WriteAsync_ContinueAfterFlushAsync(ValueTask<FlushResult> waitFor)
            {
                await waitFor;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                BufferOwner?.Dispose();

                Writer = null;
                Encoding = null;
                MemoryPool = null;
                BufferOwner = null;
            }

            return default;
        }
    }

#if DEBUG
    internal sealed partial class PipeWriterAdapter : ITestableAsyncProvider
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
