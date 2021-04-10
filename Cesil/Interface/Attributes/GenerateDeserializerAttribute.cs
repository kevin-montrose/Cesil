using System;

namespace Cesil
{

    /// <summary>
    /// When using Cesil's Source Generator (see Nuget.org for Cesil.SourceGenerator) marks a class or struct
    /// as needing a deserializer generated at compile time.
    /// 
    /// InstanceProviderType and InstanceProviderMethodName are used to indicate how to obtain an instance
    /// of the annotated type from a method.  If not set, the type's parameterless constructor is used by default.
    /// To use a constructor other than the parameterless, use the [DeserializerInstanceProvider].
    /// 
    /// When using the AheadOfTimeTypeDescriber, the created I(Async)Readers
    /// for this type will do no runtime code generation.
    /// 
    /// You can customize the behavior of the generated deserializer with [DataMemberAttribute],
    /// and [DeserializaerMemberAttribute] attributes.
    /// 
    /// Default behavior (with no additional attributes) closely follows DefaultTypeDescriver.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GenerateDeserializerAttribute : Attribute, IEquatable<GenerateDeserializerAttribute>
    {
        /// <summary>
        /// Type to lookup an InstanceProvider method or constructor on, used with InstanceProviderMethodName.
        /// 
        /// If null, defaults to the parameterless constructor of the annotated type.
        /// 
        /// If non-null, InstanceProviderMethodName must also be set.  
        /// 
        /// The type must be public (or internal, if declared in the same assembly as the annotated type).
        /// </summary>
        [NullableExposed("Truly optional")]
        public Type? InstanceProviderType { get; set; }

        /// <summary>
        /// Name of InstanceProvider method or constructor, used with InstanceProviderType.
        /// 
        /// If null, defaults to the parameterless constructor of the annotated type.
        /// 
        /// If non-null, InstanceProviderType must also be set.
        /// 
        /// If pointing at a method, it must:
        ///   - be static
        ///   - return a bool
        ///   - have two parameters
        ///   - the first must be an in ReadContext
        ///   - the second must be an out parameter of the constructed type
        ///   
        /// If pointing at a constructor, it must either:
        ///   1. take no parameters or
        ///   2. each parameter must have a [GenerateDeserializableMember] annotation
        /// </summary>
        [NullableExposed("Truly optional")]
        public string? InstanceProviderMethodName { get; set; }

        /// <summary>
        /// Create a GenerateDeserializableAttribute attribute.
        /// </summary>
        public GenerateDeserializerAttribute() { }

        /// <summary>
        /// Returns true if the given GenerateDeserializableAttribute is equal
        /// to this one.
        /// </summary>
        public bool Equals(GenerateDeserializerAttribute? attribute)
        {
            if (ReferenceEquals(attribute, null))
            {
                return false;
            }

            return
                InstanceProviderType == attribute.InstanceProviderType &&
                InstanceProviderMethodName == attribute.InstanceProviderMethodName;
        }

        /// <summary>
        /// Return true if the given object is a GenerateDeserializableAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as GenerateDeserializerAttribute);


        /// <summary>
        /// Returns a stable hash code for this GenerateDeserializableAttribute.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(InstanceProviderType, InstanceProviderMethodName);

        /// <summary>
        /// Compare two GenerateDeserializableAttribute for equality
        /// </summary>
        public static bool operator ==(GenerateDeserializerAttribute a, GenerateDeserializerAttribute b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two GenerateDeserializableAttribute for inequality
        /// </summary>
        public static bool operator !=(GenerateDeserializerAttribute a, GenerateDeserializerAttribute b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this GenerateDeserializableAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(GenerateDeserializerAttribute)} with {nameof(InstanceProviderType)}={InstanceProviderType}, {nameof(InstanceProviderMethodName)}={InstanceProviderMethodName}";
    }
}
