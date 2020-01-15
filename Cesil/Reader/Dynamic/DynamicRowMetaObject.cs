using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal sealed class DynamicRowMetaObject : DynamicMetaObject
    {
        private readonly DynamicRow Row;

        internal DynamicRowMetaObject(DynamicRow outer, Expression exp) : base(exp, BindingRestrictions.Empty, outer)
        {
            Row = outer;
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        => new DynamicRowMemberNameEnumerable(Row);

        public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
            var expressionIsDynamicRowRestriction = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowType);

            if (indexes.Length != 1)
            {
                var msg = Expression.Constant($"Only single indexers are supported.");
                var invalidOpCall = Methods.Throw.InvalidOperationExceptionOfObject;
                var call = Expression.Call(invalidOpCall, msg);

                // we can cache this forever (for this type), since there's no scenario under which indexes != 1 becomes correct
                return new DynamicMetaObject(call, expressionIsDynamicRowRestriction);
            }

            var indexExp = indexes[0].Expression;
            var indexType = indexes[0].RuntimeType.GetTypeInfo();

            if (indexType == Types.IntType)
            {
                var indexExpressionIsIntRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.IntType);

                var castToRow = Expression.Convert(Expression, Types.DynamicRowType);

                var index = Expression.Convert(indexExp, Types.IntType);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetAt, index);

                var finalRestrictions = expressionIsDynamicRowRestriction.Merge(indexExpressionIsIntRestriction);
                return new DynamicMetaObject(callOnSelf, finalRestrictions);
            }

            if (indexType == Types.StringType)
            {
                var indexExpressionIsStringRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.StringType);

                var castToRow = Expression.Convert(Expression, Types.DynamicRowType);

                var col = Expression.Convert(indexExp, Types.StringType);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetByName, col);

                var finalRestrictions = expressionIsDynamicRowRestriction.Merge(indexExpressionIsStringRestriction);
                return new DynamicMetaObject(callOnSelf, finalRestrictions);
            }

            if (indexType == Types.IndexType)
            {
                var indexExpressionIsIndexRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.IndexType);

                var castToRow = Expression.Convert(Expression, Types.DynamicRowType);

                var col = Expression.Convert(indexExp, Types.IndexType);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetByIndex, col);

                var finalRestrictions = expressionIsDynamicRowRestriction.Merge(indexExpressionIsIndexRestriction);
                return new DynamicMetaObject(callOnSelf, finalRestrictions);
            }

            if (indexType == Types.RangeType)
            {
                var indexExpressionIsRangeRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.RangeType);

                var castToRow = Expression.Convert(Expression, Types.DynamicRowType);

                var range = Expression.Convert(indexExp, Types.RangeType);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetRange, range);

                var finalRestrictions = expressionIsDynamicRowRestriction.Merge(indexExpressionIsRangeRestriction);
                return new DynamicMetaObject(callOnSelf, finalRestrictions);
            }

            if (indexType == Types.ColumnIdentifierType)
            {
                var indexExpressionIsRangeRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.ColumnIdentifierType);

                var castToRow = Expression.Convert(Expression, Types.DynamicRowType);

                var colId = Expression.Convert(indexExp, Types.ColumnIdentifierType);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetByIdentifier, colId);

                var finalRestrictions = expressionIsDynamicRowRestriction.Merge(indexExpressionIsRangeRestriction);
                return new DynamicMetaObject(callOnSelf, finalRestrictions);
            }

            // no binder
            {
                var msg = Expression.Constant($"Only string, int, index, and range indexers are supported.");
                var invalidOpCall = Methods.Throw.InvalidOperationExceptionOfObject;
                var call = Expression.Call(invalidOpCall, msg);

                // we can cache this forever (for this type), since there's no scenario under which incorrect index types become correct
                return new DynamicMetaObject(call, expressionIsDynamicRowRestriction);
            }
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowType);

            var name = Expression.Constant(binder.Name);
            var castToRow = Expression.Convert(Expression, Types.DynamicRowType);
            var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetByName, name);

            return new DynamicMetaObject(callOnSelf, restrictions);
        }

        private BindingRestrictions MakeRestrictions(DynamicRowConverter? expected, TypeInfo returnType)
        {
            var expectedConst = Expression.Constant(expected);
            var get = GetRowConverter(returnType);
            var eq = Expression.Equal(expectedConst, get);

            var sameConverterRestriction = BindingRestrictions.GetExpressionRestriction(eq);

            var expressionIsRowRestriction = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowType);

            var ret = expressionIsRowRestriction.Merge(sameConverterRestriction);

            return ret;
        }

        private Expression GetRowConverter(TypeInfo forType)
        {
            var typeConst = Expression.Constant(forType);
            var selfAsRow = Expression.Convert(Expression, Types.DynamicRowType);
            var converterNonNull = Expression.Field(selfAsRow, Fields.DynamicRow.Converter);
            var converter = Expression.Call(converterNonNull, Methods.NonNull.OfITypeDescriber.Value);
            var rowNumber = Expression.Field(selfAsRow, Fields.DynamicRow.RowNumber);
            var columnsNonNull = Expression.Field(selfAsRow, Fields.DynamicRow.Columns);
            var columns = Expression.Call(columnsNonNull, Methods.NonNull.OfIReadOnlyListOfColumnIdentifier.Value);
            var context = Expression.Field(selfAsRow, Fields.DynamicRow.Context);
            var ownerNull = Expression.Field(selfAsRow, Fields.DynamicRow.Owner);
            var owner = Expression.Call(ownerNull, Methods.NonNull.OfIDynamicRowOwner.Value);
            var options = Expression.Call(owner, Methods.IDynamicRowOwner.Options);

            var getCtx = Expression.Call(Methods.ReadContext.ConvertingRow, options, rowNumber, context);

            var dynamicRowConverter = Expression.Call(converter, Methods.ITypeDescriber.GetDynamicRowConverter, getCtx, columns, typeConst);

            return dynamicRowConverter;
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var retType = binder.ReturnType.GetTypeInfo();

            // special case, converting to IDisposable will ALWAYS succeed
            //   because every dynamic row supports disposal
            if (retType == Types.IDisposableType)
            {
                var alwaysRestrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowType);
                var cast = Expression.Convert(Expression, Types.IDisposableType);

                return new DynamicMetaObject(cast, alwaysRestrictions);
            }

            var converterInterface = Row.Converter.Value;
            var index = Row.RowNumber;

            var ctx = ReadContext.ConvertingRow(Row.Owner.Value.Options, index, Row.Context);

            var converter = converterInterface.GetDynamicRowConverter(in ctx, Row.Columns.Value, retType);

            var restrictions = MakeRestrictions(converter, retType);

            if (converter == null)
            {
                var invalidOpCall = Methods.Throw.InvalidOperationException.MakeGenericMethod(retType);
                var throwMsg = Expression.Call(invalidOpCall, Expression.Constant($"No row converter discovered for {retType}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            if (!binder.ReturnType.IsAssignableFrom(converter.TargetType))
            {
                var invalidOpCall = Methods.Throw.InvalidOperationException.MakeGenericMethod(retType);
                var throwMsg = Expression.Call(invalidOpCall, Expression.Constant($"Row converter {converter} does not create a type assignable to {binder.ReturnType}, returns {converter.TargetType}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            var selfAsRow = Expression.Convert(Expression, Types.DynamicRowType);
            var outArg = Expression.Variable(binder.ReturnType);

            var callGetContext = Expression.Call(selfAsRow, Methods.DynamicRow.GetReadContext);

            var convertExp = converter.MakeExpression(binder.ReturnType.GetTypeInfo(), selfAsRow, callGetContext, outArg);

            var errorMsg = Expression.Constant($"{nameof(DynamicRowConverter)} ({converter}) could not convert dynamic row to {binder.ReturnType}");
            var throwInvalidOp = Expression.Call(Methods.Throw.InvalidOperationExceptionOfObject, errorMsg);

            var ifFalseThrow = Expression.IfThen(Expression.Not(convertExp), throwInvalidOp);

            var block = Expression.Block(new[] { outArg }, ifFalseThrow, outArg);

            return new DynamicMetaObject(block, restrictions);
        }
    }
}
