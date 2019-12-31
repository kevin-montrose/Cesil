using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for resets that don't take an instance of the row.
    /// </summary>
    public delegate void StaticResetDelegate();

    /// <summary>
    /// Delegate type for resets.
    /// </summary>
    public delegate void ResetDelegate<TRow>(TRow onType);

    /// <summary>
    /// Represents code called before a setter is called or a field
    ///   is set.
    /// 
    /// Wraps either a MethodInfo, a ResetDelegate, or a StaticResetDelegate.
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
                switch (Mode)
                {
                    case BackingMode.Method: return Method.Value.IsStatic;
                    case BackingMode.Delegate: return !RowType.HasValue;

                    default:
                        return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}");
                }
            }
        }

        internal readonly NonNull<MethodInfo> Method;

        internal readonly NonNull<Delegate> Delegate;

        internal readonly NonNull<TypeInfo> RowType;

        private Reset(TypeInfo? rowType, MethodInfo mtd)
        {
            RowType.SetAllowNull(rowType);
            Method.Value = mtd;
        }

        private Reset(TypeInfo? rowType, Delegate del)
        {
            RowType.SetAllowNull(rowType);
            Delegate.Value = del;
        }

        /// <summary>
        /// Create a reset from a method.
        /// 
        /// If method is an instance, it can take no parameters.
        /// 
        /// If a method is static, it can take one or zero parameters.
        /// 
        /// If the reset is instance or takes a parameter, the instance or parameter
        ///   type must be assignable from the type being deserialized.
        /// </summary>
        public static Reset ForMethod(MethodInfo method)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            if (method.ReturnType.GetTypeInfo() != Types.VoidType)
            {
                return Throw.ArgumentException<Reset>($"{method} does not return void", nameof(method));
            }

            TypeInfo? rowType;

            var args = method.GetParameters();
            if (method.IsStatic)
            {
                if (args.Length == 0)
                {
                    // we're fine
                    rowType = null;
                }
                else if (args.Length == 1)
                {
                    rowType = args[0].ParameterType.GetTypeInfo();
                }
                else
                {
                    return Throw.ArgumentException<Reset>($"{method} is static, it must take 0 or 1 parameters", nameof(method));
                }
            }
            else
            {
                if (args.Length != 0)
                {
                    return Throw.ArgumentException<Reset>($"{method} is an instance method, it must take 0 parameters", nameof(method));
                }

                rowType = method.DeclaringTypeNonNull();
            }

            return new Reset(rowType, method);
        }


        /// <summary>
        /// Create a reset from a delegate.
        /// </summary>
        public static Reset ForDelegate<TRow>(ResetDelegate<TRow> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new Reset(typeof(TRow).GetTypeInfo(), del);
        }

        /// <summary>
        /// Create a reset from a delegate.
        /// </summary>
        public static Reset ForDelegate(StaticResetDelegate del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new Reset(null, del);
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
        public bool Equals(Reset reset)
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

            switch (mode)
            {
                case BackingMode.Delegate:
                    return reset.Delegate.Value == Delegate.Value;
                case BackingMode.Method:
                    return reset.Method.Value == Method.Value;

                default:
                    return Throw.Exception<bool>($"Unexpected {nameof(BackingMode)}: {mode}");

            }
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
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator Reset?(MethodInfo? mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling Reset.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator Reset?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();
            if (delType == Types.StaticResetDelegateType)
            {
                return new Reset(null, del);
            }

            if (delType.IsGenericType && delType.GetGenericTypeDefinition().GetTypeInfo() == Types.ResetDelegateType)
            {
                var rowType = delType.GetGenericArguments()[0].GetTypeInfo();

                return new Reset(rowType, del);
            }

            var mtd = del.Method;
            var retType = mtd.ReturnType.GetTypeInfo();
            if (retType != Types.VoidType)
            {
                return Throw.InvalidOperationException<Reset>($"Delegate must return void, found {retType}");
            }

            var invoke = delType.GetMethodNonNull("Invoke");

            var args = mtd.GetParameters();
            if (args.Length == 0)
            {
                var reboundDel = System.Delegate.CreateDelegate(Types.StaticResetDelegateType, del, invoke);

                return new Reset(null, reboundDel);
            }
            else if (args.Length == 1)
            {
                var rowType = args[0].ParameterType.GetTypeInfo();
                var getterDelType = Types.ResetDelegateType.MakeGenericType(rowType);

                var reboundDel = System.Delegate.CreateDelegate(getterDelType, del, invoke);

                return new Reset(rowType, reboundDel);
            }
            else
            {
                return Throw.InvalidOperationException<Reset>("Delegate must take 0 or 1 parameters");
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