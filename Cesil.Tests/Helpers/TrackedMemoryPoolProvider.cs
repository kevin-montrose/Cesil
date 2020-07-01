using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil.Tests
{
    internal sealed class TrackedMemoryPoolProvider : IMemoryPoolProvider
    {
        private readonly Dictionary<TypeInfo, object> MemoryPools = new Dictionary<TypeInfo, object>();

        public int OutstandingRentals
        {
            get
            {
                var ret = 0;

                foreach(var kv in MemoryPools)
                {
                    var poolType = typeof(TrackedMemoryPool<>).MakeGenericType(kv.Key);
                    var outstandingProp = poolType.GetProperty(nameof(TrackedMemoryPool<object>.OutstandingRentals));

                    var rentals = (int)outstandingProp.GetMethod.Invoke(kv.Value, Array.Empty<object>());
                    ret += rentals;
                }

                return ret;
            }
        }

        public MemoryPool<T> GetMemoryPool<T>()
        {
            var forType = typeof(T).GetTypeInfo();

            if(!MemoryPools.TryGetValue(forType, out var allocated))
            {
                MemoryPools[forType] = allocated = new TrackedMemoryPool<T>();
            }

            return (MemoryPool<T>)allocated;
        }
    }
}
