using System;
using System.Reflection;

namespace Cesil
{
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

        internal readonly NonNull<TypeInfo> AheadOfTimeGeneratedType;

        internal bool IsBackedByGeneratedMethod => AheadOfTimeGeneratedType.HasValue;

        private DeserializableMember(
            string name,
            Setter setter,
            Parser parser,
            bool isRequired,
            Reset? reset,
            TypeInfo? aheadOfTimeGeneratedType
        )
        {
            Name = name;
            Setter = setter;
            Parser = parser;
            IsRequired = isRequired;
            Reset.SetAllowNull(reset);
            AheadOfTimeGeneratedType.SetAllowNull(aheadOfTimeGeneratedType);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given property.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property)
        {
            var propType = property?.PropertyType.GetTypeInfo();
            var parser = propType != null ? Parser.GetDefault(propType) : null;
            return CreateInner(property?.DeclaringType?.GetTypeInfo(), property?.Name, (Setter?)property?.SetMethod, parser, MemberRequired.No, null, null);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name)
        {
            var propType = property?.PropertyType.GetTypeInfo();
            var parser = propType != null ? Parser.GetDefault(propType) : null;
            return CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Setter?)property?.SetMethod, parser, MemberRequired.No, null, null);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name and parser.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name, Parser parser)
        => CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Setter?)property?.SetMethod, parser, MemberRequired.No, null, null);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, and whether it is required.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name, Parser parser, MemberRequired required)
        => CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Setter?)property?.SetMethod, parser, required, null, null);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, whether it is required, and a reset method.
        /// </summary>
        public static DeserializableMember ForProperty(PropertyInfo property, string name, Parser parser, MemberRequired required, Reset reset)
        => CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Setter?)property?.SetMethod, parser, required, reset, null);

        /// <summary>
        /// Creates a DeserializableMember for the given field.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field)
        {
            var fieldType = field?.FieldType.GetTypeInfo();
            var parser = fieldType != null ? Parser.GetDefault(fieldType) : null;
            return CreateInner(field?.DeclaringType?.GetTypeInfo(), field?.Name, (Setter?)field, parser, MemberRequired.No, null, null);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given field, with the given name.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name)
        {
            var fieldType = field?.FieldType.GetTypeInfo();
            var parser = fieldType != null ? Parser.GetDefault(fieldType) : null;
            return CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Setter?)field, parser, MemberRequired.No, null, null);
        }

        /// <summary>
        /// Creates a DeserializableMember for the given field, with the given name and parser.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name, Parser parser)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Setter?)field, parser, MemberRequired.No, null, null);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, and whether it is required.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name, Parser parser, MemberRequired required)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Setter?)field, parser, required, null, null);

        /// <summary>
        /// Creates a DeserializableMember for the given property, with the given name, parser, whether it is required, and a reset method.
        /// </summary>
        public static DeserializableMember ForField(FieldInfo field, string name, Parser parser, MemberRequired required, Reset reset)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Setter?)field, parser, required, reset, null);

        /// <summary>
        /// Create a DeserializableMember with an explicit type being serialized, name, setter, parser, whether it is required, and a reset method.
        /// </summary>
        public static DeserializableMember Create(
            TypeInfo forType,
            string name,
            Setter setter,
            Parser parser,
            MemberRequired required,
            [NullableExposed("Reset is truly optional here, it's required and validated elsewhere")]
            Reset? reset
        )
        => CreateInner(forType, name, setter, parser, required, reset, null);

        internal static DeserializableMember CreateInner(
            TypeInfo? beingDeserializedType, 
            string? name, 
            Setter? setter, 
            Parser? parser, 
            MemberRequired isRequired, 
            Reset? reset,
            TypeInfo? aheadOfTimeGeneratedType
        )
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
                case MemberRequired.Yes:
                    isRequiredBool = true;
                    break;
                case MemberRequired.No:
                    isRequiredBool = false;
                    break;
                default:
                    return Throw.ArgumentException<DeserializableMember>($"Unexpected {nameof(MemberRequired)}: {isRequired}", nameof(isRequired));
            }

            var valueType = setter.Takes;

            if (!valueType.IsAssignableFrom(parser.Creates))
            {
                return Throw.ArgumentException<DeserializableMember>($"Provided {nameof(Parser)} creates a {parser.Creates}, which cannot be passed to {setter} which expects a {valueType}", nameof(setter));
            }

            if (reset != null && reset.RowType.HasValue)
            {
                if (!reset.RowType.Value.IsAssignableFrom(beingDeserializedType))
                {
                    return Throw.ArgumentException<DeserializableMember>($"{nameof(reset)} must be callable on {beingDeserializedType}", nameof(reset));
                }
            }

            if (setter.Mode == BackingMode.ConstructorParameter && !isRequiredBool)
            {
                return Throw.InvalidOperationException<DeserializableMember>($"{nameof(Setter)} that is backed by a constructor parameter can only be used with {nameof(MemberRequired)}.{nameof(MemberRequired.Yes)}; {nameof(setter)} was {setter}");
            }

            return new DeserializableMember(name, setter, parser, isRequiredBool, reset, aheadOfTimeGeneratedType);
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
        public bool Equals(DeserializableMember? deserializableMember)
        {
            if (ReferenceEquals(deserializableMember, null)) return false;

            if (Reset.HasValue)
            {
                if (!deserializableMember.Reset.HasValue) return false;

                if (Reset.Value != deserializableMember.Reset.Value) return false;
            }
            else
            {
                if (deserializableMember.Reset.HasValue) return false;
            }

            if (AheadOfTimeGeneratedType.HasValue)
            {
                if (!deserializableMember.AheadOfTimeGeneratedType.HasValue) return false;

                if (AheadOfTimeGeneratedType.Value != deserializableMember.AheadOfTimeGeneratedType.Value) return false;
            }
            else
            {
                if (deserializableMember.AheadOfTimeGeneratedType.HasValue) return false;
            }

            return
                deserializableMember.IsRequired == IsRequired &&
                deserializableMember.Name == Name &&
                deserializableMember.Parser == Parser &&
                deserializableMember.Setter == Setter;
        }

        /// <summary>
        /// Returns a stable hash for this DeserializableMember.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(DeserializableMember), IsRequired, Name, Parser, Reset, Setter, AheadOfTimeGeneratedType);

        /// <summary>
        /// Describes this DeserializableMember.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        => $"{nameof(DeserializableMember)} with {nameof(Name)}: {Name}\r\n{nameof(Setter)}: {Setter}\r\n{Parser}\r\n{nameof(IsRequired)}: {IsRequired}\r\n{nameof(Reset)}: {Reset}\r\n{nameof(AheadOfTimeGeneratedType)}: {AheadOfTimeGeneratedType}";

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
