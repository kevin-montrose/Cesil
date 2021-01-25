using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Automatically attached to generated methods depending on runtime behavior.
    /// 
    /// You should not use this directly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    [Obsolete("You should not use this directly, it may not be present in future versions.")]
    public sealed class ConstructorInstanceProviderAttribute : Attribute, IEquatable<ConstructorInstanceProviderAttribute>
    {
        /// <summary>
        /// Type that declares the constructor.
        /// </summary>
        public TypeInfo ForType { get; }

        /// <summary>
        /// Type of one of the parameter that corresponds to this attribute.
        /// </summary>
        public TypeInfo ParameterType { get; }
        /// <summary>
        /// Index of the parameter that corresponds to this attribute.
        /// </summary>
        public int ParameterIndex
        {
            [return: IntentionallyExposedPrimitive("Used in source generation, not part of the consumer contract")]
            get;
        }

        /// <summary>
        /// Create a new ConstructorInstanceProviderAttribute.
        /// 
        /// You should not use this directly.
        /// </summary>
        public ConstructorInstanceProviderAttribute(
            Type forType,
            Type parameterType,
            [IntentionallyExposedPrimitive("Used in source generation, not part of the consumer contract")]
            int index
        )
        {
            ForType = forType.GetTypeInfo();
            ParameterType = parameterType.GetTypeInfo();
            ParameterIndex = index;
        }

        /// <summary>
        /// Return true if the given object is a ConstructorInstanceProviderAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as ConstructorInstanceProviderAttribute);

        /// <summary>
        /// Returns true if the given ConstructorInstanceProviderAttribute is equal
        ///   to this one.
        /// </summary>
        public bool Equals(ConstructorInstanceProviderAttribute? attribute)
        {
            if (ReferenceEquals(attribute, null))
            {
                return false;
            }

            return
                attribute.ForType == ForType &&
                attribute.ParameterType == ParameterType &&
                attribute.ParameterIndex == ParameterIndex;
        }

        /// <summary>
        /// Returns a stable hash code for this ConstructorInstanceProviderAttribute.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(ForType, ParameterType, ParameterIndex);

        /// <summary>
        /// Compare two ConstructorInstanceProviderAttributes for equality
        /// </summary>
        public static bool operator ==(ConstructorInstanceProviderAttribute? a, ConstructorInstanceProviderAttribute? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two ConstructorInstanceProviderAttributes for inequality
        /// </summary>
        public static bool operator !=(ConstructorInstanceProviderAttribute? a, ConstructorInstanceProviderAttribute? b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this ConstructorInstanceProviderAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(ConstructorInstanceProviderAttribute)} with {nameof(ForType)}={ForType}, {nameof(ParameterType)}={ParameterType}, {nameof(ParameterIndex)}={ParameterIndex}";
    }
}
