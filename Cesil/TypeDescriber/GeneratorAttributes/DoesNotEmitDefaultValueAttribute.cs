using System;

namespace Cesil
{
    /// <summary>
    /// Automatically attached to generated methods depending on runtime behavior.
    /// 
    /// You should not use this directly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    [Obsolete("You should not use this directly, it may not be present in future versions.")]
    public sealed class DoesNotEmitDefaultValueAttribute : Attribute, IEquatable<DoesNotEmitDefaultValueAttribute>
    {
        /// <summary>
        /// Create a new DoesNotEmitDefaultValueAttribute.
        /// 
        /// You should not use this directly.
        /// </summary>
        public DoesNotEmitDefaultValueAttribute() { }

        /// <summary>
        /// Return true if the given object is a DoesNotEmitDefaultValueAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as DoesNotEmitDefaultValueAttribute);

        /// <summary>
        /// Returns true if the given DoesNotEmitDefaultValueAttribute is equal
        ///   to this one.
        /// </summary>
        public bool Equals(DoesNotEmitDefaultValueAttribute? attribute)
        => !ReferenceEquals(attribute, null);

        /// <summary>
        /// Returns a stable hash code for this DoesNotEmitDefaultValueAttribute.
        /// </summary>
        public override int GetHashCode()
        => 0;

        /// <summary>
        /// Compare two DoesNotEmitDefaultValueAttributes for equality
        /// </summary>
        public static bool operator ==(DoesNotEmitDefaultValueAttribute? a, DoesNotEmitDefaultValueAttribute? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two DoesNotEmitDefaultValueAttributes for inequality
        /// </summary>
        public static bool operator !=(DoesNotEmitDefaultValueAttribute? a, DoesNotEmitDefaultValueAttribute? b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this DoesNotEmitDefaultValueAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(DoesNotEmitDefaultValueAttribute)} instance";
    }
}
