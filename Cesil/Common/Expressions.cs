using System.Linq.Expressions;

namespace Cesil
{
    internal static class Expressions
    {
        internal static readonly ParameterExpression Parameter_ReadOnlySpanOfChar = Expression.Parameter(Types.ReadOnlySpanOfChar);
        internal static readonly ParameterExpression Parameter_ReadContext_ByRef = Expression.Parameter(Types.ReadContext.MakeByRefType());
        internal static readonly ParameterExpression Parameter_WriteContext_ByRef = Expression.Parameter(Types.WriteContext.MakeByRefType());
        internal static readonly ParameterExpression Parameter_Object = Expression.Parameter(Types.Object);
        internal static readonly ParameterExpression Parameter_IBufferWriterOfChar = Expression.Parameter(Types.IBufferWriterOfChar);
        internal static readonly ParameterExpression Parameter_Object_ByRef = Expression.Parameter(Types.Object.MakeByRefType());

        internal static readonly ConstantExpression Constant_True = Expression.Constant(true);
        internal static readonly ConstantExpression Constant_False = Expression.Constant(false);
        internal static readonly ConstantExpression Constant_Null = Expression.Constant(null, Types.Object);
        internal static readonly ConstantExpression Constant_NullInt = Expression.Constant(null, Types.NullableInt);

        internal static readonly ParameterExpression Variable_Bool = Expression.Variable(Types.Bool);
        internal static readonly ParameterExpression Variable_ReadOnlySpanOfChar = Expression.Variable(Types.ReadOnlySpanOfChar);
        internal static readonly ParameterExpression Variable_ReadContext = Expression.Variable(Types.ReadContext);
        internal static readonly ParameterExpression Variable_DynamicRow = Expression.Variable(Types.DynamicRow);
    }
}
