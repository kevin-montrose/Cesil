using System;

namespace Cesil
{
    // for documentation why some public class isn't sealed
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class IntentionallyExtensibleAttribute : Attribute
    {
        internal IntentionallyExtensibleAttribute(string reason)
        => Utils.CheckArgumentNull(reason, nameof(reason));
    }
}
