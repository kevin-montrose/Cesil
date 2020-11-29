using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Automatically attached to generated types to track compatibility.
    /// 
    /// You should not use this directly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    [Obsolete("You should not use this directly, it may not be present in future versions.")]
    public sealed class GeneratedSourceVersionAttribute : Attribute, IEquatable<GeneratedSourceVersionAttribute>
    {
        /// <summary>
        /// Used by GeneratedSourceVersionAttribute.
        /// 
        /// You should not use this directly.
        /// </summary>
        public enum GeneratedTypeKind : byte
        {
            /// <summary>
            /// You should not use this directly.
            /// </summary>
            None = 0,
            /// <summary>
            /// You should not use this directly.
            /// </summary>
            Serializer = 1,
            /// <summary>
            /// You should not use this directly.
            /// </summary>
            Deserializer = 2,
        }

        /// <summary>
        /// The version of Cesil the associated source was generated for.
        /// 
        /// You should not use this directly.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// The type associated with generated code.
        /// 
        /// You should not use this directly.
        /// </summary>
        public TypeInfo ForType { get; }

        /// <summary>
        /// The kind of operation this type is intended to support.
        /// 
        /// You should not use this directly.
        /// </summary>
        public GeneratedTypeKind Kind { get; }

        /// <summary>
        /// Construct a new GeneratedSourceVersionAttribute.
        /// </summary>
        public GeneratedSourceVersionAttribute(
            string version,
            Type forType,
            GeneratedTypeKind kind
        )
        {
            Version = version;
            ForType = forType.GetTypeInfo();
            Kind = kind;
        }

        /// <summary>
        /// Returns true if the given GeneratedSourceVersionAttribute is equal
        /// to this one.
        /// </summary>
        public bool Equals(GeneratedSourceVersionAttribute? attribute)
        {
            if (ReferenceEquals(attribute, null))
            {
                return false;
            }

            return
                attribute.Version == Version &&
                attribute.ForType == ForType &&
                attribute.Kind == Kind;
        }

        /// <summary>
        /// Return true if the given object is a GeneratedSourceVersionAttribute 
        ///   equal to this one.
        /// </summary>
        public override bool Equals(object? obj)
        => Equals(obj as GeneratedSourceVersionAttribute);


        /// <summary>
        /// Returns a stable hash code for this GeneratedSourceVersionAttribute.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(Version, ForType, Kind);

        /// <summary>
        /// Compare two GeneratedSourceVersionAttributes for equality
        /// </summary>
        public static bool operator ==(GeneratedSourceVersionAttribute a, GeneratedSourceVersionAttribute b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two GeneratedSourceVersionAttributes for inequality
        /// </summary>
        public static bool operator !=(GeneratedSourceVersionAttribute a, GeneratedSourceVersionAttribute b)
        => !(a == b);

        /// <summary>
        /// Returns a representation of this GeneratedSourceVersionAttribute object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(GeneratedSourceVersionAttribute)} with {nameof(Version)}={Version}, {nameof(ForType)}={ForType}, {nameof(Kind)}={Kind}";
    }
}
