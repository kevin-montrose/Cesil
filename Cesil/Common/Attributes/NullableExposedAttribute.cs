
using System;

namespace Cesil
{
    // for indicating why some reference type in the public api is nullable
    [AttributeUsage(AttributeTargets.ReturnValue | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
    internal sealed class NullableExposedAttribute : Attribute
    {
        public NullableExposedAttribute(string reason) { }
    }
}
