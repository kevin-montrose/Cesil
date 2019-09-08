using System;

namespace Cesil
{
    // for indicating that it's "OK" that something is exposed despite being kind of ambiguous
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Property)]
    internal sealed class IntentionallyExposedPrimitiveAttribute : Attribute
    {
        public IntentionallyExposedPrimitiveAttribute(string reason) { }
    }
}
