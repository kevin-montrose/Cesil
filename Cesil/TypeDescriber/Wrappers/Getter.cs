﻿using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for getters that doesn't take an instance.
    /// </summary>
    public delegate V StaticGetterDelegate<V>();

    /// <summary>
    /// Delegate type for getters.
    /// </summary>
    public delegate V GetterDelegate<T, V>(T instance);

    /// <summary>
    /// Represents code used to get a value from a type.
    /// 
    /// Wraps either a MethodInfo, a FieldInfo, a GetterDelegate, or a StaticGetterDelegate.
    /// </summary>
    public sealed class Getter : IEquatable<Getter>, ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>
    {
        internal delegate object DynamicGetterDelegate(object row);

        internal BackingMode Mode
        {
            get
            {
                if (Method != null)
                {
                    return BackingMode.Method;
                }

                if (Field != null)
                {
                    return BackingMode.Field;
                }

                if (Delegate != null)
                {
                    return BackingMode.Delegate;
                }

                return BackingMode.None;
            }
        }

        internal bool IsStatic
        {
            get
            {
                switch (Mode)
                {
                    case BackingMode.Field: return Field.IsStatic;
                    case BackingMode.Method: return Method.IsStatic;
                    case BackingMode.Delegate: return RowType == null;
                    default:
                        return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}");
                }
            }
        }

        internal MethodInfo Method { get; }
        internal FieldInfo Field { get; }
        internal Delegate Delegate { get; }

        internal TypeInfo RowType { get; }
        internal TypeInfo Returns { get; }

        DynamicGetterDelegate ICreatesCacheableDelegate<DynamicGetterDelegate>.CachedDelegate { get; set; }

        private Getter(TypeInfo rowType, TypeInfo returns, MethodInfo method)
        {
            RowType = rowType;
            Returns = returns;
            Method = method;
            Delegate = null;
            Field = null;
        }

        private Getter(TypeInfo rowType, TypeInfo returns, Delegate del)
        {
            RowType = rowType;
            Returns = returns;
            Method = null;
            Delegate = del;
            Field = null;
        }

        private Getter(TypeInfo rowType, TypeInfo returns, FieldInfo field)
        {
            RowType = rowType;
            Returns = returns;
            Method = null;
            Delegate = null;
            Field = field;
        }

        DynamicGetterDelegate ICreatesCacheableDelegate<DynamicGetterDelegate>.CreateDelegate()
        {
            var row = Expressions.Parameter_Object;

            switch (Mode)
            {
                case BackingMode.Field:
                    {
                        Expression fieldOnExp;

                        if (IsStatic)
                        {
                            fieldOnExp = null;
                        }
                        else
                        {
                            fieldOnExp = Expression.Convert(row, RowType);
                        }

                        var getField = Expression.Field(fieldOnExp, Field);
                        var convertToObject = Expression.Convert(getField, Types.ObjectType);
                        var lambda = Expression.Lambda<DynamicGetterDelegate>(convertToObject, row);
                        var del = lambda.Compile();

                        return del;
                    }
                case BackingMode.Method:
                    {
                        MethodCallExpression callMtd;

                        if (IsStatic)
                        {
                            if (RowType == null)
                            {
                                callMtd = Expression.Call(null, Method);
                            }
                            else
                            {
                                var rowAsType = Expression.Convert(row, RowType);
                                callMtd = Expression.Call(null, Method, rowAsType);
                            }
                        }
                        else
                        {
                            var rowAsType = Expression.Convert(row, RowType);
                            callMtd = Expression.Call(rowAsType, Method);
                        }

                        var convertToObject = Expression.Convert(callMtd, Types.ObjectType);
                        var lambda = Expression.Lambda<DynamicGetterDelegate>(convertToObject, row);
                        var del = lambda.Compile();

                        return del;
                    }
                case BackingMode.Delegate:
                    {
                        var delInst = Expression.Constant(Delegate);

                        InvocationExpression callDel;

                        if (IsStatic)
                        {
                            callDel = Expression.Invoke(delInst);
                        }
                        else
                        {
                            var rowAsType = Expression.Convert(row, RowType);
                            callDel = Expression.Invoke(delInst, rowAsType);
                        }

                        var convertToObject = Expression.Convert(callDel, Types.ObjectType);
                        var lambda = Expression.Lambda<DynamicGetterDelegate>(convertToObject, row);
                        var del = lambda.Compile();

                        return del;
                    }
                default:
                    return Throw.InvalidOperationException<DynamicGetterDelegate>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        void ICreatesCacheableDelegate<DynamicGetterDelegate>.Guarantee(IDelegateCache cache)
        => IDelegateCacheHelpers.GuaranteeImpl<Getter, DynamicGetterDelegate>(this, cache);

        /// <summary>
        /// Create a getter from a method.
        /// 
        /// getter can be an instance method or a static method
        ///   if it's a static method, it can take 0 or 1 parameters
        ///      the 1 parameter must be the type to be serialized, or something it is assignable to
        ///   if it's an instance method, it can only take 0 parameters
        /// </summary>
        public static Getter ForMethod(MethodInfo getter)
        {
            if (getter == null)
            {
                return Throw.ArgumentNullException<Getter>(nameof(getter));
            }

            if (getter.ReturnType == Types.VoidType)
            {
                return Throw.ArgumentException<Getter>($"{nameof(getter)} must return a non-void value", nameof(getter));
            }

            var getterParams = getter.GetParameters();

            TypeInfo rowType;
            if (getter.IsStatic)
            {
                if (getterParams.Length == 0)
                {
                    /* that's fine */
                    rowType = null;
                }
                else if (getterParams.Length == 1)
                {
                    rowType = getterParams[0].ParameterType.GetTypeInfo();
                }
                else
                {
                    return Throw.ArgumentException<Getter>($"Since {getter} is a static method, it cannot take more than 1 parameter", nameof(getter));
                }
            }
            else
            {
                rowType = getter.DeclaringType.GetTypeInfo();

                if (getterParams.Length > 0)
                {
                    return Throw.ArgumentException<Getter>($"Since {getter} is an instance method, it cannot take any parameters", nameof(getter));
                }
            }

            var returns = getter.ReturnType.GetTypeInfo();

            return new Getter(rowType, returns, getter);
        }

        /// <summary>
        /// Create a getter from a field.
        /// 
        /// field can be an instance field or a static field.        
        /// </summary>
        public static Getter ForField(FieldInfo field)
        {
            if (field == null)
            {
                return Throw.ArgumentNullException<Getter>(nameof(field));
            }

            TypeInfo onType;
            if (field.IsStatic)
            {
                onType = null;
            }
            else
            {
                onType = field.DeclaringType.GetTypeInfo();
            }

            var returns = field.FieldType.GetTypeInfo();

            return new Getter(onType, returns, field);
        }

        /// <summary>
        /// Create a Getter from the given delegate.
        /// </summary>
        public static Getter ForDelegate<T, V>(GetterDelegate<T, V> del)
        {
            if (del == null)
            {
                return Throw.ArgumentNullException<Getter>(nameof(del));
            }

            return new Getter(typeof(T).GetTypeInfo(), typeof(V).GetTypeInfo(), del);
        }

        /// <summary>
        /// Create a Getter from the given delegate.
        /// </summary>
        public static Getter ForDelegate<V>(StaticGetterDelegate<V> del)
        {
            if (del == null)
            {
                return Throw.ArgumentNullException<Getter>(nameof(del));
            }

            return new Getter(null, typeof(V).GetTypeInfo(), del);
        }

        /// <summary>
        /// Compares for equality to another Getter.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is Getter g)
            {
                return Equals(g);
            }

            return false;
        }

        /// <summary>
        /// Compares for equality to another Getter.
        /// </summary>
        public bool Equals(Getter g)
        {
            if (ReferenceEquals(g, null)) return false;

            if (g.Returns != Returns) return false;
            if (g.RowType != RowType) return false;

            var otherMode = g.Mode;
            if (otherMode != Mode) return false;

            switch (otherMode)
            {
                case BackingMode.Field:
                    return g.Field == Field;
                case BackingMode.Method:
                    return g.Method == Method;
                case BackingMode.Delegate:
                    return g.Delegate == Delegate;
                default:
                    return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {otherMode}");
            }
        }

        /// <summary>
        /// Returns a hashcode for this Getter.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(Getter), Mode, Returns, Delegate, Method, Field);

        /// <summary>
        /// Describes this Getter.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case BackingMode.Method:
                    if (!IsStatic)
                    {
                        return $"{nameof(Getter)} backed by method {Method} taking {RowType} returning {Returns}";
                    }
                    else
                    {
                        return $"{nameof(Getter)} backed by method {Method} returning {Returns}";
                    }
                case BackingMode.Delegate:
                    if (!IsStatic)
                    {
                        return $"{nameof(Getter)} backed by delegate {Delegate} taking {RowType} returning {Returns}";
                    }
                    else
                    {
                        return $"{nameof(Getter)} backed by delegate {Delegate} returning {Returns}";
                    }
                case BackingMode.Field:
                    if(!IsStatic)
                    {
                        return $"{nameof(Getter)} backed by field {Field} on {RowType} returning {Returns}";
                    }
                    else
                    {
                        return $"{nameof(Getter)} backed by field {Field} returning {Returns}";
                    }
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling Getter.ForMethod if non-null.
        /// 
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator Getter(MethodInfo mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling Getter.ForField if non-null.
        /// 
        /// Returns null if field is null.
        /// </summary>
        public static explicit operator Getter(FieldInfo field)
        => field == null ? null : ForField(field);

        /// <summary>
        /// Convenience operator, equivalent to calling Getter.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator Getter(Delegate del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();
            if (delType.IsGenericType)
            {
                var genArgs = delType.GetGenericArguments();
                var delGenType = delType.GetGenericTypeDefinition().GetTypeInfo();
                if (delGenType == Types.GetterDelegateType)
                {
                    var takes = genArgs[0].GetTypeInfo();
                    var returns = genArgs[1].GetTypeInfo();

                    return new Getter(takes, returns, del);
                }

                if (delGenType == Types.StaticGetterDelegateType)
                {
                    var returns = genArgs[0].GetTypeInfo();

                    return new Getter(null, returns, del);
                }
            }

            var mtd = del.Method;
            var ret = mtd.ReturnType.GetTypeInfo();
            if (ret == Types.VoidType)
            {
                return Throw.InvalidOperationException<Getter>($"Delegate cannot return void");
            }

            var args = mtd.GetParameters();
            if (args.Length == 1)
            {
                var takes = args[0].ParameterType.GetTypeInfo();

                var formatterDel = Types.GetterDelegateType.MakeGenericType(takes, ret);
                var invoke = del.GetType().GetMethod("Invoke");

                var reboundDel = Delegate.CreateDelegate(formatterDel, del, invoke);

                return new Getter(takes, ret, reboundDel);
            }
            else if (args.Length == 0)
            {
                var formatterDel = Types.StaticGetterDelegateType.MakeGenericType(ret);
                var invoke = del.GetType().GetMethod("Invoke");

                var reboundDel = Delegate.CreateDelegate(formatterDel, del, invoke);

                return new Getter(null, ret, reboundDel);
            }
            else
            {
                return Throw.InvalidOperationException<Getter>("Delegate must take 0 or 1 parameters");
            }
        }

        /// <summary>
        /// Compare two Getters for equality
        /// </summary>
        public static bool operator ==(Getter a, Getter b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Getters for inequality
        /// </summary>
        public static bool operator !=(Getter a, Getter b)
        => !(a == b);
    }
}
