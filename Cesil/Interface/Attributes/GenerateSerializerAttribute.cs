using System;

namespace Cesil
{
    /// <summary>
    /// When using Cesil's Source Generator (see Nuget.org for Cesil.SourceGenerator) marks a class or struct
    /// as needing a serializer generated at compile time.
    /// 
    /// When using the AheadOfTimeTypeDescriber, the created I(Async)Writers
    /// for this type will do no runtime code generation.
    /// 
    /// You can customize the behavior of the generated serialize with [DataMemberAttribute],
    /// and [SerializerMemberAttribute] attributes.
    /// 
    /// Default behavior (with no additional attributes) closely follows DefaultTypeDescriver.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GenerateSerializerAttribute : Attribute, IEquatable<GenerateSerializerAttribute>
    {
        /// <summary>
        /// Create a GenerateSerializerAttribute attribute.
        /// </summary>
        public GenerateSerializerAttribute() { }

        /// <summary>
        /// Returns true if the given GenerateSerializerAttribute is equal
        /// to this one.
        /// </summary>
        public bool Equals(GenerateSerializerAttribute? attribute)
        => !ReferenceEquals(attribute, null);

        /// <summary>
        /// Return true if the given object is a GenerateSerializerAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as GenerateSerializerAttribute);


        /// <summary>
        /// Returns a stable hash code for this GenerateSerializerAttribute.
        /// </summary>
        public override int GetHashCode()
        => 0;

        /// <summary>
        /// Compare two GenerateSerializerAttributes for equality
        /// </summary>
        public static bool operator ==(GenerateSerializerAttribute a, GenerateSerializerAttribute b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two GenerateSerializerAttributes for inequality
        /// </summary>
        public static bool operator !=(GenerateSerializerAttribute a, GenerateSerializerAttribute b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this GenerateSerializerAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(GenerateSerializerAttribute)} instance";
    }
}
