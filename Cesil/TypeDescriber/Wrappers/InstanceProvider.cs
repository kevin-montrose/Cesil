using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Delegate used to create InstanceProviders.
    /// </summary>
    public delegate bool InstanceProviderDelegate<TInstance>(in ReadContext context, out TInstance instance);

    /// <summary>
    /// Represents a way to create new instances of a type.
    /// 
    /// This can be backed by a zero-parameter constructor, a static 
    ///   method, or a delegate.
    /// </summary>
    public sealed class InstanceProvider :
        IEquatable<InstanceProvider>,
        IElseSupporting<InstanceProvider>
    {
        internal BackingMode Mode
        {
            get
            {
                if (Constructor.HasValue) return BackingMode.Constructor;
                if (Delegate.HasValue) return BackingMode.Delegate;
                if (Method.HasValue) return BackingMode.Method;

                return BackingMode.None;
            }
        }

        internal readonly TypeInfo ConstructsType;

        internal bool ConstructorTakesParameters
        {
            get
            {
                if (Mode != BackingMode.Constructor) return false;

                return Constructor.Value.GetParameters().Length > 0;
            }
        }

        internal readonly NonNull<ConstructorInfo> Constructor;

        internal readonly NonNull<Delegate> Delegate;

        internal readonly NonNull<MethodInfo> Method;

        internal bool HasFallbacks => _Fallbacks.Length > 0;

        private readonly ImmutableArray<InstanceProvider> _Fallbacks;
        ImmutableArray<InstanceProvider> IElseSupporting<InstanceProvider>.Fallbacks => _Fallbacks;

        internal InstanceProvider(ConstructorInfo cons, ImmutableArray<InstanceProvider> fallbacks)
        {
            Constructor.Value = cons;
            ConstructsType = cons.DeclaringTypeNonNull();
            _Fallbacks = fallbacks;
        }

        internal InstanceProvider(Delegate del, TypeInfo forType, ImmutableArray<InstanceProvider> fallbacks)
        {
            Delegate.Value = del;
            ConstructsType = forType;
            _Fallbacks = fallbacks;
        }

        internal InstanceProvider(MethodInfo mtd, TypeInfo forType, ImmutableArray<InstanceProvider> fallbacks)
        {
            Method.Value = mtd;
            ConstructsType = forType;
            _Fallbacks = fallbacks;
        }

        InstanceProvider IElseSupporting<InstanceProvider>.Clone(ImmutableArray<InstanceProvider> newFallbacks)
        {
            switch (Mode)
            {
                case BackingMode.Constructor: return new InstanceProvider(Constructor.Value, newFallbacks);
                case BackingMode.Delegate: return new InstanceProvider(Delegate.Value, ConstructsType, newFallbacks);
                case BackingMode.Method: return new InstanceProvider(Method.Value, ConstructsType, newFallbacks);
            }

            return Throw.Exception<InstanceProvider>($"Unexpected {nameof(BackingMode)}: {Mode}");
        }

        /// <summary>
        /// Create a new instance provider that will try this instance provider, but if it returns false
        ///   it will then try the given fallback InstanceProvider.
        /// </summary>
        public InstanceProvider Else(InstanceProvider fallbackProvider)
        {
            Utils.CheckArgumentNull(fallbackProvider, nameof(fallbackProvider));

            if (!ConstructsType.IsAssignableFrom(fallbackProvider.ConstructsType))
            {
                return Throw.ArgumentException<InstanceProvider>($"{fallbackProvider} does not provide a value assignable to {ConstructsType}, and cannot be used as a fallback for this {nameof(InstanceProvider)}", nameof(fallbackProvider));
            }

            return this.DoElse(fallbackProvider);
        }

        internal Expression MakeExpression(TypeInfo resultType, ParameterExpression context, ParameterExpression outVar)
        {
            Expression selfExp;

            switch (Mode)
            {
                case BackingMode.Delegate:
                    {
                        var del = Delegate.Value;

                        var delConst = Expression.Constant(del, del.GetType());

                        if (resultType == ConstructsType)
                        {
                            selfExp = Expression.Invoke(delConst, context, outVar);
                        }
                        else
                        {
                            var constructedVar = Expression.Variable(ConstructsType);
                            var resVar = Expression.Variable(Types.BoolType);
                            var invoke = Expression.Invoke(delConst, context, constructedVar);
                            var assignRes = Expression.Assign(resVar, invoke);

                            var convert = Expression.Convert(constructedVar, resultType);
                            var assignOut = Expression.Assign(outVar, convert);
                            var ifAssign = Expression.IfThen(resVar, assignOut);

                            var block = Expression.Block(new[] { constructedVar, resVar }, new Expression[] { assignRes, ifAssign, resVar });

                            selfExp = block;
                        }
                        break;
                    }
                case BackingMode.Constructor:
                    {
                        var cons = Constructor.Value;

                        var assignTo = Expression.Assign(outVar, Expression.New(cons));

                        var block = Expression.Block(new Expression[] { assignTo, Expressions.Constant_True });

                        selfExp = block;
                        break;
                    }
                case BackingMode.Method:
                    {
                        var mtd = Method.Value;

                        var call = Expression.Call(mtd, context, outVar);

                        selfExp = call;
                        break;
                    }
                default:
                    return Throw.Exception<Expression>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }

            var finalExp = selfExp;
            foreach (var fallback in _Fallbacks)
            {
                var fallbackExp = fallback.MakeExpression(resultType, context, outVar);
                finalExp = Expression.OrElse(finalExp, fallbackExp);
            }

            return finalExp;
        }

        /// <summary>
        /// Creates a new InstanceProvider from a method.
        /// 
        /// The method must:
        ///   - be static
        ///   - return a bool
        ///   - have two parameters
        ///   - the first must be an in ReadContext
        ///   - the second must be an out parameter of the constructed type
        /// </summary>
        public static InstanceProvider ForMethod(MethodInfo method)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            if (!method.IsStatic)
            {
                return Throw.ArgumentException<InstanceProvider>("Method must be static", nameof(method));
            }

            if (method.ReturnType.GetTypeInfo() != Types.BoolType)
            {
                return Throw.ArgumentException<InstanceProvider>("Method must return a boolean", nameof(method));
            }

            var ps = method.GetParameters();
            if (ps.Length != 2)
            {
                return Throw.ArgumentException<InstanceProvider>("Method must have two parameters", nameof(method));
            }

            if (!ps[0].IsReadContextByRef(out var msg))
            {
                return Throw.ArgumentException<InstanceProvider>($"Method's first parameter must be a `in {nameof(ReadContext)}`; {msg}", nameof(method));
            }

            var outP = ps[1].ParameterType.GetTypeInfo();
            if (!outP.IsByRef)
            {
                return Throw.ArgumentException<InstanceProvider>("Method must have a single out parameter, parameter was not by ref", nameof(method));
            }

            var constructs = outP.GetElementTypeNonNull();

            return new InstanceProvider(method, constructs, ImmutableArray<InstanceProvider>.Empty);
        }

        /// <summary>
        /// Create a new InstanceProvider from a parameterless constructor.
        /// 
        /// The constructed type must be concrete, that is:
        ///   - not an interface
        ///   - not an abstract class
        ///   - not a generic parameter
        ///   - not an unbound generic type (ie. a generic type definition)
        /// </summary>
        public static InstanceProvider ForParameterlessConstructor(ConstructorInfo constructor)
        {
            Utils.CheckArgumentNull(constructor, nameof(constructor));

            var ps = constructor.GetParameters();
            if (ps.Length != 0)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructor must take 0 parameters", nameof(constructor));
            }

            var t = constructor.DeclaringTypeNonNull();
            if (t.IsInterface)
            {
                // todo: is this possible?  if so, can we test it?
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found an interface", nameof(constructor));
            }

            if (t.IsAbstract)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found an abstract class", nameof(constructor));
            }

            if (t.IsGenericTypeParameter)
            {
                // todo: is this possible?  if so, can we test it?
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found a generic parameter", nameof(constructor));
            }

            if (t.IsGenericTypeDefinition)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found a generic type definition", nameof(constructor));
            }

            return new InstanceProvider(constructor, ImmutableArray<InstanceProvider>.Empty);
        }

        /// <summary>
        /// Create a new InstanceProvider from a constructor that takes parameters.
        /// 
        /// An InstanceProvider of this type must be paired with Setters that map to the parameters
        ///   on this constructor.
        /// 
        /// The constructed type must be concrete, that is:
        ///   - not an interface
        ///   - not an abstract class
        ///   - not a generic parameter
        ///   - not an unbound generic type (ie. a generic type definition)
        /// </summary>
        public static InstanceProvider ForConstructorWithParameters(ConstructorInfo constructor)
        {
            Utils.CheckArgumentNull(constructor, nameof(constructor));

            var ps = constructor.GetParameters();
            if (ps.Length == 0)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructor must take at least one parameter", nameof(constructor));
            }

            var t = constructor.DeclaringTypeNonNull();
            if (t.IsInterface)
            {
                // todo: is this possible?  if so, can we test it?
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found an interface", nameof(constructor));
            }

            if (t.IsAbstract)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found an abstract class", nameof(constructor));
            }

            if (t.IsGenericTypeParameter)
            {
                // todo: is this possible?  if so, can we test it?
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found a generic parameter", nameof(constructor));
            }

            if (t.IsGenericTypeDefinition)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found a generic type definition", nameof(constructor));
            }

            return new InstanceProvider(constructor, ImmutableArray<InstanceProvider>.Empty);
        }

        /// <summary>
        /// Create a new InstanceProvider from delegate.
        /// 
        /// There are no restrictions on what the give delegate may do,
        ///   but be aware that it may be called from many different contexts.
        /// </summary>
        public static InstanceProvider ForDelegate<TInstance>(InstanceProviderDelegate<TInstance> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new InstanceProvider(del, typeof(TInstance).GetTypeInfo(), ImmutableArray<InstanceProvider>.Empty);
        }

        /// <summary>
        /// Returns true if this object equals the given InstanceProvider.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is InstanceProvider i)
            {
                return Equals(i);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given InstanceProvider.
        /// </summary>
        public bool Equals(InstanceProvider instanceProvider)
        {
            if (ReferenceEquals(instanceProvider, null)) return false;

            if (Mode != instanceProvider.Mode) return false;

            if (ConstructsType != instanceProvider.ConstructsType) return false;

            if (_Fallbacks.Length != instanceProvider._Fallbacks.Length) return false;

            for (var i = 0; i < _Fallbacks.Length; i++)
            {
                var selfF = _Fallbacks[i];
                var otherF = instanceProvider._Fallbacks[i];

                if (selfF != otherF) return false;
            }

            switch (Mode)
            {
                case BackingMode.Constructor: return instanceProvider.Constructor.Value == Constructor.Value;
                case BackingMode.Delegate: return instanceProvider.Delegate.Value == Delegate.Value;
                case BackingMode.Method: return instanceProvider.Method.Value == Method.Value;
                default: return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Returns a stable hash for this InstanceProvider.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(InstanceProvider), Constructor, ConstructsType, Delegate, Method, Mode, _Fallbacks.Length);

        /// <summary>
        /// Returns a representation of this InstanceProvider object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case BackingMode.Constructor:
                    return $"{nameof(InstanceProvider)} using parameterless constructor {Constructor} to create {ConstructsType}";
                case BackingMode.Delegate:
                    return $"{nameof(InstanceProvider)} using delegate {Delegate} to create {ConstructsType}";
                case BackingMode.Method:
                    return $"{nameof(InstanceProvider)} using method {Method} to create {ConstructsType}";
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Convenience operator, equivalent to calling ForMethod if non-null.
        /// 
        /// Returns null if method is null.
        /// </summary>
        public static explicit operator InstanceProvider?(MethodInfo? method)
        => method == null ? null : ForMethod(method);

        /// <summary>
        /// Convenience operator, equivalent to calling ForParameterlessConstructor or ForConstructorWithParameters if non-null.
        /// 
        /// Returns null if field is null.
        /// </summary>
        public static explicit operator InstanceProvider?(ConstructorInfo? cons)
        {
            if (cons == null) return null;

            var ps = cons.GetParameters();

            if (ps.Length == 0) return ForParameterlessConstructor(cons);

            return ForConstructorWithParameters(cons);
        }

        /// <summary>
        /// Convenience operator, equivalent to calling ForDelegate if non-null.
        /// 
        /// Returns null if del is null.
        /// </summary>
        public static explicit operator InstanceProvider?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();
            if (delType.IsGenericType)
            {
                var delGenType = delType.GetGenericTypeDefinition().GetTypeInfo();
                if (delGenType == Types.InstanceProviderDelegateType)
                {
                    var genArgs = delType.GetGenericArguments();
                    var makes = genArgs[0].GetTypeInfo();

                    return new InstanceProvider(del, makes, ImmutableArray<InstanceProvider>.Empty);
                }
            }

            var mtd = del.Method;
            var ret = mtd.ReturnType.GetTypeInfo();
            if (ret != Types.BoolType)
            {
                return Throw.InvalidOperationException<InstanceProvider>($"Delegate must return boolean, found {ret}");
            }

            var ps = mtd.GetParameters();
            if (ps.Length != 2)
            {
                return Throw.InvalidOperationException<InstanceProvider>("Method must have two parameters");
            }

            if (!ps[0].IsReadContextByRef(out var msg))
            {
                return Throw.InvalidOperationException<InstanceProvider>($"Method's first parameter must be a `in {nameof(ReadContext)}`; {msg}");
            }

            var outP = ps[1].ParameterType.GetTypeInfo();
            if (!outP.IsByRef)
            {
                return Throw.InvalidOperationException<InstanceProvider>("Method must have a single out parameter, parameter was not by ref");
            }

            var constructs = outP.GetElementTypeNonNull();

            var instanceBuilderDel = Types.InstanceProviderDelegateType.MakeGenericType(constructs);
            var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

            var reboundDel = System.Delegate.CreateDelegate(instanceBuilderDel, del, invoke);

            return new InstanceProvider(reboundDel, constructs, ImmutableArray<InstanceProvider>.Empty);
        }

        /// <summary>
        /// Compare two InstanceProviders for equality
        /// </summary>
        public static bool operator ==(InstanceProvider? a, InstanceProvider? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two InstanceProvider for inequality
        /// </summary>
        public static bool operator !=(InstanceProvider? a, InstanceProvider? b)
        => !(a == b);
    }
}
