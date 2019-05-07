using System.Collections;
using System.Collections.Generic;

namespace Cesil
{
    internal sealed class DynamicRowEnumerable<T> : IEnumerable<T>, ITestableDisposable
    {
        private DynamicRow Row;

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

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(DynamicRowEnumerable<T>));
            }
        }

        public void Dispose()
        {
            Row = null;
        }

        public IEnumerator<T> GetEnumerator()
        {
            AssertNotDisposed();

            return new DynamicRowEnumerator<T>(Row);
        }

        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
    }
}
