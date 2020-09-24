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

            return DynamicExpressionHelper.BindGetIndexFor(restrictions, Expression, selfAsITestableDisposable, binder, indexes, null, null);
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRow);
            var selfAsITestable = Expression.Convert(Expression, Types.ITestableDisposable);

            return DynamicExpressionHelper.BindGetMemberFor(restrictions, Expression, selfAsITestable, binder, null, null);
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

                restrictions = DynamicExpressionHelper.MakeRestrictions(Types.DynamicRow, Expression, converter, selfAsRow, columns, retType);
            }

            var selfAsDynamicRow = Expression.Convert(Expression, Types.DynamicRow);
            var selfAsITestableDispoable = Expression.Convert(Expression, Types.ITestableDisposable);
            var assertNotDisposed = DynamicExpressionHelper.MakeAssertNotDisposedExpression(selfAsITestableDispoable);

            return DynamicExpressionHelper.BindConvertFor(restrictions, selfAsDynamicRow, selfAsDynamicRow, assertNotDisposed, Row, retType, converter, null, null);
        }
    }
}
