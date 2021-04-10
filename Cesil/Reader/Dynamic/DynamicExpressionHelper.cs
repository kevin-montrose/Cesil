using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal static class DynamicExpressionHelper
    {
        internal static DynamicMetaObject BindGetIndexFor(
            BindingRestrictions restrictions,
            Expression expression,
            Expression testableDisposable,
            GetIndexBinder _,
            DynamicMetaObject[] indexes,
            Expression? offset,
            Expression? length
        )
        {
            if (indexes.Length != 1)
            {
                var msg = Expression.Constant($"Only single indexers are supported.");
                var invalidOpCall = Methods.Throw.InvalidOperationException;
                var call = Expression.Call(invalidOpCall, msg);

                var throwBlock = Expression.Block(call, Expression.Default(Types.Object));

                // we can cache this forever (for this type), since there's no scenario under which indexes != 1 becomes correct
                return new DynamicMetaObject(throwBlock, restrictions);
            }

            var assertNotDisposed = MakeAssertNotDisposedExpression(testableDisposable);

            var offsetVar = offset == null ? Expressions.Constant_NullInt : offset;
            var lengthVar = length == null ? Expressions.Constant_NullInt : length;

            var indexExp = indexes[0].Expression;
            var indexType = indexes[0].RuntimeType?.GetTypeInfo();

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
                var invalidOpCall = Methods.Throw.InvalidOperationException;
                var call = Expression.Call(invalidOpCall, msg);

                var throwBlock = Expression.Block(call, Expression.Default(Types.Object));

                // we can cache this forever (for this type), since there's no scenario under which incorrect index types become correct
                return new DynamicMetaObject(throwBlock, restrictions);
            }
        }

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

        internal static BindingRestrictions MakeRestrictions(TypeInfo rowType, Expression expression, DynamicRowConverter? expected, Expression getDynamicRow, Expression getColumns, TypeInfo returnType)
        {
            var expectedConst = Expression.Constant(expected);
            var get = GetRowConverter(getDynamicRow, getColumns, returnType);
            var eq = Expression.Equal(expectedConst, get);

            var sameConverterRestriction = BindingRestrictions.GetExpressionRestriction(eq);

            var expressionIsRowRestriction = BindingRestrictions.GetTypeRestriction(expression, rowType);

            var ret = expressionIsRowRestriction.Merge(sameConverterRestriction);

            return ret;

            // create an expression that will obtain the current DynamicRowConverter for the DynamicRow obtained from the given expression
            static Expression GetRowConverter(Expression getDynamicRow, Expression getColumns, TypeInfo forType)
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
        }

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
                var invalidOpCall = Methods.Throw.InvalidOperationException;
                var throwMsg = Expression.Call(invalidOpCall, Expression.Constant($"No row converter discovered for {retType}"));

                var throwBlock = Expression.Block(throwMsg, Expression.Default(retType));

                return new DynamicMetaObject(throwBlock, restrictions);
            }

            if (!retType.IsAssignableFrom(converter.TargetType))
            {
                var invalidOpCall = Methods.Throw.InvalidOperationException;
                var throwMsg = Expression.Call(invalidOpCall, Expression.Constant($"Row converter {converter} does not create a type assignable to {retType}, returns {converter.TargetType}"));

                var throwBlock = Expression.Block(throwMsg, Expression.Default(retType));

                return new DynamicMetaObject(throwBlock, restrictions);
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
            var throwInvalidOp = Expression.Call(Methods.Throw.InvalidOperationException, errorMsg);

            var ifFalseThrow = Expression.IfThen(Expression.Not(convertExp), throwInvalidOp);
            statements.Add(ifFalseThrow);
            statements.Add(outArg);

            var block = Expression.Block(new[] { outArg, dynRowVar, readCtxVar, expressionVar }, statements);

            return new DynamicMetaObject(block, restrictions);
        }

        internal static MethodCallExpression MakeAssertNotDisposedExpression(Expression exp)
        {
            var call = Expression.Call(Methods.DisposableHelper.AssertNotDisposed, exp);

            return call;
        }
    }
}
