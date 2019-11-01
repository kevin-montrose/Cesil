﻿using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;

namespace Cesil.Tests
{
    /// <summary>
    /// Memory pool that keeps track of how many IMemoryOwners it's handed out,
    ///   and if they're still "alive".
    /// </summary>
    public sealed class TrackedMemoryPool<T> : MemoryPool<T>
    {
        private const bool LOG = true;

        private sealed class TrackedMemoryOwner : IMemoryOwner<T>
        {
            public Memory<T> Memory => Inner.Memory;

            private readonly int Id;
            private readonly IMemoryOwner<T> Inner;
            private readonly TrackedMemoryPool<T> Leaser;
            private bool IsDisposed;

            internal TrackedMemoryOwner(int id, IMemoryOwner<T> inner, TrackedMemoryPool<T> leaser)
            {
                Id = id;
                Inner = inner;
                Leaser = leaser;
                IsDisposed = false;
            }

            public void Dispose()
            {
                if (IsDisposed) throw new InvalidOperationException($"Double dispose; Id={Id}");

                IsDisposed = true;
                Inner.Dispose();

                var res = Interlocked.Decrement(ref Leaser._OutstandinRentals);

                if (res < 0) throw new InvalidOperationException("Outstanding rentals became negative");

                Debug.WriteLineIf(LOG, $"\tFreed {Id}");
            }
        }

        private int NextId;

        private int _OutstandinRentals;
        public int OutstandingRentals => _OutstandinRentals;

        private int _TotalRentals;
        public int TotalRentals => _TotalRentals;

        public override int MaxBufferSize => Shared.MaxBufferSize;

        public TrackedMemoryPool()
        {
            Debug.WriteLine($"Initializing {nameof(TrackedMemoryOwner)}");

            _OutstandinRentals = 0;
            _TotalRentals = 0;
        }

        protected override void Dispose(bool disposing) { }

        public override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            var rent = Shared.Rent(minBufferSize);
            Interlocked.Increment(ref _OutstandinRentals);
            Interlocked.Increment(ref _TotalRentals);

            var id = Interlocked.Increment(ref NextId);

            Debug.WriteLineIf(LOG, $"\tRented {id}");

            return new TrackedMemoryOwner(id, rent, this);
        }
    }
}
