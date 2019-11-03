using System;

namespace Cesil
{
    internal interface IDelegateCache
    {
        CachedDelegate<V> TryGet<T, V>(T key)
            where T : IEquatable<T>
            where V : Delegate;

        void Add<T, V>(T key, V cached)
            where T : IEquatable<T>
            where V : Delegate;
    }
}
