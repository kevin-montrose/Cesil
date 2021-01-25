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
    public sealed class SetterBackedByConstructorParameterAttribute : Attribute, IEquatable<SetterBackedByConstructorParameterAttribute>
    {
        /// <summary>
        /// Index of parameter in InstanceProvider constructor that the annotated method's column corresponds to.
        /// </summary>
        public int Index
        {
            [return: IntentionallyExposedPrimitive("Used in source generation, not part of the consumer contract")]
            get;
        }

        /// <summary>
        /// Create a new SetterBackedByConstructorParameterAttribute.
        /// 
        /// You should not use this directly.
        /// </summary>
        public SetterBackedByConstructorParameterAttribute(
            [IntentionallyExposedPrimitive("Used in source generation, not part of the consumer contract")]
            int index
        )
        {
            Index = index;
        }

        /// <summary>
        /// Return true if the given object is a SetterBackedByConstructorParameterAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as SetterBackedByConstructorParameterAttribute);

        /// <summary>
        /// Returns true if the given SetterBackedByConstructorParameterAttribute is equal
        ///   to this one.
        /// </summary>
        public bool Equals(SetterBackedByConstructorParameterAttribute? attribute)
        {
            if (ReferenceEquals(attribute, null))
            {
                return false;
            }

            return attribute.Index == Index;
        }

        /// <summary>
        /// Returns a stable hash code for this SetterBackedByConstructorParameterAttribute.
        /// </summary>
        public override int GetHashCode()
        => Index;

        /// <summary>
        /// Compare two SetterBackedByConstructorParameterAttributes for equality
        /// </summary>
        public static bool operator ==(SetterBackedByConstructorParameterAttribute? a, SetterBackedByConstructorParameterAttribute? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two SetterBackedByConstructorParameterAttributes for inequality
        /// </summary>
        public static bool operator !=(SetterBackedByConstructorParameterAttribute? a, SetterBackedByConstructorParameterAttribute? b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this SetterBackedByConstructorParameterAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(SetterBackedByConstructorParameterAttribute)} with {nameof(Index)}={Index}";
    }
}
