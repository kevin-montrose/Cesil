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

        private sealed class Node
        {
            public static readonly Node EmptyNode = new Node(true);

            internal IMemoryOwner<char> Owner;
            internal Memory<char> Allocation;

            internal int BytesUsed;

            internal bool HasNext;
            internal Node Next;

            private Node(bool isEmpty)
            {
                Owner = EmptyMemoryOwner.Singleton;
                Allocation = Memory<char>.Empty;

                HasNext = false;
                BytesUsed = 0;

                Next = isEmpty? this : EmptyNode;
            }

            internal Node() : this(false) { }

            public void Init(IMemoryOwner<char> owner)
            {
                Owner = owner;
                Allocation = owner.Memory;

                BytesUsed = 0;

                HasNext = false;
                Next = EmptyNode;
            }
        }

        public bool IsDisposed { get; private set; }

        // we can allocate a whole bunch of these in a row when
        //    serializing...
        // so keep one around to optimize the IsSingleSegment case
        private Node? FreeNode;

        // likewise, we spend a _lot_ of time creating memory chunks...
        private IMemoryOwner<char>? FreeMemory;

        private bool HasNodes;
        private Node Head;
        private Node Tail;
        private bool IsSingleSegment;

        private readonly MemoryPool<char> MemoryPool;

        private readonly int SizeHint;

        internal MaxSizedBufferWriter(MemoryPool<char> memoryPool, int? sizeHint)
        {
            MemoryPool = memoryPool;

            if (sizeHint == null || sizeHint == 0)
            {
                SizeHint = Math.Min(DEFAULT_STAGING_SIZE, memoryPool.MaxBufferSize);
            }

            Head = Tail = Node.EmptyNode;
            HasNodes = false;
        }

        internal void Reset()
        {
            if (!HasNodes) return;

            IMemoryOwner<char>? largest = null;

            var n = Head;
            while(true)
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

                if (n.HasNext)
                {
                    n = n.Next;
                }
                else
                {
                    break;
                }
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

            Head = Tail = Node.EmptyNode;
            HasNodes = false;
        }

        public void Advance(int count)
        {
            AssertNotDisposedInternal(this);

            if (count < 0)
            {
                Throw.ArgumentException<object>($"Must be >= 0", nameof(count));
                return;
            }

            Tail.BytesUsed += count;
        }

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            AssertNotDisposedInternal(this);

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
            if (HasNodes)
            {
                var tailValue = Tail;

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

            if (!HasNodes)
            {
                HasNodes = true;
                Head = newTail;
                Tail = newTail;
                IsSingleSegment = true;
            }
            else
            {
                Tail.HasNext = true;
                Tail.Next = newTail;

                Tail = newTail;
                IsSingleSegment = false;
            }

            return newTail.Allocation;
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            AssertNotDisposedInternal(this);
            return GetMemory(sizeHint).Span;
        }

        public bool MakeSequence(ref ReadOnlySequence<char> nonEmpty)
        {
            AssertNotDisposedInternal(this);

            // nothing written
            if (!HasNodes || Head.BytesUsed == 0)
            {
                return false;
            }

            // single segment case
            if (IsSingleSegment)
            {
                var usedPartOfHead = Head.Allocation.Slice(0, Head.BytesUsed);
                nonEmpty = new ReadOnlySequence<char>(usedPartOfHead);
                return true;
            }

            // multi segment series
            //   we need this mapping because the 
            //   Node representation isn't "finished"
            //   and still has extra space floating around
            //   between each node
            var headSeg = new ReadOnlyCharSegment(Head.Allocation, Head.BytesUsed);
            var tailSeg = headSeg;
            if (Head.HasNext)
            {
                var n = Head.Next;
                while (true)
                {
                    tailSeg = tailSeg.Append(n.Allocation, n.BytesUsed);
                    if (n.HasNext)
                    {
                        n = n.Next;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            var startIx = 0;

            var endIx = Tail.BytesUsed;

            nonEmpty = new ReadOnlySequence<char>(headSeg, startIx, tailSeg, endIx);
            return true;
        }


        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                Reset();
                FreeMemory?.Dispose();
            }
        }

        public override string ToString()
        => $"{nameof(MaxSizedBufferWriter)} with {nameof(IsSingleSegment)}={IsSingleSegment}, {nameof(MemoryPool)}={MemoryPool}";
    }
}
