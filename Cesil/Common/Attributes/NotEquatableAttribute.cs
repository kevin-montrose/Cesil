using System;

namespace Cesil
{
    // for explaining why some public type hasn't implemented IEquatable
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    internal sealed class NotEquatableAttribute : Attribute
    {
        internal NotEquatableAttribute(string reason)
        => Utils.CheckArgumentNull(reason, nameof(reason));
    }
}
