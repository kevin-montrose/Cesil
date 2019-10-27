using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Whether or not the default value for a member will be serialized.
    /// </summary>
    public enum WillEmitDefaultValue : byte
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
    /// Represents a member of a type to use when serializing.
    /// </summary>
    public sealed class SerializableMember : IEquatable<SerializableMember>
    {
        /// <summary>
        /// The name of the column that maps to this member.
        /// </summary>
        public string Name { get; }

        internal Getter Getter { get; }
        internal Formatter Formatter { get; }

        private ShouldSerialize? _ShouldSerialize;
        internal bool HasShouldSerialize => _ShouldSerialize != null;
        internal ShouldSerialize ShouldSerialize => Utils.NonNull(_ShouldSerialize);

        internal bool EmitDefaultValue { get; }

        private SerializableMember(string name, Getter getter, Formatter formatter, ShouldSerialize? shouldSerialize, bool emitDefaultValue)
        {
            Name = name;
            Getter = getter;
            Formatter = formatter;
            _ShouldSerialize = shouldSerialize;
            EmitDefaultValue = emitDefaultValue;
        }

        /// <summary>
        /// Creates a SerializableMember for the given property.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop)
        {
            var propType = prop?.PropertyType;
            var formatter = propType != null ? Formatter.GetDefault(propType.GetTypeInfo()) : null;
            return CreateInner(prop?.DeclaringType?.GetTypeInfo(), prop?.Name, (Getter?)prop?.GetMethod, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name)
        {
            var propType = prop?.PropertyType;
            var formatter = propType != null ? Formatter.GetDefault(propType.GetTypeInfo()) : null;
            return CreateInner(prop?.DeclaringType?.GetTypeInfo(), name, (Getter?)prop?.GetMethod, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name and formatter.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name, Formatter formatter)
        => CreateInner(prop?.DeclaringType?.GetTypeInfo(), name, (Getter?)prop?.GetMethod, formatter, null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        => CreateInner(prop?.DeclaringType?.GetTypeInfo(), name, (Getter?)prop?.GetMethod, formatter, shouldSerialize, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        => CreateInner(prop?.DeclaringType?.GetTypeInfo(), name, (Getter?)prop?.GetMethod, formatter, shouldSerialize, emitDefaultValue);

        /// <summary>
        /// Creates a SerializableMember for the given field.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field)
        {
            var fieldType = field?.FieldType;
            var formatter = fieldType != null ? Formatter.GetDefault(fieldType.GetTypeInfo()) : null;
            return CreateInner(field?.DeclaringType?.GetTypeInfo(), field?.Name, (Getter?)field, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name)
        {
            var fieldType = field?.FieldType;
            var formatter = fieldType != null ? Formatter.GetDefault(fieldType.GetTypeInfo()) : null;
            return CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Getter?)field, formatter, null, WillEmitDefaultValue.Yes);
        }

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name and formatter.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, Formatter formatter)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Getter?)field, formatter, null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Getter?)field, formatter, shouldSerialize, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Getter?)field, formatter, shouldSerialize, emitDefaultValue);

        /// <summary>
        /// Create a SerializableMember with an explicit type being serialized, name, getter, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember Create(TypeInfo beingSerializedType, string name, Getter getter, Formatter formatter, ShouldSerialize? shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        => CreateInner(beingSerializedType, name, getter, formatter, shouldSerialize, emitDefaultValue);

        internal static SerializableMember CreateInner(TypeInfo? beingSerializedType, string? name, Getter? getter, Formatter? formatter, ShouldSerialize? shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        {
            if (beingSerializedType == null)
            {
                return Throw.ArgumentNullException<SerializableMember>(nameof(beingSerializedType));
            }

            if (name == null)
            {
                return Throw.ArgumentNullException<SerializableMember>(nameof(name));
            }

            if (getter == null)
            {
                return Throw.ArgumentNullException<SerializableMember>(nameof(getter));
            }

            if (formatter == null)
            {
                return Throw.ArgumentNullException<SerializableMember>(nameof(formatter));
            }

            bool emitDefaultValueBool;
            switch (emitDefaultValue)
            {
                case WillEmitDefaultValue.Yes:
                    emitDefaultValueBool = true;
                    break;
                case WillEmitDefaultValue.No:
                    emitDefaultValueBool = false;
                    break;
                default:
                    return Throw.InvalidOperationException<SerializableMember>($"Unexpected {nameof(WillEmitDefaultValue)}: {emitDefaultValue}");
            }

            var toSerializeType = getter.Returns;

            if (!formatter.Takes.IsAssignableFrom(toSerializeType))
            {
                return Throw.ArgumentException<SerializableMember>($"The first paramater to {nameof(formatter)} must be accept a {toSerializeType}", nameof(formatter));
            }

            var shouldSerializeOnType = getter.HasRowType ? getter.RowType : null;

            CheckShouldSerializeMethod(shouldSerialize, shouldSerializeOnType);

            return new SerializableMember(name, getter, formatter, shouldSerialize, emitDefaultValueBool);
        }

        private static void CheckShouldSerializeMethod(ShouldSerialize? shouldSerialize, TypeInfo? onTypeNull)
        {
            if (shouldSerialize == null) return;

            if (shouldSerialize.HasTakes)
            {
                var shouldSerializeInstType = shouldSerialize.Takes;

                var onType = Utils.NonNull(onTypeNull);

                var isInstOrSubclass = onType.IsAssignableFrom(shouldSerializeInstType);

                if (!isInstOrSubclass)
                {
                    Throw.ArgumentException<object>($"{nameof(shouldSerialize)} be either static method, an instance method on the type declaring the field or property, or a delegate taking the type declaring the field or property", nameof(shouldSerialize));
                }
            }
        }

        /// <summary>
        /// Returns true if this object equals the given SerializableMember.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is SerializableMember s)
            {
                return Equals(s);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given SerializableMember.
        /// </summary>
        public bool Equals(SerializableMember s)
        {
            if (ReferenceEquals(s, null)) return false;

            if (HasShouldSerialize)
            {
                if (!s.HasShouldSerialize) return false;

                if (ShouldSerialize != s.ShouldSerialize) return false;
            }
            else
            {
                if (s.HasShouldSerialize) return false;
            }

            return
                s.EmitDefaultValue == EmitDefaultValue &&
                s.Formatter == Formatter &&
                s.Getter == Getter &&
                s.Name == Name;
        }

        /// <summary>
        /// Returns a stable hash for this SerializableMember.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(SerializableMember), EmitDefaultValue, Formatter, Getter, Name, _ShouldSerialize);

        /// <summary>
        /// Describes this SerializableMember.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        => $"{nameof(SerializableMember)} with {nameof(Name)}: {Name}\r\n{nameof(Getter)}: {Getter}\r\n{nameof(Formatter)}: {Formatter}\r\n{nameof(ShouldSerialize)}: {_ShouldSerialize}";

        /// <summary>
        /// Compare two SerializableMembers for equality
        /// </summary>
        public static bool operator ==(SerializableMember? a, SerializableMember? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two SerializableMembers for inequality
        /// </summary>
        public static bool operator !=(SerializableMember? a, SerializableMember? b)
        => !(a == b);
    }
}
