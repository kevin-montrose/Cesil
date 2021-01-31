using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    internal static class ReflectionExtensionMethods
    {
        internal static object GetValueNonNull(this FieldInfo field, object? obj)
        {
            var ret = field.GetValue(obj);
            if (ret == null)
            {
                Throw.InvalidOperationException($"Expected non-null value when reading field {field}, but was null");
            }

            return ret;
        }

        internal static object GetValueNonNull(this PropertyInfo prop, object? obj)
        {
            var ret = prop.GetValue(obj);
            if (ret == null)
            {
                Throw.InvalidOperationException($"Expected non-null value when reading property {prop}, but was null");
            }

            return ret;
        }

        internal static bool AllowsNullLikeValue(this TypeInfo type)
        {
            while (type.IsByRef)
            {
                type = type.GetElementTypeNonNull();
            }

            if (!type.IsValueType)
            {
                return true;
            }

            return type.IsNullableValueType(out _);
        }

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

            // need to de-ref member type, because the "ref-ness" of it doesn't matter
            //   for nullability purposes
            var effectiveMemberType = memberType;
            while (effectiveMemberType.IsByRef)
            {
                effectiveMemberType = effectiveMemberType.GetElementTypeNonNull();
            }

            // value types can only have meaningful nullable annotations
            //   for their generic parameters, which means we never care
            //   about it's annotations
            //
            // since ints and whatnot are pretty common, this shortcut
            //   can save some real time on type describing.
            if (effectiveMemberType.IsValueType)
            {
                if (effectiveMemberType.IsNullableValueType(out _))
                {
                    return NullHandling.AllowNull;
                }

                return NullHandling.CannotBeNull;
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
                        Throw.ImpossibleException($@"NullableAttribute with null first argument (on member {member}), this should not be possible");
                        return default;
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
                            Throw.ImpossibleException($@"NullableAttribute with missing or null first argument (on member {member}), this should not be possible");
                            return default;
                        }

                        // we only care about the _first_ byte which describe the root reference
                        var firstByte = (byte)firstArg;

                        return GetHandlingByByte(firstByte, member, effectiveMemberType);
                    }
                    else
                    {
                        Throw.ImpossibleException($@"NullableAttribute with unexpected argument type {argType} (on member {member}), this should not be possible");
                        return default;
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
                            Throw.ImpossibleException($@"NullableContextAttribute with null first argument (on member {member}), this should not be possible");
                            return default;
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
                return val switch
                {
                    0 => GetObliviousNullHandling(effectiveMemberType),
                    1 => NullHandling.ForbidNull,
                    2 => NullHandling.AllowNull,
                    _ => Throw.ImpossibleException_Returns<NullHandling>($@"NullableAttribute with unexpected argument {val} (on member {member}), this should not be possible"),
                };
            }

            // no annotation found, do what is "natural"
            static NullHandling GetObliviousNullHandling(TypeInfo forType)
            {
                if (forType.IsValueType)
                {
                    if (forType.IsNullableValueType(out _))
                    {
                        return NullHandling.AllowNull;
                    }

                    return NullHandling.CannotBeNull;
                }

                return NullHandling.AllowNull;
            }
        }

        internal static bool IsNullableValueType(this TypeInfo type, [NotNullWhen(returnValue: true)] out TypeInfo? elementType)
        {
            elementType = Nullable.GetUnderlyingType(type)?.GetTypeInfo();
            return elementType != null;
        }

        internal static bool IsFlagsEnum(this TypeInfo type)
        {
            if (!type.IsEnum)
            {
                return false;
            }

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
                Throw.InvalidOperationException($"Could not get constructor on {type} for {string.Join(", ", (IEnumerable<TypeInfo>)types)} with {bindingAttr}");
            }

            return consNull;
        }

        internal static ConstructorInfo GetConstructorNonNull(this TypeInfo type, TypeInfo[] args)
        {
            var consNull = type.GetConstructor(args);
            if (consNull == null)
            {
                Throw.InvalidOperationException($"Could not get constructor on {type} for {string.Join(", ", (IEnumerable<TypeInfo>)args)}");
            }

            return consNull;
        }

        internal static TypeInfo GetElementTypeNonNull(this TypeInfo type)
        {
            var elemNull = type.GetElementType();
            if (elemNull == null)
            {
                Throw.InvalidOperationException($"Could not get element type for {type}");
            }

            return elemNull.GetTypeInfo();
        }

        internal static TypeInfo DeclaringTypeNonNull(this ConstructorInfo cons)
        {
            var declNull = cons.DeclaringType;

            // this is basically impossible, BUT if something has caused a constructor
            //   to be defined in the fake-ish <Module> type in an assembly it will
            //   happen
            if (declNull == null)
            {
                Throw.InvalidOperationException($"Could not find declaring type for {cons}");
            }

            return declNull.GetTypeInfo();
        }

        internal static TypeInfo DeclaringTypeNonNull(this MethodInfo mtd)
        {
            var declNull = mtd.DeclaringType;

            // this happens if the method is declared as part of a _module_ but not a type
            //   which is weird, but legal, so check for it
            if (declNull == null)
            {
                Throw.InvalidOperationException($"Could not find declaring type for {mtd}");
            }

            return declNull.GetTypeInfo();
        }

        internal static TypeInfo DeclaringTypeNonNull(this FieldInfo field)
        {
            var declNull = field.DeclaringType;

            // this happens if the field is declared as part of a _module_
            //   which is weird, but legal, so check for it
            if (declNull == null)
            {
                Throw.InvalidOperationException($"Could not find declaring type for {field}");
            }

            return declNull.GetTypeInfo();
        }

        internal static FieldInfo GetFieldNonNull(this TypeInfo type, string fieldName, BindingFlags flags)
        {
            var fieldNull = type.GetField(fieldName, flags);
            if (fieldNull == null)
            {
                Throw.InvalidOperationException($"Could not find field {fieldName} with {flags} on {type}");
            }

            return fieldNull;
        }

        internal static MethodInfo GetMethodNonNull(this TypeInfo type, string methodName)
        {
            var mtdNull = type.GetMethod(methodName);
            if (mtdNull == null)
            {
                Throw.InvalidOperationException($"Could not find method {methodName} on {type}");
            }

            return mtdNull;
        }

        internal static MethodInfo GetMethodNonNull(this TypeInfo type, string methodName, BindingFlags flags)
        {
            var mtdNull = type.GetMethod(methodName, flags);
            if (mtdNull == null)
            {
                Throw.InvalidOperationException($"Could not find method {methodName} with {flags} on {type}");
            }

            return mtdNull;
        }

        internal static MethodInfo GetMethodNonNull(this TypeInfo type, string methodName, BindingFlags flags, Binder? binder, TypeInfo[] parameterTypes, ParameterModifier[]? modifiers)
        {
            var mtdNull = type.GetMethod(methodName, flags, binder, parameterTypes, modifiers);

            if (mtdNull == null)
            {
                Throw.InvalidOperationException(
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
                Throw.InvalidOperationException($"Could not find property {propName} with {flags} on {type}");
            }

            return propNull;
        }

        internal static MethodInfo GetGetMethodNonNull(this PropertyInfo prop)
        {
            var getNull = prop.GetMethod;
            if (getNull == null)
            {
                Throw.InvalidOperationException($"Could not find getter on {prop}");
            }

            return getNull;
        }

        internal static TypeInfo CreateTypeNonNull(this TypeBuilder builder)
        {
            var type = builder.CreateTypeInfo();

            // this is possible if the TypeBuilder is making the <Module> type in the assembly
            //   which is craaaaazy unlikely but technically possible
            if (type == null)
            {
                Throw.InvalidOperationException($"Created type was null");
            }

            return type;
        }

        internal static (ConstructorInfo PrimaryCons, ImmutableHashSet<PropertyInfo> SetByCons, int? RecordDepth) ReadRecordType(this TypeInfo type)
        {
            if(!type.IsRecordType(out var cons, out var props, out var depth))
            {
                Throw.ImpossibleException($"Type {type} was assumed to be a record, but isn't");
            }

            return (cons, props, depth);
        }

        internal static bool IsRecordType(
            this TypeInfo type,
            [MaybeNullWhen(returnValue: false)] out ConstructorInfo primaryCons,
            out ImmutableHashSet<PropertyInfo> setByCons,
            out int? recordDepth
        )
        {
            if (!LooksLikeRecordType(type))
            {
                primaryCons = null;
                setByCons = ImmutableHashSet<PropertyInfo>.Empty;
                recordDepth = null;
                return false;
            }

            // has to have a protected constructor taking self
            var cons = type.GetConstructor(InternalInstance, null, new[] { type }, null);

            var methods = type.GetMethods(PublicInstance);
            var deconstruct = methods.SingleOrDefault(m => m.Name == "Deconstruct" && m.DeclaringTypeNonNull() == type);

            if (deconstruct == null)
            {
                // implies this has NO parameters to a primary constructor
                // that is, it's a declaration of the form
                //   record Foo;

                var publicCons = type.GetConstructorNonNull(PublicInstance, null, Array.Empty<TypeInfo>(), null);
                primaryCons = publicCons;
                setByCons = ImmutableHashSet<PropertyInfo>.Empty;
                recordDepth = DetermineDepth(type);
                return true;
            }

            var deconstructParams = deconstruct.GetParameters();
            if (deconstructParams.Length == 0 || deconstructParams.Any(p => !p.ParameterType.IsByRef))
            {
                // wut

                primaryCons = null;
                setByCons = ImmutableHashSet<PropertyInfo>.Empty;
                recordDepth = null;
                return false;
            }

            var primaryConsParamTypess = deconstructParams.Select(p => p.ParameterType.GetTypeInfo().GetElementTypeNonNull()).ToArray();
            primaryCons = type.GetConstructor(PublicInstance, null, primaryConsParamTypess, null);

            if (primaryCons == null)
            {
                setByCons = ImmutableHashSet<PropertyInfo>.Empty;
                recordDepth = null;
                return false;
            }

            var properties = type.GetProperties(PublicInstance);
            var primaryConsParams = primaryCons.GetParameters();
            setByCons =
                properties
                    .Where(
                        p =>
                            p.IsAutoInit() &&
                            primaryConsParams.Any(pcp => pcp.Name == p.Name && pcp.ParameterType == p.PropertyType)
                    )
                    .ToImmutableHashSet();
            recordDepth = DetermineDepth(type);

            return true;

            // how deep in the heirarchy of record types is this type?
            static int DetermineDepth(TypeInfo d)
            {
                var ret = 0;
                var baseType = d.BaseType?.GetTypeInfo();
                while (baseType != null && LooksLikeRecordType(baseType))
                {
                    ret++;
                    baseType = baseType.BaseType?.GetTypeInfo();
                }

                return ret;
            }

            // does it have the minimum bits needed to be a record?
            static bool LooksLikeRecordType(TypeInfo type)
            {
                // has to have a protected constructor taking self
                var cons = type.GetConstructor(InternalInstance, null, new[] { type }, null);
                if (cons == null)
                {
                    return false;
                }

                // has to have this "unspeakable" clone method
                var mtd = type.GetMethod("<Clone>$", PublicInstance);
                if (mtd == null)
                {
                    return false;
                }

                return true;
            }
        }

        internal static bool IsAutoInit(this PropertyInfo prop)
        {
            if (prop.SetMethod == null)
            {
                return false;
            }

            var declType = prop.DeclaringType;
            if (declType == null)
            {
                return false;
            }

            var backingField = declType.GetField($"<{prop.Name}>k__BackingField", All);
            if (backingField == null)
            {
                return false;
            }

            return !backingField.IsPublic && backingField.IsInitOnly && !backingField.IsStatic;
        }
    }
}