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
                if (_Method != null) return BackingMode.Method;
                if (_Delegate != null) return BackingMode.Delegate;

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
                    case BackingMode.Delegate: return !HasTakes;
                    default:
                        return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}");
                }
            }
        }

        private readonly MethodInfo? _Method;
        internal MethodInfo Method => Utils.NonNull(_Method);

        private readonly Delegate? _Delegate;
        internal Delegate Delegate => Utils.NonNull(_Delegate);

        private readonly TypeInfo? _Takes;
        internal bool HasTakes => _Takes != null;
        internal TypeInfo Takes => Utils.NonNull(_Takes);

        private ShouldSerialize(TypeInfo? takes, MethodInfo method)
        {
            _Takes = takes;
            _Method = method;
            _Delegate = null;
        }

        private ShouldSerialize(TypeInfo? takes, Delegate del)
        {
            _Takes = takes;
            _Method = null;
            _Delegate = del;
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
                return Throw.ArgumentNullException<ShouldSerialize>(nameof(method));
            }

            var args = method.GetParameters();
            if (args.Length > 0)
            {
                return Throw.ArgumentException<ShouldSerialize>($"{nameof(method)} cannot take parameters", nameof(method));
            }

            var ret = method.ReturnType.GetTypeInfo();
            if (ret != Types.BoolType)
            {
                return Throw.ArgumentException<ShouldSerialize>($"{nameof(method)} must return a boolean", nameof(method));
            }

            TypeInfo? takes;

            if (!method.IsStatic)
            {
                var shouldSerializeInstType = method.DeclaringTypeNonNull();

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
                return Throw.ArgumentNullException<ShouldSerialize>(nameof(del));
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
                return Throw.ArgumentNullException<ShouldSerialize>(nameof(del));
            }

            return new ShouldSerialize(null, del);
        }

        /// <summary>
        /// Returns true if this object equals the given ShouldSerialize.
        /// </summary>
        public override bool Equals(object? obj)
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
            if (ReferenceEquals(s, null)) return false;

            var mode = Mode;
            var otherMode = s.Mode;
            if (mode != otherMode) return false;

            if (IsStatic != s.IsStatic) return false;

            if (HasTakes)
            {
                if (!s.HasTakes) return false;

                if (Takes != s.Takes) return false;
            }
            else
            {
                if (s.HasTakes) return false;
            }

            switch (mode)
            {
                case BackingMode.Delegate:
                    return Delegate == s.Delegate;
                case BackingMode.Method:
                    return Method == s.Method;

                default:
                    return Throw.Exception<bool>($"Unexpected {nameof(BackingMode)}: {mode}");
            }
        }

        /// <summary>
        /// Returns a stable hash for this ShouldSerialize.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(ShouldSerialize), _Delegate, IsStatic, _Method, Mode, _Takes);

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
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling ShouldSerialize.ForMethod if non-null.
        /// 
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator ShouldSerialize?(MethodInfo? mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling ShouldSerialize.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator ShouldSerialize?(Delegate? del)
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
                return Throw.InvalidOperationException<ShouldSerialize>($"Delegate must return boolean, found {retType}");
            }

            var invoke = delType.GetMethodNonNull("Invoke");

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
                return Throw.InvalidOperationException<ShouldSerialize>($"Delegate must take 0 or 1 paramters");
            }
        }

        /// <summary>
        /// Compare two ShouldSerializes for equality
        /// </summary>
        public static bool operator ==(ShouldSerialize? a, ShouldSerialize? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two ShouldSerializes for inequality
        /// </summary>
        public static bool operator !=(ShouldSerialize? a, ShouldSerialize? b)
        => !(a == b);
    }
}
