using System.Linq.Expressions;

namespace Cesil
{
    internal static class Expressions
    {
        internal static readonly ParameterExpression Parameter_ReadOnlySpanOfChar = Expression.Parameter(Types.ReadOnlySpanOfCharType);
        internal static readonly ParameterExpression Parameter_ReadContext_ByRef = Expression.Parameter(Types.ReadContextType.MakeByRefType());
        internal static readonly ParameterExpression Parameter_WriteContext_ByRef = Expression.Parameter(Types.WriteContextType.MakeByRefType());
        internal static readonly ParameterExpression Parameter_Object = Expression.Parameter(Types.ObjectType);
        internal static readonly ParameterExpression Parameter_IBufferWriterOfChar = Expression.Parameter(Types.IBufferWriterOfCharType);
        internal static readonly ParameterExpression Parameter_Object_ByRef = Expression.Parameter(Types.ObjectType.MakeByRefType());

        internal static readonly ConstantExpression Constant_True = Expression.Constant(true);
        internal static readonly ConstantExpression Constant_False = Expression.Constant(false);
        internal static readonly ConstantExpression Constant_Null = Expression.Constant(null, Types.ObjectType);

        internal static readonly ParameterExpression Variable_Bool = Expression.Variable(Types.BoolType);
        internal static readonly ParameterExpression Variable_ReadOnlySpanOfChar = Expression.Variable(Types.ReadOnlySpanOfCharType);
        internal static readonly ParameterExpression Variable_ReadContext = Expression.Variable(Types.ReadContextType);
        internal static readonly ParameterExpression Variable_DynamicRow = Expression.Variable(Types.DynamicRowType);
    }
}
