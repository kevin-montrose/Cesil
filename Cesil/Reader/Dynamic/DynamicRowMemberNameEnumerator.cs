using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRowMemberNameEnumerator : IEnumerator<string>, ITestableDisposable
    {
        private readonly DynamicRow Inner;
        private readonly uint Generation;

        public bool IsDisposed { get; private set; }

        private int Index;

        public string Current
        {
            get
            {
                AssertNotDisposed(this);
                Inner.AssertGenerationMatch(Generation);
                return Inner.Columns.Value[Index].Name;
            }
        }

        object IEnumerator.Current => Current;

        internal DynamicRowMemberNameEnumerator(DynamicRow row)
        {
            Inner = row;
            Generation = Inner.Generation;
            Index = -1;
        }

        public bool MoveNext()
        {
            AssertNotDisposed(this);
            Inner.AssertGenerationMatch(Generation);

            var columnsValue = Inner.Columns.Value;

            if (Index >= columnsValue.Count)
            {
                return false;
            }

            Index++;

            while (Index < columnsValue.Count)
            {
                if (columnsValue[Index].HasName)
                {
                    return true;
                }

                Index++;
            }

            return false;
        }

        public void Reset()
        {
            AssertNotDisposed(this);
            Inner.AssertGenerationMatch(Generation);

            Index = -1;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
            }
        }

        public override string ToString()
        => $"{nameof(DynamicRowMemberNameEnumerator)} bound to {Inner}";
    }
}
