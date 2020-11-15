using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Represents a member of a type to use when serializing.
    /// </summary>
    public sealed class SerializableMember : IEquatable<SerializableMember>
    {
        /// <summary>
        /// The name of the column that maps to this member.
        /// </summary>
        public string Name { get; }

        internal bool IsBackedByGeneratedMethod => GeneratedMethod.HasValue;

        internal Getter Getter;
        internal Formatter Formatter;

        internal NonNull<ShouldSerialize> ShouldSerialize;

        internal bool EmitDefaultValue;

        internal NonNull<MethodInfo> GeneratedMethod;

        private SerializableMember(string name, Getter getter, Formatter formatter, ShouldSerialize? shouldSerialize, bool emitDefault)
        {
            Name = name;
            Getter = getter;
            Formatter = formatter;
            ShouldSerialize.SetAllowNull(shouldSerialize);
            EmitDefaultValue = emitDefault;

            GeneratedMethod.Clear();
        }

        private SerializableMember(string name, MethodInfo generatedMethod, Getter getter, Formatter formatter, ShouldSerialize? shouldSerialize, bool emitDefault)
        {
            Name = name;
            GeneratedMethod.Value = generatedMethod;
            Getter = getter;
            Formatter = formatter;
            ShouldSerialize.SetAllowNull(shouldSerialize);
            EmitDefaultValue = emitDefault;
        }

        /// <summary>
        /// Creates a SerializableMember for the given property.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo property)
        {
            var propType = property?.PropertyType;
            var formatter = propType != null ? Cesil.Formatter.GetDefault(propType.GetTypeInfo()) : null;
            return CreateInner(property?.DeclaringType?.GetTypeInfo(), property?.Name, (Getter?)property?.GetMethod, formatter, null, Cesil.EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo property, string name)
        {
            var propType = property?.PropertyType;
            var formatter = propType != null ? Cesil.Formatter.GetDefault(propType.GetTypeInfo()) : null;
            return CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Getter?)property?.GetMethod, formatter, null, Cesil.EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name and formatter.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo property, string name, Formatter formatter)
        => CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Getter?)property?.GetMethod, formatter, null, Cesil.EmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo property, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        => CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Getter?)property?.GetMethod, formatter, shouldSerialize, Cesil.EmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo property, string name, Formatter formatter, ShouldSerialize shouldSerialize, EmitDefaultValue emitDefault)
        => CreateInner(property?.DeclaringType?.GetTypeInfo(), name, (Getter?)property?.GetMethod, formatter, shouldSerialize, emitDefault);

        /// <summary>
        /// Creates a SerializableMember for the given field.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field)
        {
            var fieldType = field?.FieldType;
            var formatter = fieldType != null ? Cesil.Formatter.GetDefault(fieldType.GetTypeInfo()) : null;
            return CreateInner(field?.DeclaringType?.GetTypeInfo(), field?.Name, (Getter?)field, formatter, null, Cesil.EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name)
        {
            var fieldType = field?.FieldType;
            var formatter = fieldType != null ? Cesil.Formatter.GetDefault(fieldType.GetTypeInfo()) : null;
            return CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Getter?)field, formatter, null, Cesil.EmitDefaultValue.Yes);
        }

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name and formatter.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, Formatter formatter)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Getter?)field, formatter, null, Cesil.EmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Getter?)field, formatter, shouldSerialize, Cesil.EmitDefaultValue.Yes);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, Formatter formatter, ShouldSerialize shouldSerialize, EmitDefaultValue emitDefault)
        => CreateInner(field?.DeclaringType?.GetTypeInfo(), name, (Getter?)field, formatter, shouldSerialize, emitDefault);

        /// <summary>
        /// Create a SerializableMember with an explicit type being serialized, name, getter, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember Create(
            TypeInfo forType,
            string name,
            Getter getter,
            Formatter formatter,
            [NullableExposed("ShouldSerialize is truly optional here, it's required and validated elsewhere")]
            ShouldSerialize? shouldSerialize,
            EmitDefaultValue emitDefault
        )
        => CreateInner(forType, name, getter, formatter, shouldSerialize, emitDefault);

        internal static SerializableMember CreateInner(TypeInfo? beingSerializedType, string? name, Getter? getter, Formatter? formatter, ShouldSerialize? shouldSerialize, EmitDefaultValue emitDefault)
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
            switch (emitDefault)
            {
                case Cesil.EmitDefaultValue.Yes:
                    emitDefaultValueBool = true;
                    break;
                case Cesil.EmitDefaultValue.No:
                    emitDefaultValueBool = false;
                    break;
                default:
                    return Throw.InvalidOperationException<SerializableMember>($"Unexpected {nameof(Cesil.EmitDefaultValue)}: {emitDefault}");
            }

            var toSerializeType = getter.Returns;

            if (!formatter.Takes.IsAssignableFrom(toSerializeType))
            {
                return Throw.ArgumentException<SerializableMember>($"The first parameter to {nameof(formatter)} must accept a {toSerializeType}", nameof(formatter));
            }

            CheckShouldSerializeMethod(shouldSerialize, getter.RowType);

            return new SerializableMember(name, getter, formatter, shouldSerialize, emitDefaultValueBool);
        }

        internal static SerializableMember ForGeneratedMethod(string name, MethodInfo generated, Getter getter, Formatter formatter, ShouldSerialize? shouldSerialize, bool emitDefaultValue)
        {
            if (name == null)
            {
                return Throw.ArgumentNullException<SerializableMember>(nameof(name));
            }

            if (generated == null)
            {
                return Throw.ArgumentNullException<SerializableMember>(nameof(generated));
            }

            if(getter == null)
            {
                return Throw.ArgumentNullException<SerializableMember>(nameof(getter));
            }

            if (formatter == null)
            {
                return Throw.ArgumentNullException<SerializableMember>(nameof(formatter));
            }

            if (shouldSerialize == null)
            {
                return Throw.ArgumentNullException<SerializableMember>(nameof(shouldSerialize));
            }

            if (!generated.IsPublic)
            {
                return Throw.ArgumentException<SerializableMember>(nameof(generated), "Generated method should be public, but wasn't");
            }

            if (!generated.IsStatic)
            {
                return Throw.ArgumentException<SerializableMember>(nameof(generated), "Generated method should be static, but wasn't");
            }

            if (generated.ReturnType != Types.Bool)
            {
                return Throw.ArgumentException<SerializableMember>(nameof(generated), "Generated method should return bool, but doesn't");
            }

            var ps = generated.GetParameters();
            if (ps.Length != 3)
            {
                return Throw.ArgumentException<SerializableMember>(nameof(generated), "Generated method should take 3 parameters, but doesn't");
            }

            var p0 = ps[0].ParameterType.GetTypeInfo();

            if (p0 != Types.Object)
            {
                return Throw.ArgumentException<SerializableMember>(nameof(generated), $"Generated method's first parameter should be object, but was {p0}");
            }

            var p1 = ps[1];
            if (!p1.IsWriteContextByRef(out var error))
            {
                return Throw.ArgumentException<SerializableMember>(nameof(generated), $"Generated method's second parameter should be in WriteContext; {error}");
            }

            var p2 = ps[2].ParameterType.GetTypeInfo();
            if (p2 != Types.IBufferWriterOfChar)
            {
                return Throw.ArgumentException<SerializableMember>(nameof(generated), $"Generated method's third parameter should be IBufferWriter<char>, but was {p2}");
            }

            return new SerializableMember(name, generated, getter, formatter, shouldSerialize, emitDefaultValue);
        }

        private static void CheckShouldSerializeMethod(ShouldSerialize? shouldSerialize, NonNull<TypeInfo> onTypeNull)
        {
            if (shouldSerialize == null) return;

            if (shouldSerialize.Takes.HasValue)
            {
                var shouldSerializeInstType = shouldSerialize.Takes.Value;

                var onType = onTypeNull.Value;

                var isInstOrSubclass = onType.IsAssignableFrom(shouldSerializeInstType);

                if (!isInstOrSubclass)
                {
                    Throw.ArgumentException<object>($"{nameof(shouldSerialize)} must be either static method taking no parameters, a static method taking the type being serialized, an instance method on the type being serialized, or a delegate taking the type being serialized", nameof(shouldSerialize));
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
        public bool Equals(SerializableMember? serializableMember)
        {
            if (ReferenceEquals(serializableMember, null)) return false;

            if (IsBackedByGeneratedMethod)
            {
                if (!serializableMember.IsBackedByGeneratedMethod) return false;

                // this is fine because the getter, formatter, and so on are derived from this method
                return GeneratedMethod.Value == serializableMember.GeneratedMethod.Value;
            }
            else
            {
                if (serializableMember.IsBackedByGeneratedMethod) return false;
            }

            if (ShouldSerialize.HasValue)
            {
                if (!serializableMember.ShouldSerialize.HasValue) return false;

                if (ShouldSerialize.Value != serializableMember.ShouldSerialize.Value) return false;
            }
            else
            {
                if (serializableMember.ShouldSerialize.HasValue) return false;
            }

            return
                serializableMember.EmitDefaultValue == EmitDefaultValue &&
                serializableMember.Formatter == Formatter &&
                serializableMember.Getter == Getter &&
                serializableMember.Name == Name;
        }

        /// <summary>
        /// Returns a stable hash for this SerializableMember.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(SerializableMember), EmitDefaultValue, Formatter, Getter, Name, ShouldSerialize, GeneratedMethod);

        /// <summary>
        /// Describes this SerializableMember.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        => $"{nameof(SerializableMember)} with {nameof(Name)}: {Name}\r\n{nameof(Getter)}: {Getter}\r\n{nameof(Formatter)}: {Formatter}\r\n{nameof(ShouldSerialize)}: {ShouldSerialize}\r\n{nameof(GeneratedMethod)}: {GeneratedMethod}";

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
