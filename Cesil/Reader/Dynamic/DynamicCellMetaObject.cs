using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal sealed class DynamicCellMetaObject : DynamicMetaObject
    {
        private readonly DynamicCell Cell;

        internal DynamicCellMetaObject(DynamicCell outer, Expression exp) : base(exp, BindingRestrictions.Empty, outer)
        {
            Cell = outer;
        }

        private BindingRestrictions MakeRestrictions(Parser? expected, TypeInfo returnType)
        {
            var expectedConst = Expression.Constant(expected);
            var get = GetParser(returnType);
            var eq = Expression.Equal(expectedConst, get);

            var sameParserRestriction = BindingRestrictions.GetExpressionRestriction(eq);

            var expressionIsCellRestriction = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicCell);

            return expressionIsCellRestriction.Merge(sameParserRestriction);
        }

        private Expression GetParser(TypeInfo forType)
        {
            var typeConst = Expression.Constant(forType);
            var selfAsCell = Expression.Convert(Expression, Types.DynamicCell);
            var converter = Expression.Call(selfAsCell, Methods.DynamicCell.Converter);
            var ctx = Expression.Call(selfAsCell, Methods.DynamicCell.GetReadContext);
            var parser = Expression.Call(converter, Methods.ITypeDescriber.GetDynamicCellParserFor, ctx, typeConst);

            return parser;
        }

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
            var expressionIsCellRestriction = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicCell);

            var retType = binder.ReturnType != Types.Void ? binder.ReturnType : Types.Object;

            var invalidOpCall = Methods.Throw.InvalidOperationException.MakeGenericMethod(retType);
            var throwMsg = Expression.Call(invalidOpCall, Expression.Constant($"Dynamic cells have no methods.  Explicitly cast to desired type or IConvertible."));

            return new DynamicMetaObject(throwMsg, expressionIsCellRestriction);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var retType = binder.ReturnType.GetTypeInfo();

            // casting to IConvertible is always supported
            if(binder.Type == Types.IConvertible)
            {
                var expressionIsCellRestriction = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicCell);
                var selfAsIConvertible = Expression.Convert(Expression, Types.IConvertible);

                // this will always succeed provide the dynamic doesn't become a non-DynamicCell type
                return new DynamicMetaObject(selfAsIConvertible, expressionIsCellRestriction);
            }

            var parser = Cell.GetParser(retType, out _);
            var restrictions = MakeRestrictions(parser, retType);

            if (parser == null)
            {
                var invalidOpCall = Methods.Throw.InvalidOperationException.MakeGenericMethod(retType);
                var throwMsg = Expression.Call(invalidOpCall, Expression.Constant($"No cell converter discovered for {binder.ReturnType}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            if (!binder.ReturnType.IsAssignableFrom(parser.Creates))
            {
                var invalidOpCall = Methods.Throw.InvalidOperationException.MakeGenericMethod(retType);
                var throwMsg = Expression.Call(invalidOpCall, Expression.Constant($"Cell converter {parser} does not create a type assignable to {binder.ReturnType}, returns {parser.Creates}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            var statements = new List<Expression>();

            var selfAsCell = Expression.Convert(Expression, Types.DynamicCell);

            var callGetSpan = Expression.Call(selfAsCell, Methods.DynamicCell.GetDataSpan);
            var dataSpanVar = Expressions.Variable_ReadOnlySpanOfChar;
            var assignDataSpan = Expression.Assign(dataSpanVar, callGetSpan);
            statements.Add(assignDataSpan);

            var callMakeCtx = Expression.Call(selfAsCell, Methods.DynamicCell.GetReadContext);
            var readCtxVar = Expressions.Variable_ReadContext;
            var assignReadCtx = Expression.Assign(readCtxVar, callMakeCtx);
            statements.Add(assignReadCtx);

            var outVar = Expression.Parameter(parser.Creates);
            var resVar = Expressions.Variable_Bool;
            var parserExp = parser.MakeExpression(dataSpanVar, readCtxVar, outVar);
            var assignRes = Expression.Assign(resVar, parserExp);


            statements.Add(assignRes);

            var invalidCallOp = Methods.Throw.InvalidOperationExceptionOfObject;
            var callThrow = Expression.Call(invalidCallOp, Expression.Constant($"{nameof(Parser)} {parser} returned false"));

            var ifNot = Expression.IfThen(Expression.Not(resVar), callThrow);
            statements.Add(ifNot);

            var convertOut = Expression.Convert(outVar, binder.ReturnType);
            statements.Add(convertOut);

            var block = Expression.Block(new ParameterExpression[] { outVar, dataSpanVar, readCtxVar, resVar }, statements);

            return new DynamicMetaObject(block, restrictions);
        }
    }
}
