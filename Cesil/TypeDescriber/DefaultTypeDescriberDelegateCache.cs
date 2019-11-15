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

        CachedDelegate<V> IDelegateCache.TryGet<T, V>(T key)
            where V : class
        {
            if (!Cache.TryGetValue(key, out var cachedNull))
            {
                return CachedDelegate<V>.Empty;
            }

            return new CachedDelegate<V>(cachedNull as V);
        }
    }
}
