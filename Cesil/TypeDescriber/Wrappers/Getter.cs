using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for getters that doesn't take a row.
    /// </summary>
    public delegate TValue StaticGetterDelegate<TValue>(in WriteContext context);

    /// <summary>
    /// Delegate type for getters.
    /// </summary>
    public delegate TValue GetterDelegate<TRow, TValue>(TRow instance, in WriteContext context);

    /// <summary>
    /// Represents code used to get a value from a type.
    /// 
    /// Wraps either a MethodInfo, a FieldInfo, a GetterDelegate, or a StaticGetterDelegate.
    /// </summary>
    public sealed class Getter : IEquatable<Getter>, ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>
    {
        internal delegate object DynamicGetterDelegate(object row, in WriteContext ctx);

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

        internal readonly bool TakesContext;

        private NonNull<DynamicGetterDelegate> _CachedDelegate;
        ref NonNull<DynamicGetterDelegate> ICreatesCacheableDelegate<DynamicGetterDelegate>.CachedDelegate => ref _CachedDelegate;

        private Getter(TypeInfo? rowType, TypeInfo returns, MethodInfo method, bool takesContext)
        {
            RowType.SetAllowNull(rowType);
            Returns = returns;
            Method.Value = method;
            TakesContext = takesContext;
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
            TakesContext = true;
        }

        private Getter(TypeInfo? rowType, TypeInfo returns, FieldInfo field)
        {
            RowType.SetAllowNull(rowType);
            Returns = returns;
            Method.Clear();
            Delegate.Clear();
            Field.Value = field;
            TakesContext = false;
        }

        DynamicGetterDelegate ICreatesCacheableDelegate<DynamicGetterDelegate>.CreateDelegate()
        {
            var row = Expressions.Parameter_Object;
            var ctx = Expressions.Parameter_WriteContext_ByRef;

            Expression onType;
            if (IsStatic)
            {
                onType = Expressions.Constant_Null;
            }
            else
            {
                onType = Expression.Convert(row, RowType.Value);
            }

            var body = MakeExpression(onType, ctx);
            var convertToObject = Expression.Convert(body, Types.ObjectType);

            var lambda = Expression.Lambda<DynamicGetterDelegate>(convertToObject, row, ctx);
            var del = lambda.Compile();

            return del;
        }

        void ICreatesCacheableDelegate<DynamicGetterDelegate>.Guarantee(IDelegateCache cache)
        => IDelegateCacheHelpers.GuaranteeImpl<Getter, DynamicGetterDelegate>(this, cache);

        internal Expression MakeExpression(Expression rowVar, Expression ctxVar)
        {
            // todo: there's no particular reason this couldn't be chainable?

            Expression selfExp;
            
            switch (Mode)
            {
                case BackingMode.Method:
                    {
                        var mtd = Method.Value;
                        if (mtd.IsStatic)
                        {
                            var ps = mtd.GetParameters();

                            if (ps.Length == 0)
                            {
                                selfExp = Expression.Call(mtd);
                            }
                            else if(ps.Length == 1)
                            {
                                if (TakesContext)
                                {
                                    selfExp = Expression.Call(mtd, ctxVar);
                                }
                                else
                                {
                                    selfExp = Expression.Call(mtd, rowVar);
                                }
                            }
                            else
                            {
                                selfExp = Expression.Call(mtd, rowVar, ctxVar);
                            }
                        }
                        else
                        {
                            if (TakesContext)
                            {
                                selfExp = Expression.Call(rowVar, mtd, ctxVar);
                            }
                            else
                            {
                                selfExp = Expression.Call(rowVar, mtd);
                            }
                        }
                    };
                    break;
                case BackingMode.Field:
                    {
                        var field = Field.Value;
                        if (field.IsStatic)
                        {
                            selfExp = Expression.Field(null, field);
                        }
                        else
                        {
                            selfExp = Expression.Field(rowVar, field);
                        }
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var getterDel = Delegate.Value;
                        var delRef = Expression.Constant(getterDel);

                        if (IsStatic)
                        {
                            selfExp = Expression.Invoke(delRef, ctxVar);
                        }
                        else
                        {
                            selfExp = Expression.Invoke(delRef, rowVar, ctxVar);
                        }
                    }
                    break;
                default:
                    return Throw.InvalidOperationException<Expression>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }

            return selfExp;
        }

        /// <summary>
        /// Create a getter from a PropertyInfo.
        /// 
        /// Throws if the property does not have a getter.
        /// </summary>
        public static Getter ForProperty(PropertyInfo property)
        {
            Utils.CheckArgumentNull(property, nameof(property));

            var get = property.GetMethod;

            if (get == null)
            {
                return Throw.ArgumentException<Getter>("Property does not have a getter", nameof(property));
            }

            return ForMethod(get);
        }

        /// <summary>
        /// Create a getter from a method.
        /// 
        /// getter can be an instance method or a static method
        ///   if it's a static method, it can take 0, 1, or 2 parameters
        ///      - if there is 1 parameter, it may be an `in WriteContext` or the row type
        ///      - if there are 2 parameters, the first must be the row type, and the second must be `in WriteContext`
        ///   if it's an instance method, it can only take 0 or 1 parameters
        ///      - if it takes a parameter, it must be an `in WriteContext`
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
            bool takesContext;
            if (method.IsStatic)
            {
                if (getterParams.Length == 0)
                {
                    /* that's fine */
                    rowType = null;
                    takesContext = false;
                }
                else if (getterParams.Length == 1)
                {
                    var p0 = getterParams[0].ParameterType.GetTypeInfo();
                    if (p0.IsByRef)
                    {
                        var p0Elem = p0.GetElementTypeNonNull();
                        if (p0Elem != Types.WriteContextType)
                        {
                            return Throw.ArgumentException<Getter>($"If the first parameter to a {nameof(Getter)} method is by ref, it must be an `in WriteContext`", nameof(method));
                        }

                        rowType = null;
                        takesContext = true;
                    }
                    else
                    {
                        rowType = p0;
                        takesContext = false;
                    }
                }
                else if(getterParams.Length == 2)
                {
                    var p0 = getterParams[0].ParameterType.GetTypeInfo();
                    var p1 = getterParams[1].ParameterType.GetTypeInfo();

                    if (p0.IsByRef)
                    {
                        return Throw.ArgumentException<Getter>($"If the first parameter to a static {nameof(Getter)} method with two parameters cannot be by ref", nameof(method));
                    }

                    if (!p1.IsByRef)
                    {
                        return Throw.ArgumentException<Getter>($"If the second parameter to a static {nameof(Getter)} method with two parameters must be `in WriteContext`, was not by ref", nameof(method));
                    }

                    var p1Elem = p1.GetElementTypeNonNull();
                    if(p1Elem != Types.WriteContextType)
                    {
                        return Throw.ArgumentException<Getter>($"If the second parameter to a static {nameof(Getter)} method with two parameters must be `in WriteContext`, was not `WriteContext`", nameof(method));
                    }

                    rowType = p0;
                    takesContext = true;
                }
                else
                {
                    return Throw.ArgumentException<Getter>($"Since {method} is a static method, it cannot take more than 2 parameters", nameof(method));
                }
            }
            else
            {
                rowType = method.DeclaringTypeNonNull();

                if(getterParams.Length == 0)
                {
                    takesContext = false;
                }
                else if(getterParams.Length == 1)
                {
                    var p0 = getterParams[0].ParameterType.GetTypeInfo();

                    if (!p0.IsByRef)
                    {
                        return Throw.ArgumentException<Getter>($"If the first parameter to an instance {nameof(Getter)} method with one parameter must be `in WriteContext`, was not by ref", nameof(method));
                    }

                    var p0Elem = p0.GetElementTypeNonNull();
                    if (p0Elem != Types.WriteContextType)
                    {
                        return Throw.ArgumentException<Getter>($"If the first parameter to an instance {nameof(Getter)} method with one parameter must be `in WriteContext`, was not `WriteContext`", nameof(method));
                    }

                    takesContext = true;
                }
                else
                {
                    return Throw.ArgumentException<Getter>($"Since {method} is an instance method, it cannot take 1 parameter", nameof(method));
                }
            }

            var returns = method.ReturnType.GetTypeInfo();

            return new Getter(rowType, returns, method, takesContext);
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
                    if (!IsStatic)
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
            if (args.Length == 2)
            {
                var takes = args[0].ParameterType.GetTypeInfo();
                var ctx = args[1].ParameterType.GetTypeInfo();

                if (!ctx.IsByRef)
                {
                    return Throw.InvalidOperationException<Getter>($"Delegate's second parameter must be a `in WriteContext`, was not by ref");
                }

                var ctxElem = ctx.GetElementTypeNonNull();
                if(ctxElem != Types.WriteContextType)
                {
                    return Throw.InvalidOperationException<Getter>($"Delegate's second parameter must be a `in WriteContext`, was not `WriteContext`");
                }

                var formatterDel = Types.GetterDelegateType.MakeGenericType(takes, ret);
                var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

                var reboundDel = System.Delegate.CreateDelegate(formatterDel, del, invoke);

                return new Getter(takes, ret, reboundDel);
            }
            else if (args.Length == 1)
            {
                var ctx = args[0].ParameterType.GetTypeInfo();

                if (!ctx.IsByRef)
                {
                    return Throw.InvalidOperationException<Getter>($"Delegate's first parameter must be a `in WriteContext`, was not by ref");
                }

                var ctxElem = ctx.GetElementTypeNonNull();
                if (ctxElem != Types.WriteContextType)
                {
                    return Throw.InvalidOperationException<Getter>($"Delegate's first parameter must be a `in WriteContext`, was not `WriteContext`");
                }

                var formatterDel = Types.StaticGetterDelegateType.MakeGenericType(ret);
                var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

                var reboundDel = System.Delegate.CreateDelegate(formatterDel, del, invoke);

                return new Getter(null, ret, reboundDel);
            }
            else
            {
                return Throw.InvalidOperationException<Getter>("Delegate must take 1 or 2 parameters");
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
