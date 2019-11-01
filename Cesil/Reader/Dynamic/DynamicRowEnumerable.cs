using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    // todo: why is this disposable?  and why isn't it checking row generation?
    internal sealed class DynamicRowEnumerable<T> : IEnumerable<T>, ITestableDisposable
    {
        private bool EnumerableDisposed;
        private DynamicRow Row;

        public bool IsDisposed
        {
            get
            {
                if (EnumerableDisposed) return true;

                return Row.IsDisposed;
            }
        }

        internal DynamicRowEnumerable(object row)
        {
            Row = (DynamicRow)row;
        }

        public void Dispose()
        {
            EnumerableDisposed = true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            AssertNotDisposed(this);

            return new DynamicRowEnumerator<T>(Row);
        }

        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public override string ToString()
        => $"{nameof(DynamicRowEnumerable<T>)} bound to {Row}";
    }
}
