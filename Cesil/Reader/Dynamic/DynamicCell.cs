using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal sealed class DynamicCell : IDynamicMetaObjectProvider
    {
        internal readonly uint Generation;
        internal readonly DynamicRow Row;
        internal readonly int ColumnNumber;

        internal ITypeDescriber Converter => Row.Converter.Value;

        public DynamicCell(DynamicRow row, int num)
        {
            Row = row;
            Generation = row.Generation;
            ColumnNumber = num;
        }

        internal object? CoerceTo(TypeInfo toType)
        {
            var mtd = Methods.DynamicCell.CastTo.MakeGenericMethod(toType);

            return mtd.Invoke(this, Array.Empty<object>());
        }

        internal T CastTo<T>()
        => (T)(dynamic)this;

        internal ReadOnlySpan<char> GetDataSpan()
        => SafeRowGet().GetDataSpan(ColumnNumber);

        internal ReadContext GetReadContext()
        {
            var r = SafeRowGet();

            var name = r.Columns.Value[ColumnNumber];

            var owner = r.Owner.Value;

            return ReadContext.ReadingColumn(owner.Options, r.RowNumber, name, owner.Context);
        }

        public DynamicMetaObject GetMetaObject(Expression exp)
        => new DynamicCellMetaObject(this, exp);

        private DynamicRow SafeRowGet()
        {
            var ret = Row;
            ret.AssertGenerationMatch(Generation);

            return ret;
        }

        public override string ToString()
        => $"{nameof(DynamicCell)} {nameof(Generation)}={Generation}, {nameof(Row)}={Row}, {nameof(ColumnNumber)}={ColumnNumber}";
    }
}
