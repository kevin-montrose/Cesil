using System;

namespace Cesil
{
    internal static class IDelegateCacheHelpers
    {
        internal static TDelegate GuaranteeImpl<TSelf, TDelegate>(TSelf inst, IDelegateCache cache)
            where TSelf : ICreatesCacheableDelegate<TDelegate>, IEquatable<TSelf>
            where TDelegate : class, Delegate
        {
            var del = inst.CachedDelegate;

            if (del != null)
            {
                return del;
            }

            if(cache.TryGetDelegate<TSelf, TDelegate>(inst, out var cached))
            {
                inst.CachedDelegate = cached;
                return cached;
            }

            del = inst.CreateDelegate();
            cache.AddDelegate(inst, del);
            inst.CachedDelegate = del;
            return del;
        }
    }
}
