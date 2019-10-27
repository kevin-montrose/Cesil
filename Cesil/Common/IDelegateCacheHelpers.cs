using System;

namespace Cesil
{
    internal static class IDelegateCacheHelpers
    {
        public static void GuaranteeImpl<TSelf, TDelegate>(TSelf inst, IDelegateCache cache)
            where TSelf : ICreatesCacheableDelegate<TDelegate>, IEquatable<TSelf>
            where TDelegate : Delegate
        {
            if (inst.HasCachedDelegate) return;

            if (cache.TryGet<TSelf, TDelegate>(inst, out var val))
            {
                val = Utils.NonNull(val);
                inst.CachedDelegate = val;
                return;
            }

            var newDel = inst.CreateDelegate();
            cache.Add(inst, newDel);
            inst.CachedDelegate = newDel;
        }
    }
}
