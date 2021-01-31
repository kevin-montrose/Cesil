using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal sealed class DynamicRowRangeMetaObject : DynamicMetaObject
    {
        private readonly DynamicRowRange Range;

        internal DynamicRowRangeMetaObject(DynamicRowRange range, Expression exp) : base(exp, BindingRestrictions.Empty, range)
        {
            Range = range;
        }

        private void GetCommonExpressions(out Expression asDynamicRowRange, out Expression offset, out Expression length, out Expression parent)
        {
            asDynamicRowRange = Expression.Convert(Expression, Types.DynamicRowRange);
            offset = Expression.Field(asDynamicRowRange, Fields.DynamicRowRange.Offset);
            length = Expression.Field(asDynamicRowRange, Fields.DynamicRowRange.Length);
            parent = Expression.Field(asDynamicRowRange, Fields.DynamicRowRange.Parent);
        }

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
            var expressionIsDynamicRowRangeRestriction = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowRange);

            // only supported operation is .Dispose()
            if (binder.Name == nameof(DynamicRowRange.Dispose) && args.Length == 0)
            {
                var castToRow = Expression.Convert(Expression, Types.DynamicRowRange);
                var callDispose = Expression.Call(castToRow, Methods.DynamicRowRange.Dispose);

                var final = Expression.Block(callDispose, Expression.Default(binder.ReturnType));

                // we can cache this forever (for this type), doesn't vary by anything else
                return new DynamicMetaObject(final, expressionIsDynamicRowRangeRestriction);
            }

            var msg = Expression.Constant($"Only the Dispose() method is supported.");
            var invalidOpCall = Methods.Throw.InvalidOperationException;
            var call = Expression.Call(invalidOpCall, msg);

            var errorExp = Expression.Block(call, Expression.Default(binder.ReturnType));
            
            // we can cache this forever (for this type), since there's no scenario under which a non-Dispose call
            //    becomes legal
            return new DynamicMetaObject(errorExp, expressionIsDynamicRowRangeRestriction);
        }

        public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowRange);

            GetCommonExpressions(out var selfAsDynamicRowRange, out var offset, out var length, out var dynamicRow);

            var selfAsITestableDisposable = Expression.Convert(selfAsDynamicRowRange, Types.ITestableDisposable);

            return DynamicExpressionHelper.BindGetIndexFor(restrictions, dynamicRow, selfAsITestableDisposable, binder, indexes, offset, length);
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowRange);

            GetCommonExpressions(out var selfAsDynamicRowRange, out var offset, out var length, out var dynamicRow);

            var selfAsITestableDisposable = Expression.Convert(selfAsDynamicRowRange, Types.ITestableDisposable);

            return DynamicExpressionHelper.BindGetMemberFor(restrictions, dynamicRow, selfAsITestableDisposable, binder, offset, length);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {

            GetCommonExpressions(out var selfAsDynamicRowRange, out var offset, out var length, out var dynamicRow);

            var row = Range.Parent;

            var retType = binder.ReturnType.GetTypeInfo();
            var isIDisposable = retType == Types.IDisposable;

            DynamicRowConverter? converter;
            BindingRestrictions restrictions;

            if (isIDisposable)
            {
                converter = null;

                restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowRange);
            }
            else
            {
                var cols = Range.Columns;

                var ctx = row.GetReadContext(Range.Offset, Range.Length);
                converter = row.Converter.GetDynamicRowConverter(in ctx, cols, retType);

                var callGetColumn = Expression.Field(selfAsDynamicRowRange, Fields.DynamicRowRange.Columns);
                var ienumerableOfColumnIdentifiers = Types.IEnumerableOfT.MakeGenericType(Types.ColumnIdentifier).GetTypeInfo();
                var asIEnumerable = Expression.Convert(callGetColumn, ienumerableOfColumnIdentifiers);

                restrictions = DynamicExpressionHelper.MakeRestrictions(Types.DynamicRowRange, Expression, converter, dynamicRow, asIEnumerable, retType);
            }

            var selfAsITestableDisposable = Expression.Convert(Expression, Types.ITestableDisposable);
            var assertNotDisposed = DynamicExpressionHelper.MakeAssertNotDisposedExpression(selfAsITestableDisposable);

            return DynamicExpressionHelper.BindConvertFor(restrictions, selfAsDynamicRowRange, dynamicRow, assertNotDisposed, Range.Parent, retType, converter, offset, length);
        }
    }
}
