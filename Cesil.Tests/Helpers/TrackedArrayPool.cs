using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Cesil.Tests
{
    /// <summary>
    /// Array pool that keeps track of how many arrays it's handed out.
    /// </summary>
    internal sealed class TrackedArrayPool<T> : ArrayPool<T>
    {
        private static int NextPoolId;

        private readonly int PoolId;

        private readonly HashSet<T[]> Available;
        private readonly HashSet<T[]> Leased;

        private readonly Dictionary<T[], int> Ids;

        private int NextId;

        public int OutstandingRentals => Leased.Count;

        public int TotalRentals { get; private set; }

        public TrackedArrayPool()
        {
            PoolId = Interlocked.Increment(ref NextPoolId);

            Available = new HashSet<T[]>();
            Leased = new HashSet<T[]>();
            Ids = new Dictionary<T[], int>();

            LogHelper.TrackedArrayPool_New(PoolId);
        }

        public override T[] Rent(int minimumLength)
        {
            TotalRentals++;

            var id = Interlocked.Increment(ref NextId);

            var matching = Available.FirstOrDefault(a => a.Length >= minimumLength);
            if (matching != null)
            {
                Available.Remove(matching);
            }
            else
            {
                matching = new T[minimumLength];
            }

            Leased.Add(matching);

            Ids[matching] = id;

            LogHelper.TrackedArrayPool_Rent(id);

            return matching;
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            if (!Leased.Contains(array))
            {
                throw new InvalidOperationException("Attempted to return array that wasn't owned by this pool");
            }

            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }

            var id = Ids[array];

            Leased.Remove(array);
            Available.Add(array);
            Ids.Remove(array);

            LogHelper.TrackedArrayPool_Freed(id);
        }
    }
}
