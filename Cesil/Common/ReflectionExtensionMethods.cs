﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    internal static class ReflectionExtensionMethods
    {
        public static ConstructorInfo GetConstructorNonNull(this TypeInfo type, BindingFlags bindingAttr, Binder? binder, TypeInfo[] types, ParameterModifier[]? modifiers)
        {
            var consNull = type.GetConstructor(bindingAttr, binder, types, modifiers);
            if(consNull == null)
            {
                return Throw.InvalidOperationException<ConstructorInfo>($"Could not get constructor on {type} for {string.Join(", ", (IEnumerable<TypeInfo>)types)} with {bindingAttr}");
            }

            return consNull;
        }

        public static ConstructorInfo GetConstructorNonNull(this TypeInfo type, TypeInfo[] args)
        {
            var consNull = type.GetConstructor(args);
            if(consNull == null)
            {
                return Throw.InvalidOperationException<ConstructorInfo>($"Could not get constructor on {type} for {string.Join(", ", (IEnumerable<TypeInfo>)args)}");
            }

            return consNull;
        }

        public static TypeInfo GetElementTypeNonNull(this TypeInfo type)
        {
            var elemNull = type.GetElementType();
            if (elemNull == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Could not get element type for {type}");
            }

            return elemNull.GetTypeInfo();
        }

        public static TypeInfo DeclaringTypeNonNull(this ConstructorInfo cons)
        {
            var declNull = cons.DeclaringType;
            if(declNull == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Could not find declaring type for {cons}");
            }

            return declNull.GetTypeInfo();
        }

        public static TypeInfo DeclaringTypeNonNull(this MethodInfo mtd)
        {
            var declNull = mtd.DeclaringType;
            if (declNull == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Could not find declaring type for {mtd}");
            }

            return declNull.GetTypeInfo();
        }

        public static TypeInfo DeclaringTypeNonNull(this FieldInfo field)
        {
            var declNull = field.DeclaringType;
            if (declNull == null)
            {
                return Throw.InvalidOperationException<TypeInfo>($"Could not find declaring type for {field}");
            }

            return declNull.GetTypeInfo();
        }

        public static FieldInfo GetFieldNonNull(this TypeInfo type, string fieldName, BindingFlags flags)
        {
            var fieldNull = type.GetField(fieldName, flags);
            if (fieldNull == null)
            {
                return Throw.InvalidOperationException<FieldInfo>($"Could not find {fieldName} with {flags} on {type}");
            }

            return fieldNull;
        }

        public static MethodInfo GetMethodNonNull(this TypeInfo type, string methodName)
        {
            var mtdNull = type.GetMethod(methodName);
            if (mtdNull == null)
            {
                return Throw.InvalidOperationException<MethodInfo>($"Could not find {methodName} on {type}");
            }

            return mtdNull;
        }

        public static MethodInfo GetMethodNonNull(this TypeInfo type, string methodName, BindingFlags flags)
        {
            var mtdNull = type.GetMethod(methodName, flags);
            if (mtdNull == null)
            {
                return Throw.InvalidOperationException<MethodInfo>($"Could not find {methodName} with {flags} on {type}");
            }

            return mtdNull;
        }

        public static PropertyInfo GetPropertyNonNull(this TypeInfo type, string propName, BindingFlags flags)
        {
            var propNull = type.GetProperty(propName, flags);
            if (propNull == null)
            {
                return Throw.InvalidOperationException<PropertyInfo>($"Could not find {propName} with {flags} on {type}");
            }

            return propNull;
        }

        public static MethodInfo GetGetMethodNonNull(this PropertyInfo prop)
        {
            var getNull = prop.GetMethod;
            if (getNull == null)
            {
                return Throw.InvalidOperationException<MethodInfo>($"Could not find getter on {prop}");
            }

            return getNull;
        }
    }
}