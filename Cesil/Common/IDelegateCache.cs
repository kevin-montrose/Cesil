using System;

namespace Cesil
{
    internal interface IDelegateCache
    {
        bool TryGet<T, V>(T key, out V cached)
            where T : IEquatable<T>
            where V : Delegate;

        void Add<T, V>(T key, V cached)
            where T : IEquatable<T>
            where V : Delegate;
    }
}
