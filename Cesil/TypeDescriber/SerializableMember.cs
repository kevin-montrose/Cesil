using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Represents a member of a type to use when serializing.
    /// </summary>
    public sealed class SerializableMember
    {
        private static readonly IReadOnlyDictionary<TypeInfo, MethodInfo> TypeFormatters;

        static SerializableMember()
        {
            // load up default formatters
            var ret = new Dictionary<TypeInfo, MethodInfo>();
            foreach (var mtd in Types.DefaultTypeFormattersType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
            {
                var firstArg = mtd.GetParameters()[0];
                var forType = firstArg.ParameterType;

                ret.Add(forType.GetTypeInfo(), mtd);
            }

            TypeFormatters = ret;
        }

        /// <summary>
        /// The name of the column that maps to this member.
        /// </summary>
        public string Name { get; }
        
        internal MethodInfo Getter { get; }
        internal FieldInfo Field { get; }

        internal MethodInfo Formatter { get; }
        internal MethodInfo ShouldSerialize { get; }

        internal bool EmitDefaultValue { get; }

        private SerializableMember(string name, MethodInfo getter, FieldInfo field, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        {
            Name = name;
            Getter = getter;
            Field = field;
            Formatter = formatter;
            ShouldSerialize = shouldSerialize;
            EmitDefaultValue = emitDefaultValue;
        }

        /// <summary>
        /// Returns the default formatter for the given type, if one exists.
        /// </summary>
        public static MethodInfo GetDefaultFormatter(TypeInfo forType)
        {
            if (forType.IsEnum)
            {
                if (forType.GetCustomAttribute<FlagsAttribute>() == null)
                {
                    var parsingClass = Types.DefaultEnumTypeFormatterType.MakeGenericType(forType);
                    var parsingMtd = parsingClass.GetMethod(nameof(DefaultTypeFormatters.DefaultEnumTypeFormatter<StringComparison>.TryFormatEnum), BindingFlags.Static | BindingFlags.NonPublic);   // <StringComparison> doesn't matter here

                    return parsingMtd;
                }
                else
                {
                    var parsingClass = Types.DefaultFlagsEnumTypeFormatterType.MakeGenericType(forType);
                    var parsingMtd = parsingClass.GetMethod(nameof(DefaultTypeFormatters.DefaultFlagsEnumTypeFormatter<StringComparison>.TryFormatFlagsEnum), BindingFlags.Static | BindingFlags.NonPublic);   // <StringComparison> doesn't matter here

                    return parsingMtd;
                }
            }

            var nullableElem = Nullable.GetUnderlyingType(forType)?.GetTypeInfo();
            if (nullableElem != null && nullableElem.IsEnum)
            {
                if (nullableElem.GetCustomAttribute<FlagsAttribute>() == null)
                {
                    var parsingClass = Types.DefaultEnumTypeFormatterType.MakeGenericType(nullableElem);
                    var parsingMtd = parsingClass.GetMethod(nameof(DefaultTypeFormatters.DefaultEnumTypeFormatter<StringComparison>.TryFormatNullableEnum), BindingFlags.Static | BindingFlags.NonPublic);   // <StringComparison> doesn't matter here

                    return parsingMtd;
                }
                else
                {
                    var parsingClass = Types.DefaultFlagsEnumTypeFormatterType.MakeGenericType(nullableElem);
                    var parsingMtd = parsingClass.GetMethod(nameof(DefaultTypeFormatters.DefaultFlagsEnumTypeFormatter<StringComparison>.TryFormatNullableFlagsEnum), BindingFlags.Static | BindingFlags.NonPublic);   // <StringComparison> doesn't matter here

                    return parsingMtd;
                }
            }

            if (!TypeFormatters.TryGetValue(forType, out var ret))
            {
                return null;
            }

            return ret;
        }

        /// <summary>
        /// Creates a SerializableMember for the given property.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop)
        => Create(prop?.DeclaringType?.GetTypeInfo(), prop?.Name, prop?.GetMethod, null, GetDefaultFormatter(prop?.PropertyType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name)
        => Create(prop?.DeclaringType?.GetTypeInfo(), name, prop?.GetMethod, null, GetDefaultFormatter(prop?.PropertyType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name and formatter.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name, MethodInfo formatter)
        => Create(prop?.DeclaringType?.GetTypeInfo(), name, prop?.GetMethod, null, formatter, null, true);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name, MethodInfo formatter, MethodInfo shouldSerialize)
        => Create(prop?.DeclaringType?.GetTypeInfo(), name, prop?.GetMethod, null, formatter, shouldSerialize, true);

        /// <summary>
        /// Creates a SerializableMember for the given property, with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember ForProperty(PropertyInfo prop, string name, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        => Create(prop?.DeclaringType?.GetTypeInfo(), name, prop?.GetMethod, null, formatter, shouldSerialize, emitDefaultValue);

        /// <summary>
        /// Creates a SerializableMember for the given field.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field)
        => Create(field?.DeclaringType?.GetTypeInfo(), field?.Name, null, field, GetDefaultFormatter(field?.FieldType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name)
        => Create(field?.DeclaringType?.GetTypeInfo(), name, null, field, GetDefaultFormatter(field?.FieldType?.GetTypeInfo()), null, true);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name and formatter.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, MethodInfo formatter)
        => Create(field?.DeclaringType?.GetTypeInfo(), name, null, field, formatter, null, true);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name, formatter, and ShouldSerialize method.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, MethodInfo formatter, MethodInfo shouldSerialize)
        => Create(field?.DeclaringType?.GetTypeInfo(), name, null, field, formatter, shouldSerialize, true);

        /// <summary>
        /// Creates a SerializableMember for the given field, with the given name, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember ForField(FieldInfo field, string name, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        => Create(field?.DeclaringType?.GetTypeInfo(), name, null, field, formatter, shouldSerialize, emitDefaultValue);

        /// <summary>
        /// Create a SerializableMember with an explicit type being serialized, name, backing field, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember Create(TypeInfo beingSerializedType, string name, FieldInfo field, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        => Create(beingSerializedType, name, null, field, formatter, shouldSerialize, emitDefaultValue);

        /// <summary>
        /// Create a SerializableMember with an explicit type being serialized, name, getter, formatter, ShouldSerialize method, and whether to emit a default value.
        /// </summary>
        public static SerializableMember Create(TypeInfo beingSerializedType, string name, MethodInfo getter, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        => Create(beingSerializedType, name, getter, null, formatter, shouldSerialize, emitDefaultValue);

        private static SerializableMember Create(TypeInfo beingSerializedType, string name, MethodInfo getter, FieldInfo field, MethodInfo formatter, MethodInfo shouldSerialize, bool emitDefaultValue)
        {
            if(beingSerializedType == null)
            {
                Throw.ArgumentNullException(nameof(beingSerializedType));
            }

            if (name == null)
            {
                Throw.ArgumentNullException(nameof(name));
            }

            if (field == null && getter == null)
            {
                Throw.InvalidOperation($"At least one of {nameof(field)} and {nameof(getter)} must be non-null");
            }

            if (field != null && getter != null)
            {
                Throw.InvalidOperation($"Only one of {nameof(field)} and {nameof(getter)} can be non-null");
            }

            if(formatter == null)
            {
                Throw.ArgumentNullException(nameof(formatter));
            }

            TypeInfo toSerializeType;

            // getter can be an instance method or a static method
            //   if it's a static method, it can take 0 or 1 parameters
            //      the 1 parameter must be the type to be serialized, or something it is assignable to
            //   if it's an instance method, it can only take 0 parameters
            if (getter != null)
            {
                if (getter.ReturnType == Types.VoidType)
                {
                    Throw.ArgumentException($"{nameof(getter)} must return a non-void value", nameof(getter));
                }

                var getterParams = getter.GetParameters();

                if (getter.IsStatic)
                {
                    if (getterParams.Length == 0)
                    {
                        /* that's fine */
                    }
                    else if (getterParams.Length == 1)
                    {
                        var takenParam = getterParams[0].ParameterType.GetTypeInfo();
                        if (!takenParam.IsAssignableFrom(beingSerializedType))
                        {
                            Throw.ArgumentException($"{getter}'s single parameter must be assignable from {beingSerializedType}", nameof(getter));
                        }
                    }
                    else
                    {
                        Throw.ArgumentException($"Since {getter} is a static method, it cannot take more than 1 parameter", nameof(getter));
                    }
                }
                else
                {
                    if (getterParams.Length > 0)
                    {
                        Throw.ArgumentException($"Since {getter} is an instance method, it cannot take any parameters", nameof(getter));
                    }
                }

                toSerializeType = getter.ReturnType.GetTypeInfo();
            }
            else
            {
                toSerializeType = field.FieldType.GetTypeInfo();
            }

            // formatter needs to take the toSerializeType (or a type it's assignable to)
            //   and a IBufferWriter<char>
            //   and return bool (false indicates insufficient space was available)
            {
                if (!formatter.IsStatic)
                {
                    Throw.ArgumentException($"{nameof(formatter)} must be a static method", nameof(formatter));
                }

                var formatterRetType = formatter.ReturnType.GetTypeInfo();
                if(formatterRetType != Types.BoolType)
                {
                    Throw.ArgumentException($"{nameof(formatter)} must return bool", nameof(formatter));
                }

                var args = formatter.GetParameters();
                if(args.Length != 2)
                {
                    Throw.ArgumentException($"{nameof(formatter)} must take 2 parameters", nameof(formatter));
                }

                if (!args[0].ParameterType.IsAssignableFrom(toSerializeType))
                {
                    Throw.ArgumentException($"The first paramater to {nameof(formatter)} must be accept a {toSerializeType.FullName}", nameof(formatter));
                }

                if (args[1].ParameterType.GetTypeInfo() != Types.IBufferWriterOfCharType)
                {
                    Throw.ArgumentException($"The second paramater to {nameof(formatter)} must be a {nameof(IBufferWriter<char>)}", nameof(formatter));
                }
            }

            var shouldSerializeOnType = (getter?.DeclaringType ?? field?.DeclaringType).GetTypeInfo();

            CheckShouldSerializeMethod(shouldSerialize, shouldSerializeOnType);

            return new SerializableMember(name, getter, field, formatter, shouldSerialize, emitDefaultValue);
        }

        private static void CheckShouldSerializeMethod(MethodInfo shouldSerialize, TypeInfo onType)
        {
            if (shouldSerialize == null) return;

            // shouldSerialize must be an argument-less method
            //   that is either static, or on an instance of the same type as (or a baseclass of) setter
            //   and cannot be generic or otherwise weird

            if (shouldSerialize.IsGenericMethodDefinition)
            {
                Throw.ArgumentException($"Cannot use a generic method for the {nameof(shouldSerialize)} argument", nameof(shouldSerialize));
            }

            var args = shouldSerialize.GetParameters();
            if (args.Length > 0)
            {
                Throw.ArgumentException($"{nameof(shouldSerialize)} cannot take parameters", nameof(shouldSerialize));
            }

            var ret = shouldSerialize.ReturnType.GetTypeInfo();
            if (ret != Types.BoolType)
            {
                Throw.ArgumentException($"{nameof(shouldSerialize)} must return a boolean", nameof(shouldSerialize));
            }

            if (!shouldSerialize.IsStatic)
            {
                var shouldSerializeInstType = shouldSerialize.DeclaringType.GetTypeInfo();
                
                var isInstOrSubclass = onType.IsAssignableFrom(shouldSerializeInstType);

                if (!isInstOrSubclass)
                {
                    Throw.ArgumentException($"{nameof(shouldSerialize)} be either static or a member of an instance of the type declaring the field or property", nameof(shouldSerialize));
                }
            }
        }

        /// <summary>
        /// Describes this SerializableMember.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        => $"{nameof(Name)}: {Name}\r\n{nameof(Getter)}: {Getter}\r\n{nameof(Field)}: {Field}\r\n{nameof(Formatter)}: {Formatter}\r\n{nameof(ShouldSerialize)}: {ShouldSerialize}";
    }
}
