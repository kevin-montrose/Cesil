using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for resets that don't take an instance of the row.
    /// </summary>
    public delegate void StaticResetDelegate(in ReadContext context);

    /// <summary>
    /// Delegate type for resets.
    /// </summary>
    public delegate void ResetDelegate<TRow>(TRow onType, in ReadContext context);

    /// <summary>
    /// Delegate type for resets, where row is passed by ref.
    /// </summary>
    public delegate void ResetByRefDelegate<TRow>(ref TRow onType, in ReadContext context);

    /// <summary>
    /// Represents code called before a setter is called or a field
    ///   is set.
    /// 
    /// Wraps a static method, an instance method, or a delegate.
    /// </summary>
    public sealed class Reset : IEquatable<Reset>
    {
        internal BackingMode Mode
        {
            get
            {
                if (Method.HasValue) return BackingMode.Method;
                if (Delegate.HasValue) return BackingMode.Delegate;

                return BackingMode.None;
            }
        }

        internal bool IsStatic
        {
            get
            {
                return
                    Mode switch
                    {
                        BackingMode.Method => Method.Value.IsStatic,
                        BackingMode.Delegate => !RowType.HasValue,
                        _ => Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}")
                    };
            }
        }

        internal readonly NonNull<MethodInfo> Method;

        internal readonly NonNull<Delegate> Delegate;

        internal readonly NonNull<TypeInfo> RowType;
        internal readonly NullHandling? RowTypeNullability;

        internal readonly bool TakesContext;

        private Reset(TypeInfo? rowType, MethodInfo mtd, bool takesContext, NullHandling? rowTypeNullability)
        {
            RowType.SetAllowNull(rowType);
            RowTypeNullability = rowTypeNullability;
            Method.Value = mtd;
            TakesContext = takesContext;
        }

        private Reset(TypeInfo? rowType, Delegate del, NullHandling? rowTypeNullability)
        {
            RowType.SetAllowNull(rowType);
            RowTypeNullability = rowTypeNullability;
            Delegate.Value = del;
            TakesContext = true;
        }

        internal Expression MakeExpression(ParameterExpression rowVar, ParameterExpression contextVar)
        {
            Expression selfExp;
            switch (Mode)
            {
                case BackingMode.Method:
                    {
                        var resetMtd = Method.Value;
                        if (IsStatic)
                        {
                            if (RowType.HasValue)
                            {
                                if (TakesContext)
                                {
                                    selfExp = Expression.Call(resetMtd, rowVar, contextVar);
                                }
                                else
                                {
                                    selfExp = Expression.Call(resetMtd, rowVar);
                                }
                            }
                            else
                            {
                                if (TakesContext)
                                {
                                    selfExp = Expression.Call(resetMtd, contextVar);
                                }
                                else
                                {
                                    selfExp = Expression.Call(resetMtd);
                                }
                            }
                        }
                        else
                        {
                            if (TakesContext)
                            {
                                selfExp = Expression.Call(rowVar, resetMtd, contextVar);
                            }
                            else
                            {
                                selfExp = Expression.Call(rowVar, resetMtd);
                            }
                        }
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var resetDel = Delegate.Value;
                        var delRef = Expression.Constant(resetDel);

                        if (IsStatic)
                        {
                            selfExp = Expression.Invoke(delRef, contextVar);
                        }
                        else
                        {
                            selfExp = Expression.Invoke(delRef, rowVar, contextVar);
                        }
                    }
                    break;
                default:
                    return Throw.InvalidOperationException<Expression>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }

            return selfExp;
        }

        private Reset ChangeRowNullHandling(NullHandling nullHandling)
        {
            if (nullHandling == RowTypeNullability)
            {
                return this;
            }

            if (RowTypeNullability == null)
            {
                return Throw.InvalidOperationException<Reset>($"{this} does not take rows, and so cannot have a {nameof(NullHandling)} specified");
            }

            Utils.ValidateNullHandling(RowType.Value, nullHandling);

            return
                Mode switch
                {
                    BackingMode.Method => new Reset(RowType.Value, Method.Value, TakesContext, nullHandling),
                    BackingMode.Delegate => new Reset(RowType.Value, Delegate.Value, nullHandling),
                    _ => Throw.ImpossibleException<Reset>($"Unexpected: {nameof(BackingMode)}: {Mode}")
                };
        }

        /// <summary>
        /// Returns a Reset that differs from this by explicitly allowing
        ///   null rows be passed to it.
        ///   
        /// If the backing delegate, or method does not expect null rows
        ///   this could result in errors at runtime.
        /// </summary>
        public Reset AllowNullRows()
        => ChangeRowNullHandling(NullHandling.AllowNull);

        /// <summary>
        /// Returns a Reset that differs from this by explicitly forbidding
        ///   null rows be passed to it.
        ///   
        /// If the .NET runtime cannot guarantee that nulls will not be passed,
        ///   null checks will be injected.
        /// </summary>
        public Reset ForbidNullRows()
        => ChangeRowNullHandling(NullHandling.ForbidNull);

        /// <summary>
        /// Create a reset from a method.
        /// 
        /// The method must return void.
        /// 
        /// If method is an instance, it can take:
        ///  - zero parameters or 
        ///  - a single `in ReadContext` parameter.
        /// 
        /// If a method is static, it can take:
        ///  - zero parameters or
        ///  - a single parameter of the row type or
        ///  - a single parameter of `in ReadContext` or
        ///  - two parameters, the first of the row type (which may be by ref) and the second of `in ReadContext`
        /// 
        /// If the reset is instance or takes a of the row type parameter, the instance or parameter
        ///   type must be assignable from the type being deserialized.
        /// </summary>
        public static Reset ForMethod(MethodInfo method)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            if (method.ReturnType.GetTypeInfo() != Types.Void)
            {
                return Throw.ArgumentException<Reset>($"{method} does not return void", nameof(method));
            }

            TypeInfo? rowType;
            NullHandling? rowTypeNullability;

            bool takesContext;
            var args = method.GetParameters();
            if (method.IsStatic)
            {
                if (args.Length == 0)
                {
                    // we're fine
                    rowType = null;
                    rowTypeNullability = null;
                    takesContext = false;
                }
                else if (args.Length == 1)
                {
                    var arg0 = args[0];
                    var p0 = arg0.ParameterType.GetTypeInfo();

                    if (p0.IsByRef)
                    {
                        var p0Elem = p0.GetElementTypeNonNull();
                        if (p0Elem != Types.ReadContext)
                        {
                            return Throw.ArgumentException<Reset>($"A {nameof(Reset)} backed by a static method with a single by ref parameter must take `in {nameof(ReadContext)}`, was not `{nameof(ReadContext)}`", nameof(method));
                        }

                        rowType = null;
                        rowTypeNullability = null;
                        takesContext = true;
                    }
                    else
                    {
                        rowType = p0;
                        rowTypeNullability = arg0.DetermineNullability();
                        takesContext = false;
                    }
                }
                else if (args.Length == 2)
                {
                    var arg0 = args[0];
                    rowType = arg0.ParameterType.GetTypeInfo();

                    if (rowType.IsByRef)
                    {
                        rowType = rowType.GetElementTypeNonNull();
                    }

                    rowTypeNullability = arg0.DetermineNullability();

                    var p1 = args[1].ParameterType.GetTypeInfo();
                    if (!args[1].IsReadContextByRef(out var msg))
                    {
                        return Throw.ArgumentException<Reset>($"A {nameof(Reset)} backed by a static method taking 2 parameters must take `in {nameof(ReadContext)}` as it's second parameter; {msg}", nameof(method));
                    }

                    takesContext = true;
                }
                else
                {
                    return Throw.ArgumentException<Reset>($"{method} is static, it must take 0, 1, or 2 parameters", nameof(method));
                }
            }
            else
            {
                rowTypeNullability = NullHandling.CannotBeNull;

                if (args.Length == 0)
                {
                    rowType = method.DeclaringTypeNonNull();
                    takesContext = false;
                }
                else if (args.Length == 1)
                {
                    rowType = method.DeclaringTypeNonNull();

                    if (!args[0].IsReadContextByRef(out var msg))
                    {
                        return Throw.ArgumentException<Reset>($"A {nameof(Reset)} backed by a instance method taking a single parameter must take `in {nameof(ReadContext)}`; {msg}", nameof(method));
                    }

                    takesContext = true;
                }
                else
                {
                    return Throw.ArgumentException<Reset>($"{method} is an instance method, it must take 0 or 1 parameters", nameof(method));
                }

                rowType = method.DeclaringTypeNonNull();
            }

            return new Reset(rowType, method, takesContext, rowTypeNullability);
        }


        /// <summary>
        /// Create a reset from a delegate.
        /// </summary>
        public static Reset ForDelegate<TRow>(ResetDelegate<TRow> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            var nullability = del.Method.GetParameters()[0].DetermineNullability();

            return new Reset(typeof(TRow).GetTypeInfo(), del, nullability);
        }

        /// <summary>
        /// Create a reset from a delegate.
        /// </summary>
        public static Reset ForDelegate<TRow>(ResetByRefDelegate<TRow> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            var nullability = del.Method.GetParameters()[0].DetermineNullability();

            return new Reset(typeof(TRow).GetTypeInfo(), del, nullability);
        }

        /// <summary>
        /// Create a reset from a delegate.
        /// </summary>
        public static Reset ForDelegate(StaticResetDelegate del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new Reset(null, del, null);
        }

        /// <summary>
        /// Returns true if this object equals the given Reset.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is Reset r)
            {
                return Equals(r);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given Reset.
        /// </summary>
        public bool Equals(Reset? reset)
        {
            if (ReferenceEquals(reset, null)) return false;

            var mode = Mode;
            var otherMode = reset.Mode;

            if (mode != otherMode) return false;

            if (IsStatic != reset.IsStatic) return false;

            if (RowType.HasValue)
            {
                if (!reset.RowType.HasValue) return false;

                if (RowType.Value != reset.RowType.Value) return false;
            }
            else
            {
                if (reset.RowType.HasValue) return false;
            }

            return
                mode switch
                {
                    BackingMode.Delegate => reset.Delegate.Value == Delegate.Value,
                    BackingMode.Method => reset.Method.Value == Method.Value,
                    _ => Throw.ImpossibleException<bool>($"Unexpected {nameof(BackingMode)}: {mode}")
                };
        }

        /// <summary>
        /// Returns a stable hash for this Reset.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(Reset), Delegate, IsStatic, Method, Mode, RowType);

        /// <summary>
        /// Describes this Reset.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case BackingMode.Method:

                    if (IsStatic)
                    {
                        return $"{nameof(Reset)} backed by method {Method}";
                    }
                    else
                    {
                        return $"{nameof(Reset)} backed by method {Method} taking {RowType}";
                    }
                case BackingMode.Delegate:
                    if (IsStatic)
                    {
                        return $"{nameof(Reset)} backed by delegate {Delegate}";
                    }
                    else
                    {
                        return $"{nameof(Reset)} backed by delegate {Delegate} taking {RowType}";
                    }
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling Reset.ForMethod if non-null.
        /// 
        /// Returns null if method is null.
        /// </summary>
        public static explicit operator Reset?(MethodInfo? method)
        => method == null ? null : ForMethod(method);

        /// <summary>
        /// Convenience operator, equivalent to calling Reset.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator Reset?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();
            if (delType == Types.StaticResetDelegate)
            {
                return new Reset(null, del, null);
            }

            if (delType.IsGenericType && delType.GetGenericTypeDefinition().GetTypeInfo() == Types.ResetDelegate)
            {
                var rowType = delType.GetGenericArguments()[0].GetTypeInfo();

                var nullability = del.Method.GetParameters()[0].DetermineNullability();

                return new Reset(rowType, del, nullability);
            }

            var mtd = del.Method;
            var retType = mtd.ReturnType.GetTypeInfo();
            if (retType != Types.Void)
            {
                return Throw.InvalidOperationException<Reset>($"Delegate must return void, found {retType}");
            }

            var invoke = delType.GetMethodNonNull("Invoke");

            var args = mtd.GetParameters();
            if (args.Length == 1)
            {
                if (!args[0].IsReadContextByRef(out var msg))
                {
                    return Throw.InvalidOperationException<Reset>($"Delegate of one parameter must take an `in {nameof(ReadContext)}`; {msg}");
                }

                var reboundDel = System.Delegate.CreateDelegate(Types.StaticResetDelegate, del, invoke);

                return new Reset(null, reboundDel, null);
            }
            else if (args.Length == 2)
            {
                var arg0 = args[0];
                var rowType = arg0.ParameterType.GetTypeInfo();

                if (!args[1].IsReadContextByRef(out var msg))
                {
                    return Throw.InvalidOperationException<Reset>($"Delegate of two parameters must take an `in {nameof(ReadContext)}` as it's second parameter; {msg}");
                }

                var nullability = arg0.DetermineNullability();

                TypeInfo rebindDelType;
                if (rowType.IsByRef)
                {
                    rowType = rowType.GetElementTypeNonNull();
                    rebindDelType = Types.ResetByRefDelegate;
                }
                else
                {
                    rebindDelType = Types.ResetDelegate;
                }

                var getterDelType = rebindDelType.MakeGenericType(rowType);

                var reboundDel = System.Delegate.CreateDelegate(getterDelType, del, invoke);

                return new Reset(rowType, reboundDel, nullability);
            }
            else
            {
                return Throw.InvalidOperationException<Reset>("Delegate must take 1 or 2 parameters");
            }
        }

        /// <summary>
        /// Compare two Resets for equality
        /// </summary>
        public static bool operator ==(Reset? a, Reset? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Resets for inequality
        /// </summary>
        public static bool operator !=(Reset? a, Reset? b)
        => !(a == b);
    }
}