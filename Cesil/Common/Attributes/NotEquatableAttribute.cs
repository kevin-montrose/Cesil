using System;

namespace Cesil
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    internal sealed class NotEquatableAttribute : Attribute
    {
        public NotEquatableAttribute(string reason) { }
    }
}
