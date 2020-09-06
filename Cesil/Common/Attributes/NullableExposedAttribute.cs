
using System;

namespace Cesil
{
    // for indicating why some reference type in the public api is nullable
    [AttributeUsage(AttributeTargets.ReturnValue | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableExposedAttribute : Attribute
    {
        internal NullableExposedAttribute(string reason)
        => Utils.CheckArgumentNull(reason, nameof(reason));
    }
}
