using System;

namespace Cesil
{
    internal interface ICreatesCacheableDelegate<TDelegate>
        where TDelegate : Delegate
    {
        ref NonNull<TDelegate> CachedDelegate { get; }

        void Guarantee(IDelegateCache cache);
        TDelegate CreateDelegate();
    }
}
