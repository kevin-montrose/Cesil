using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Automatically attached to generated methods depending on runtime behavior.
    /// 
    /// You should not use this directly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    [Obsolete("You should not use this directly, it may not be present in future versions.")]
    public sealed class SetterBackedByInitOnlyPropertyAttribute : Attribute, IEquatable<SetterBackedByInitOnlyPropertyAttribute>
    {
        /// <summary>
        /// Property annotated method corresponds to.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// BindingFlags for property.
        /// </summary>
        public BindingFlags BindingFlags { get; }

        /// <summary>
        /// Create a new SetterBackedByInitOnlyPropertyAttribute.
        /// 
        /// You should not use this directly.
        /// </summary>
        public SetterBackedByInitOnlyPropertyAttribute(string propertyName, BindingFlags bindingFlags)
        {
            PropertyName = propertyName;
            BindingFlags = bindingFlags;
        }

        /// <summary>
        /// Return true if the given object is a SetterBackedByInitOnlyPropertyAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as SetterBackedByInitOnlyPropertyAttribute);

        /// <summary>
        /// Returns true if the given SetterBackedByInitOnlyPropertyAttribute is equal
        ///   to this one.
        /// </summary>
        public bool Equals(SetterBackedByInitOnlyPropertyAttribute? attribute)
        {
            if (ReferenceEquals(attribute, null))
            {
                return false;
            }

            return
                attribute.BindingFlags == BindingFlags &&
                attribute.PropertyName == PropertyName;
        }

        /// <summary>
        /// Returns a stable hash code for this SetterBackedByInitOnlyPropertyAttribute.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(BindingFlags, PropertyName);

        /// <summary>
        /// Compare two SetterBackedByInitOnlyPropertyAttribute for equality
        /// </summary>
        public static bool operator ==(SetterBackedByInitOnlyPropertyAttribute? a, SetterBackedByInitOnlyPropertyAttribute? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two SetterBackedByInitOnlyPropertyAttribute for inequality
        /// </summary>
        public static bool operator !=(SetterBackedByInitOnlyPropertyAttribute? a, SetterBackedByInitOnlyPropertyAttribute? b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this SetterBackedByInitOnlyPropertyAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(SetterBackedByInitOnlyPropertyAttribute)} with {nameof(PropertyName)}={PropertyName}, {nameof(BindingFlags)}={BindingFlags}";
    }
}
