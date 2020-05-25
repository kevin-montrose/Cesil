using System;

namespace Cesil
{
    // for documentation why some public class isn't sealed
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class IntentionallyExtensibleAttribute : Attribute
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "For documentation, not use")]
        internal IntentionallyExtensibleAttribute(string reason)
        => Utils.CheckArgumentNull(reason, nameof(reason));
    }
}
