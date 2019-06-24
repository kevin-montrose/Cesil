using System;
using System.Buffers;

namespace Cesil
{
    // An implementation of IBufferWriter that will _never_ 
    //   (baring OOM or max allocation size constraints) return
    //   a Memory or Span smaller than the given sizeHint.
    internal sealed class MaxSizedBufferWriter : IBufferWriter<char>, ITestableDisposable
    {
        internal const int DEFAULT_STAGING_SIZE = (4098 - 8) / sizeof(char);

        private class Node
        {
            internal IMemoryOwner<char> Owner;
            internal Node Next;
            internal int BytesUsed;

            internal Memory<char> Allocation => Owner.Memory;

            internal Node() { }

            public void Init(IMemoryOwner<char> owner)
            {
                Owner = owner;
                Next = null;
                BytesUsed = 0;
            }
        }

        public bool IsDisposed => MemoryPool == null;

        internal ReadOnlySequence<char> Buffer => MakeSequence();

        // we can allocate a whooooole bunch of these in a row when
        //    serializing...
        // so keep one around to optimize the IsSingleSegment case
        private Node FreeNode;

        // likewise, we spend a _lot_ of time creating memory chunks...
        private IMemoryOwner<char> FreeMemory;

        private Node Head;
        private Node Tail;
        private bool IsSingleSegment;

        private MemoryPool<char> MemoryPool;

        private readonly int SizeHint;

        internal MaxSizedBufferWriter(MemoryPool<char> memoryPool, int? sizeHint)
        {
            MemoryPool = memoryPool;
            Head = Tail = null;

            if (sizeHint == null || sizeHint == 0)
            {
                SizeHint = DEFAULT_STAGING_SIZE;
            }
        }

        internal void Reset()
        {
            IMemoryOwner<char> largest = null;
            var n = Head;
            while (n != null)
            {
                var alloc = n.Owner;
                if (largest == null || alloc.Memory.Length > largest.Memory.Length)
                {
                    largest = alloc;
                }
                else
                {
                    alloc.Dispose();
                }
                n = n.Next;
            }

            FreeNode = Head;

            if (largest != null)
            {
                if (FreeMemory == null)
                {
                    FreeMemory = largest;
                }
                else
                {
                    if (largest.Memory.Length > FreeMemory.Memory.Length)
                    {
                        FreeMemory.Dispose();
                        FreeMemory = largest;
                    }
                    else
                    {
                        largest.Dispose();
                    }
                }
            }

            Head = Tail = null;
        }

        public void Advance(int count)
        {
            AssertNotDisposed();

            if (count < 0)
            {
                Throw.ArgumentException($"Must be >= 0", nameof(count));
                return;
            }

            Tail.BytesUsed += count;
        }

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            AssertNotDisposed();

            if (sizeHint < 0)
            {
                Throw.ArgumentException($"Must be >= 0", nameof(sizeHint));
                return default;
            }

            int size;
            if (sizeHint == 0)
            {
                size = SizeHint;
            }
            else
            {
                size = sizeHint;
            }

            // can we fit the remainder in the last allocation?
            if (Tail != null)
            {
                var leftInTail = Tail.Allocation.Length - Tail.BytesUsed;
                if (leftInTail >= size)
                {
                    var tailAlloc = Tail.Allocation.Slice(Tail.Allocation.Length - leftInTail);
                    return tailAlloc;
                }
            }

            Node newTail;
            if (FreeNode != null)
            {
                newTail = FreeNode;
                FreeNode = null;
            }
            else
            {
                newTail = new Node();
            }

            IMemoryOwner<char> alloc;
            if (FreeMemory != null && FreeMemory.Memory.Length >= size)
            {
                alloc = FreeMemory;
                FreeMemory = null;
            }
            else
            {
                if (size > MemoryPool.MaxBufferSize)
                {
                    Throw.InvalidOperationException($"Needed a larger memory segment than could be requested, needed {size:N0}; {nameof(MemoryPool<char>.MaxBufferSize)} = {MemoryPool.MaxBufferSize:N0}");
                }

                alloc = MemoryPool.Rent(size);
            }

            newTail.Init(alloc);

            if (Head == null)
            {
                Head = Tail = newTail;
                IsSingleSegment = true;
            }
            else
            {
                Tail.Next = newTail;
                Tail = newTail;
                IsSingleSegment = false;
            }

            return Tail.Allocation;
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            AssertNotDisposed();
            return GetMemory(sizeHint).Span;
        }

        private ReadOnlySequence<char> MakeSequence()
        {
            // nothing written
            if (Head == null || Head.BytesUsed == 0)
            {
                return ReadOnlySequence<char>.Empty;
            }

            // single segement case
            if (IsSingleSegment)
            {
                return new ReadOnlySequence<char>(Head.Allocation.Slice(0, Head.BytesUsed));
            }

            // multi segment series
            //   we need this mapping because the 
            //   Node represention isn't "finished"
            //   and still has extra space floating around
            //   between each node
            var headSeg = new ReadOnlyCharSegment(Head.Allocation, Head.BytesUsed);
            var n = Head.Next;
            var tailSeg = headSeg;
            while (n != null)
            {
                tailSeg = tailSeg.Append(n.Allocation, n.BytesUsed);
                n = n.Next;
            }

            var startIx = 0;
            var endIx = Tail.BytesUsed;

            var ret = new ReadOnlySequence<char>(headSeg, startIx, tailSeg, endIx);

            return ret;
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(MaxSizedBufferWriter));
            }
        }


        public void Dispose()
        {
            if (!IsDisposed)
            {
                FreeMemory?.Dispose();
                MemoryPool = null;
            }
        }

        public override string ToString()
        => $"{nameof(IsSingleSegment)}={IsSingleSegment}, {nameof(MemoryPool)}={MemoryPool}";
    }
}
