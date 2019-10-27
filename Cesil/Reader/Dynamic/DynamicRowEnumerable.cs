using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRowEnumerable<T> : IEnumerable<T>, ITestableDisposable
    {
        private DynamicRow? Row;

        public bool IsDisposed
        {
            get
            {
                var r = Row;
                if (r == null) return true;

                return r.IsDisposed;
            }
        }

        internal DynamicRowEnumerable(object row)
        {
            Row = (DynamicRow)row;
        }

        public void Dispose()
        {
            Row = null;
        }

        public IEnumerator<T> GetEnumerator()
        {
            AssertNotDisposed(this);

            var r = Utils.NonNull(Row);
            return new DynamicRowEnumerator<T>(r);
        }

        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public override string ToString()
        => $"{nameof(DynamicRowEnumerable<T>)} bound to {Row}";
    }
}
