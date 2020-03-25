using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class PassthroughRowEnumerator : IEnumerator<object>, ITestableDisposable
    {
        // this checks that reusing the underlying DynamicRow will
        //   cause a generation check failure
        private readonly DynamicRow.DynamicColumnEnumerator Enumerator;

        public bool IsDisposed => Enumerator.IsDisposed;

        private object _Current;
        public object Current
        {
            get
            {
                AssertNotDisposed(this);
                return _Current;
            }
        }

        object? IEnumerator.Current => Current;

        internal PassthroughRowEnumerator(DynamicRow row)
        {
            _Current = default!;
            Enumerator = new DynamicRow.DynamicColumnEnumerator(row);
        }

        public bool MoveNext()
        {
            AssertNotDisposed(this);

            if (!Enumerator.MoveNext())
            {
                _Current = default!;
                return false;
            }

            var col = Enumerator.Current;

            var val = Enumerator.Row.GetCellAt(col.Index);
            if (val == null)
            {
                _Current = default!;
            }
            else
            {
                _Current = val;
            }

            return true;
        }

        public void Reset()
        {
            AssertNotDisposed(this);

            _Current = default!;
            Enumerator.Reset();
        }


        public void Dispose()
        {
            if (!IsDisposed)
            {
                Enumerator.Dispose();
            }
        }

        public override string ToString()
        => $"{nameof(PassthroughRowEnumerator)} bound to {Enumerator}";
    }
}
