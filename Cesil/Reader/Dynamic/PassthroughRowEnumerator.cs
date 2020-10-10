using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class PassthroughRowEnumerator : IEnumerator<object?>, ITestableDisposable
    {
        // this checks that reusing the underlying DynamicRow will
        //   cause a generation check failure
        private readonly DynamicRow.DynamicColumnEnumerator Enumerator;
        private ITestableDisposable DependsOn;
        private readonly int? Offset;
        private readonly int? Length;

        public bool IsDisposed => Enumerator.IsDisposed;

        private object? _Current;
        public object? Current
        {
            get
            {
                AssertNotDisposed(this);
                return _Current;
            }
        }

        [ExcludeFromCoverage("Trivial, and covered by IEnumerator<T>.Current")]
        object? IEnumerator.Current => Current;

        internal PassthroughRowEnumerator(DynamicRow row, ITestableDisposable dependsOn, int? offset, int? length)
        {
            _Current = null;
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
                _Current = null;
                return false;
            }

            var col = Enumerator.Current;

            var trueIndex = col.Index + (Offset ?? 0);

            var val = Enumerator.Row.GetCellAt(DependsOn, trueIndex);
            if (val == null)
            {
                _Current = null;
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

            _Current = null;
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
