using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Cesil
{
    internal static class DefaultTypeInstanceProviders
    {
        private static readonly ImmutableDictionary<TypeInfo, InstanceProvider> ReferenceTypeProviders = GetProviders();

        internal static bool TryGetReferenceInstanceProvider(TypeInfo forType, [MaybeNullWhen(returnValue: false)]out InstanceProvider provider)
        => ReferenceTypeProviders.TryGetValue(forType, out provider);

        // any struct can be backed by this
        internal static bool TryCreateInstance<T>(in ReadContext _, out T val)
            where T: struct
        {
            val = default;
            return true;
        }

        // any nullable struct can be backed by this
        internal static bool TryCreateNullableInstance<T>(in ReadContext _, out T? val)
            where T : struct
        {
            val = default;
            return true;
        }

        // we actually need this to be generic because of the out parameter, we
        //    can't just use an object
        internal static bool TryCreateNull<T>(in ReadContext _, out T? val)
            where T: class
        {
            val = null;
            return true;
        }

        // pre-allocate known types
        private static ImmutableDictionary<TypeInfo, InstanceProvider> GetProviders()
        {
            var builder = ImmutableDictionary.CreateBuilder<TypeInfo, InstanceProvider>();

            Add(builder, Types.String);
            Add(builder, Types.Uri);
            Add(builder, Types.Version);

            return builder.ToImmutable();

            // do the reflection-y bits necessary to add an instance provider backed by WellKnownSetter bound
            //    to the given type to the dictionary builder
            static void Add(ImmutableDictionary<TypeInfo, InstanceProvider>.Builder builder, TypeInfo type)
            {
                var mtd = Methods.DefaultTypeInstanceProviders.TryCreateNull.MakeGenericMethod(type);
                var provider = InstanceProvider.ForMethod(mtd);
                builder.Add(type, provider);

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
