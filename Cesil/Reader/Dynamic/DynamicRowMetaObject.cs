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

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
            var expressionIsDynamicRowRestriction = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRow);

            // only supported operation is .Dispose()
            if (binder.Name == nameof(DynamicRow.Dispose) && args.Length == 0)
            {
                var castToRow = Expression.Convert(Expression, Types.DynamicRow);
                var callDispose = Expression.Call(castToRow, Methods.DynamicRow.Dispose);

                Expression final;

                if (binder.ReturnType == Types.Void)
                {
                    final = callDispose;
                }
                else
                {
                    if (binder.ReturnType == Types.Object)
                    {
                        final = Expression.Block(callDispose, Expressions.Constant_Null);
                    }
                    else
                    {
                        final = Expression.Block(callDispose, Expression.Default(binder.ReturnType));
                    }
                }

                // we can cache this forever (for this type), doesn't vary by anything else
                return new DynamicMetaObject(final, expressionIsDynamicRowRestriction);
            }

            var msg = Expression.Constant($"Only the Dispose() method is supported.");
            var invalidOpCall = Methods.Throw.InvalidOperationExceptionOfObject;
            var call = Expression.Call(invalidOpCall, msg);

            // we can cache this forever (for this type), since there's no scenario under which a non-Dispose call
            //    becomes legal
            return new DynamicMetaObject(call, expressionIsDynamicRowRestriction);
        }

        public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRow);
            var selfAsITestableDisposable = Expression.Convert(Expression, Types.ITestableDisposable);

            return BindGetIndexFor(restrictions, Expression, selfAsITestableDisposable, binder, indexes, null, null);
        }

        // todo: maybe move this to some common place?
        internal static DynamicMetaObject BindGetIndexFor(
            BindingRestrictions restrictions,
            Expression expression, 
            Expression testableDisposable,
            //MethodCallExpression assertNotDisposed,
            GetIndexBinder _, 
            DynamicMetaObject[] indexes, 
            Expression? offset, 
            Expression? length
        )
        {
            if (indexes.Length != 1)
            {
                var msg = Expression.Constant($"Only single indexers are supported.");
                var invalidOpCall = Methods.Throw.InvalidOperationExceptionOfObject;
                var call = Expression.Call(invalidOpCall, msg);

                // we can cache this forever (for this type), since there's no scenario under which indexes != 1 becomes correct
                return new DynamicMetaObject(call, restrictions);
            }

            var assertNotDisposed = MakeAssertNotDisposedExpression(testableDisposable);

            var offsetVar = offset == null ? Expressions.Constant_NullInt : offset;
            var lengthVar = length == null ? Expressions.Constant_NullInt : length;

            var indexExp = indexes[0].Expression;
            var indexType = indexes[0].RuntimeType.GetTypeInfo();

            if (indexType == Types.Int)
            {
                var indexExpressionIsIntRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.Int);

                var castToRow = Expression.Convert(expression, Types.DynamicRow);

                var index = Expression.Convert(indexExp, Types.Int);
                
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetAt, index, testableDisposable, offsetVar, lengthVar);

                var block = Expression.Block(assertNotDisposed, callOnSelf);

                var finalRestrictions = restrictions.Merge(indexExpressionIsIntRestriction);
                return new DynamicMetaObject(block, finalRestrictions);
            }

            if (indexType == Types.String)
            {
                var indexExpressionIsStringRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.String);

                var castToRow = Expression.Convert(expression, Types.DynamicRow);

                var col = Expression.Convert(indexExp, Types.String);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetByName, col, testableDisposable, offsetVar, lengthVar);

                var block = Expression.Block(assertNotDisposed, callOnSelf);

                var finalRestrictions = restrictions.Merge(indexExpressionIsStringRestriction);
                return new DynamicMetaObject(block, finalRestrictions);
            }

            if (indexType == Types.Index)
            {
                var indexExpressionIsIndexRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.Index);

                var castToRow = Expression.Convert(expression, Types.DynamicRow);

                var col = Expression.Convert(indexExp, Types.Index);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetByIndex, col, testableDisposable, offsetVar, lengthVar);

                var block = Expression.Block(assertNotDisposed, callOnSelf);

                var finalRestrictions = restrictions.Merge(indexExpressionIsIndexRestriction);
                return new DynamicMetaObject(block, finalRestrictions);
            }

            if (indexType == Types.Range)
            {
                var indexExpressionIsRangeRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.Range);

                var castToRow = Expression.Convert(expression, Types.DynamicRow);

                var range = Expression.Convert(indexExp, Types.Range);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetRange, range, offsetVar, lengthVar);

                var block = Expression.Block(assertNotDisposed, callOnSelf);

                var finalRestrictions = restrictions.Merge(indexExpressionIsRangeRestriction);
                return new DynamicMetaObject(block, finalRestrictions);
            }

            if (indexType == Types.ColumnIdentifier)
            {
                var indexExpressionIsRangeRestriction = BindingRestrictions.GetTypeRestriction(indexExp, Types.ColumnIdentifier);

                var castToRow = Expression.Convert(expression, Types.DynamicRow);

                var colId = Expression.Convert(indexExp, Types.ColumnIdentifier);
                var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetByIdentifier, colId, testableDisposable, offsetVar, lengthVar);

                var block = Expression.Block(assertNotDisposed, callOnSelf);

                var finalRestrictions = restrictions.Merge(indexExpressionIsRangeRestriction);
                return new DynamicMetaObject(block, finalRestrictions);
            }

            // no binder
            {
                var msg = Expression.Constant($"Only string, int, Index, and Range indexers are supported.");
                var invalidOpCall = Methods.Throw.InvalidOperationExceptionOfObject;
                var call = Expression.Call(invalidOpCall, msg);

                // we can cache this forever (for this type), since there's no scenario under which incorrect index types become correct
                return new DynamicMetaObject(call, restrictions);
            }
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRow);
            var selfAsITestable = Expression.Convert(Expression, Types.ITestableDisposable);

            return BindGetMemberFor(restrictions, Expression, selfAsITestable, binder, null, null);
        }

        // todo: maybe move this to some common place?
        internal static DynamicMetaObject BindGetMemberFor(
            BindingRestrictions restrictions,
            Expression expression, 
            Expression iTestableDisposable,
            GetMemberBinder binder, 
            Expression? offset,
            Expression? length
        )
        {
            var offsetVar = offset == null ? Expressions.Constant_NullInt : offset;
            var lengthVar = length == null ? Expressions.Constant_NullInt : length;

            var assertNotDisposed = MakeAssertNotDisposedExpression(iTestableDisposable);

            var name = Expression.Constant(binder.Name);
            var castToRow = Expression.Convert(expression, Types.DynamicRow);
            var callOnSelf = Expression.Call(castToRow, Methods.DynamicRow.GetByName, name, iTestableDisposable, offsetVar, lengthVar);

            var block = Expression.Block(assertNotDisposed, callOnSelf);

            return new DynamicMetaObject(block, restrictions);
        }

        // todo: maybe move this somewhere else?
        internal static BindingRestrictions MakeRestrictions(TypeInfo rowType, Expression expression, DynamicRowConverter? expected, Expression getDynamicRow, Expression getColumns, TypeInfo returnType)
        {
            var expectedConst = Expression.Constant(expected);
            var get = GetRowConverter(getDynamicRow, getColumns, returnType);
            var eq = Expression.Equal(expectedConst, get);

            var sameConverterRestriction = BindingRestrictions.GetExpressionRestriction(eq);

            var expressionIsRowRestriction = BindingRestrictions.GetTypeRestriction(expression, rowType);

            var ret = expressionIsRowRestriction.Merge(sameConverterRestriction);

            return ret;
        }

        private static Expression GetRowConverter(Expression getDynamicRow, Expression getColumns, TypeInfo forType)
        {
            var typeConst = Expression.Constant(forType);
            var converter = Expression.Field(getDynamicRow, Fields.DynamicRow.Converter);
            var rowNumber = Expression.Field(getDynamicRow, Fields.DynamicRow.RowNumber);
            var context = Expression.Field(getDynamicRow, Fields.DynamicRow.Context);
            var owner = Expression.Field(getDynamicRow, Fields.DynamicRow.Owner);
            var options = Expression.Call(owner, Methods.IDynamicRowOwner.Options);

            var getCtx = Expression.Call(Methods.ReadContext.ConvertingRow, options, rowNumber, context);

            var dynamicRowConverter = Expression.Call(converter, Methods.ITypeDescriber.GetDynamicRowConverter, getCtx, getColumns, typeConst);

            return dynamicRowConverter;
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var retType = binder.ReturnType.GetTypeInfo();
            var isIDisposable = retType == Types.IDisposable;

            DynamicRowConverter? converter;
            BindingRestrictions restrictions;
            
            if (isIDisposable)
            {
                converter = null;
                restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRow);
            }
            else
            {
                var ctx = ReadContext.ConvertingRow(Row.Owner.Options, Row.RowNumber, Row.Context);
                converter = Row.Converter.GetDynamicRowConverter(in ctx, Row.Columns, retType);

                var selfAsRow = Expression.Convert(Expression, Types.DynamicRow);
                var columns = Expression.Field(selfAsRow, Fields.DynamicRow.Columns);

                restrictions = MakeRestrictions(Types.DynamicRow, Expression, converter, selfAsRow, columns, retType);
            }

            var selfAsDynamicRow = Expression.Convert(Expression, Types.DynamicRow);
            var selfAsITestableDispoable = Expression.Convert(Expression, Types.ITestableDisposable);
            var assertNotDisposed = MakeAssertNotDisposedExpression(selfAsITestableDispoable);

            return BindConvertFor(restrictions, selfAsDynamicRow, selfAsDynamicRow, assertNotDisposed, Row, retType, converter, null, null);
        }

        // todo: maybe move this to some common place?
        internal static DynamicMetaObject BindConvertFor(
            BindingRestrictions restrictions, 
            Expression callSiteExpression,   // this needs to be either DynamicRow or DynamicRowRange typed
            Expression dynamicRowExpression, // must be DynamicRow type
            MethodCallExpression assertNotDisposed,
            DynamicRow row, 
            TypeInfo retType,
            DynamicRowConverter? converter,
            Expression? offset, 
            Expression? length
        )
        {
            // special case, converting to IDisposable will ALWAYS succeed
            //   because every dynamic row supports disposal
            if (retType == Types.IDisposable)
            {
                var cast = Expression.Convert(callSiteExpression, Types.IDisposable);

                // intentionally NOT checking if the row is already disposed
                return new DynamicMetaObject(cast, restrictions);
            }

            if (converter == null)
            {
                var invalidOpCall = Methods.Throw.InvalidOperationException.MakeGenericMethod(retType);
                var throwMsg = Expression.Call(invalidOpCall, Expression.Constant($"No row converter discovered for {retType}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            if (!retType.IsAssignableFrom(converter.TargetType))
            {
                var invalidOpCall = Methods.Throw.InvalidOperationException.MakeGenericMethod(retType);
                var throwMsg = Expression.Call(invalidOpCall, Expression.Constant($"Row converter {converter} does not create a type assignable to {retType}, returns {converter.TargetType}"));

                return new DynamicMetaObject(throwMsg, restrictions);
            }

            var offsetVar = offset == null ? Expressions.Constant_NullInt : offset;
            var lengthVar = length == null ? Expressions.Constant_NullInt : length;

            var statements = new List<Expression>();
            statements.Add(assertNotDisposed);

            var dynRowVar = Expressions.Variable_DynamicRow;
            var assignDynRow = Expression.Assign(dynRowVar, dynamicRowExpression);
            statements.Add(assignDynRow);

            var outArg = Expression.Variable(retType);

            var callGetContext = Expression.Call(dynRowVar, Methods.DynamicRow.GetReadContext, offsetVar, lengthVar);
            var readCtxVar = Expressions.Variable_ReadContext;
            var assignReadCtx = Expression.Assign(readCtxVar, callGetContext);
            statements.Add(assignReadCtx);

            var expressionVar = Expression.Variable(callSiteExpression.Type.GetTypeInfo());
            var assignExpressionVar = Expression.Assign(expressionVar, callSiteExpression);
            statements.Add(assignExpressionVar);
            var convertExp = converter.MakeExpression(retType, expressionVar, readCtxVar, outArg);

            var errorMsg = Expression.Constant($"{nameof(DynamicRowConverter)} ({converter}) could not convert dynamic row to {retType}");
            var throwInvalidOp = Expression.Call(Methods.Throw.InvalidOperationExceptionOfObject, errorMsg);

            var ifFalseThrow = Expression.IfThen(Expression.Not(convertExp), throwInvalidOp);
            statements.Add(ifFalseThrow);
            statements.Add(outArg);

            var block = Expression.Block(new[] { outArg, dynRowVar, readCtxVar, expressionVar }, statements);

            return new DynamicMetaObject(block, restrictions);
        }

        // todo: move somewhere else?
        internal static MethodCallExpression MakeAssertNotDisposedExpression(Expression exp)
        {
            var call = Expression.Call(Methods.DisposableHelper.AssertNotDisposed, exp);

            return call;
        }
    }
}
