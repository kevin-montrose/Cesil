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

        internal static class WellKnownEnumRowType<TEnum>
            where TEnum: struct, Enum
        {
            internal static void WellKnownSetter(ref TEnum row, TEnum value, in ReadContext _)
            {
                row = value;
            }

            internal static void WellKnownNullableSetter(ref TEnum? row, TEnum? value, in ReadContext _)
            {
                row = value;
            }
        }

        internal static bool TryGetSetter(TypeInfo type, [MaybeNullWhen(returnValue: false)]out Setter setter)
        {
            var nonNull = Nullable.GetUnderlyingType(type) ?? type;

            if (nonNull.IsEnum)
            {
                var typeIsNullable = nonNull != type;

                var wellKnownEnum = Types.WellKnownEnumRowType.MakeGenericType(nonNull).GetTypeInfo();
                MethodInfo setterMtd;

                if(typeIsNullable)
                {
                    setterMtd = wellKnownEnum.GetMethodNonNull(nameof(WellKnownEnumRowType<StringComparison>.WellKnownNullableSetter), InternalStatic);
                }
                else
                {
                    setterMtd = wellKnownEnum.GetMethodNonNull(nameof(WellKnownEnumRowType<StringComparison>.WellKnownSetter), InternalStatic);
                }

                setter = Setter.ForMethod(setterMtd);
                return true;
            }

            return Setters.TryGetValue(type, out setter);
        }

        internal static void WellKnownSetter<TRow>(ref TRow row, TRow value, in ReadContext _)
        {
            row = value;
        }

        private static ImmutableDictionary<TypeInfo, Setter> GetSetters()
        {
            var builder = ImmutableDictionary.CreateBuilder<TypeInfo, Setter>();

            Add(builder, Types.Bool);
            Add(builder, Types.Char);
            Add(builder, Types.Byte);
            Add(builder, Types.SByte);
            Add(builder, Types.Short);
            Add(builder, Types.UShort);
            Add(builder, Types.Int);
            Add(builder, Types.UInt);
            Add(builder, Types.Long);
            Add(builder, Types.ULong);
            Add(builder, Types.Float);
            Add(builder, Types.Double);
            Add(builder, Types.Decimal);
            Add(builder, Types.DateTime);
            Add(builder, Types.DateTimeOffset);
            Add(builder, Types.TimeSpan);
            Add(builder, Types.String);
            Add(builder, Types.Uri);
            Add(builder, Types.Version);
            Add(builder, Types.Guid);
            Add(builder, Types.Index);
            Add(builder, Types.Range);

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
    }
}
