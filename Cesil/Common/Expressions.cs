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

        internal static readonly ConstantExpression Constant_True = Expression.Constant(true);
        internal static readonly ConstantExpression Constant_False = Expression.Constant(false);
        internal static readonly ConstantExpression Constant_Null = Expression.Constant(null, Types.ObjectType);

        internal static readonly DefaultExpression Default_Bool = Expression.Default(Types.BoolType);
        internal static readonly DefaultExpression Default_Object = Expression.Default(Types.ObjectType);

        internal static readonly ParameterExpression Variable_Bool = Expression.Variable(Types.BoolType);
    }
}
