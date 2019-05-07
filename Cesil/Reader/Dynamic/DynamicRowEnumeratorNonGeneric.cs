using System.Collections;

namespace Cesil
{
    internal sealed class DynamicRowEnumeratorNonGeneric : IEnumerator
    {
        private DynamicRow Row;
        private int NextIndex;

        public object Current { get; private set; }

        internal DynamicRowEnumeratorNonGeneric(DynamicRow row)
        {
            Row = row;
            Reset();
        }

        public bool MoveNext()
        {
            Row.AssertNotDisposed();

            if(NextIndex >= Row.Width)
            {
                Current = null;
                return false;
            }

            Current = Row.GetCellAt(NextIndex);

            NextIndex++;

            return true;
        }

        public void Reset()
        {
            Row.AssertNotDisposed();

            NextIndex = 0;
            Current = null;
        }
    }
}
