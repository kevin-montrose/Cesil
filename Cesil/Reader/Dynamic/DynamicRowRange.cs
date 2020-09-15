using System.Collections.Immutable;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    // todo: move this
    internal sealed class DynamicRowRangeMetaObject : DynamicMetaObject
    {
        private readonly DynamicRowRange Range;

        internal DynamicRowRangeMetaObject(DynamicRowRange range, Expression exp) : base(exp, BindingRestrictions.Empty, range)
        {
            Range = range;
        }

        private Expression AsDynamicRowRange()
        => Expression.Convert(Expression, Types.DynamicRowRange);

        private Expression GetOffset()
        => Expression.Field(AsDynamicRowRange(), Fields.DynamicRowRange.Offset);

        private Expression GetLength()
        => Expression.Field(AsDynamicRowRange(), Fields.DynamicRowRange.Length);

        private Expression GetParent()
        => Expression.Field(AsDynamicRowRange(), Fields.DynamicRowRange.Parent);

        // todo: BindInvokeMember

        public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowRange);

            var dynamicRow = GetParent();
            var offset = GetOffset();
            var length = GetLength();

            return DynamicRowMetaObject.BindGetIndexFor(restrictions, dynamicRow, binder, indexes, offset, length);
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            var restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowRange);

            var dynamicRow = GetParent();
            var offset = GetOffset();
            var length = GetLength();

            return DynamicRowMetaObject.BindGetMemberFor(restrictions, dynamicRow, binder, offset, length);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var dynamicRow = GetParent();
            var offset = GetOffset();
            var length = GetLength();

            var row = Range.Parent;

            var retType = binder.ReturnType.GetTypeInfo();
            var isIDisposable = retType == Types.IDisposable;

            var selfAsDynamicRowRange = Expression.Convert(Expression, Types.DynamicRowRange);

            DynamicRowConverter? converter;
            BindingRestrictions restrictions;

            if (isIDisposable)
            {
                converter = null;

                restrictions = BindingRestrictions.GetTypeRestriction(Expression, Types.DynamicRowRange);
            }
            else
            {
                var cols = Range.GetColumns();

                var ctx = row.GetReadContext(Range.Offset, Range.Length);
                converter = row.Converter.GetDynamicRowConverter(in ctx, cols, retType);

                var callGetColumn = Expression.Call(selfAsDynamicRowRange, Methods.DynamicRowRange.GetColumns);
                var ienumerableOfColumnIdentifiers = Types.IEnumerableOfT.MakeGenericType(Types.ColumnIdentifier).GetTypeInfo();
                var asIEnumerable = Expression.Convert(callGetColumn, ienumerableOfColumnIdentifiers);

                restrictions = DynamicRowMetaObject.MakeRestrictions(Types.DynamicRowRange, Expression, converter, dynamicRow, asIEnumerable, retType);
            }

            return DynamicRowMetaObject.BindConvertFor(restrictions, selfAsDynamicRowRange, dynamicRow, Range.Parent, retType, converter, offset, length);
        }
    }

    internal sealed class DynamicRowRange : IDynamicMetaObjectProvider, ITestableDisposable
    {
        internal readonly DynamicRow Parent;

        // keeping these nullable makes generating expressions easier
        internal readonly int? Offset;
        internal readonly int? Length;

        public bool IsDisposed { get; private set; }

        internal DynamicRowRange(DynamicRow parent, int offset, int length)
        {
            Parent = parent;
            Offset = offset;
            Length = length;
        }

        internal ImmutableArray<ColumnIdentifier> GetColumns()
        {
            // todo: remove allocations
            var row = Parent;

            var colsBuilder = ImmutableArray.CreateBuilder<ColumnIdentifier>();
            var ix = 0;
            foreach (var col in row.Columns)
            {
                if (ix < (Offset ?? 0))
                {
                    goto end;
                }

                if (colsBuilder.Count >= (Length ?? 0))
                {
                    break;
                }

                var n = col.HasName ? col.Name : "";

                var newCol = ColumnIdentifier.CreateInner(colsBuilder.Count, n, null);

                colsBuilder.Add(newCol);

end:
                ix++;
            }

            return colsBuilder.ToImmutable();
        }

        public DynamicMetaObject GetMetaObject(Expression parameter)
        => new DynamicRowRangeMetaObject(this, parameter);

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                // todo: something?
            }
        }

        public override string ToString()
        => $"{nameof(DynamicRowRange)} with Offset={Offset}, Length={Length}, Parent={Parent}";
    }
}
