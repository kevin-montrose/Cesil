using System;
using System.Collections.Concurrent;

namespace Cesil
{
    internal sealed class DefaultTypeDescriberDelegateCache : IDelegateCache
    {
        public static readonly IDelegateCache Instance = new DefaultTypeDescriberDelegateCache();

        private readonly ConcurrentDictionary<object, Delegate> Cache;

        private DefaultTypeDescriberDelegateCache()
        {
            Cache = new ConcurrentDictionary<object, Delegate>();
        }

        void IDelegateCache.Add<T, V>(T key, V cached)
        => Cache.TryAdd(key, cached);

        bool IDelegateCache.TryGet<T, V>(T key, out V val)
        {
            if (!Cache.TryGetValue(key, out var cached))
            {
                val = default;
                return false;
            }

            val = (V)cached;
            return true;
        }
    }
}
