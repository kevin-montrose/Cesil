using System;

namespace Cesil
{
    /// <summary>
    /// When using Cesil's Source Generator (see Nuget.org for Cesil.SourceGenerator) marks a constructor as the InstanceProvider
    /// for the containing type.
    /// 
    /// To use a method instead of a constructor, use the InstanceProviderType and InstanceProviderMethodName properties on
    /// [GenerateDeserializableAttribute].
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class DeserializerInstanceProviderAttribute : Attribute, IEquatable<DeserializerInstanceProviderAttribute>
    {
        /// <summary>
        /// Create a DeserializerInstanceProviderAttribute attribute.
        /// </summary>
        public DeserializerInstanceProviderAttribute() { }

        /// <summary>
        /// Returns true if the given DeserializerInstanceProviderAttribute is equal
        /// to this one.
        /// </summary>
        public bool Equals(DeserializerInstanceProviderAttribute? attribute)
        => !ReferenceEquals(attribute, null);

        /// <summary>
        /// Return true if the given object is a DeserializerInstanceProviderAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as DeserializerInstanceProviderAttribute);


        /// <summary>
        /// Returns a stable hash code for this DeserializerInstanceProviderAttribute.
        /// </summary>
        public override int GetHashCode()
        => 0;

        /// <summary>
        /// Compare two DeserializerInstanceProviderAttributes for equality
        /// </summary>
        public static bool operator ==(DeserializerInstanceProviderAttribute a, DeserializerInstanceProviderAttribute b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two DeserializerInstanceProviderAttributes for inequality
        /// </summary>
        public static bool operator !=(DeserializerInstanceProviderAttribute a, DeserializerInstanceProviderAttribute b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this DeserializerInstanceProviderAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(DeserializerInstanceProviderAttribute)} instance";
    }
}
