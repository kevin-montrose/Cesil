using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Describes how to convert a dynamic row value
    ///   into an instance of a type.
    ///   
    /// Conversions can implemented as constructors taking a single 
    ///   dynamic/object, constructors taking typed parameters, an 
    ///   empty constructor paired with setter methods,
    ///   or static methods.
    /// </summary>
    public sealed class DynamicRowConverter
    {
        internal readonly ConstructorInfo ConstructorForObject;

        internal readonly MethodInfo Method;

        internal readonly ConstructorInfo ConstructorTakingParams;
        internal readonly TypeInfo[] ParameterTypes;
        internal readonly int[] ColumnsForParameters;

        internal readonly ConstructorInfo EmptyConstructor;
        internal readonly MethodInfo[] Setters;
        internal readonly TypeInfo[] SetterParameters;
        internal readonly int[] ColumnsForSetters;

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

        private DynamicRowConverter(ConstructorInfo cons, TypeInfo[] parmTypes, int[] colsForPs)
        {
            ConstructorTakingParams = cons;
            ColumnsForParameters = colsForPs;
            ParameterTypes = parmTypes;
            TargetType = cons.DeclaringType.GetTypeInfo();
        }

        private DynamicRowConverter(ConstructorInfo cons, MethodInfo[] setters, TypeInfo[] setterParams, int[] colsForSetters)
        {
            EmptyConstructor = cons;
            Setters = setters;
            ColumnsForSetters = colsForSetters;
            SetterParameters = setterParams;
            TargetType = cons.DeclaringType.GetTypeInfo();
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
                Throw.ArgumentNullException(nameof(cons));
            }

            var ps = cons.GetParameters();
            if (ps.Length != 1)
            {
                Throw.ArgumentException($"Constructor {cons} must take a single object", nameof(cons));
            }

            var p = ps[0].ParameterType.GetTypeInfo();
            if (p != Types.ObjectType)
            {
                Throw.ArgumentException($"Constructor {cons} must take a object, found a {p}", nameof(cons));
            }

            return new DynamicRowConverter(cons);
        }

        /// <summary>
        /// Create a DynamicRowConverter for the given constructor, which maps specific columns to the parameters
        ///   for the of the constructor.
        ///   
        /// Constructor must take the same number of parameters as column indexes passed in.
        /// </summary>
        public static DynamicRowConverter ForConstructorTakingTypedParameters(ConstructorInfo cons, IEnumerable<int> columnIndexesForParams)
        {
            if (cons == null)
            {
                Throw.ArgumentNullException(nameof(cons));
            }

            if (columnIndexesForParams == null)
            {
                Throw.ArgumentNullException(nameof(columnIndexesForParams));
            }

            var cifp = columnIndexesForParams.ToArray();

            var ps = cons.GetParameters();
            if (ps.Length != cifp.Length)
            {
                Throw.InvalidOperationException($"Constructor {cons} takes {ps.Length} parameters, while only {cifp.Length} column indexes were passed");
            }

            for (var i = 0; i < cifp.Length; i++)
            {
                var colIx = cifp[i];
                if (colIx < 0)
                {
                    Throw.ArgumentException($"Column indexes must be >= 0, found {colIx} at {i}", nameof(columnIndexesForParams));
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
        /// The same number of setters and columns must be passed.  Setter methods must take a single parameter,
        ///   and either be callable on the constructed type or static.  Setters must return void.
        /// </summary>
        public static DynamicRowConverter ForEmptyConstructorAndSetters(ConstructorInfo cons, IEnumerable<MethodInfo> setters, IEnumerable<int> colsToSetters)
        {
            if (cons == null)
            {
                Throw.ArgumentNullException(nameof(cons));
            }

            var consPs = cons.GetParameters();
            if (consPs.Length != 0)
            {
                Throw.ArgumentException($"Constructor {cons} must take zero parameters", nameof(cons));
            }

            if (setters == null)
            {
                Throw.ArgumentNullException(nameof(setters));
            }

            if (colsToSetters == null)
            {
                Throw.ArgumentNullException(nameof(colsToSetters));
            }

            var s = setters.ToArray();
            var cts = colsToSetters.ToArray();

            if (s.Length != cts.Length)
            {
                Throw.InvalidOperationException($"{nameof(setters)} and {nameof(colsToSetters)} must be the same length, found {s.Length} and {cts.Length}");
            }

            var constructedType = cons.DeclaringType.GetTypeInfo();
            var setterParams = new TypeInfo[s.Length];

            for (var i = 0; i < s.Length; i++)
            {
                var setter = s[i];

                var setterPs = setter.GetParameters();
                if (setterPs.Length != 1)
                {
                    Throw.ArgumentException($"Setter {setter} at {i} does not take a single parameter, takes {setterPs.Length}", nameof(setters));
                }

                setterParams[i] = setterPs[0].ParameterType.GetTypeInfo();

                if (setter.IsStatic)
                {
                    continue;
                }

                var setterOnType = setter.DeclaringType;

                if (!setterOnType.IsAssignableFrom(constructedType))
                {
                    Throw.ArgumentException($"Setter {setter} at {i} cannot be called on {constructedType}", nameof(setters));
                }
            }

            for (var i = 0; i < cts.Length; i++)
            {
                var colIx = cts[i];
                if (colIx < 0)
                {
                    Throw.ArgumentException($"Column indexes must be >= 0, found {colIx} at {i}", nameof(colsToSetters));
                }
            }

            return new DynamicRowConverter(cons, s, setterParams, cts);
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
                Throw.ArgumentNullException(nameof(method));
            }

            if (!method.IsStatic)
            {
                Throw.ArgumentException($"Method {method} must be static", nameof(method));
            }

            if (method.ReturnType.GetTypeInfo() != Types.BoolType)
            {
                Throw.ArgumentException($"Method {method} must return a bool", nameof(method));
            }

            var ps = method.GetParameters();
            if (ps.Length != 3)
            {
                Throw.ArgumentException($"Method {method} must take three parameters", nameof(method));
            }

            var p1 = ps[0].ParameterType.GetTypeInfo();
            if (p1 != Types.ObjectType)
            {
                Throw.ArgumentException($"Method {method}'s first parameter must be an object", nameof(method));
            }

            var p2 = ps[1].ParameterType.GetTypeInfo();
            if (!p2.IsByRef)
            {
                Throw.ArgumentException($"Method {method}'s second parameter must be an in {nameof(ReadContext)}, was not passed by reference", nameof(method));
            }

            var p2Elem = p2.GetElementType().GetTypeInfo();
            if(p2Elem != Types.ReadContextType)
            {
                Throw.ArgumentException($"Method {method}'s second parameter must be a {nameof(ReadContext)}", nameof(method));
            }

            var p3 = ps[2].ParameterType.GetTypeInfo();
            if (!p3.IsByRef)
            {
                Throw.ArgumentException($"Method {method}'s second parameter must be a by ref type", nameof(method));
            }

            var targetType = p3.GetElementType().GetTypeInfo();

            return new DynamicRowConverter(targetType, method);
        }
    }
}
