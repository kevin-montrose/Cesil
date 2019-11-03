using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for getters that doesn't take a row.
    /// </summary>
    public delegate TValue StaticGetterDelegate<TValue>();

    /// <summary>
    /// Delegate type for getters.
    /// </summary>
    public delegate TValue GetterDelegate<TRow, TValue>(TRow instance);

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
                if (Method.HasValue)
                {
                    return BackingMode.Method;
                }

                if (Field.HasValue)
                {
                    return BackingMode.Field;
                }

                if (Delegate.HasValue)
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
                    case BackingMode.Field: return Field.Value.IsStatic;
                    case BackingMode.Method: return Method.Value.IsStatic;
                    case BackingMode.Delegate: return !RowType.HasValue;
                    default:
                        return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}");
                }
            }
        }

        internal readonly NonNull<MethodInfo> Method;

        internal readonly NonNull<FieldInfo> Field;

        internal readonly NonNull<Delegate> Delegate;

        internal readonly NonNull<TypeInfo> RowType;

        internal readonly TypeInfo Returns;

        private NonNull<DynamicGetterDelegate> _CachedDelegate;
        ref NonNull<DynamicGetterDelegate> ICreatesCacheableDelegate<DynamicGetterDelegate>.CachedDelegate => ref _CachedDelegate;

        private Getter(TypeInfo? rowType, TypeInfo returns, MethodInfo method)
        {
            RowType.SetAllowNull(rowType);
            Returns = returns;
            Method.Value = method;
            Delegate.Clear();
            Field.Clear();
        }

        private Getter(TypeInfo? rowType, TypeInfo returns, Delegate del)
        {
            RowType.SetAllowNull(rowType);
            Returns = returns;
            Method.Clear();
            Delegate.Value = del;
            Field.Clear();
        }

        private Getter(TypeInfo? rowType, TypeInfo returns, FieldInfo field)
        {
            RowType.SetAllowNull(rowType);
            Returns = returns;
            Method.Clear();
            Delegate.Clear();
            Field.Value = field;
        }

        DynamicGetterDelegate ICreatesCacheableDelegate<DynamicGetterDelegate>.CreateDelegate()
        {
            var row = Expressions.Parameter_Object;

            switch (Mode)
            {
                case BackingMode.Field:
                    {
                        Expression? fieldOnExp;

                        if (IsStatic)
                        {
                            fieldOnExp = null;
                        }
                        else
                        {
                            fieldOnExp = Expression.Convert(row, RowType.Value);
                        }

                        var getField = Expression.Field(fieldOnExp, Field.Value);
                        var convertToObject = Expression.Convert(getField, Types.ObjectType);
                        var lambda = Expression.Lambda<DynamicGetterDelegate>(convertToObject, row);
                        var del = lambda.Compile();

                        return del;
                    }
                case BackingMode.Method:
                    {
                        MethodCallExpression callMtd;

                        var mtd = Method.Value;

                        if (IsStatic)
                        {
                            if (!RowType.HasValue)
                            {
                                callMtd = Expression.Call(null, mtd);
                            }
                            else
                            {
                                var rowAsType = Expression.Convert(row, RowType.Value);
                                callMtd = Expression.Call(null, mtd, rowAsType);
                            }
                        }
                        else
                        {
                            var rowAsType = Expression.Convert(row, RowType.Value);
                            callMtd = Expression.Call(rowAsType, mtd);
                        }

                        var convertToObject = Expression.Convert(callMtd, Types.ObjectType);
                        var lambda = Expression.Lambda<DynamicGetterDelegate>(convertToObject, row);
                        var del = lambda.Compile();

                        return del;
                    }
                case BackingMode.Delegate:
                    {
                        var delInst = Expression.Constant(Delegate.Value);

                        InvocationExpression callDel;

                        if (IsStatic)
                        {
                            callDel = Expression.Invoke(delInst);
                        }
                        else
                        {
                            var rowAsType = Expression.Convert(row, RowType.Value);
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
        public static Getter ForMethod(MethodInfo method)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            if (method.ReturnType == Types.VoidType)
            {
                return Throw.ArgumentException<Getter>($"{nameof(method)} must return a non-void value", nameof(method));
            }

            var getterParams = method.GetParameters();

            TypeInfo? rowType;
            if (method.IsStatic)
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
                    return Throw.ArgumentException<Getter>($"Since {method} is a static method, it cannot take more than 1 parameter", nameof(method));
                }
            }
            else
            {
                rowType = method.DeclaringTypeNonNull();

                if (getterParams.Length > 0)
                {
                    return Throw.ArgumentException<Getter>($"Since {method} is an instance method, it cannot take any parameters", nameof(method));
                }
            }

            var returns = method.ReturnType.GetTypeInfo();

            return new Getter(rowType, returns, method);
        }

        /// <summary>
        /// Create a getter from a field.
        /// 
        /// field can be an instance field or a static field.        
        /// </summary>
        public static Getter ForField(FieldInfo field)
        {
            Utils.CheckArgumentNull(field, nameof(field));

            TypeInfo? onType;
            if (field.IsStatic)
            {
                onType = null;
            }
            else
            {
                onType = field.DeclaringTypeNonNull();
            }

            var returns = field.FieldType.GetTypeInfo();

            return new Getter(onType, returns, field);
        }

        /// <summary>
        /// Create a Getter from the given delegate.
        /// </summary>
        public static Getter ForDelegate<TRow, TValue>(GetterDelegate<TRow, TValue> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new Getter(typeof(TRow).GetTypeInfo(), typeof(TValue).GetTypeInfo(), del);
        }

        /// <summary>
        /// Create a Getter from the given delegate.
        /// </summary>
        public static Getter ForDelegate<TValue>(StaticGetterDelegate<TValue> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new Getter(null, typeof(TValue).GetTypeInfo(), del);
        }

        /// <summary>
        /// Compares for equality to another Getter.
        /// </summary>
        public override bool Equals(object? obj)
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
        public bool Equals(Getter getter)
        {
            if (ReferenceEquals(getter, null)) return false;

            if (getter.Returns != Returns) return false;
            if (getter.RowType.HasValue)
            {
                if (!RowType.HasValue)
                {
                    return false;
                }

                if (getter.RowType.Value != RowType.Value) return false;
            }
            else
            {
                if (RowType.HasValue) return false;
            }

            var otherMode = getter.Mode;
            if (otherMode != Mode) return false;

            switch (otherMode)
            {
                case BackingMode.Field:
                    return getter.Field.Value == Field.Value;
                case BackingMode.Method:
                    return getter.Method.Value == Method.Value;
                case BackingMode.Delegate:
                    return getter.Delegate.Value == Delegate.Value;
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
        public static explicit operator Getter?(MethodInfo? mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling Getter.ForField if non-null.
        /// 
        /// Returns null if field is null.
        /// </summary>
        public static explicit operator Getter?(FieldInfo? field)
        => field == null ? null : ForField(field);

        /// <summary>
        /// Convenience operator, equivalent to calling Getter.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator Getter?(Delegate? del)
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
                var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

                var reboundDel = System.Delegate.CreateDelegate(formatterDel, del, invoke);

                return new Getter(takes, ret, reboundDel);
            }
            else if (args.Length == 0)
            {
                var formatterDel = Types.StaticGetterDelegateType.MakeGenericType(ret);
                var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

                var reboundDel = System.Delegate.CreateDelegate(formatterDel, del, invoke);

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
        public static bool operator ==(Getter? a, Getter? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Getters for inequality
        /// </summary>
        public static bool operator !=(Getter? a, Getter? b)
        => !(a == b);
    }
}
