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

        private BindingRestrictions MakeRestrictions(Parser expected, TypeInfo returnType)
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
            var converter = Expression.Call(selfAsCell, Methods.DynamicCell.GetConverter);
            var ctx = Expression.Call(selfAsCell, Methods.DynamicCell.GetReadContext);
            var parser = Expression.Call(converter, Methods.ITypeDescriber.GetCellParserFor, ctx, typeConst);

            return parser;
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var retType = binder.ReturnType.GetTypeInfo();

            var converterInterface = Cell.Converter;
            var index = Cell.ColumnNumber;

            var col = Cell.Row.Columns[index];

            var ctx = ReadContext.ConvertingColumn(Cell.Row.RowNumber, col, Cell.Row.Owner.Context);

            var converter = converterInterface.GetDynamicCellParserFor(in ctx, retType);
            var restrictions = MakeRestrictions(converter, retType);

            if (converter == null)
            {
                var throwMsg = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"No cell converter discovered for {binder.ReturnType}"));
                var def = Expression.Default(retType);

                var block = Expression.Block(throwMsg, def);

                return new DynamicMetaObject(block, restrictions);
            }

            if (!binder.ReturnType.IsAssignableFrom(converter.Creates))
            {
                var throwMsg = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"Cell converter {converter} does not create a type assignable to {binder.ReturnType}, returns {converter.Creates}"));
                var def = Expression.Default(retType);

                var block = Expression.Block(throwMsg, def);

                return new DynamicMetaObject(block, restrictions);
            }

            var selfAsCell = Expression.Convert(Expression, Types.DynamicCellType);

            var callGetSpan = Expression.Call(selfAsCell, Methods.DynamicCell.GetDataSpan);

            switch (converter.Mode)
            {
                case BackingMode.Constructor:
                    {
                        var cons = converter.Constructor;
                        var consPCount = cons.GetParameters().Length;

                        NewExpression createType;
                        if (consPCount == 1)
                        {
                            createType = Expression.New(converter.Constructor, callGetSpan);
                        }
                        else
                        {
                            var makeCtx = Expression.Call(selfAsCell, Methods.DynamicCell.GetReadContext);
                            createType = Expression.New(converter.Constructor, callGetSpan, makeCtx);
                        }
                        var cast = Expression.Convert(createType, binder.ReturnType);

                        return new DynamicMetaObject(cast, restrictions);
                    }
                case BackingMode.Method:
                    {
                        var statements = new List<Expression>();

                        var makeCtx = Expression.Call(selfAsCell, Methods.DynamicCell.GetReadContext);
                        var outVar = Expression.Parameter(converter.Creates);
                        var resVar = Expressions.Variable_Bool;

                        var mtd = converter.Method;
                        var callConvert = Expression.Call(mtd, callGetSpan, makeCtx, outVar);
                        var assignRes = Expression.Assign(resVar, callConvert);

                        statements.Add(assignRes);

                        var callThrow = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"{nameof(Parser)} backing method {mtd} returned false"));

                        var ifNot = Expression.IfThen(Expression.Not(resVar), callThrow);
                        statements.Add(ifNot);

                        var convertOut = Expression.Convert(outVar, binder.ReturnType);
                        statements.Add(convertOut);

                        var block = Expression.Block(new ParameterExpression[] { outVar, resVar }, statements);

                        return new DynamicMetaObject(block, restrictions);
                    }
                case BackingMode.Delegate:
                    {
                        var statements = new List<Expression>();

                        var del = converter.Delegate;
                        var delRef = Expression.Constant(del);

                        var makeCtx = Expression.Call(selfAsCell, Methods.DynamicCell.GetReadContext);
                        var outVar = Expression.Parameter(converter.Creates);
                        var resVar = Expressions.Variable_Bool;

                        var callParser = Expression.Invoke(delRef, callGetSpan, makeCtx, outVar);
                        var assignRes = Expression.Assign(resVar, callParser);

                        statements.Add(assignRes);

                        var callThrow = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"{nameof(Parser)} backing delegate {del} returned false"));
                        var ifNot = Expression.IfThen(Expression.Not(resVar), callThrow);
                        statements.Add(ifNot);

                        var convertOut = Expression.Convert(outVar, binder.ReturnType);
                        statements.Add(convertOut);

                        var block = Expression.Block(new ParameterExpression[] { outVar, resVar }, statements);

                        return new DynamicMetaObject(block, restrictions);
                    }
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(BackingMode)}: {converter.Mode}");
                    // just for control flow
                    return default;

            }
        }
    }
}
