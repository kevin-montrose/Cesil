﻿using System;
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
    /// Represents a way to obtain an instance of a type.
    /// 
    /// This can be backed by a zero-parameter constructor, a constructor
    ///   taking typed parameters, a static method, or a delegate.
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
        internal readonly NullHandling ConstructsNullability;

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

        internal readonly NonNull<TypeInfo> AheadOfTimeGeneratedType;

        internal bool IsBackedByGeneratedMethod => AheadOfTimeGeneratedType.HasValue;

        internal bool HasFallbacks => _Fallbacks.Length > 0;

        private readonly ImmutableArray<InstanceProvider> _Fallbacks;
        ImmutableArray<InstanceProvider> IElseSupporting<InstanceProvider>.Fallbacks => _Fallbacks;

        internal InstanceProvider(ConstructorInfo cons, ImmutableArray<InstanceProvider> fallbacks, NullHandling nullability, TypeInfo? aheadOfTimeGeneratedType)
        {
            Constructor.Value = cons;
            ConstructsType = cons.DeclaringTypeNonNull();
            ConstructsNullability = nullability;            // this isn't _always_ CannotBeNull because there might be fallbacks
            _Fallbacks = fallbacks;

            AheadOfTimeGeneratedType.SetAllowNull(aheadOfTimeGeneratedType);
        }

        internal InstanceProvider(Delegate del, TypeInfo forType, ImmutableArray<InstanceProvider> fallbacks, NullHandling nullability)
        {
            Delegate.Value = del;
            ConstructsType = forType;
            ConstructsNullability = nullability;
            _Fallbacks = fallbacks;
        }

        internal InstanceProvider(MethodInfo mtd, TypeInfo forType, ImmutableArray<InstanceProvider> fallbacks, NullHandling nullability, TypeInfo? aheadOfTimeGeneratedType)
        {
            Method.Value = mtd;
            ConstructsType = forType;
            ConstructsNullability = nullability;
            _Fallbacks = fallbacks;
            AheadOfTimeGeneratedType.SetAllowNull(aheadOfTimeGeneratedType);
        }

        InstanceProvider IElseSupporting<InstanceProvider>.Clone(ImmutableArray<InstanceProvider> newFallbacks, NullHandling? rowHandling, NullHandling? _)
        {
            var rowHandlingValue = Utils.NonNullValue(rowHandling);

            return Mode switch
            {
                BackingMode.Constructor => new InstanceProvider(Constructor.Value, newFallbacks, rowHandlingValue, AheadOfTimeGeneratedType.HasValue ? AheadOfTimeGeneratedType.Value : null),
                BackingMode.Delegate => new InstanceProvider(Delegate.Value, ConstructsType, newFallbacks, rowHandlingValue),
                BackingMode.Method => new InstanceProvider(Method.Value, ConstructsType, newFallbacks, rowHandlingValue, AheadOfTimeGeneratedType.HasValue ? AheadOfTimeGeneratedType.Value : null),
                _ => Throw.ImpossibleException<InstanceProvider>($"Unexpected {nameof(BackingMode)}: {Mode}")
            };
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

            var newRowNullability = Utils.CommonOutputNullHandling(ConstructsNullability, fallbackProvider.ConstructsNullability);

            return this.DoElse(fallbackProvider, newRowNullability, null);
        }

        private InstanceProvider ChangeRowNullHandling(NullHandling nullHandling)
        {
            if (nullHandling == ConstructsNullability)
            {
                return this;
            }

            Utils.ValidateNullHandling(ConstructsType, nullHandling);

            return
                Mode switch
                {
                    BackingMode.Constructor => new InstanceProvider(Constructor.Value, _Fallbacks, nullHandling, AheadOfTimeGeneratedType.HasValue ? AheadOfTimeGeneratedType.Value : null),
                    BackingMode.Method => new InstanceProvider(Method.Value, ConstructsType, _Fallbacks, nullHandling, AheadOfTimeGeneratedType.HasValue ? AheadOfTimeGeneratedType.Value : null),
                    BackingMode.Delegate => new InstanceProvider(Delegate.Value, ConstructsType, _Fallbacks, nullHandling),
                    _ => Throw.ImpossibleException<InstanceProvider>($"Unexpected {nameof(BackingMode)}: {Mode}"),
                };
        }

        /// <summary>
        /// Returns a InstanceProvider that differs from this by explicitly allowing
        ///   null rows to be created by it.
        /// </summary>
        public InstanceProvider AllowNullRows()
        => ChangeRowNullHandling(NullHandling.AllowNull);

        /// <summary>
        /// Returns a InstanceProvider that differs from this by explicitly forbidding
        ///   null rows be created by it.
        ///   
        /// If the .NET runtime cannot guarantee that null rows will not be created at runtime, 
        ///   null checks will be injected.
        /// </summary>
        public InstanceProvider ForbidNullRows()
        => ChangeRowNullHandling(NullHandling.ForbidNull);

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
                            var resVar = Expression.Variable(Types.Bool);
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
                    return Throw.ImpossibleException<Expression>($"Unexpected {nameof(BackingMode)}: {Mode}");
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
        => ForMethodInner(method, null);

        internal static InstanceProvider ForMethodInner(MethodInfo method, TypeInfo? aheadOfTimeGeneratedType)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            if (!method.IsStatic)
            {
                return Throw.ArgumentException<InstanceProvider>("Method must be static", nameof(method));
            }

            if (method.ReturnType.GetTypeInfo() != Types.Bool)
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

            var p1 = ps[1];
            var outP = p1.ParameterType.GetTypeInfo();
            if (!outP.IsByRef)
            {
                return Throw.ArgumentException<InstanceProvider>("Method must have a single out parameter, parameter was not by ref", nameof(method));
            }

            var constructs = outP.GetElementTypeNonNull();
            var constructsNullability = p1.DetermineNullability();

            return new InstanceProvider(method, constructs, ImmutableArray<InstanceProvider>.Empty, constructsNullability, aheadOfTimeGeneratedType);
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

            if (t.IsAbstract)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found an abstract class", nameof(constructor));
            }

            if (t.IsGenericTypeDefinition)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found a generic type definition", nameof(constructor));
            }

            return new InstanceProvider(constructor, ImmutableArray<InstanceProvider>.Empty, NullHandling.CannotBeNull, null);
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
        => ForConstructorWithParametersInner(constructor, null);

        internal static InstanceProvider ForConstructorWithParametersInner(ConstructorInfo constructor, TypeInfo? paired)
        {
            Utils.CheckArgumentNull(constructor, nameof(constructor));

            var ps = constructor.GetParameters();
            if (ps.Length == 0)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructor must take at least one parameter", nameof(constructor));
            }

            var t = constructor.DeclaringTypeNonNull();
            if (t.IsAbstract)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found an abstract class", nameof(constructor));
            }

            if (t.IsGenericTypeDefinition)
            {
                return Throw.ArgumentException<InstanceProvider>("Constructed type must be concrete, found a generic type definition", nameof(constructor));
            }

            return new InstanceProvider(constructor, ImmutableArray<InstanceProvider>.Empty, NullHandling.CannotBeNull, paired);
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

            var nullability = del.Method.GetParameters()[1].DetermineNullability();

            return new InstanceProvider(del, typeof(TInstance).GetTypeInfo(), ImmutableArray<InstanceProvider>.Empty, nullability);
        }

        /// <summary>
        /// Returns the default instance provider for the given type, if one exists.
        /// 
        /// For reference types, it will use the parameterless constructor.
        /// 
        /// For value types, it will use the all-zero (aka default) value.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        public static InstanceProvider? GetDefault(TypeInfo forType)
        {
            Utils.CheckArgumentNull(forType, nameof(forType));

            if (forType.IsByRef)
            {
                return Throw.ArgumentException<InstanceProvider>($"Cannot create an {nameof(InstanceProvider)} for a by ref type", nameof(forType));
            }

            if (forType.IsPointer)
            {
                return Throw.ArgumentException<InstanceProvider>($"Cannot create an {nameof(InstanceProvider)} for a pointer type", nameof(forType));
            }

            if (forType.IsGenericTypeDefinition)
            {
                return Throw.ArgumentException<InstanceProvider>($"Cannot create an {nameof(InstanceProvider)} for an unbound generic type", nameof(forType));
            }

            if (forType.IsGenericTypeParameter)
            {
                return Throw.ArgumentException<InstanceProvider>($"Cannot create an {nameof(InstanceProvider)} for an generic parameter", nameof(forType));
            }

            // any value type is constructable by definition
            if (forType.IsValueType)
            {
                var underlying = Nullable.GetUnderlyingType(forType);
                MethodInfo mtd;
                if (underlying != null)
                {
                    mtd = Methods.DefaultTypeInstanceProviders.TryCreateNullableInstance.MakeGenericMethod(underlying);
                }
                else
                {
                    mtd = Methods.DefaultTypeInstanceProviders.TryCreateInstance.MakeGenericMethod(forType);
                }

                return ForMethod(mtd);
            }

            // we have special cases for well known reference types
            if (DefaultTypeInstanceProviders.TryGetReferenceInstanceProvider(forType, out var wellKnownReferenceType))
            {
                return wellKnownReferenceType;
            }

            // and we can construct anything with an empty constructor
            var cons = forType.GetConstructor(Type.EmptyTypes);

            return (InstanceProvider?)cons;
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
        public bool Equals(InstanceProvider? instanceProvider)
        {
            if (ReferenceEquals(instanceProvider, null)) return false;

            if (Mode != instanceProvider.Mode) return false;

            if (AheadOfTimeGeneratedType.HasValue)
            {
                if (!instanceProvider.AheadOfTimeGeneratedType.HasValue) return false;

                if (AheadOfTimeGeneratedType.Value != instanceProvider.AheadOfTimeGeneratedType.Value) return false;
            }
            else
            {
                if (instanceProvider.AheadOfTimeGeneratedType.HasValue) return false;
            }

            if (ConstructsNullability != instanceProvider.ConstructsNullability) return false;
            if (ConstructsType != instanceProvider.ConstructsType) return false;

            if (_Fallbacks.Length != instanceProvider._Fallbacks.Length) return false;

            for (var i = 0; i < _Fallbacks.Length; i++)
            {
                var selfF = _Fallbacks[i];
                var otherF = instanceProvider._Fallbacks[i];

                if (selfF != otherF) return false;
            }

            return
                Mode switch
                {
                    BackingMode.Constructor => instanceProvider.Constructor.Value == Constructor.Value,
                    BackingMode.Delegate => instanceProvider.Delegate.Value == Delegate.Value,
                    BackingMode.Method => instanceProvider.Method.Value == Method.Value,
                    _ => Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {Mode}")
                };
        }

        /// <summary>
        /// Returns a stable hash for this InstanceProvider.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(InstanceProvider), Constructor, ConstructsType, Delegate, Method, Mode, _Fallbacks.Length, HashCode.Combine(ConstructsNullability, AheadOfTimeGeneratedType));

        /// <summary>
        /// Returns a representation of this InstanceProvider object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            return Mode switch
            {
                BackingMode.Constructor => $"{nameof(InstanceProvider)} using parameterless constructor {Constructor} to create {ConstructsType}",
                BackingMode.Delegate => $"{nameof(InstanceProvider)} using delegate {Delegate} to create {ConstructsType} ({ConstructsNullability})",
                BackingMode.Method => $"{nameof(InstanceProvider)} using method {Method} to create {ConstructsType} ({ConstructsNullability})",
                _ => Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}")
            };
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
                if (delGenType == Types.InstanceProviderDelegate)
                {
                    var genArgs = delType.GetGenericArguments();
                    var makes = genArgs[0].GetTypeInfo();

                    var nullability = del.Method.GetParameters()[1].DetermineNullability();

                    return new InstanceProvider(del, makes, ImmutableArray<InstanceProvider>.Empty, nullability);
                }
            }

            var mtd = del.Method;
            var ret = mtd.ReturnType.GetTypeInfo();
            if (ret != Types.Bool)
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

            var p1 = ps[1];
            var outP = p1.ParameterType.GetTypeInfo();
            if (!outP.IsByRef)
            {
                return Throw.InvalidOperationException<InstanceProvider>("Method must have a single out parameter, parameter was not by ref");
            }

            var constructs = outP.GetElementTypeNonNull();
            var constructsNullability = p1.DetermineNullability();

            var instanceBuilderDel = Types.InstanceProviderDelegate.MakeGenericType(constructs);
            var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

            var reboundDel = System.Delegate.CreateDelegate(instanceBuilderDel, del, invoke);

            return new InstanceProvider(reboundDel, constructs, ImmutableArray<InstanceProvider>.Empty, constructsNullability);
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
