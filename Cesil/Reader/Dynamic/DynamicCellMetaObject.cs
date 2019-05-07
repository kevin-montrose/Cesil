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

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var converterInterface = Cell.Converter;
            var index = Cell.ColumnNumber;
            var colName = Cell.Row.Names?[index];
            var converter = converterInterface.GetCellConverter(index, colName, binder.ReturnType.GetTypeInfo());

            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

            if (converter == null)
            {
                var throwMsg = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"No cell converter discovered for {binder.ReturnType}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            if (!binder.ReturnType.IsAssignableFrom(converter.TargetType))
            {
                var throwMsg = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"Cell converter {converter} does not create a type assignable to {binder.ReturnType}, returns {converter.TargetType}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            var selfAsCell = Expression.Convert(Expression, Types.DynamicCell);
            
            var callGetSpan = Expression.Call(selfAsCell, Methods.DynamicCell.GetDataSpan);

            var cons = converter.Constructor;
            if (cons != null)
            {
                var createType = Expression.New(converter.Constructor, callGetSpan);
                var cast = Expression.Convert(createType, binder.ReturnType);

                return new DynamicMetaObject(cast, restrictions);
            }

            var mtd = converter.Method;
            if(mtd != null)
            {
                var statements = new List<Expression>();

                var makeCtx = Expression.Call(selfAsCell, Methods.DynamicCell.GetReadContext);
                var outVar = Expression.Parameter(converter.TargetType);
                var resVar = Expression.Variable(Types.BoolType);

                var callConvert = Expression.Call(mtd, callGetSpan, makeCtx, outVar);
                var assignRes = Expression.Assign(resVar, callConvert);

                statements.Add(assignRes);

                var callThrow = Expression.Call(Methods.Throw.InvalidOperationException, Expression.Constant($"{nameof(DynamicCellConverter)} returned false"));

                var ifNot = Expression.IfThen(Expression.Not(resVar), callThrow);
                statements.Add(ifNot);

                var convertOut = Expression.Convert(outVar, binder.ReturnType);
                statements.Add(convertOut);

                var block = Expression.Block(new ParameterExpression[] { outVar, resVar }, statements);

                return new DynamicMetaObject(block, restrictions);
            }

            Throw.Exception("Converter could not be turned into an expression, this shouldn't be possible");
            return null;
        }
    }
}
