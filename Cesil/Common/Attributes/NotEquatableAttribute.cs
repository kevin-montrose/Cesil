using System;

namespace Cesil
{
    // for explaining why some public type hasn't implemented IEquatable
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    internal sealed class NotEquatableAttribute : Attribute
    {
        public NotEquatableAttribute(string reason) { }
    }
}
