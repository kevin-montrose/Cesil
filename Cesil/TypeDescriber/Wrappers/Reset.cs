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
    public delegate void ResetDelegate<T>(T onType);

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
                if (Method != null) return BackingMode.Method;
                if (Delegate != null) return BackingMode.Delegate;

                return BackingMode.None;
            }
        }

        internal bool IsStatic
        {
            get
            {
                switch (Mode)
                {
                    case BackingMode.Method: return Method.IsStatic;
                    case BackingMode.Delegate: return RowType == null;

                    default:
                        Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {Mode}");
                        // just for control flow
                        return default;
                }
            }
        }

        internal MethodInfo Method { get; }
        internal Delegate Delegate { get; }

        internal TypeInfo RowType { get; }

        private Reset(TypeInfo rowType, MethodInfo mtd)
        {
            RowType = rowType;
            Method = mtd;
        }

        private Reset(TypeInfo rowType, Delegate del)
        {
            RowType = rowType;
            Delegate = del;
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
        public static Reset ForMethod(MethodInfo resetMethod)
        {
            if (resetMethod == null)
            {
                Throw.ArgumentNullException(nameof(resetMethod));
            }

            TypeInfo rowType;

            var args = resetMethod.GetParameters();
            if (resetMethod.IsStatic)
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
                    Throw.ArgumentException($"{resetMethod} is static, it must take 0 or 1 parameters", nameof(resetMethod));
                    // won't actually be reached
                    return default;
                }
            }
            else
            {
                if (args.Length != 0)
                {
                    Throw.ArgumentException($"{resetMethod} is an instance method, it must take 0 parameters", nameof(resetMethod));
                }

                rowType = resetMethod.DeclaringType.GetTypeInfo();
            }

            return new Reset(rowType, resetMethod);
        }


        /// <summary>
        /// Create a reset from a delegate.
        /// </summary>
        public static Reset ForDelegate<T>(ResetDelegate<T> del)
        {
            if (del == null)
            {
                Throw.ArgumentNullException(nameof(del));
            }

            return new Reset(typeof(T).GetTypeInfo(), del);
        }

        /// <summary>
        /// Create a reset from a delegate.
        /// </summary>
        public static Reset ForDelegate(StaticResetDelegate del)
        {
            if (del == null)
            {
                Throw.ArgumentNullException(nameof(del));
            }

            return new Reset(null, del);
        }

        /// <summary>
        /// Returns true if this object equals the given Reset.
        /// </summary>
        public override bool Equals(object obj)
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
        public bool Equals(Reset r)
        {
            if (r == null) return false;

            return
                r.Delegate == Delegate &&
                r.IsStatic == IsStatic &&
                r.Method == Method &&
                r.Mode == Mode &&
                r.RowType == RowType;
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
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {Mode}");
                    // just for control flow
                    return default;
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling Reset.ForMethod if non-null.
        /// 
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator Reset(MethodInfo mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling Reset.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator Reset(Delegate del)
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
                Throw.InvalidOperationException($"Delegate must return void, found {retType}");
            }

            var invoke = delType.GetMethod("Invoke");

            var args = mtd.GetParameters();
            if (args.Length == 0)
            {
                var reboundDel = Delegate.CreateDelegate(Types.StaticResetDelegateType, del, invoke);

                return new Reset(null, reboundDel);
            }
            else if (args.Length == 1)
            {
                var rowType = args[0].ParameterType.GetTypeInfo();
                var getterDelType = Types.ResetDelegateType.MakeGenericType(rowType);

                var reboundDel = Delegate.CreateDelegate(getterDelType, del, invoke);

                return new Reset(rowType, reboundDel);
            }
            else
            {
                Throw.InvalidOperationException("Delegate must take 0 or 1 parameters");
                // just for control flow
                return default;
            }
        }

        /// <summary>
        /// Compare two Resets for equality
        /// </summary>
        public static bool operator ==(Reset a, Reset b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Resets for inequality
        /// </summary>
        public static bool operator !=(Reset a, Reset b)
        => !(a == b);
    }
}