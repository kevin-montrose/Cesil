using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Whether or not a member is required during deserialization.
    /// </summary>
    public enum IsMemberRequired : byte
    {
        /// <summary>
        /// A member must be present, it is
        /// an error to omit it.
        /// </summary>
        Yes = 1,

        /// <summary>
        /// A member does not have to be present,
        /// it is not an error if it is omitted.
        /// </summary>
        No = 2
    }

    /// <summary>
    /// Represents a member of a type to use when deserializing.
    /// </summary>
    public sealed class DeserializableMember : IEquatable<DeserializableMember>
    {
        /// <summary>
        /// The name of the column that maps to this member.
        /// </summary>
        public string Name { get; }

        internal Setter Setter { get; }

        internal Parser Parser { get; }

        internal bool IsRequired { get; }

        internal readonly NonNull<Reset> Reset;

        private DeserializableMember(
            string name,
            Setter setter,
            Parser parser,
            bool isRequired,
            Reset? reset
        )
        {
            Name = name;
            Setter = setter;
            Parser = parser;
            IsRequired = isRequired;
            Reset.SetAllowNull(reset);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given property.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property)
        {
            var propType = property?.PropertyType.GetTypeInfo();
            var parser = propType != null ? Parser.GetDefault(propType) : null;
            return CreateInner(property?.DeclaringType?.GetTypeInfo(), property?.Name, (Setter?)property?.SetMethod, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name)
        {
            var propType = property?.PropertyType.GetTypeInfo();
            var parser = propType != null ? Parser.GetDefault(propType) : null;
            return CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Setter?)property?.SetMethod, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name and parser.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name, Parser parser)
        => CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Setter?)property?.SetMethod, parser, IsMemberRequired.No, null);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, and whether it is required.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name, Parser parser, IsMemberRequired isRequired)
        => CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Setter?)property?.SetMethod, parser, isRequired, null);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, whether it is required, and a reset method.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name, Parser parser, IsMemberRequired isRequired, Reset reset)
        => CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Setter?)property?.SetMethod, parser, isRequired, reset);

        /// <summary>
        /// Creates a DeserializableMember for the given field.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field)
        {
            var fieldType = field?.FieldType.GetTypeInfo();
            var parser = fieldType != null ? Parser.GetDefault(fieldType) : null;
            return CreateInner(field?.DeclaringType?.GetTypeInfo(), field?.Name, (Setter?)field, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given field, with the given name.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name)
        {
            var fieldType = field?.FieldType.GetTypeInfo();
            var parser = fieldType != null ? Parser.GetDefault(fieldType) : null;
            return CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Setter?)field, parser, IsMemberRequired.No, null);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given field, with the given name and parser.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name, Parser parser)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Setter?)field, parser, IsMemberRequired.No, null);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, and whether it is required.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name, Parser parser, IsMemberRequired isRequired)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Setter?)field, parser, isRequired, null);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, whether it is required, and a reset method.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name, Parser parser, IsMemberRequired isRequired, Reset reset)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Setter?)field, parser, isRequired, reset);

        /// <summary>
        /// Create a Deserializable member with an explicit name, setter, parser, whether it is required, and a reset method.
        /// </summary>
        public static DeserializableMember Create(TypeInfo beingDeserializedType, string name, Setter setter, Parser parser, IsMemberRequired isRequired, Reset? reset)
        => CreateInner(beingDeserializedType, name, setter, parser, isRequired, reset);

        internal static DeserializableMember CreateInner(TypeInfo? beingDeserializedType, string? name, Setter? setter, Parser? parser, IsMemberRequired isRequired, Reset? reset)
        {
            if (beingDeserializedType == null)
            {
                return Throw.ArgumentNullException<DeserializableMember>(nameof(beingDeserializedType));
            }

            if (name == null)
            {
                return Throw.ArgumentNullException<DeserializableMember>(nameof(name));
            }

            if (setter == null)
            {
                return Throw.ArgumentNullException<DeserializableMember>(nameof(setter));
            }

            if (parser == null)
            {
                return Throw.ArgumentNullException<DeserializableMember>(nameof(parser));
            }

            if (name.Length == 0)
            {
                return Throw.ArgumentException<DeserializableMember>($"{nameof(name)} must be at least 1 character long", nameof(name));
            }

            bool isRequiredBool;

            switch (isRequired)
            {
                case IsMemberRequired.Yes:
                    isRequiredBool = true;
                    break;
                case IsMemberRequired.No:
                    isRequiredBool = false;
                    break;
                default:
                    return Throw.ArgumentException<DeserializableMember>($"Unexpected {nameof(IsMemberRequired)}: {isRequired}", nameof(isRequired));
            }

            var valueType = setter.Takes;

            if (!valueType.IsAssignableFrom(parser.Creates))
            {
                return Throw.ArgumentException<DeserializableMember>($"Provided {nameof(Parser)} creates a {parser.Creates}, which cannot be passed to {setter} which expectes a {valueType}", nameof(setter));
            }

            if (reset != null && reset.RowType.HasValue)
            {
                if (!reset.RowType.Value.IsAssignableFrom(beingDeserializedType))
                {
                    return Throw.ArgumentException<DeserializableMember>($"{nameof(reset)} must be callable on {beingDeserializedType}", nameof(reset));
                }
            }

            return new DeserializableMember(name, setter, parser, isRequiredBool, reset);
        }

        /// <summary>
        /// Returns true if this object equals the given DeserializableMember.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is DeserializableMember d)
            {
                return Equals(d);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given DeserializableMember.
        /// </summary>
        public bool Equals(DeserializableMember d)
        {
            if (ReferenceEquals(d, null)) return false;

            if (Reset.HasValue)
            {
                if (!d.Reset.HasValue) return false;

                if (Reset.Value != d.Reset.Value) return false;
            }
            else
            {
                if (d.Reset.HasValue) return false;
            }

            return
                d.IsRequired == IsRequired &&
                d.Name == Name &&
                d.Parser == Parser &&
                d.Setter == Setter;
        }

        /// <summary>
        /// Returns a stable hash for this DeserializableMember.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(DeserializableMember), IsRequired, Name, Parser, Reset, Setter);

        /// <summary>
        /// Describes this DeserializableMember.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        => $"{nameof(DeserializableMember)} with {nameof(Name)}: {Name}\r\n{nameof(Setter)}: {Setter}\r\n{Parser}\r\n{nameof(IsRequired)}: {IsRequired}\r\n{nameof(Reset)}: {Reset}";

        /// <summary>
        /// Compare two DeserializableMembers for equality
        /// </summary>
        public static bool operator ==(DeserializableMember? a, DeserializableMember? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two DeserializableMembers for inequality
        /// </summary>
        public static bool operator !=(DeserializableMember? a, DeserializableMember? b)
        => !(a == b);
    }
}
