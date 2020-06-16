using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRowEnumerator<T> : IEnumerator<T>, ITestableDisposable
    {
        // this checks that reusing the underlying DynamicRow will
        //   cause a generation check failure
        private readonly DynamicRow.DynamicColumnEnumerator Enumerator;

        public bool IsDisposed => Enumerator.IsDisposed;

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
#pragma warning disable CES0005 // T is generic, and we'll overwrite it before it's used, so default! is needed
            _Current = default!;
#pragma warning restore CES0005
            Enumerator = new DynamicRow.DynamicColumnEnumerator(row);
        }

        public bool MoveNext()
        {
            AssertNotDisposed(this);

            if (!Enumerator.MoveNext())
            {
                return false;
            }

            var col = Enumerator.Current;

            var val = Enumerator.Row.GetCellAt(col.Index);
            if (val == null)
            {
#pragma warning disable CES0005 // empty value needs to be mapped to whatever default is for T, which may well be null, but we can't annotate T because it could be anything
                _Current = default!;
#pragma warning restore CES0005
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
        => $"{nameof(DynamicRowEnumerator<T>)} bound to {Enumerator}";
    }
}
