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
    public sealed class IsRequiredAttribute : Attribute, IEquatable<IsRequiredAttribute>
    {
        /// <summary>
        /// Create a new IsRequiredAttribute.
        /// 
        /// You should not use this directly.
        /// </summary>
        public IsRequiredAttribute() { }

        /// <summary>
        /// Return true if the given object is a IsRequiredAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as IsRequiredAttribute);

        /// <summary>
        /// Returns true if the given IsRequiredAttribute is equal
        ///   to this one.
        /// </summary>
        public bool Equals(IsRequiredAttribute? attribute)
        => !ReferenceEquals(attribute, null);

        /// <summary>
        /// Returns a stable hash code for this IsRequiredAttribute.
        /// </summary>
        public override int GetHashCode()
        => 0;

        /// <summary>
        /// Compare two IsRequiredAttributes for equality
        /// </summary>
        public static bool operator ==(IsRequiredAttribute? a, IsRequiredAttribute? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two IsRequiredAttributes for inequality
        /// </summary>
        public static bool operator !=(IsRequiredAttribute? a, IsRequiredAttribute? b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this IsRequiredAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(IsRequiredAttribute)} instance";
    }
}
