using System;
using System.Buffers;

using static Cesil.DisposableHelper;

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
            internal NonNull<IMemoryOwner<char>> Owner;

            internal NonNull<Node> Next;
            internal int BytesUsed;

            internal Memory<char> Allocation => Owner.Value.Memory;

            internal Node() { }

            public void Init(IMemoryOwner<char> owner)
            {
                Owner.Value = owner;
                Next.Clear();
                BytesUsed = 0;
            }
        }

        public bool IsDisposed { get; private set; }

        internal ReadOnlySequence<char> Buffer => MakeSequence();

        // we can allocate a whooooole bunch of these in a row when
        //    serializing...
        // so keep one around to optimize the IsSingleSegment case
        private Node? FreeNode;

        // likewise, we spend a _lot_ of time creating memory chunks...
        private IMemoryOwner<char>? FreeMemory;

        private NonNull<Node> Head;
        private NonNull<Node> Tail;
        private bool IsSingleSegment;

        private readonly MemoryPool<char> MemoryPool;

        private readonly int SizeHint;

        internal MaxSizedBufferWriter(MemoryPool<char> memoryPool, int? sizeHint)
        {
            MemoryPool = memoryPool;

            if (sizeHint == null || sizeHint == 0)
            {
                SizeHint = DEFAULT_STAGING_SIZE;
            }
        }

        internal void Reset()
        {
            IMemoryOwner<char>? largest = null;
            var n = Head;
            while (n.HasValue)
            {
                var nValue = n.Value;
                var alloc = nValue.Owner.Value;
                if (largest == null || alloc.Memory.Length > largest.Memory.Length)
                {
                    largest = alloc;
                }
                else
                {
                    alloc.Dispose();
                }

                if (nValue.Next.HasValue)
                {
                    n = nValue.Next;
                }
                else
                {
                    break;
                }
            }

            if (Head.HasValue)
            {
                FreeNode = Head.Value;
            }
            else
            {
                FreeNode = null;
            }

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

            Head.Clear();
            Tail.Clear();
        }

        public void Advance(int count)
        {
            AssertNotDisposed(this);

            if (count < 0)
            {
                Throw.ArgumentException<object>($"Must be >= 0", nameof(count));
            }

            Tail.Value.BytesUsed += count;
        }

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            AssertNotDisposed(this);

            if (sizeHint < 0)
            {
                return Throw.ArgumentException<Memory<char>>($"Must be >= 0", nameof(sizeHint));
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
            if (Tail.HasValue)
            {
                var tailValue = Tail.Value;

                var leftInTail = tailValue.Allocation.Length - tailValue.BytesUsed;
                if (leftInTail >= size)
                {
                    var tailAlloc = tailValue.Allocation.Slice(tailValue.Allocation.Length - leftInTail);
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
                    return Throw.InvalidOperationException<Memory<char>>($"Needed a larger memory segment than could be requested, needed {size:N0}; {nameof(MemoryPool<char>.MaxBufferSize)} = {MemoryPool.MaxBufferSize:N0}");
                }

                alloc = MemoryPool.Rent(size);
            }

            newTail.Init(alloc);

            if (!Head.HasValue)
            {
                Head.Value = newTail;
                Tail.Value = newTail;
                IsSingleSegment = true;
            }
            else
            {
                Tail.Value.Next.Value = newTail;
                Tail.Value = newTail;
                IsSingleSegment = false;
            }

            return newTail.Allocation;
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            AssertNotDisposed(this);
            return GetMemory(sizeHint).Span;
        }

        private ReadOnlySequence<char> MakeSequence()
        {
            // nothing written
            if (!Head.HasValue || Head.Value.BytesUsed == 0)
            {
                return ReadOnlySequence<char>.Empty;
            }

            var headValue = Head.Value;

            // single segement case
            if (IsSingleSegment)
            {
                return new ReadOnlySequence<char>(headValue.Allocation.Slice(0, headValue.BytesUsed));
            }

            // multi segment series
            //   we need this mapping because the 
            //   Node represention isn't "finished"
            //   and still has extra space floating around
            //   between each node
            var headSeg = new ReadOnlyCharSegment(headValue.Allocation, headValue.BytesUsed);
            var n = headValue.Next;
            var tailSeg = headSeg;
            while (n.HasValue)
            {
                var nValue = n.Value;

                tailSeg = tailSeg.Append(nValue.Allocation, nValue.BytesUsed);
                if (nValue.Next.HasValue)
                {
                    n = nValue.Next;
                }
                else
                {
                    break;
                }
            }

            var startIx = 0;

            var endIx = Tail.Value.BytesUsed;

            var ret = new ReadOnlySequence<char>(headSeg, startIx, tailSeg, endIx);

            return ret;
        }


        public void Dispose()
        {
            if (!IsDisposed)
            {
                Reset();
                FreeMemory?.Dispose();
                IsDisposed = true;
            }
        }

        public override string ToString()
        => $"{nameof(MaxSizedBufferWriter)} with {nameof(IsSingleSegment)}={IsSingleSegment}, {nameof(MemoryPool)}={MemoryPool}";
    }
}
