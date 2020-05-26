using System;
using System.Diagnostics.CodeAnalysis;

namespace Cesil
{
    internal interface IDelegateCache
    {
        bool TryGetDelegate<T, V>(T key, [MaybeNullWhen(returnValue: false)]out V del)
            where T : IEquatable<T>
            where V : class, Delegate;

        void AddDelegate<T, V>(T key, V cached)
            where T : IEquatable<T>
            where V : class, Delegate;
    }
}
