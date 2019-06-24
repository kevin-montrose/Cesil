using System;
using System.Collections.Generic;
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
        internal ShouldSerialize ShouldSerialize { get; }

        internal bool EmitDefaultValue { get; }

        private SerializableMember(string name, Getter getter, Formatter formatter, ShouldSerialize shouldSerialize, bool emitDefaultValue)
        {
            Name = name;
            Getter = getter;
            Formatter = formatter;
            ShouldSerialize = shouldSerialize;
            EmitDefaultValue = emitDefaultValue;
        }

        /// <summary>
        /// Creates a SerializableMember for the given property.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop)
        => Create(prop?.DeclaringType?.GetTypeInfo(), prop?.Name, (Getter)prop?.GetMethod, Formatter.GetDefault(prop?.PropertyType?.GetTypeInfo()), null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name)
        => Create(prop?.DeclaringType?.GetTypeInfo(), name, (Getter)prop?.GetMethod, Formatter.GetDefault(prop?.PropertyType?.GetTypeInfo()), null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name and formatter.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name, Formatter formatter)
        => Create(prop?.DeclaringType?.GetTypeInfo(), name, (Getter)prop?.GetMethod, formatter, null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        => Create(prop?.DeclaringType?.GetTypeInfo(), name, (Getter)prop?.GetMethod, formatter, shouldSerialize, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        => Create(prop?.DeclaringType?.GetTypeInfo(), name, (Getter)prop?.GetMethod, formatter, shouldSerialize, emitDefaultValue);

        /// <summary>
        /// Creates a SerializableMember for the given field.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field)
        => Create(field?.DeclaringType?.GetTypeInfo(), field?.Name, (Getter)field, Formatter.GetDefault(field?.FieldType?.GetTypeInfo()), null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name)
        => Create(field?.DeclaringType?.GetTypeInfo(), name, (Getter)field, Formatter.GetDefault(field?.FieldType?.GetTypeInfo()), null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name and formatter.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, Formatter formatter)
        => Create(field?.DeclaringType?.GetTypeInfo(), name, (Getter)field, formatter, null, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        => Create(field?.DeclaringType?.GetTypeInfo(), name, (Getter)field, formatter, shouldSerialize, WillEmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        => Create(field?.DeclaringType?.GetTypeInfo(), name, (Getter)field, formatter, shouldSerialize, emitDefaultValue);

        /// <summary>
        /// Create a SerializableMember with an explicit type being serialized, name, getter, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember Create(TypeInfo beingSerializedType, string name, Getter getter, Formatter formatter, ShouldSerialize shouldSerialize, WillEmitDefaultValue emitDefaultValue)
        {
            if (beingSerializedType == null)
            {
                Throw.ArgumentNullException(nameof(beingSerializedType));
            }

            if (name == null)
            {
                Throw.ArgumentNullException(nameof(name));
            }

            if (getter == null)
            {
                Throw.ArgumentNullException(nameof(getter));
            }

            if (formatter == null)
            {
                Throw.ArgumentNullException(nameof(formatter));
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
                    Throw.InvalidOperationException($"Unexpected {nameof(WillEmitDefaultValue)}: {emitDefaultValue}");
                    // just for control flow
                    return default;
            }

            var toSerializeType = getter.Returns;

            if (!formatter.Takes.IsAssignableFrom(toSerializeType))
            {
                Throw.ArgumentException($"The first paramater to {nameof(formatter)} must be accept a {toSerializeType}", nameof(formatter));
            }

            var shouldSerializeOnType = getter.RowType;

            CheckShouldSerializeMethod(shouldSerialize, shouldSerializeOnType);

            return new SerializableMember(name, getter, formatter, shouldSerialize, emitDefaultValueBool);
        }

        private static void CheckShouldSerializeMethod(ShouldSerialize shouldSerialize, TypeInfo onType)
        {
            if (shouldSerialize == null) return;

            if (shouldSerialize.Takes != null)
            {
                var shouldSerializeInstType = shouldSerialize.Takes;

                var isInstOrSubclass = onType.IsAssignableFrom(shouldSerializeInstType);

                if (!isInstOrSubclass)
                {
                    Throw.ArgumentException($"{nameof(shouldSerialize)} be either static method, an instance method on the type declaring the field or property, or a delegate taking the type declaring the field or property", nameof(shouldSerialize));
                }
            }
        }

        /// <summary>
        /// Returns true if this object equals the given SerializableMember.
        /// </summary>
        public override bool Equals(object obj)
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
            if (s == null) return false;

            return
                s.EmitDefaultValue == EmitDefaultValue &&
                s.Formatter == Formatter &&
                s.Getter == Getter &&
                s.Name == Name &&
                s.ShouldSerialize == ShouldSerialize;
        }

        /// <summary>
        /// Returns a stable hash for this SerializableMember.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(SerializableMember), EmitDefaultValue, Formatter, Getter, Name, ShouldSerialize);

        /// <summary>
        /// Describes this SerializableMember.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        => $"{nameof(Name)}: {Name}\r\n{nameof(Getter)}: {Getter}\r\n{nameof(Formatter)}: {Formatter}\r\n{nameof(ShouldSerialize)}: {ShouldSerialize}";

        /// <summary>
        /// Compare two SerializableMembers for equality
        /// </summary>
        public static bool operator ==(SerializableMember a, SerializableMember b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two SerializableMembers for inequality
        /// </summary>
        public static bool operator !=(SerializableMember a, SerializableMember b)
        => !(a == b);
    }
}
