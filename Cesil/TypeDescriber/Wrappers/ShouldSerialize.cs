using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for 'should serialize' that don't take an instance.
    /// </summary>
    public delegate bool StaticShouldSerializeDelegate();

    /// <summary>
    /// Delegate type for 'should serialize'.
    /// </summary>
    public delegate bool ShouldSerializeDelegate<T>(T instance);

    /// <summary>
    /// Represents code used to determine whether or not to write a value.
    /// 
    /// Wraps either a MethodInfo, a ShouldSerializeDelegate, or a StaticShouldSerializeDelegate.
    /// </summary>
    public sealed class ShouldSerialize : IEquatable<ShouldSerialize>
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
                    case BackingMode.Delegate: return Takes == null;
                    default:
                        Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {Mode}");
                        // just for control flow
                        return default;
                }
            }
        }

        internal MethodInfo Method { get; }
        internal Delegate Delegate { get; }

        internal TypeInfo Takes { get; }

        private ShouldSerialize(TypeInfo takes, MethodInfo method)
        {
            Takes = takes;
            Method = method;
            Delegate = null;
        }

        private ShouldSerialize(TypeInfo takes, Delegate del)
        {
            Takes = takes;
            Method = null;
            Delegate = del;
        }

        /// <summary>
        /// Create a ShouldSerialize from a method.
        /// 
        /// Method must be an argument-less method
        ///   that is either static, or on an instance of the same type as (or a baseclass of) setter.
        /// </summary>
        public static ShouldSerialize ForMethod(MethodInfo method)
        {
            if (method == null)
            {
                Throw.ArgumentNullException(nameof(method));
            }

            var args = method.GetParameters();
            if (args.Length > 0)
            {
                Throw.ArgumentException($"{nameof(method)} cannot take parameters", nameof(method));
            }

            var ret = method.ReturnType.GetTypeInfo();
            if (ret != Types.BoolType)
            {
                Throw.ArgumentException($"{nameof(method)} must return a boolean", nameof(method));
            }

            TypeInfo takes;

            if (!method.IsStatic)
            {
                var shouldSerializeInstType = method.DeclaringType.GetTypeInfo();

                takes = shouldSerializeInstType;
            }
            else
            {
                takes = null;
            }

            return new ShouldSerialize(takes, method);
        }

        /// <summary>
        /// Create a ShouldSerialize from the given delegate.
        /// </summary>
        public static ShouldSerialize ForDelegate<T>(ShouldSerializeDelegate<T> del)
        {
            if (del == null)
            {
                Throw.ArgumentNullException(nameof(del));
            }

            return new ShouldSerialize(typeof(T).GetTypeInfo(), del);
        }

        /// <summary>
        /// Create a ShouldSerialize from the given delegate.
        /// </summary>
        public static ShouldSerialize ForDelegate(StaticShouldSerializeDelegate del)
        {
            if (del == null)
            {
                Throw.ArgumentNullException(nameof(del));
            }

            return new ShouldSerialize(null, del);
        }

        /// <summary>
        /// Returns true if this object equals the given ShouldSerialize.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is ShouldSerialize s)
            {
                return Equals(s);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given ShouldSerialize.
        /// </summary>
        public bool Equals(ShouldSerialize s)
        {
            if (s == null) return false;

            return
                s.Delegate == Delegate &&
                s.IsStatic == IsStatic &&
                s.Method == Method &&
                s.Mode == Mode &&
                s.Takes == Takes;
        }

        /// <summary>
        /// Returns a stable hash for this ShouldSerialize.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(ShouldSerialize), Delegate, IsStatic, Method, Mode, Takes);

        /// <summary>
        /// Describes this ShouldSerialize.
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
                        return $"{nameof(ShouldSerialize)} backed by method {Method}";
                    }
                    else
                    {
                        return $"{nameof(ShouldSerialize)} backed by method {Method} on {Takes}";
                    }
                case BackingMode.Delegate:
                    if (IsStatic)
                    {
                        return $"{nameof(ShouldSerialize)} backed by delegate {Delegate}";
                    }
                    else
                    {
                        return $"{nameof(ShouldSerialize)} backed by delegate {Delegate} taking {Takes}";
                    }
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {Mode}");
                    // just for control flow
                    return default;
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling ShouldSerialize.ForMethod if non-null.
        /// 
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator ShouldSerialize(MethodInfo mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling ShouldSerialize.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator ShouldSerialize(Delegate del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();
            if (delType == Types.StaticShouldSerializeDelegateType)
            {
                return new ShouldSerialize(null, del);
            }

            if (delType.IsGenericType && delType.GetGenericTypeDefinition().GetTypeInfo() == Types.ShouldSerializeDelegateType)
            {
                var genArgs = delType.GetGenericArguments();
                var takesType = genArgs[0].GetTypeInfo();

                return new ShouldSerialize(takesType, del);
            }

            var mtd = del.Method;
            var retType = mtd.ReturnType.GetTypeInfo();
            if (retType != Types.BoolType)
            {
                Throw.InvalidOperationException($"Delegate must return boolean, found {retType}");
            }

            var invoke = delType.GetMethod("Invoke");

            var ps = mtd.GetParameters();
            if (ps.Length == 0)
            {
                var reboundDel = Delegate.CreateDelegate(Types.StaticShouldSerializeDelegateType, del, invoke);

                return new ShouldSerialize(null, reboundDel);
            }
            else if (ps.Length == 1)
            {
                var takesType = ps[0].ParameterType.GetTypeInfo();
                var shouldSerializeDelType = Types.ShouldSerializeDelegateType.MakeGenericType(takesType);
                var reboundDel = Delegate.CreateDelegate(shouldSerializeDelType, del, invoke);

                return new ShouldSerialize(takesType, reboundDel);
            }
            else
            {
                Throw.InvalidOperationException($"Delegate must take 0 or 1 paramters");
                // just for control flow
                return default;
            }
        }

        /// <summary>
        /// Compare two ShouldSerializes for equality
        /// </summary>
        public static bool operator ==(ShouldSerialize a, ShouldSerialize b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two ShouldSerializes for inequality
        /// </summary>
        public static bool operator !=(ShouldSerialize a, ShouldSerialize b)
        => !(a == b);
    }
}
