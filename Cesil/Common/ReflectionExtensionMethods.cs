using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Cesil
{
    internal static class ReflectionExtensionMethods
    {
        // todo: test these DetermineNullability methods

        internal static NullHandling DetermineNullability(this PropertyInfo? property)
        {
            if (property == null)
            {
                return NullHandling.AllowNull;
            }

            return DetermineNullabilityImpl(property, property.CustomAttributes, property.DeclaringType?.CustomAttributes, property.PropertyType.GetTypeInfo());
        }

        internal static NullHandling DetermineNullability(this FieldInfo? field)
        {
            if (field == null)
            {
                return NullHandling.AllowNull;
            }

            return DetermineNullabilityImpl(field, field.CustomAttributes, field.DeclaringType?.CustomAttributes, field.FieldType.GetTypeInfo());
        }

        internal static NullHandling DetermineNullability(this ParameterInfo? parameter)
        {
            if (parameter == null)
            {
                return NullHandling.AllowNull;
            }

            return DetermineNullabilityImpl(parameter, parameter.CustomAttributes, parameter.Member.CustomAttributes, parameter.ParameterType.GetTypeInfo());
        }

        private static NullHandling DetermineNullabilityImpl<T>(T member, IEnumerable<CustomAttributeData> memberAttributes, IEnumerable<CustomAttributeData>? contextAttributes, TypeInfo memberType)
        {
            // from: https://github.com/dotnet/roslyn/blob/3182fd79a7790188d4facfc3fa2da22c598895f2/docs/features/nullable-metadata.md

            // nullable annotations are either on the member itself, or on the declaring type
            //   if on the member, it's a [Nullable]
            //   if on the type, it's a [NullableContext]
            //
            // Nullable can take either a byte or a byte[], NullableContext always takes a byte
            //
            // we only ever care about the first byte (even if we have an array)
            // because that refers to the "root"
            //
            // bytes are as follows:
            //   0 - null oblivious
            //   1 - non-null
            //   2 - allow-null
            //
            // the absense of a nullable annotation is equivalent to null oblivious
            //
            // null oblivious maps to whatever is "natural" that is:
            //  - reference types allow null
            //  - nullable value types allow null
            //  - value types forbid null

            // value types can only have meaningful nullable annotations
            //   for their generic parameters, which means we never care
            //   about it's annotations
            //
            // since ints and whatnot are pretty common, this shortcut
            //   can save some real time on type describing.

            // need to de-ref member type, because the "ref-ness" of it doesn't matter
            //   for nullability purposes
            var effectiveMemberType = memberType;
            while (effectiveMemberType.IsByRef)
            {
                effectiveMemberType = effectiveMemberType.GetElementTypeNonNull();
            }

            if (effectiveMemberType.IsValueType)
            {
                if (effectiveMemberType.IsNullableValueType())
                {
                    return NullHandling.AllowNull;
                }

                return NullHandling.ForbidNull;
            }

            // check for explicit attributes
            foreach (var attr in memberAttributes)
            {
                if (attr.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute")
                {
                    var arg = attr.ConstructorArguments[0];
                    var argType = arg.ArgumentType.GetTypeInfo();

                    var val = arg.Value;
                    if (val == null)
                    {
                        return Throw.ImpossibleException<NullHandling>($@"NullableAttribute with null first argument (on member {member}), this should not be possible");
                    }

                    if (argType == Types.Byte)
                    {
                        var valByte = (byte)val;
                        return GetHandlingByByte(valByte, member, effectiveMemberType);
                    }
                    else if (argType == Types.ByteArray)
                    {
                        var valArr = (IReadOnlyCollection<CustomAttributeTypedArgument>)val;

                        var firstArg = valArr.FirstOrDefault().Value;
                        if (firstArg == null)
                        {
                            return Throw.ImpossibleException<NullHandling>($@"NullableAttribute with missing or null first argument (on member {member}), this should not be possible");
                        }

                        // we only care about the _first_ byte which describe the root reference
                        var firstByte = (byte)firstArg;

                        return GetHandlingByByte(firstByte, member, effectiveMemberType);
                    }
                    else
                    {
                        return Throw.ImpossibleException<NullHandling>($@"NullableAttribute with unexpected argument type {argType} (on member {member}), this should not be possible");
                    }
                }
            }

            // check for the ambient context
            if (contextAttributes != null)
            {
                foreach (var attr in contextAttributes)
                {
                    if (attr.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute")
                    {
                        var arg = attr.ConstructorArguments[0];
                        var argType = arg.ArgumentType.GetTypeInfo();

                        var val = arg.Value;
                        if (val == null)
                        {
                            return Throw.ImpossibleException<NullHandling>($@"NullableContextAttribute with null first argument (on member {member}), this should not be possible");
                        }

                        var valByte = (byte)val;
                        return GetHandlingByByte(valByte, member, effectiveMemberType);
                    }
                }
            }

            // no annotations found, do whatever is "natural"
            return GetObliviousNullHandling(effectiveMemberType);

            // found an actual attribute and extracted the relevant bit, what does it mean?
            static NullHandling GetHandlingByByte(byte val, T member, TypeInfo effectiveMemberType)
            {
                switch (val)
                {
                    case 0: return GetObliviousNullHandling(effectiveMemberType);
                    case 1: return NullHandling.ForbidNull;
                    case 2: return NullHandling.AllowNull;

                    default:
                        return Throw.ImpossibleException<NullHandling>($@"NullableAttribute with unexpected argument {val} (on member {member}), this should not be possible");
                }
            }

            // no annotation found, do what is "natural"
            static NullHandling GetObliviousNullHandling(TypeInfo forType)
            {
                if (forType.IsValueType)
                {
                    if (forType.IsNullableValueType())
                    {
                        return NullHandling.AllowNull;
                    }

                    return NullHandling.ForbidNull;
                }

                return NullHandling.AllowNull;
            }
        }

        internal static bool IsNullableValueType(this TypeInfo type)
        => Nullable.GetUnderlyingType(type) != null;

        internal static bool IsFlagsEnum(this TypeInfo type)
        {
            if (!type.IsEnum) return false;

            return type.GetCustomAttribute<FlagsAttribute>() != null;
        }

        internal static bool IsBigTuple(this TypeInfo type)
        {
            if (!type.IsGenericType) return false;

            if (!type.IsGenericTypeDefinition)
            {
                type = type.GetGenericTypeDefinition().GetTypeInfo();
            }

            return type == Types.Tuple_Array[^1];
        }

        internal static bool IsBigValueTuple(this TypeInfo type)
        {
            if (!type.IsGenericType) return false;

            if (!type.IsGenericTypeDefinition)
            {
                type = type.GetGenericTypeDefinition().GetTypeInfo();
            }

            return type == Types.ValueTuple_Array[^1];
        }

        internal static bool IsValueTuple(this TypeInfo type)
        {
            if (!type.IsGenericType) return false;

            if (!type.IsGenericTypeDefinition)
            {
                type = type.GetGenericTypeDefinition().GetTypeInfo();
            }

            return Array.IndexOf(Types.ValueTuple_Array, type) != -1;
        }

        internal static bool IsReadContextByRef(this ParameterInfo p, out string error)
        {
            var pType = p.ParameterType.GetTypeInfo();

            if (!pType.IsByRef)
            {
                error = "was not by ref";
                return false;
            }

            var pElem = pType.GetElementTypeNonNull();
            if (pElem != Types.ReadContext)
            {
                error = $"was not {nameof(ReadContext)}";
                return false;
            }

            error = "";
            return true;
        }

        internal static bool IsWriteContextByRef(this ParameterInfo p, out string error)
        {
            var pType = p.ParameterType.GetTypeInfo();

            if (!pType.IsByRef)
            {
                error = "was not by ref";
                return false;
            }

            var pElem = pType.GetElementTypeNonNull();
            if (pElem != Types.WriteContext)
            {
                error = $"was not {nameof(WriteContext)}";
                return false;
            }

            error = "";
            return true;
        }

        internal static ConstructorInfo GetConstructorNonNull(this TypeInfo type, BindingFlags bindingAttr, Binder? binder, TypeInfo[] types, ParameterModifier[]? modifiers)
        {
            var consNull = type.GetConstructor(bindingAttr, binder, types, modifiers);
            if (consNull == null)
            {
                return Throw.InvalidOperationException<ConstructorInfo>($"Could not get constructor on {type} for {string.Join(", ", (IEnumerable<TypeInfo>)types)} with {bindingAttr}");
            }

            return consNull;
        }

        internal static ConstructorInfo GetConstructorNonNull(this TypeInfo type, TypeInfo[] args)
        {
            var consNull = type.GetConstructor(args);
            if (consNull == null)
            {
                return Throw.InvalidOperationException<ConstructorInfo>($"Could not get constructor on {type} for {string.Join(", ", (IEnumerable<TypeInfo>)args)}");
            }

            return consNull;
        }

        internal static TypeInfo GetElementTypeNonNull(this TypeInfo type)
        {
            var elemNull = type.GetElementType();
            if (elemNull == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Could not get element type for {type}");
            }

            return elemNull.GetTypeInfo();
        }

        internal static TypeInfo DeclaringTypeNonNull(this ConstructorInfo cons)
        {
            var declNull = cons.DeclaringType;

            // technically possible, but fantastically hard to do in C#
            // todo: find a way to test this? (tracking issue: https://github.com/kevin-montrose/Cesil/issues/3)
            if (declNull == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Could not find declaring type for {cons}");
            }

            return declNull.GetTypeInfo();
        }

        internal static TypeInfo DeclaringTypeNonNull(this MethodInfo mtd)
        {
            var declNull = mtd.DeclaringType;

            // technically possible, but fantastically hard to do in C#
            // todo: find a way to test this? (tracking issue: https://github.com/kevin-montrose/Cesil/issues/3)
            if (declNull == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Could not find declaring type for {mtd}");
            }

            return declNull.GetTypeInfo();
        }

        internal static TypeInfo DeclaringTypeNonNull(this FieldInfo field)
        {
            var declNull = field.DeclaringType;

            // technically possible, but fantastically hard to do in C#
            // todo: find a way to test this? (tracking issue: https://github.com/kevin-montrose/Cesil/issues/3)
            if (declNull == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Could not find declaring type for {field}");
            }

            return declNull.GetTypeInfo();
        }

        internal static FieldInfo GetFieldNonNull(this TypeInfo type, string fieldName, BindingFlags flags)
        {
            var fieldNull = type.GetField(fieldName, flags);
            if (fieldNull == null)
            {
                return Throw.InvalidOperationException<FieldInfo>($"Could not find field {fieldName} with {flags} on {type}");
            }

            return fieldNull;
        }

        internal static MethodInfo GetMethodNonNull(this TypeInfo type, string methodName)
        {
            var mtdNull = type.GetMethod(methodName);
            if (mtdNull == null)
            {
                return Throw.InvalidOperationException<MethodInfo>($"Could not find method {methodName} on {type}");
            }

            return mtdNull;
        }

        internal static MethodInfo GetMethodNonNull(this TypeInfo type, string methodName, BindingFlags flags)
        {
            var mtdNull = type.GetMethod(methodName, flags);
            if (mtdNull == null)
            {
                return Throw.InvalidOperationException<MethodInfo>($"Could not find method {methodName} with {flags} on {type}");
            }

            return mtdNull;
        }

        internal static MethodInfo GetMethodNonNull(this TypeInfo type, string methodName, BindingFlags flags, Binder? binder, TypeInfo[] parameterTypes, ParameterModifier[]? modifiers)
        {
            var mtdNull = type.GetMethod(methodName, flags, binder, parameterTypes, modifiers);

            if (mtdNull == null)
            {
                return
                    Throw.InvalidOperationException<MethodInfo>(
                        $"Could not find method {methodName} with {flags} and ({string.Join(", ", parameterTypes.Select(s => s.FullName))}) on {type}"
                    );
            }

            return mtdNull;
        }

        internal static PropertyInfo GetPropertyNonNull(this TypeInfo type, string propName, BindingFlags flags)
        {
            var propNull = type.GetProperty(propName, flags);
            if (propNull == null)
            {
                return Throw.InvalidOperationException<PropertyInfo>($"Could not find property {propName} with {flags} on {type}");
            }

            return propNull;
        }

        internal static MethodInfo GetGetMethodNonNull(this PropertyInfo prop)
        {
            var getNull = prop.GetMethod;
            if (getNull == null)
            {
                return Throw.InvalidOperationException<MethodInfo>($"Could not find getter on {prop}");
            }

            return getNull;
        }

        internal static TypeInfo CreateTypeNonNull(this TypeBuilder builder)
        {
            var type = builder.CreateTypeInfo();

            // is this ever really possible?
            // todo: find a way to test (tracking issue: https://github.com/kevin-montrose/Cesil/issues/3)
            if (type == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Created type was null");
            }

            return type;
        }
    }
}