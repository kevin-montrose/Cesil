using System.Collections;
using System.Collections.Generic;

namespace Cesil
{
    internal sealed class DynamicRowEnumerator<T> : IEnumerator<T>, ITestableDisposable
    {
        private DynamicRow Row;
        private int NextIndex;

        public bool IsDisposed
        {
            get
            {
                var r = Row;
                if (r == null) return true;

                return r.IsDisposed;
            }
        }

        private T _Current;
        public T Current
        {
            get
            {
                AssertNotDisposed();
                return _Current;
            }
        }

        object IEnumerator.Current => Current;

        internal DynamicRowEnumerator(DynamicRow row)
        {
            Row = row;
            Reset();
        }

        public bool MoveNext()
        {
            AssertNotDisposed();

            if (NextIndex == Row.Width)
            {
                _Current = default;
                return false;
            }

            dynamic val = Row.GetCellAt(NextIndex);
            _Current = val;

            NextIndex++;

            return true;
        }

        public void Reset()
        {
            AssertNotDisposed();

            _Current = default;
            NextIndex = 0;
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(DynamicRowEnumerator<T>));
            }
        }

        public void Dispose()
        {
            Row = null;
        }
    }
}
