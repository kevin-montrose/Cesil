using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;

namespace Cesil
{
    internal sealed class DynamicRowRange : IDynamicMetaObjectProvider, ITestableDisposable
    {
        internal readonly DynamicRow Parent;
        internal readonly IReadOnlyList<ColumnIdentifier> Columns;

        // keeping these nullable makes generating expressions easier
        internal readonly int? Offset;
        internal readonly int? Length;

        public bool IsDisposed { get; private set; }

        internal DynamicRowRange(DynamicRow parent, int offset, int length)
        {
            Parent = parent;
            Offset = offset;
            Length = length;

            Columns = new DynamicRow.DynamicColumnEnumerable(parent, offset, length);
        }

        public DynamicMetaObject GetMetaObject(Expression parameter)
        => new DynamicRowRangeMetaObject(this, parameter);

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Parent.TryDataDispose();
            }
        }

        public override string ToString()
        => $"{nameof(DynamicRowRange)} {nameof(Offset)}={Offset}, {nameof(Length)}={Length}, {nameof(Parent)}={Parent}";
    }
}
