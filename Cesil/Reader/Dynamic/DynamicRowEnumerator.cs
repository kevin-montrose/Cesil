using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRowEnumerator<T> : IEnumerator<T>, ITestableDisposable
    {
        private DynamicRow.DynamicColumnEnumerator? Enumerator;

        public bool IsDisposed
        {
            get
            {
                var e = Enumerator;
                if (e == null) return true;

                return e.IsDisposed;
            }
        }

        private T _Current;
        public T Current
        {
            get
            {
                AssertNotDisposed(this);
                return _Current;
            }
        }

        object? IEnumerator.Current => Current;

        internal DynamicRowEnumerator(DynamicRow row)
        {
            _Current = default!;
            Enumerator = new DynamicRow.DynamicColumnEnumerator(row);
        }

        public bool MoveNext()
        {
            AssertNotDisposed(this);

            var e = Utils.NonNull(Enumerator);

            if (!e.MoveNext())
            {
                _Current = default!;
                return false;
            }

            var col = e.Current;

            var val = e.Row.GetCellAt(col.Index);
            if(val == null)
            {
                if(typeof(T).IsValueType)
                {
                    return Throw.InvalidOperationException<bool>($"Attempted to coerce missing value to {typeof(T)}, a value type");
                }
                _Current = default!;
            }
            else
            {
                _Current = (dynamic)val;
            }

            return true;
        }

        public void Reset()
        {
            AssertNotDisposed(this);

            _Current = default!;
            Utils.NonNull(Enumerator).Reset();
        }


        public void Dispose()
        {
            Enumerator?.Dispose();
            Enumerator = null;
        }

        public override string ToString()
        => $"{nameof(DynamicRowEnumerator<T>)} bound to {Enumerator}";
    }
}
