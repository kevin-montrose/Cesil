using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate type for 'should serialize' that don't take a row.
    /// </summary>
    public delegate bool StaticShouldSerializeDelegate(in WriteContext context);

    /// <summary>
    /// Delegate type for 'should serialize'.
    /// </summary>
    public delegate bool ShouldSerializeDelegate<TRow>(TRow instance, in WriteContext context);

    /// <summary>
    /// Represents code used to determine whether or not to write a value.
    /// 
    /// Wraps a static method, an instance method, or a delegate.
    /// </summary>
    public sealed class ShouldSerialize : IEquatable<ShouldSerialize>
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
                        BackingMode.Delegate => !Takes.HasValue,
                        _ => Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}")
                    };
            }
        }

        internal readonly NonNull<MethodInfo> Method;
        internal readonly NonNull<Delegate> Delegate;
        internal readonly NonNull<TypeInfo> Takes;
        internal readonly bool TakesContext;

        private ShouldSerialize(TypeInfo? takes, MethodInfo method, bool takesContext)
        {
            Takes.SetAllowNull(takes);
            Method.Value = method;
            Delegate.Clear();
            TakesContext = takesContext;
        }

        private ShouldSerialize(TypeInfo? takes, Delegate del)
        {
            Takes.SetAllowNull(takes);
            Method.Clear();
            Delegate.Value = del;
            TakesContext = true;
        }

        internal Expression MakeExpression(ParameterExpression rowVar, ParameterExpression ctxVar)
        {
            // todo: would require some work to make this chainable... but doable?

            Expression selfExp;

            switch (Mode)
            {
                case BackingMode.Method:
                    {
                        var mtd = Method.Value;

                        if (IsStatic)
                        {
                            if (Takes.HasValue)
                            {
                                if (TakesContext)
                                {
                                    selfExp = Expression.Call(mtd, rowVar, ctxVar);
                                }
                                else
                                {
                                    selfExp = Expression.Call(mtd, rowVar);
                                }
                            }
                            else
                            {
                                if (TakesContext)
                                {
                                    selfExp = Expression.Call(mtd, ctxVar);
                                }
                                else
                                {
                                    selfExp = Expression.Call(mtd);
                                }
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
                    }
                    break;
                case BackingMode.Delegate:
                    {
                        var shouldSerializeDel = Delegate.Value;
                        var delRef = Expression.Constant(shouldSerializeDel);

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
        /// Create a ShouldSerialize from a method.
        /// 
        /// Method must return bool.
        /// 
        /// If method is an instance method it must:
        ///   - take zero parameters or
        ///   - take one parameter, of type `in WriteContext`
        /// 
        /// If method is a static method, it must:
        ///   - take zero parameters or 
        ///   - take one parameter of the type being serialized or
        ///   - take two parameters, the first being the type being serialized and the second being `in WriteContext`
        /// </summary>
        public static ShouldSerialize ForMethod(MethodInfo method)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            var ret = method.ReturnType.GetTypeInfo();
            if (ret != Types.Bool)
            {
                return Throw.ArgumentException<ShouldSerialize>($"{nameof(method)} must return a boolean", nameof(method));
            }

            var args = method.GetParameters();

            TypeInfo? takes;
            bool takesContext;

            if (!method.IsStatic)
            {
                takes = method.DeclaringTypeNonNull();

                if (args.Length == 0)
                {
                    takesContext = false;
                }
                else if (args.Length == 1)
                {
                    if (!args[0].IsWriteContextByRef(out var msg))
                    {
                        return Throw.ArgumentException<ShouldSerialize>($"If an instance method takes a parameter it must be a `in {nameof(WriteContext)}`; {msg}", nameof(method));
                    }

                    takesContext = true;
                }
                else
                {
                    return Throw.ArgumentException<ShouldSerialize>($"{nameof(method)} cannot take parameters, it's an instance method", nameof(method));
                }
            }
            else
            {
                if (args.Length == 0)
                {
                    takes = null;
                    takesContext = false;
                }
                else if (args.Length == 1)
                {
                    var p0 = args[0].ParameterType.GetTypeInfo();

                    if (p0.IsByRef)
                    {
                        var p0Elem = p0.GetElementTypeNonNull();
                        if (p0Elem != Types.WriteContext)
                        {
                            return Throw.ArgumentException<ShouldSerialize>($"If an static method takes one parameter and it is by ref it must be a `in {nameof(WriteContext)}`, wasn't `{nameof(WriteContext)}`", nameof(method));
                        }

                        takes = null;
                        takesContext = true;
                    }
                    else
                    {
                        takes = p0;
                        takesContext = false;
                    }
                }
                else if (args.Length == 2)
                {
                    takes = args[0].ParameterType.GetTypeInfo();

                    if (!args[1].IsWriteContextByRef(out var msg))
                    {
                        return Throw.ArgumentException<ShouldSerialize>($"If an static method takes two parameters the second must be a `in {nameof(WriteContext)}`; {msg}", nameof(method));
                    }

                    takesContext = true;
                }
                else
                {
                    return Throw.ArgumentException<ShouldSerialize>($"{nameof(method)} as a static method must take zero or one parameter", nameof(method));
                }
            }

            return new ShouldSerialize(takes, method, takesContext);
        }

        /// <summary>
        /// Create a ShouldSerialize from the given delegate.
        /// </summary>
        public static ShouldSerialize ForDelegate<TRow>(ShouldSerializeDelegate<TRow> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new ShouldSerialize(typeof(TRow).GetTypeInfo(), del);
        }

        /// <summary>
        /// Create a ShouldSerialize from the given delegate.
        /// </summary>
        public static ShouldSerialize ForDelegate(StaticShouldSerializeDelegate del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

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
        public bool Equals(ShouldSerialize? shouldSerialize)
        {
            if (ReferenceEquals(shouldSerialize, null)) return false;

            var mode = Mode;
            var otherMode = shouldSerialize.Mode;
            if (mode != otherMode) return false;

            if (IsStatic != shouldSerialize.IsStatic) return false;

            if (Takes.HasValue)
            {
                if (!shouldSerialize.Takes.HasValue) return false;

                if (Takes.Value != shouldSerialize.Takes.Value) return false;
            }
            else
            {
                if (shouldSerialize.Takes.HasValue) return false;
            }

            return
                mode switch
                {
                    BackingMode.Delegate => Delegate.Value == shouldSerialize.Delegate.Value,
                    BackingMode.Method => Method.Value == shouldSerialize.Method.Value,
                    _ => Throw.ImpossibleException<bool>($"Unexpected {nameof(BackingMode)}: {mode}")
                };
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
                        if (Takes.HasValue)
                        {
                            return $"{nameof(ShouldSerialize)} backed by method {Method} taking {Takes}";
                        }
                        else
                        {
                            return $"{nameof(ShouldSerialize)} backed by method {Method}";
                        }
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
        /// Returns null if method is null.
        /// </summary>
        public static explicit operator ShouldSerialize?(MethodInfo? method)
        => method == null ? null : ForMethod(method);

        /// <summary>
        /// Convenience operator, equivalent to calling ShouldSerialize.ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator ShouldSerialize?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();
            if (delType == Types.StaticShouldSerializeDelegate)
            {
                return new ShouldSerialize(null, del);
            }

            if (delType.IsGenericType && delType.GetGenericTypeDefinition().GetTypeInfo() == Types.ShouldSerializeDelegate)
            {
                var genArgs = delType.GetGenericArguments();
                var takesType = genArgs[0].GetTypeInfo();

                return new ShouldSerialize(takesType, del);
            }

            var mtd = del.Method;
            var retType = mtd.ReturnType.GetTypeInfo();
            if (retType != Types.Bool)
            {
                return Throw.InvalidOperationException<ShouldSerialize>($"Delegate must return boolean, found {retType}");
            }

            var invoke = delType.GetMethodNonNull("Invoke");

            var ps = mtd.GetParameters();
            if (ps.Length == 1)
            {
                if (!ps[0].IsWriteContextByRef(out var msg))
                {
                    return Throw.InvalidOperationException<ShouldSerialize>($"If an delegate takes a single parameter it must be a `in {nameof(WriteContext)}`; {msg}");
                }

                var reboundDel = System.Delegate.CreateDelegate(Types.StaticShouldSerializeDelegate, del, invoke);

                return new ShouldSerialize(null, reboundDel);
            }
            else if (ps.Length == 2)
            {
                var takesType = ps[0].ParameterType.GetTypeInfo();

                if (!ps[1].IsWriteContextByRef(out var msg))
                {
                    return Throw.InvalidOperationException<ShouldSerialize>($"If an delegate takes two parameters the second must be an `in {nameof(WriteContext)}`; {msg}");
                }

                var shouldSerializeDelType = Types.ShouldSerializeDelegate.MakeGenericType(takesType);
                var reboundDel = System.Delegate.CreateDelegate(shouldSerializeDelType, del, invoke);

                return new ShouldSerialize(takesType, reboundDel);
            }
            else
            {
                return Throw.InvalidOperationException<ShouldSerialize>($"Delegate must take 1or 2 parameters");
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
