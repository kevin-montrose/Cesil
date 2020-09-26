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
        private readonly ITestableDisposable DependsOn;
        private readonly int? Offset;
        private readonly int? Length;

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

        [ExcludeFromCoverage("Trivial, and covered by IEnumerator<T>.Current")]
        object? IEnumerator.Current => Current;

        internal DynamicRowEnumerator(DynamicRow row, ITestableDisposable dependsOn, int? offset, int? length)
        {
#pragma warning disable CES0005 // T is generic, and we'll overwrite it before it's used, so default! is needed
            _Current = default!;
#pragma warning restore CES0005
            Enumerator = new DynamicRow.DynamicColumnEnumerator(row, offset, length);
            DependsOn = dependsOn;
            Offset = offset;
            Length = length;
        }

        public bool MoveNext()
        {
            AssertNotDisposed(this);

            if (!Enumerator.MoveNext())
            {
                return false;
            }

            var col = Enumerator.Current;

            var trueIx = col.Index + (Offset ?? 0);

            var val = Enumerator.Row.GetCellAt(DependsOn, trueIx);
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
