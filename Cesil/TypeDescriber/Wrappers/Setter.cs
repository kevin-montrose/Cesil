using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for setters that don't take an instance.
    /// </summary>
    public delegate void StaticSetterDelegate<V>(V value);

    /// <summary>
    /// Delegate type for setters.
    /// </summary>
    public delegate void SetterDelegate<T, V>(T instance, V value);

    /// <summary>
    /// Represents code used to set parsed values onto types.
    /// 
    /// Wraps either a MethodInfo, a FieldInfo, a SetterDelegate, or a StaticSetterDelegate.
    /// </summary>
    public sealed class Setter : IEquatable<Setter>
    {
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

        internal readonly TypeInfo Takes;

        private Setter(TypeInfo? rowType, TypeInfo takes, MethodInfo method)
        {
            RowType.SetAllowNull(rowType);
            Takes = takes;
            Method.Value = method;
        }

        private Setter(TypeInfo? rowType, TypeInfo takes, Delegate del)
        {
            RowType.SetAllowNull(rowType);
            Takes = takes;
            Delegate.Value = del;
        }

        private Setter(TypeInfo? rowType, TypeInfo takes, FieldInfo field)
        {
            RowType.SetAllowNull(rowType);
            Takes = takes;
            Field.Value = field;
        }

        /// <summary>
        /// Create a setter from a method.
        /// 
        /// setter must take single parameter (the result of parser)
        ///   can be instance or static
        ///   and cannot return a value
        /// -- OR --
        /// setter must take two parameters, 
        ///    the first is the record value
        ///    the second is the value (the result of parser)
        ///    cannot return a value
        ///    and must be static
        /// </summary>
        public static Setter ForMethod(MethodInfo setter)
        {
            if (setter == null)
            {
                return Throw.ArgumentNullException<Setter>(nameof(setter));
            }

            var returnsNoValue = setter.ReturnType == Types.VoidType;

            if (!returnsNoValue)
            {
                return Throw.ArgumentException<Setter>($"{nameof(setter)} must not return a value", nameof(setter));
            }

            TypeInfo? setOnType;
            TypeInfo takesType;

            var args = setter.GetParameters();
            if (args.Length == 1)
            {
                takesType = args[0].ParameterType.GetTypeInfo();

                if (setter.IsStatic)
                {
                    setOnType = null;
                }
                else
                {
                    setOnType = setter.DeclaringTypeNonNull();
                }
            }
            else if (args.Length == 2)
            {
                setOnType = args[0].ParameterType.GetTypeInfo();
                takesType = args[1].ParameterType.GetTypeInfo();

                if (!setter.IsStatic)
                {
                    return Throw.ArgumentException<Setter>($"{nameof(setter)} taking two parameters must be static", nameof(setter));
                }
            }
            else
            {
                return Throw.ArgumentException<Setter>($"{nameof(setter)} must take one or two parameters", nameof(setter));
            }

            return new Setter(setOnType, takesType, setter);
        }

        /// <summary>
        /// Creates setter from a field.
        /// 
        /// Field can be either an instance field or static field.
        /// </summary>
        public static Setter ForField(FieldInfo field)
        {
            if (field == null)
            {
                return Throw.ArgumentNullException<Setter>(nameof(field));
            }

            TypeInfo? rowType;
            if (field.IsStatic)
            {
                rowType = null;
            }
            else
            {
                rowType = field.DeclaringTypeNonNull();
            }

            var takesType = field.FieldType.GetTypeInfo();

            return new Setter(rowType, takesType, field);
        }

        /// <summary>
        /// Create a Setter from the given delegate.
        /// </summary>
        public static Setter ForDelegate<V>(StaticSetterDelegate<V> del)
        {
            if (del == null)
            {
                return Throw.ArgumentNullException<Setter>(nameof(del));
            }

            var takesType = typeof(V).GetTypeInfo();

            return new Setter(null, takesType, del);
        }

        /// <summary>
        /// Create a Setter from the given delegate.
        /// </summary>
        public static Setter ForDelegate<T, V>(SetterDelegate<T, V> del)
        {
            if (del == null)
            {
                return Throw.ArgumentNullException<Setter>(nameof(del));
            }

            var setOnType = typeof(T).GetTypeInfo();
            var takesType = typeof(V).GetTypeInfo();

            return new Setter(setOnType, takesType, del);
        }

        /// <summary>
        /// Returns true if this object equals the given Setter.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is Setter s)
            {
                return Equals(s);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given Setter.
        /// </summary>
        public bool Equals(Setter s)
        {
            if (ReferenceEquals(s, null)) return false;

            var mode = Mode;
            var otherMode = s.Mode;

            if (mode != otherMode) return false;
            if (Takes != s.Takes) return false;
            if (IsStatic != IsStatic) return false;

            if (RowType.HasValue)
            {
                if (!s.RowType.HasValue) return false;

                if (RowType.Value != s.RowType.Value) return false;
            }
            else
            {
                if (s.RowType.HasValue) return false;
            }

            switch (mode)
            {
                case BackingMode.Delegate: return Delegate.Value == s.Delegate.Value;
                case BackingMode.Field: return Field.Value == s.Field.Value;
                case BackingMode.Method: return Method.Value == s.Method.Value;

                default:
                    return Throw.Exception<bool>($"Unexpected {nameof(BackingMode)}: {mode}");
            }
        }

        /// <summary>
        /// Returns a stable hash for this Setter.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(Setter), Delegate, Field, IsStatic, Method, Mode, RowType, Takes);

        /// <summary>
        /// Describes this Setter.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case BackingMode.Delegate:
                    if (IsStatic)
                    {
                        return $"{nameof(Setter)} backed by delegate {Delegate} taking {Takes}";
                    }
                    else
                    {
                        return $"{nameof(Setter)} backed by delegate {Delegate} taking {RowType} and {Takes}";
                    }
                case BackingMode.Method:
                    if (IsStatic)
                    {
                        return $"{nameof(Setter)} backed by method {Method} taking {Takes}";
                    }
                    else
                    {
                        return $"{nameof(Setter)} on {RowType} backed by method {Method} taking {Takes}";
                    }
                case BackingMode.Field:
                    if (IsStatic)
                    {
                        return $"{nameof(Setter)} backed by field {Field} of {Takes}";
                    }
                    else
                    {
                        return $"{nameof(Setter)} on {RowType} backed by field {Field} of {Takes}";
                    }
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling Setter.ForMethod if non-null.
        /// 
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator Setter?(MethodInfo? mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling Setter.ForField if non-null.
        /// 
        /// Returns null if field is null.
        /// </summary>
        public static explicit operator Setter?(FieldInfo? field)
        => field == null ? null : ForField(field);

        /// <summary>
        /// Convenience operator, equivalent to calling Setter.ForDelegate if non-null.
        /// 
        /// Returns null if field is null.
        /// </summary>
        public static explicit operator Setter?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();
            if (delType.IsGenericType)
            {
                var delGenType = delType.GetGenericTypeDefinition().GetTypeInfo();
                if (delGenType == Types.SetterDelegateType)
                {
                    var genArgs = delType.GetGenericArguments();
                    var rowType = genArgs[0].GetTypeInfo();
                    var takesType = genArgs[1].GetTypeInfo();

                    return new Setter(rowType, takesType, del);
                }
                else if (delGenType == Types.StaticSetterDelegateType)
                {
                    var genArgs = delType.GetGenericArguments();
                    var takesType = genArgs[0].GetTypeInfo();

                    return new Setter(null, takesType, del);
                }
            }

            var mtd = del.Method;
            var retType = mtd.ReturnType.GetTypeInfo();
            if (retType != Types.VoidType)
            {
                return Throw.InvalidOperationException<Setter>($"Delegate must return void, found {retType}");
            }

            var ps = mtd.GetParameters();
            var invoke = delType.GetMethodNonNull("Invoke");
            if (ps.Length == 2)
            {
                var rowType = ps[0].ParameterType.GetTypeInfo();
                var takesType = ps[1].ParameterType.GetTypeInfo();

                var setterDelType = Types.SetterDelegateType.MakeGenericType(rowType, takesType);

                var reboundDel = System.Delegate.CreateDelegate(setterDelType, del, invoke);

                return new Setter(rowType, takesType, reboundDel);
            }
            else if (ps.Length == 1)
            {
                var takesType = ps[0].ParameterType.GetTypeInfo();
                var setterDelType = Types.StaticSetterDelegateType.MakeGenericType(takesType);

                var reboundDel = System.Delegate.CreateDelegate(setterDelType, del, invoke);

                return new Setter(null, takesType, reboundDel);
            }
            else
            {
                return Throw.InvalidOperationException<Setter>("Delegate must take 1 or 2 parameters");
            }
        }

        /// <summary>
        /// Compare two Setters for equality
        /// </summary>
        public static bool operator ==(Setter? a, Setter? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Setters for inequality
        /// </summary>
        public static bool operator !=(Setter? a, Setter? b)
        => !(a == b);
    }
}
