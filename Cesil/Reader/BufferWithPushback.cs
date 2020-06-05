﻿using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class BufferWithPushback : ITestableDisposable
    {
        private readonly MemoryPool<char> MemoryPool;

        public bool IsDisposed { get; private set; }

        private IMemoryOwner<char> BackingOwner;

        internal Memory<char> Buffer;
        private Memory<char> PushBack;

        private int InPushBack;

        internal BufferWithPushback(
            MemoryPool<char> memoryPool,
            int initialBufferSize
        )
        {
            MemoryPool = memoryPool;
            BackingOwner = MemoryPool.Rent(initialBufferSize);

            UpdateBufferAndPushBack();
        }

        internal void PushBackFromOutsideBuffer(Memory<char> pushback)
        {
            if (pushback.Length > PushBack.Length)
            {
                // blow away the current buffer, we
                //   don't need any of the data in there
                var oldSize = BackingOwner.Memory.Length;

                var newSize = pushback.Length * 2;
                BackingOwner.Dispose();
                BackingOwner = Utils.RentMustIncrease(MemoryPool, newSize, oldSize);

                UpdateBufferAndPushBack();
            }

            var pushbackSpan = PushBack.Span;
            pushback.Span.CopyTo(pushbackSpan);

            InPushBack = pushback.Length;
        }

        private void UpdateBufferAndPushBack()
        {
            var halfPoint = BackingOwner.Memory.Length / 2;

            Buffer = BackingOwner.Memory.Slice(0, halfPoint);
            PushBack = BackingOwner.Memory.Slice(halfPoint);
        }

        internal int Read(IReaderAdapter reader)
        {
            if (InPushBack > 0)
            {
                PushBack.Slice(0, InPushBack).CopyTo(Buffer);

                var ret = InPushBack;
                InPushBack = 0;
                return ret;
            }

            return reader.Read(Buffer.Span);
        }

        internal ValueTask<int> ReadAsync(IAsyncReaderAdapter reader, CancellationToken cancellationToken)
        {
            if (InPushBack > 0)
            {
                PushBack.Slice(0, InPushBack).CopyTo(Buffer);
                var ret = InPushBack;
                InPushBack = 0;
                return new ValueTask<int>(ret);
            }

            return reader.ReadAsync(Buffer, cancellationToken);
        }

        internal void PushBackFromBuffer(int readCount, int pushBackCount)
        {
            var copyFrom = readCount - pushBackCount;

            Buffer.Slice(copyFrom, pushBackCount).CopyTo(PushBack);
            InPushBack = pushBackCount;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                BackingOwner.Dispose();

                IsDisposed = true;
            }
        }
    }
}