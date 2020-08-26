using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    internal static class WellKnownRowTypes
    {
        private static readonly ImmutableDictionary<TypeInfo, Setter> Setters = GetSetters();
        private static readonly ImmutableDictionary<TypeInfo, Getter> Getters = GetGetters();
        private static readonly ImmutableDictionary<TypeInfo, string> Names = GetNames();

        internal static class WellKnownEnumRowType<TEnum>
            where TEnum : struct, Enum
        {
            internal static readonly string Name = typeof(TEnum).Name;
            internal static readonly string NullableName = "Nullable" + typeof(TEnum).Name;

            internal static void WellKnownSetter(ref TEnum row, TEnum value, in ReadContext _)
            {
                row = value;
            }

            internal static void WellKnownNullableSetter(ref TEnum? row, TEnum? value, in ReadContext _)
            {
                row = value;
            }

            internal static TEnum WellKnownGetter(TEnum row, in WriteContext _)
            => row;

            internal static TEnum? WellKnownNullableGetter(TEnum? row, in WriteContext _)
            => row;
        }

        internal static bool TryGetSetter(TypeInfo type, [MaybeNullWhen(returnValue: false)] out string name, [MaybeNullWhen(returnValue: false)] out Setter setter)
        {
            var nonNull = Nullable.GetUnderlyingType(type) ?? type;

            if (nonNull.IsEnum)
            {
                var typeIsNullable = nonNull != type;

                var wellKnownEnum = Types.WellKnownEnumRowType.MakeGenericType(nonNull).GetTypeInfo();
                MethodInfo setterMtd;
                FieldInfo nameField;

                if (typeIsNullable)
                {
                    setterMtd = wellKnownEnum.GetMethodNonNull(nameof(WellKnownEnumRowType<StringComparison>.WellKnownNullableSetter), InternalStatic);
                    nameField = wellKnownEnum.GetFieldNonNull(nameof(WellKnownEnumRowType<StringComparison>.NullableName), InternalStatic);
                }
                else
                {
                    setterMtd = wellKnownEnum.GetMethodNonNull(nameof(WellKnownEnumRowType<StringComparison>.WellKnownSetter), InternalStatic);
                    nameField = wellKnownEnum.GetFieldNonNull(nameof(WellKnownEnumRowType<StringComparison>.Name), InternalStatic);
                }

                name = Utils.NonNull(nameField.GetValue(null) as string);
                setter = Setter.ForMethod(setterMtd);
                return true;
            }

            if (Setters.TryGetValue(type, out setter))
            {
                name = Names[type];
                return true;
            }

            name = null;
            setter = null;
            return false;
        }

        internal static bool TryGetGetter(TypeInfo type, [MaybeNullWhen(returnValue: false)] out string name, [MaybeNullWhen(returnValue: false)] out Getter getter)
        {
            var nonNull = Nullable.GetUnderlyingType(type) ?? type;

            if (nonNull.IsEnum)
            {
                var typeIsNullable = nonNull != type;

                var wellKnownEnum = Types.WellKnownEnumRowType.MakeGenericType(nonNull).GetTypeInfo();
                MethodInfo getterMtd;
                FieldInfo nameField;

                if (typeIsNullable)
                {
                    getterMtd = wellKnownEnum.GetMethodNonNull(nameof(WellKnownEnumRowType<StringComparison>.WellKnownNullableGetter), InternalStatic);
                    nameField = wellKnownEnum.GetFieldNonNull(nameof(WellKnownEnumRowType<StringComparison>.NullableName), InternalStatic);
                }
                else
                {
                    getterMtd = wellKnownEnum.GetMethodNonNull(nameof(WellKnownEnumRowType<StringComparison>.WellKnownGetter), InternalStatic);
                    nameField = wellKnownEnum.GetFieldNonNull(nameof(WellKnownEnumRowType<StringComparison>.Name), InternalStatic);
                }

                name = Utils.NonNull(nameField.GetValue(null) as string);
                getter = Getter.ForMethod(getterMtd);
                return true;
            }

            if (Getters.TryGetValue(type, out getter))
            {
                name = Names[type];
                return true;
            }

            name = null;
            getter = null;
            return false;
        }

        internal static void WellKnownSetter<TRow>(ref TRow row, TRow value, in ReadContext _)
        {
            row = value;
        }

        internal static TRow WellKnownGetter<TRow>(TRow row, in WriteContext _)
        => row;

        private static ImmutableDictionary<TypeInfo, Setter> GetSetters()
        {
            var builder = ImmutableDictionary.CreateBuilder<TypeInfo, Setter>();

            foreach (var type in Types.WellKnownTypes_Array)
            {
                Add(builder, type);
            }

            return builder.ToImmutable();

            // do the reflection-y bits necessary to add a setter backed by WellKnownSetter bound
            //    to the given type to the dictionary builder
            static void Add(ImmutableDictionary<TypeInfo, Setter>.Builder builder, TypeInfo type)
            {
                var mtd = Methods.WellKnownRowTypes.WellKnownSetter.MakeGenericMethod(type);
                var setter = Setter.ForMethod(mtd);
                builder.Add(type, setter);

                if (type.IsValueType)
                {
                    var isNullable = Nullable.GetUnderlyingType(type) != null;
                    if (!isNullable)
                    {
                        var nullableType = Types.Nullable.MakeGenericType(type).GetTypeInfo();
                        Add(builder, nullableType);
                    }
                }
            }
        }

        private static ImmutableDictionary<TypeInfo, Getter> GetGetters()
        {
            var builder = ImmutableDictionary.CreateBuilder<TypeInfo, Getter>();

            foreach (var type in Types.WellKnownTypes_Array)
            {
                Add(builder, type);
            }

            return builder.ToImmutable();

            // do the reflection-y bits necessary to add a getter backed by WellKnownGetter bound
            //    to the given type to the dictionary builder
            static void Add(ImmutableDictionary<TypeInfo, Getter>.Builder builder, TypeInfo type)
            {
                var mtd = Methods.WellKnownRowTypes.WellKnownGetter.MakeGenericMethod(type);
                var getter = Getter.ForMethod(mtd);
                builder.Add(type, getter);

                if (type.IsValueType)
                {
                    var isNullable = Nullable.GetUnderlyingType(type) != null;
                    if (!isNullable)
                    {
                        var nullableType = Types.Nullable.MakeGenericType(type).GetTypeInfo();
                        Add(builder, nullableType);
                    }
                }
            }
        }

        private static ImmutableDictionary<TypeInfo, string> GetNames()
        {
            var builder = ImmutableDictionary.CreateBuilder<TypeInfo, string>();

            foreach (var type in Types.WellKnownTypes_Array)
            {
                Add(builder, type);
            }

            return builder.ToImmutable();

            // do the reflection-y bits necessary to add a name for the given type (and maybe
            //    it's nullable variant)
            static void Add(ImmutableDictionary<TypeInfo, string>.Builder builder, TypeInfo type)
            {
                var underlying = Nullable.GetUnderlyingType(type);

                string name;
                if (underlying != null)
                {
                    // it's a nullable type
                    name = "Nullable" + underlying.Name;
                }
                else
                {
                    name = type.Name;
                }

                builder.Add(type, name);

                if (type.IsValueType)
                {
                    var isNullable = underlying != null;
                    if (!isNullable)
                    {
                        var nullableType = Types.Nullable.MakeGenericType(type).GetTypeInfo();
                        Add(builder, nullableType);
                    }
                }
            }
        }
    }
}
