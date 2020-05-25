using System;

namespace Cesil
{
    // for indicating that it's "OK" that something is exposed despite being kind of ambiguous
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Property, Inherited = false)]
    internal sealed class IntentionallyExposedPrimitiveAttribute : Attribute
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "For documentation, not use")]
        internal IntentionallyExposedPrimitiveAttribute(string reason)
        => Utils.CheckArgumentNull(reason, nameof(reason));
    }
}
