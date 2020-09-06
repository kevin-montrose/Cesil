using System;

namespace Cesil
{
    /// <summary>
    /// It appears that Coverlet uses _just the name_ to identify attributes,
    ///   so we can avoid taking a dependency on it and just define this
    ///   otherwise pointless attribute ourselves.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Struct)]
    internal sealed class ExcludeFromCoverageAttribute : Attribute
    {
        internal ExcludeFromCoverageAttribute(string reason)
        => Utils.CheckArgumentNull(reason, nameof(reason));
    }
}
