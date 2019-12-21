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

            var expressionIsCellRestriction = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicCellType);

            return expressionIsCellRestriction.Merge(sameParserRestriction);
        }

        private Expression GetParser(TypeInfo forType)
        {
            var typeConst = Expression.Constant(forType);
            var selfAsCell = Expression.Convert(Expression, Types.DynamicCellType);
            var converter = Expression.Call(selfAsCell, Methods.DynamicCell.Converter);
            var ctx = Expression.Call(selfAsCell, Methods.DynamicCell.GetReadContext);
            var parser = Expression.Call(converter, Methods.ITypeDescriber.GetDynamicCellParserFor, ctx, typeConst);

            return parser;
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var retType = binder.ReturnType.GetTypeInfo();

            var converterInterface = Cell.Converter;
            var index = Cell.ColumnNumber;

            var row = Cell.Row;
            var owner = row.Owner.Value;

            var col = row.Columns.Value[index];

            var ctx = ReadContext.ConvertingColumn(owner.Options, row.RowNumber, col, owner.Context);

            var parser = converterInterface.GetDynamicCellParserFor(in ctx, retType);
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

            var selfAsCell = Expression.Convert(Expression, Types.DynamicCellType);

            var callGetSpan = Expression.Call(selfAsCell, Methods.DynamicCell.GetDataSpan);

            var makeCtx = Expression.Call(selfAsCell, Methods.DynamicCell.GetReadContext);

            var outVar = Expression.Parameter(parser.Creates);
            var resVar = Expression.Parameter(Types.BoolType);
            var parserExp = parser.MakeExpression(callGetSpan, makeCtx, outVar);
            var assign = Expression.Assign(resVar, parserExp);

            var statements = new List<Expression>();
            statements.Add(assign);

            var invalidCallOp = Methods.Throw.InvalidOperationExceptionOfObject;
            var callThrow = Expression.Call(invalidCallOp, Expression.Constant($"{nameof(Parser)} {parser} returned false"));

            var ifNot = Expression.IfThen(Expression.Not(resVar), callThrow);
            statements.Add(ifNot);

            var convertOut = Expression.Convert(outVar, binder.ReturnType);
            statements.Add(convertOut);

            var block = Expression.Block(new ParameterExpression[] { outVar, resVar }, statements);

            return new DynamicMetaObject(block, restrictions);
        }
    }
}
