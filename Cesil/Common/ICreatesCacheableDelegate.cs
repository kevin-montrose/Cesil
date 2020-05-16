using System;

namespace Cesil
{
    internal interface ICreatesCacheableDelegate<TDelegate>
        where TDelegate : Delegate
    {
        TDelegate? CachedDelegate { get; set; }

        TDelegate Guarantee(IDelegateCache cache);
        TDelegate CreateDelegate();
    }
}
