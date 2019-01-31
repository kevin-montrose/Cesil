using System;
using System.Buffers;

namespace Cesil.Tests
{
    internal sealed class TestMemoryPool<T> : MemoryPool<T>
    {
        private class Owner : IMemoryOwner<T>
        {
            private readonly IMemoryOwner<T> Inner;
            private readonly int Size;
            public Memory<T> Memory => Inner.Memory.Slice(0, Size);

            public Owner(IMemoryOwner<T> inner, int size)
            {
                Inner = inner;
                Size = size;
            }

            public void Dispose()
            => Inner.Dispose();
        }

        public override int MaxBufferSize { get; }

        public TestMemoryPool(int maxSize)
        {
            MaxBufferSize = maxSize;
        }

        public override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            if (minBufferSize > MaxBufferSize) throw new Exception("NO");

            var ret = MemoryPool<T>.Shared.Rent(minBufferSize);

            var externalSize = Math.Min(MaxBufferSize, ret.Memory.Length);

            return new Owner(ret, externalSize);
        }

        protected override void Dispose(bool disposing)
        { }
    }
}
