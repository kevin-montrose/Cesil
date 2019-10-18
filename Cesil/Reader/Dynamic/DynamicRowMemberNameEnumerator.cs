using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRowMemberNameEnumerator : IEnumerator<string>, ITestableDisposable
    {
        private DynamicRow Inner;
        private readonly uint Generation;

        public bool IsDisposed => Inner == null;

        private int Index;

        public string Current
        {
            get
            {
                AssertNotDisposed(this);
                Inner.AssertGenerationMatch(Generation);
                return Inner.Columns[Index].Name;
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

            if (Index >= Inner.Columns.Count)
            {
                return false;
            }

            Index++;

            while (Index < Inner.Columns.Count)
            {
                if (Inner.Columns[Index].HasName)
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
                Inner = null;
            }
        }

        public override string ToString()
        => $"{nameof(DynamicRowMemberNameEnumerator)} bound to {Inner}";
    }
}
