using System;

namespace Cesil
{
    // todo: reorg
    internal readonly struct CachedDelegate<T>
        where T: class
    {
        public static readonly CachedDelegate<T> Empty = new CachedDelegate<T>();

        public readonly NonNull<T> Value;

        public CachedDelegate(T? value)
        {
            Value = default;
            Value.SetAllowNull(value);
        }
    }

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
