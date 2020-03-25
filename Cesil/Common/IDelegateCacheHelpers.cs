using System;

namespace Cesil
{
    internal static class IDelegateCacheHelpers
    {
        internal static void GuaranteeImpl<TSelf, TDelegate>(TSelf inst, IDelegateCache cache)
            where TSelf : ICreatesCacheableDelegate<TDelegate>, IEquatable<TSelf>
            where TDelegate : Delegate
        {
            if (inst.CachedDelegate.HasValue) return;

            ref var del = ref inst.CachedDelegate;

            var cachedRes = cache.TryGet<TSelf, TDelegate>(inst);

            if (cachedRes.Value.HasValue)
            {
                del.Value = cachedRes.Value.Value;
                return;
            }

            var newDel = inst.CreateDelegate();
            cache.Add(inst, newDel);
            del.Value = newDel;
        }
    }
}
