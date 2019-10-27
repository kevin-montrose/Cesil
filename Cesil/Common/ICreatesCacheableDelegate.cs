using System;

namespace Cesil
{
    internal interface ICreatesCacheableDelegate<TDelegate>
        where TDelegate : Delegate
    {
        bool HasCachedDelegate { get; }
        TDelegate CachedDelegate { get; set; }

        void Guarantee(IDelegateCache cache);
        TDelegate CreateDelegate();
    }
}
