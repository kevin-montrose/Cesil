using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Describes how to convert a dynamic cell value
    ///   into an instance of a type.
    ///   
    /// Conversions can implemented as constructors,
    ///   or static methods.
    /// </summary>
    public sealed class DynamicCellConverter
    {
        internal ConstructorInfo Constructor;
        internal MethodInfo Method;

        internal TypeInfo TargetType;

        private DynamicCellConverter(ConstructorInfo cons)
        {
            Constructor = cons;
            TargetType = cons.DeclaringType.GetTypeInfo();
        }

        private DynamicCellConverter(TypeInfo target, MethodInfo del)
        {
            Method = del;
            TargetType = target;
        }

        /// <summary>
        /// Create a DynamicCellConverter from the given constructor.
        /// 
        /// Constructor must take a ReadOnlySpan(char).
        /// </summary>
        public static DynamicCellConverter ForConstructor(ConstructorInfo cons)
        {
            if (cons == null)
            {
                Throw.ArgumentNullException(nameof(cons));
            }

            var ps = cons.GetParameters();
            if (ps.Length != 1)
            {
                Throw.ArgumentException($"Constructor {cons} must take a single ReadOnlySpan<char>", nameof(cons));
            }

            var p = ps[0].ParameterType.GetTypeInfo();
            if (p != Types.ReadOnlySpanOfCharType)
            {
                Throw.ArgumentException($"Constructor {cons} must take a ReadOnlySpan<char>, found a {p}", nameof(cons));
            }

            return new DynamicCellConverter(cons);
        }

        /// <summary>
        /// Create a DynamicCellConverter from the given method.
        /// 
        /// Method must be static, return a bool, take a ReadOnlySpan(char) 
        ///   as it's first parameter, a ReadContext as it's second parameter,
        ///   and have a third parameter that is an out for the result value.
        /// </summary>
        public static DynamicCellConverter ForMethod(MethodInfo method)
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
            if (p1 != Types.ReadOnlySpanOfCharType)
            {
                Throw.ArgumentException($"Method {method}'s first parameter must be a ReadOnlySpan<char>", nameof(method));
            }

            var p2 = ps[1].ParameterType.GetTypeInfo();
            if (!p2.IsByRef)
            {
                Throw.ArgumentException($"Method {method}'s second parameter must an in {nameof(ReadContext)}, was not passed as a reference", nameof(method));
            }

            var p2Elem = p2.GetElementType().GetTypeInfo();
            if(p2Elem != Types.ReadContextType)
            {
                Throw.ArgumentException($"Method {method}'s second parameter must be an in {nameof(ReadContext)}", nameof(method));
            }

            var p3 = ps[2].ParameterType.GetTypeInfo();
            if (!p3.IsByRef)
            {
                Throw.ArgumentException($"Method {method}'s second parameter must be a by ref type", nameof(method));
            }

            var targetType = p3.GetElementType().GetTypeInfo();

            return new DynamicCellConverter(targetType, method);
        }
    }
}
