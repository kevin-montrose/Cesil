﻿using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    internal sealed class DynamicCell: IDynamicMetaObjectProvider
    {
        internal readonly uint Generation;
        internal readonly DynamicRow Row;
        internal readonly int ColumnNumber;

        internal IDynamicTypeConverter Converter => Row.Converter;

        public DynamicCell(DynamicRow row, int num)
        {
            Row = row;
            Generation = row.Generation;
            ColumnNumber = num;
        }

        internal object CoerceTo(TypeInfo toType)
        {
            var mtd =  Methods.DynamicCell.CastTo.MakeGenericMethod(toType);

            return mtd.Invoke(this, Array.Empty<object>());
        }

        internal T CastTo<T>()
        => (T)(dynamic)this;

        internal ReadOnlySpan<char> GetDataSpan()
        => SafeRowGet().GetDataSpan(ColumnNumber);

        internal ReadContext GetReadContext()
        {
            var r = SafeRowGet();

            return new ReadContext(r.RowNumber, ColumnNumber, r.Names?[ColumnNumber], r.Owner.Context);
        }

        public DynamicMetaObject GetMetaObject(Expression exp)
        => new DynamicCellMetaObject(this, exp);

        private DynamicRow SafeRowGet()
        {
            var ret = Row;
            ret.AssertGenerationMatch(Generation);

            return ret;
        }
    }
}
