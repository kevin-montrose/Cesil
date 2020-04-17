using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Cesil
{
    internal static class ReflectionExtensionMethods
    {
        internal static bool IsBigTuple(this TypeInfo type)
        {
            if (!type.IsGenericType) return false;

            if (!type.IsGenericTypeDefinition)
            {
                type = type.GetGenericTypeDefinition().GetTypeInfo();
            }

            return type == Types.Tuple_Array[Types.Tuple_Array.Length - 1];
        }

        internal static bool IsBigValueTuple(this TypeInfo type)
        {
            if (!type.IsGenericType) return false;

            if (!type.IsGenericTypeDefinition)
            {
                type = type.GetGenericTypeDefinition().GetTypeInfo();
            }

            return type == Types.ValueTuple_Array[Types.ValueTuple_Array.Length - 1];
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
            // todo: find a way to test this?
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
            // todo: find a way to test this?
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
            // todo: find a way to test this?
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
            // todo: find a way to test
            if (type == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Created type was null");
            }

            return type;
        }
    }
}