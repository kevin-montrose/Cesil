using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// Delegate type for DynamicRowConverters.
    /// </summary>
    public delegate bool DynamicRowConverterDelegate<T>(object row, in ReadContext ctx, out T result);

    /// <summary>
    /// Describes how to convert a dynamic row value
    ///   into an instance of a type.
    ///   
    /// Conversions can implemented as constructors taking a single 
    ///   dynamic/object, constructors taking typed parameters, an 
    ///   empty constructor paired with setter methods,
    ///   or static methods.
    /// </summary>
    public sealed class DynamicRowConverter : IEquatable<DynamicRowConverter>
    {
        internal BackingMode Mode
        {
            get
            {
                if (ConstructorForObject != null) return BackingMode.Constructor;
                if (ConstructorTakingParams != null) return BackingMode.Constructor;
                if (EmptyConstructor != null) return BackingMode.Constructor;
                if (Method != null) return BackingMode.Method;
                if (Delegate != null) return BackingMode.Delegate;

                return BackingMode.None;
            }
        }

        internal readonly ConstructorInfo ConstructorForObject;

        internal readonly MethodInfo Method;

        internal readonly ConstructorInfo ConstructorTakingParams;
        internal readonly TypeInfo[] ParameterTypes;
        internal readonly ColumnIdentifier[] ColumnsForParameters;

        internal readonly ConstructorInfo EmptyConstructor;
        internal readonly Setter[] Setters;
        internal readonly ColumnIdentifier[] ColumnsForSetters;

        internal readonly Delegate Delegate;

        internal readonly TypeInfo TargetType;

        private DynamicRowConverter(ConstructorInfo cons)
        {
            ConstructorForObject = cons;
            TargetType = cons.DeclaringType.GetTypeInfo();
        }

        private DynamicRowConverter(TypeInfo target, MethodInfo del)
        {
            Method = del;
            TargetType = target;
        }

        private DynamicRowConverter(ConstructorInfo cons, TypeInfo[] paramTypes, ColumnIdentifier[] colsForPs)
        {
            ConstructorTakingParams = cons;
            ColumnsForParameters = colsForPs;
            ParameterTypes = paramTypes;
            TargetType = cons.DeclaringType.GetTypeInfo();
        }

        private DynamicRowConverter(ConstructorInfo cons, Setter[] setters, ColumnIdentifier[] colsForSetters)
        {
            EmptyConstructor = cons;
            Setters = setters;
            ColumnsForSetters = colsForSetters;
            TargetType = cons.DeclaringType.GetTypeInfo();
        }

        private DynamicRowConverter(TypeInfo target, Delegate del)
        {
            Delegate = del;
            TargetType = target;
        }

        /// <summary>
        /// Create a DynamicRowConverter from the given delegate.
        /// </summary>
        public static DynamicRowConverter ForDelegate<T>(DynamicRowConverterDelegate<T> del)
        {
            if (del == null)
            {
                return Throw.ArgumentNullException<DynamicRowConverter>(nameof(del));
            }

            return new DynamicRowConverter(typeof(T).GetTypeInfo(), del);
        }

        /// <summary>
        /// Create a DynamicRowConverter from the given constructor.
        /// 
        /// Constructor must take an object (which can be dynamic in source).
        /// </summary>
        public static DynamicRowConverter ForConstructorTakingDynamic(ConstructorInfo cons)
        {
            if (cons == null)
            {
                return Throw.ArgumentNullException<DynamicRowConverter>(nameof(cons));
            }

            var ps = cons.GetParameters();
            if (ps.Length != 1)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Constructor {cons} must take a single object", nameof(cons));
            }

            var p = ps[0].ParameterType.GetTypeInfo();
            if (p != Types.ObjectType)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Constructor {cons} must take a object, found a {p}", nameof(cons));
            }

            return new DynamicRowConverter(cons);
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
            ConstructorInfo cons,
            IEnumerable<ColumnIdentifier> columnIndexesForParams
        )
        {
            if (cons == null)
            {
                return Throw.ArgumentNullException<DynamicRowConverter>(nameof(cons));
            }

            if (columnIndexesForParams == null)
            {
                return Throw.ArgumentNullException<DynamicRowConverter>(nameof(columnIndexesForParams));
            }

            var cifp = columnIndexesForParams.ToArray();

            var ps = cons.GetParameters();
            if (ps.Length != cifp.Length)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"Constructor {cons} takes {ps.Length} parameters, while only {cifp.Length} column indexes were passed");
            }

            for (var i = 0; i < cifp.Length; i++)
            {
                var colIx = cifp[i].Index;
                if (colIx < 0)
                {
                    return Throw.ArgumentException<DynamicRowConverter>($"Column indexes must be >= 0, found {colIx} at {i}", nameof(columnIndexesForParams));
                }
            }

            var psAsTypeInfo = new TypeInfo[ps.Length];
            for (var i = 0; i < ps.Length; i++)
            {
                psAsTypeInfo[i] = ps[i].ParameterType.GetTypeInfo();
            }

            return new DynamicRowConverter(cons, psAsTypeInfo, cifp);
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
            ConstructorInfo cons,
            IEnumerable<Setter> setters,
            IEnumerable<ColumnIdentifier> colsToSetters
        )
        {
            if (cons == null)
            {
                return Throw.ArgumentNullException<DynamicRowConverter>(nameof(cons));
            }

            var consPs = cons.GetParameters();
            if (consPs.Length != 0)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Constructor {cons} must take zero parameters", nameof(cons));
            }

            if (setters == null)
            {
                return Throw.ArgumentNullException<DynamicRowConverter>(nameof(setters));
            }

            if (colsToSetters == null)
            {
                return Throw.ArgumentNullException<DynamicRowConverter>(nameof(colsToSetters));
            }

            var s = setters.ToArray();
            var cts = colsToSetters.ToArray();

            if (s.Length != cts.Length)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"{nameof(setters)} and {nameof(colsToSetters)} must be the same length, found {s.Length} and {cts.Length}");
            }

            var constructedType = cons.DeclaringType.GetTypeInfo();

            for (var i = 0; i < s.Length; i++)
            {
                var setter = s[i];

                // can always call it, doesn't matter
                if (setter.IsStatic) continue;

                var setterOnType = setter.RowType;

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
                    return Throw.ArgumentException<DynamicRowConverter>($"Column indexes must be >= 0, found {colIx} at {i}", nameof(colsToSetters));
                }
            }

            return new DynamicRowConverter(cons, s, cts);
        }

        /// <summary>
        /// Create a DynamicRowConverter from the given method.
        /// 
        /// Method must be static, return a bool, take an object (which can be dynamic in source)
        ///   as it's first parameter, a ReadContext as it's second paramter, and have a third parameter that is an out 
        ///   for the result value.
        /// </summary>
        public static DynamicRowConverter ForMethod(MethodInfo method)
        {
            if (method == null)
            {
                return Throw.ArgumentNullException<DynamicRowConverter>(nameof(method));
            }

            if (!method.IsStatic)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method} must be static", nameof(method));
            }

            if (method.ReturnType.GetTypeInfo() != Types.BoolType)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method} must return a bool", nameof(method));
            }

            var ps = method.GetParameters();
            if (ps.Length != 3)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method} must take three parameters", nameof(method));
            }

            var p1 = ps[0].ParameterType.GetTypeInfo();
            if (p1 != Types.ObjectType)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method}'s first parameter must be an object", nameof(method));
            }

            var p2 = ps[1].ParameterType.GetTypeInfo();
            if (!p2.IsByRef)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method}'s second parameter must be an in {nameof(ReadContext)}, was not passed by reference", nameof(method));
            }

            var p2Elem = p2.GetElementType().GetTypeInfo();
            if (p2Elem != Types.ReadContextType)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method}'s second parameter must be a {nameof(ReadContext)}", nameof(method));
            }

            var p3 = ps[2].ParameterType.GetTypeInfo();
            if (!p3.IsByRef)
            {
                return Throw.ArgumentException<DynamicRowConverter>($"Method {method}'s second parameter must be a by ref type", nameof(method));
            }

            var targetType = p3.GetElementType().GetTypeInfo();

            return new DynamicRowConverter(targetType, method);
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
                        if (ConstructorForObject != null)
                        {
                            return $"{nameof(DynamicRowConverter)} using 1 parameter constructor {ConstructorForObject} creating {TargetType}";
                        }

                        if (ConstructorTakingParams != null)
                        {
                            string columnTypeMap;
                            {
                                var map = new StringBuilder();
                                for (var i = 0; i < ColumnsForParameters.Length; i++)
                                {
                                    var col = ColumnsForParameters[i];
                                    var type = ParameterTypes[i];

                                    if (i != 0)
                                    {
                                        map.Append(", ");
                                    }
                                    map.Append($"parameter {i} = column {col} as {type}");
                                }

                                columnTypeMap = map.ToString();
                            }

                            return $"{nameof(DynamicRowConverter)} using {ColumnsForParameters.Length} parameter constructor {ConstructorTakingParams} with ({columnTypeMap}) creating {TargetType}";
                        }

                        if (EmptyConstructor != null)
                        {
                            string setterMap;
                            {
                                var map = new StringBuilder();
                                for (var i = 0; i < Setters.Length; i++)
                                {
                                    var setter = Setters[i];
                                    var col = ColumnsForSetters[i];

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

                        return Throw.Exception<string>("Shouldn't be possible");
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
        public override bool Equals(object obj)
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
        public bool Equals(DynamicRowConverter other)
        {
            if (ReferenceEquals(other, null)) return false;

            var thisMode = Mode;
            var otherMode = other.Mode;
            if (thisMode != otherMode)
            {
                return false;
            }

            switch (thisMode)
            {
                case BackingMode.Method: return Method.Equals(other.Method);
                case BackingMode.Constructor:
                    if (ConstructorForObject != null)
                    {
                        return ConstructorForObject.Equals(other.ConstructorForObject);
                    }

                    if (ConstructorTakingParams != null)
                    {
                        if (!ConstructorTakingParams.Equals(other.ConstructorTakingParams))
                        {
                            return false;
                        }

                        // type compatibility is implicitly checked by comparing constructors

                        for (var i = 0; i < ParameterTypes.Length; i++)
                        {
                            var thisCol = ColumnsForParameters[i];
                            var otherCol = other.ColumnsForParameters[i];

                            if (thisCol != otherCol)
                            {
                                return false;
                            }
                        }

                        return true;
                    }

                    if (EmptyConstructor != null)
                    {
                        if (!EmptyConstructor.Equals(other.EmptyConstructor))
                        {
                            return false;
                        }

                        if (Setters.Length != other.Setters.Length)
                        {
                            return false;
                        }

                        for (var i = 0; i < Setters.Length; i++)
                        {
                            var thisSetter = Setters[i];
                            var otherSetter = other.Setters[i];

                            var thisCol = ColumnsForSetters[i];
                            var otherCol = other.ColumnsForSetters[i];

                            if (thisSetter != otherSetter || thisCol != otherCol)
                            {
                                return false;
                            }
                        }

                        return true;
                    }

                    return Throw.Exception<bool>($"Shouldn't be possible, unexpected Constructor configuration");
                case BackingMode.Delegate: return Delegate.Equals(other.Delegate);
                default:
                    return Throw.InvalidOperationException<bool>($"Unexpected {nameof(BackingMode)}: {thisMode}");
            }
        }

        /// <summary>
        /// Returns a stable hash for this DynamicRowConverter.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(DynamicRowConverter), Mode, Method, ConstructorForObject, ConstructorTakingParams, EmptyConstructor, Delegate);

        /// <summary>
        /// Compare two DynamicRowConverters for equality
        /// </summary>
        public static bool operator ==(DynamicRowConverter a, DynamicRowConverter b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two DynamicRowConverters for inequality
        /// </summary>
        public static bool operator !=(DynamicRowConverter a, DynamicRowConverter b)
        => !(a == b);

        /// <summary>
        /// Convenience operator, equivalent to calling DynamicRowConverter.ForMethod if non-null.
        /// 
        /// Returns null if mtd is null.
        /// </summary>
        public static explicit operator DynamicRowConverter(MethodInfo mtd)
        => mtd == null ? null : ForMethod(mtd);

        /// <summary>
        /// Convenience operator, equivalent to calling DynamicRowConverter.ForConstructorTakingDynamic if non-null.
        /// 
        /// Returns null if cons is null.
        /// </summary>
        public static explicit operator DynamicRowConverter(ConstructorInfo cons)
        => cons == null ? null : ForConstructorTakingDynamic(cons);

        /// <summary>
        /// Convenience operator, equivalent to calling DynamicRowConverter.ForDelegate if non-null.
        /// 
        /// Returns null if cons is null.
        /// </summary>
        public static explicit operator DynamicRowConverter(Delegate del)
        {
            if (del == null) return null;

            var delType = del.GetType().GetTypeInfo();

            if (delType.IsGenericType && delType.GetGenericTypeDefinition() == Types.DynamicRowConverterDelegateType)
            {
                var t = delType.GetGenericArguments()[0].GetTypeInfo();

                return new DynamicRowConverter(t, del);
            }

            var mtd = del.Method;
            var ret = mtd.ReturnType.GetTypeInfo();
            if (ret != Types.BoolType)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"Delegate must return a bool");
            }

            var args = mtd.GetParameters();
            if (args.Length != 3)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"Delegate must take 3 parameters");
            }

            var p1 = args[0].ParameterType.GetTypeInfo();
            if (p1 != Types.ObjectType)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"The first parameter to the delegate must be an object (can be dynamic in source)");
            }

            var p2 = args[1].ParameterType.GetTypeInfo();
            if (!p2.IsByRef)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"The second paramater to the delegate must be an in {nameof(ReadContext)}, was not by ref");
            }

            if (p2.GetElementType() != Types.ReadContextType)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"The second paramater to the delegate must be an in {nameof(ReadContext)}");
            }

            var createsRef = args[2].ParameterType.GetTypeInfo();
            if (!createsRef.IsByRef)
            {
                return Throw.InvalidOperationException<DynamicRowConverter>($"The third paramater to the delegate must be an out type, was not by ref");
            }

            var creates = createsRef.GetElementType().GetTypeInfo();

            var converterDel = Types.DynamicRowConverterDelegateType.MakeGenericType(creates);
            var invoke = del.GetType().GetMethod("Invoke");

            var reboundDel = Delegate.CreateDelegate(converterDel, del, invoke);

            return new DynamicRowConverter(creates, reboundDel);
        }
    }
}
