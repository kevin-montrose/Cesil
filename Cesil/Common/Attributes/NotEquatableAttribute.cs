using System;

namespace Cesil
{
    // for explaining why some public type hasn't implemented IEquatable
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    internal sealed class NotEquatableAttribute : Attribute
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "For documentation, not use")]
        internal NotEquatableAttribute(string reason)
        => Utils.CheckArgumentNull(reason, nameof(reason));
    }
}
