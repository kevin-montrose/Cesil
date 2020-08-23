using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// Delegate type for DynamicRowConverters.
    /// </summary>
    public delegate bool DynamicRowConverterDelegate<TOutput>(object row, in ReadContext context, out TOutput result);

    /// <summary>
    /// Describes how to convert a dynamic row value
    ///   into an instance of a type.
    ///   
    /// Conversions can implemented as constructors taking a single 
    ///   dynamic/object, constructors taking typed parameters, an 
    ///   empty constructor paired with setter methods,
    ///   or static methods.
    /// </summary>
    public sealed class DynamicRowConverter :
        IElseSupporting<DynamicRowConverter>,
        IEquatable<DynamicRowConverter>
    {
        internal BackingMode Mode
        {
            get
            {
                if (ConstructorForObject.HasValue) return BackingMode.Constructor;
                if (ConstructorTakingParams.HasValue) return BackingMode.Constructor;
                if (EmptyConstructor.HasValue) return BackingMode.Constructor;
                if (Method.HasValue) return BackingMode.Method;
                if (Delegate.HasValue) return BackingMode.Delegate;

                return BackingMode.None;
            }
        }

        internal readonly NonNull<ConstructorInfo> ConstructorForObject;

        internal readonly NonNull<MethodInfo> Method;

        internal readonly NonNull<ConstructorInfo> ConstructorTakingParams;

        internal readonly NonNull<TypeInfo[]> ParameterTypes;

        internal readonly NonNull<ColumnIdentifier[]> ColumnsForParameters;

        internal readonly NonNull<ConstructorInfo> EmptyConstructor;

        internal readonly NonNull<Setter[]> Setters;

        internal readonly NonNull<ColumnIdentifier[]> ColumnsForSetters;

        internal readonly NonNull<Delegate> Delegate;

        internal readonly TypeInfo TargetType;

        private readonly ImmutableArray<DynamicRowConverter> _Fallbacks;

        ImmutableArray<DynamicRowConverter> IElseSupporting<DynamicRowConverter>.Fallbacks => _Fallbacks;

        private DynamicRowConverter(ConstructorInfo cons, ImmutableArray<DynamicRowConverter> fallbacks)
        {
            ConstructorForObject.Value = cons;
            TargetType = cons.DeclaringTypeNonNull();
            _Fallbacks = fallbacks;
        }

        private DynamicRowConverter(TypeInfo target, MethodInfo del, ImmutableArray<DynamicRowConverter> fallbacks)
        {
            Method.Value = del;
            TargetType = target;
            _Fallbacks = fallbacks;
        }

        private DynamicRowConverter(ConstructorInfo cons, TypeInfo[] paramTypes, ColumnIdentifier[] colsForPs, ImmutableArray<DynamicRowConverter> fallbacks)
        {
            ConstructorTakingParams.Value = cons;
            ColumnsForParameters.Value = colsForPs;
            ParameterTypes.Value = paramTypes;
            TargetType = cons.DeclaringTypeNonNull();
            _Fallbacks = fallbacks;
        }

        private DynamicRowConverter(ConstructorInfo cons, Setter[] setters, ColumnIdentifier[] colsForSetters, ImmutableArray<DynamicRowConverter> fallbacks)
        {
            EmptyConstructor.Value = cons;
            Setters.Value = setters;
            ColumnsForSetters.Value = colsForSetters;
            TargetType = cons.DeclaringTypeNonNull();
            _Fallbacks = fallbacks;
        }

        private DynamicRowConverter(TypeInfo target, Delegate del, ImmutableArray<DynamicRowConverter> fallbacks)
        {
            Delegate.Value = del;
            TargetType = target;
            _Fallbacks = fallbacks;
        }

        /// <summary>
        /// Create a new converter that will try this converter, but if it returns false
        ///   it will then try the given fallback DynamicRowConverter.
        /// </summary>
        public DynamicRowConverter Else(DynamicRowConverter fallbackConverter)
        {
            Utils.CheckArgumentNull(fallbackConverter, nameof(fallbackConverter));

            if (!TargetType.IsAssignableFrom(fallbackConverter.TargetType))
            {
                return Throw.ArgumentException<DynamicRowConverter>($"{fallbackConverter} does not produce a value assignable to {TargetType}, and cannot be used as a fallback for this {nameof(DynamicRowConverter)}", nameof(fallbackConverter));
            }

            return this.DoElse(fallbackConverter, null, null);
        }

        DynamicRowConverter IElseSupporting<DynamicRowConverter>.Clone(ImmutableArray<DynamicRowConverter> newFallbacks, NullHandling? _, NullHandling? __)
        {
            switch (Mode)
            {
                case BackingMode.Constructor:
                    {
                        if (ConstructorForObject.HasValue)
                        {
                            return new DynamicRowConverter(ConstructorForObject.Value, newFallbacks);

                        }

                        if (ConstructorTakingParams.HasValue)
                        {
                            return new DynamicRowConverter(ConstructorTakingParams.Value, ParameterTypes.Value, ColumnsForParameters.Value, newFallbacks);
                        }

                        if (EmptyConstructor.HasValue)
                        {
                            return new DynamicRowConverter(EmptyConstructor.Value, Setters.Value, ColumnsForSetters.Value, newFallbacks);
                        }
                    }
                    break;
                case BackingMode.Delegate: return new DynamicRowConverter(TargetType, Delegate.Value, newFallbacks);
                case BackingMode.Method: return new DynamicRowConverter(TargetType, Method.Value, newFallbacks);
            }

            return Throw.ImpossibleException<DynamicRowConverter>($"Unexpected {nameof(BackingMode)}: {Mode}");
        }

        internal Expression MakeExpression(TypeInfo targetType, ParameterExpression rowVar, ParameterExpression contextVar, ParameterExpression outVar)
        {
            Expression selfExp;

            switch (Mode)
            {
                case BackingMode.Constructor:
                    {
                        if (ConstructorForObject.HasValue)
                        {
                            var cons = ConstructorForObject.Value;
                            var createType = Expression.New(cons, rowVar);
                            var cast = Expression.Convert(createType, targetType);

                            var assign = Expression.Assign(outVar, cast);

                            selfExp = Expression.Block(assign, Expressions.Constant_True);
                            break;
                        }

                        if (ConstructorTakingParams.HasValue)
                        {
                            var typedCons = ConstructorTakingParams.Value;

                            var colsForPs = ColumnsForParameters.Value;
                            var paramTypes = ParameterTypes.Value;

                            var ps = new List<Expression>();
                            for (var pIx = 0; pIx < colsForPs.Length; pIx++)
                            {
                                var colIx = colsForPs[pIx];
                                var pType = paramTypes[pIx];
                                var getter = Methods.DynamicRow.GetAtTyped.MakeGenericMethod(pType);

                                var call = Expression.Call(rowVar, getter, Expression.Constant(colIx));

                                ps.Add(call);
                            }

                            var createType = Expression.New(typedCons, ps);
                            var cast = Expression.Convert(createType, targetType);

                            var assign = Expression.Assign(outVar, cast);

                            selfExp = Expression.Block(assign, Expressions.Constant_True);
                            break;
                        }

                        if (EmptyConstructor.HasValue)
                        {
                            var zeroCons = EmptyConstructor.Value;
                            var setters = Setters.Value;
                            var setterCols = ColumnsForSetters.Value;

                            var retVar = Expression.Variable(TargetType);

                            var createType = Expression.New(zeroCons);
                            var assignToVar = Expression.Assign(retVar, createType);

                            var statements =
                                new List<Expression>
                                {
                                    assignToVar
                                };

                            var locals =
                                new List<ParameterExpression>
                                {
                                    retVar
                                };

                            for (var i = 0; i < setters.Length; i++)
                            {
                                var setter = setters[i];
                                var setterColumn = setterCols[i];

                                var getValueMtd = Methods.DynamicRow.GetAtTyped.MakeGenericMethod(setter.Takes);

                                var getValueCall = Expression.Call(rowVar, getValueMtd, Expression.Constant(setterColumn));
                                var valueVar = Expression.Parameter(setter.Takes);
                                var assignValueVar = Expression.Assign(valueVar, getValueCall);
                                locals.Add(valueVar);
                                statements.Add(assignValueVar);

                                var callSetter = setter.MakeExpression(retVar, valueVar, contextVar);

                                statements.Add(callSetter);
                            }

                            var cast = Expression.Convert(retVar, targetType);
                            var assign = Expression.Assign(outVar, cast);
                            statements.Add(assign);
                            statements.Add(Expressions.Constant_True);

                            var block = Expression.Block(locals, statements);

                            selfExp = block;
                            break;
                        }

                        return Throw.ImpossibleException<Expression>($"Constructor converter couldn't be turned into an expression, shouldn't be possible");
                    }
                case BackingMode.Method:
                    {
                        var mtd = Method.Value;
                        var statements = new List<Expression>();

                        var tempVar = Expression.Parameter(TargetType);
                        var resVar = Expressions.Variable_Bool;

                        var callConvert = Expression.Call(mtd, rowVar, contextVar, tempVar);
                        var assignRes = Expression.Assign(resVar, callConvert);

                        statements.Add(assignRes);

                        var castTemp = Expression.Convert(tempVar, targetType);
                        var assignOut = Expression.Assign(outVar, castTemp);

                        var ifAssign = Expression.IfThen(resVar, assignOut);
                        statements.Add(ifAssign);

                        statements.Add(resVar);

                        selfExp = Expression.Block(new ParameterExpression[] { tempVar, resVar }, statements);

                        break;
                    }
                case BackingMode.Delegate:
                    {
                        var del = Delegate.Value;
                        var delRef = Expression.Constant(del);

                        var statements = new List<Expression>();

                        var tempVar = Expression.Parameter(TargetType);
                        var resVar = Expressions.Variable_Bool;

                        var callConvert = Expression.Invoke(delRef, rowVar, contextVar, tempVar);
                        var assignRes = Expression.Assign(resVar, callConvert);

                        statements.Add(assignRes);

                        var castTemp = Expression.Convert(tempVar, targetType);
                        var assignOut = Expression.Assign(outVar, castTemp);

                        var ifAssign = Expression.IfThen(resVar, assignOut);
                        statements.Add(ifAssign);

                        statements.Add(resVar);

                        selfExp = Expression.Block(new ParameterExpression[] { tempVar, resVar }, statements);

                        break;
                    }
                default:
                    return Throw.ImpossibleException<Expression>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }

            var finalExp = selfExp;
            foreach (var fallback in _Fallbacks)
            {
                var fallbackExp = fallback.MakeExpression(targetType, rowVar, contextVar, outVar);
                finalExp = Expression.OrElse(finalExp, fallbackExp);
            }

            return finalExp;
        }

        /// <summary>
        /// Create a DynamicRowConverter from the given delegate.
        /// </summary>
        public static DynamicRowConverter ForDelegate<TOutput>(DynamicRowConverterDelegate<TOutput> del)
        {
            Utils.CheckArgumentNull(del, nameof(del));

            return new DynamicRowConverter(typeof(TOutput).GetTypeInfo(), del, ImmutableArray<DynamicRowConverter>.Empty);
        }

        /// <summary>
        /// Create a DynamicRowConverter from the given constructor.
        /// 
        /// Constructor must take an object (which can be dynamic in source).
        /// </summary>
        public static DynamicRowConverter ForConstructorTakingDynamic(ConstructorInfo constructor)
        {
            Utils.CheckArgumentNull(constructor, nameof(constructor));

            var ps = constructor.GetParameters();
            if (ps.Length != 1)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Constructor {constructor} must take a single object", nameof(constructor));
            }

            var p = ps[0].ParameterType.GetTypeInfo();
            if (p != Types.Object)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Constructor {constructor} must take a object, found a {p}", nameof(constructor));
            }

            return new DynamicRowConverter(constructor, ImmutableArray<DynamicRowConverter>.Empty);
        }

        /// <summary>
        /// Create a DynamicRowConverter for the given constructor, which maps specific columns to the parameters
        ///   for the of the constructor.
        ///   
        /// Constructor must take the same number of parameters as column indexes passed in.
        /// 
        /// Mapping will use column names if available, and fallback to indexes.
        /// </summary>
        public static DynamicRowConverter ForConstructorTakingTypedParameters(
            ConstructorInfo constructor,
            IEnumerable<ColumnIdentifier> columnsForParameters
        )
        {
            Utils.CheckArgumentNull(constructor, nameof(constructor));
            Utils.CheckArgumentNull(columnsForParameters, nameof(columnsForParameters));

            var cifp = columnsForParameters.ToArray();

            var ps = constructor.GetParameters();
            if (ps.Length != cifp.Length)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"Constructor {constructor} takes {ps.Length} parameters, while only {cifp.Length} column indexes were passed");
            }

            for (var i = 0; i < cifp.Length; i++)
            {
                var colIx = cifp[i].Index;
                if (colIx < 0)
                {
                    return Throw.ArgumentException<DynamicRowConverter>($"Column indexes must be >= 0, found {colIx} at {i}", nameof(columnsForParameters));
                }
            }

            var psAsTypeInfo = new TypeInfo[ps.Length];
            for (var i = 0; i < ps.Length; i++)
            {
                psAsTypeInfo[i] = ps[i].ParameterType.GetTypeInfo();
            }

            return new DynamicRowConverter(constructor, psAsTypeInfo, cifp, ImmutableArray<DynamicRowConverter>.Empty);
        }

        /// <summary>
        /// Create a DynamicRowConverter for the given zero parameter constructor, which will call each setter
        ///   with the column indicated in the passed column to setters.
        ///   
        /// The same number of setters and columns must be passed.
        /// 
        /// Mapping will use column names if available, and fallback to indexes.
        /// </summary>
        public static DynamicRowConverter ForEmptyConstructorAndSetters(
            ConstructorInfo constructor,
            IEnumerable<Setter> setters,
            IEnumerable<ColumnIdentifier> columnsForSetters
        )
        {
            Utils.CheckArgumentNull(constructor, nameof(constructor));

            var consPs = constructor.GetParameters();
            if (consPs.Length != 0)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Constructor {constructor} must take zero parameters", nameof(constructor));
            }

            Utils.CheckArgumentNull(setters, nameof(setters));
            Utils.CheckArgumentNull(columnsForSetters, nameof(columnsForSetters));

            var s = setters.ToArray();
            var cts = columnsForSetters.ToArray();

            if (s.Length != cts.Length)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"{nameof(setters)} and {nameof(columnsForSetters)} must be the same length, found {s.Length} and {cts.Length}");
            }

            var constructedType = constructor.DeclaringTypeNonNull();


            for (var i = 0; i < s.Length; i++)
            {
                var setter = s[i];

                // can always call it, doesn't matter
                if (setter.IsStatic) continue;

                var setterOnType = setter.RowType.Value;

                if (!setterOnType.IsAssignableFrom(constructedType))
                {
                    return Throw.ArgumentException<DynamicRowConverter>($"Setter {setter} at {i} cannot be called on {constructedType}", nameof(setters));
                }
            }

            for (var i = 0; i < cts.Length; i++)
            {
                var colIx = cts[i].Index;
                if (colIx < 0)
                {
                    return Throw.ArgumentException<DynamicRowConverter>($"Column indexes must be >= 0, found {colIx} at {i}", nameof(columnsForSetters));
                }
            }

            return new DynamicRowConverter(constructor, s, cts, ImmutableArray<DynamicRowConverter>.Empty);
        }

        /// <summary>
        /// Create a DynamicRowConverter from the given method.
        /// 
        /// Method must be static, return a bool, take an object (which can be dynamic in source)
        ///   as it's first parameter, a ReadContext as it's second parameter, and have a third parameter that is an out 
        ///   for the result value.
        /// </summary>
        public static DynamicRowConverter ForMethod(MethodInfo method)
        {
            Utils.CheckArgumentNull(method, nameof(method));

            if (!method.IsStatic)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method} must be static", nameof(method));
            }

            if (method.ReturnType.GetTypeInfo() != Types.Bool)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method} must return a bool", nameof(method));
            }

            var ps = method.GetParameters();
            if (ps.Length != 3)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method} must take three parameters", nameof(method));
            }

            var p1 = ps[0].ParameterType.GetTypeInfo();
            if (p1 != Types.Object)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method}'s first parameter must be an object", nameof(method));
            }

            if (!ps[1].IsReadContextByRef(out string? msg))
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method}'s second parameter must be an `in {nameof(ReadContext)}`; {msg}", nameof(method));
            }

            var p3 = ps[2].ParameterType.GetTypeInfo();
            if (!p3.IsByRef)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method}'s second parameter must be a by ref type", nameof(method));
            }

            var targetType = p3.GetElementTypeNonNull();

            return new DynamicRowConverter(targetType, method, ImmutableArray<DynamicRowConverter>.Empty);
        }

        /// <summary>
        /// Returns a representation of this DynamicRowConverter object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case BackingMode.Constructor:
                    {
                        if (ConstructorForObject.HasValue)
                        {
                            return $"{nameof(DynamicRowConverter)} using 1 parameter constructor {ConstructorForObject} creating {TargetType}";
                        }

                        if (ConstructorTakingParams.HasValue)
                        {
                            string columnTypeMap;
                            var columnsForParametersValue = ColumnsForParameters.Value;
                            var parameterTypesValue = ParameterTypes.Value;

                            {
                                var map = new StringBuilder();
                                for (var i = 0; i < columnsForParametersValue.Length; i++)
                                {
                                    var col = columnsForParametersValue[i];
                                    var type = parameterTypesValue[i];

                                    if (i != 0)
                                    {
                                        map.Append(", ");
                                    }
                                    map.Append($"parameter {i} = column {col} as {type}");
                                }

                                columnTypeMap = map.ToString();
                            }

                            return $"{nameof(DynamicRowConverter)} using {columnsForParametersValue.Length} parameter constructor {ConstructorTakingParams} with ({columnTypeMap}) creating {TargetType}";
                        }

                        if (EmptyConstructor.HasValue)
                        {
                            string setterMap;
                            {
                                var settersValue = Setters.Value;
                                var columnsForSettersValue = ColumnsForSetters.Value;
                                var map = new StringBuilder();
                                for (var i = 0; i < settersValue.Length; i++)
                                {
                                    var setter = settersValue[i];
                                    var col = columnsForSettersValue[i];

                                    if (i != 0)
                                    {
                                        map.Append(", ");
                                    }
                                    map.Append($"setter {setter} with column {col}");
                                }

                                setterMap = map.ToString();
                            }

                            return $"{nameof(DynamicRowConverter)} using parameterless constructor {EmptyConstructor} then invoking ({setterMap}) creating {TargetType}";
                        }

                        return Throw.ImpossibleException<string>("Shouldn't be possible");
                    }
                case BackingMode.Method:
                    {
                        return $"{nameof(DynamicRowConverter)} using method {Method} creating {TargetType}";
                    }
                case BackingMode.Delegate:
                    {
                        return $"{nameof(DynamicRowConverter)} using delegate {Delegate} creation {TargetType}";
                    }
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(BackingMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Returns true if the given object is equivalent to this one
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is DynamicRowConverter row)
            {
                return Equals(row);
            }

            return false;
        }

        /// <summary>
        /// Returns true if the given DynamicRowConverter is equivalent to this one
        /// </summary>
        public bool Equals(DynamicRowConverter? rowConverter)
        {
            if (ReferenceEquals(rowConverter, null)) return false;

            var thisMode = Mode;
            var otherMode = rowConverter.Mode;
            if (thisMode != otherMode)
            {
                return false;
            }

            if (_Fallbacks.Length != rowConverter._Fallbacks.Length) return false;

            for (var i = 0; i < _Fallbacks.Length; i++)
            {
                var selfF = _Fallbacks[i];
                var otherF = rowConverter._Fallbacks[i];

                if (selfF != otherF) return false;
            }

            switch (thisMode)
            {
                case BackingMode.Method: return Method.Value.Equals(rowConverter.Method.Value);
                case BackingMode.Constructor:
                    if (ConstructorForObject.HasValue)
                    {
                        if (!rowConverter.ConstructorForObject.HasValue) return false;

                        return ConstructorForObject.Value.Equals(rowConverter.ConstructorForObject.Value);
                    }

                    if (ConstructorTakingParams.HasValue)
                    {
                        if (!rowConverter.ConstructorTakingParams.HasValue) return false;

                        if (!ConstructorTakingParams.Value.Equals(rowConverter.ConstructorTakingParams.Value))
                        {
                            return false;
                        }

                        // type compatibility and parameter counts are implicitly checked by comparing constructors

                        var parameterTypesValue = ParameterTypes.Value;
                        var columnsForParametersValue = ColumnsForParameters.Value;
                        var otherColumnsForParametersValue = rowConverter.ColumnsForParameters.Value;
                        for (var i = 0; i < parameterTypesValue.Length; i++)
                        {
                            var thisCol = columnsForParametersValue[i];
                            var otherCol = otherColumnsForParametersValue[i];

                            if (thisCol != otherCol)
                            {
                                return false;
                            }
                        }

                        return true;
                    }

                    if (EmptyConstructor.HasValue)
                    {
                        if (!rowConverter.EmptyConstructor.HasValue)
                        {
                            return false;
                        }

                        if (!EmptyConstructor.Value.Equals(rowConverter.EmptyConstructor.Value))
                        {
                            return false;
                        }

                        var settersValue = Setters.Value;
                        var otherSettersValue = rowConverter.Setters.Value;
                        if (settersValue.Length != otherSettersValue.Length)
                        {
                            return false;
                        }

                        var columnsForSettersValue = ColumnsForSetters.Value;
                        var otherColumnsForSettersValue = rowConverter.ColumnsForSetters.Value;
                        for (var i = 0; i < settersValue.Length; i++)
                        {
                            var thisSetter = settersValue[i];
                            var otherSetter = otherSettersValue[i];

                            var thisCol = columnsForSettersValue[i];
                            var otherCol = otherColumnsForSettersValue[i];

                            if (thisSetter != otherSetter || thisCol != otherCol)
                            {
                                return false;
                            }
                        }

                        return true;
                    }

                    return Throw.ImpossibleException<bool>($"Shouldn't be possible, unexpected Constructor configuration");
                case BackingMode.Delegate: return Delegate.Value.Equals(rowConverter.Delegate.Value);
                default:
                    return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {thisMode}");
            }
        }

        /// <summary>
        /// Returns a stable hash for this DynamicRowConverter.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(
            nameof(DynamicRowConverter),
            Mode,
            Method,
            ConstructorForObject,
            ConstructorTakingParams,
            EmptyConstructor,
            Delegate,
            _Fallbacks.Length
        );

        /// <summary>
        /// Compare two DynamicRowConverters for equality
        /// </summary>
        public static bool operator ==(DynamicRowConverter? a, DynamicRowConverter? b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two DynamicRowConverters for inequality
        /// </summary>
        public static bool operator !=(DynamicRowConverter? a, DynamicRowConverter? b)
        => !(a == b);

        /// <summary>
        /// Convenience operator, equivalent to calling DynamicRowConverter.ForMethod if non-null.
        /// 
        /// Returns null if method is null.
        /// </summary>
        public static explicit operator DynamicRowConverter?(MethodInfo? method)
        => method == null ? null : ForMethod(method);

        /// <summary>
        /// Convenience operator, equivalent to calling DynamicRowConverter.ForConstructorTakingDynamic if non-null.
        /// 
        /// Returns null if cons is null.
        /// </summary>
        public static explicit operator DynamicRowConverter?(ConstructorInfo? cons)
        => cons == null ? null : ForConstructorTakingDynamic(cons);

        /// <summary>
        /// Convenience operator, equivalent to calling DynamicRowConverter.ForDelegate if non-null.
        /// 
        /// Returns null if cons is null.
        /// </summary>
        public static explicit operator DynamicRowConverter?(Delegate? del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();

            if (delType.IsGenericType && delType.GetGenericTypeDefinition() == Types.DynamicRowConverterDelegate)
            {
                var t = delType.GetGenericArguments()[0].GetTypeInfo();

                return new DynamicRowConverter(t, del, ImmutableArray<DynamicRowConverter>.Empty);
            }

            var mtd = del.Method;
            var ret = mtd.ReturnType.GetTypeInfo();
            if (ret != Types.Bool)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"Delegate must return a bool");
            }

            var args = mtd.GetParameters();
            if (args.Length != 3)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"Delegate must take 3 parameters");
            }

            var p1 = args[0].ParameterType.GetTypeInfo();
            if (p1 != Types.Object)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"The first parameter to the delegate must be an object (can be dynamic in source)");
            }

            if (!args[1].IsReadContextByRef(out var msg))
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"The second parameter to the delegate must be an `in {nameof(ReadContext)}`; {msg}");
            }

            var createsRef = args[2].ParameterType.GetTypeInfo();
            if (!createsRef.IsByRef)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"The third parameter to the delegate must be an out type, was not by ref");
            }

            var creates = createsRef.GetElementTypeNonNull();

            var converterDel = Types.DynamicRowConverterDelegate.MakeGenericType(creates);
            var invoke = del.GetType().GetTypeInfo().GetMethodNonNull("Invoke");

            var reboundDel = System.Delegate.CreateDelegate(converterDel, del, invoke);

            return new DynamicRowConverter(creates, reboundDel, ImmutableArray<DynamicRowConverter>.Empty);
        }
    }
}
