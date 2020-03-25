using System;

namespace Cesil
{
    // for indicating that it's "OK" that something is exposed despite being kind of ambiguous
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Property, Inherited = false)]
    internal sealed class IntentionallyExposedPrimitiveAttribute : Attribute
    {
        internal IntentionallyExposedPrimitiveAttribute(string reason) { }
    }
}
