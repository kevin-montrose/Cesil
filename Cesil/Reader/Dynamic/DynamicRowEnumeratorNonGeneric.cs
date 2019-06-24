using System.Collections;

namespace Cesil
{
    internal sealed class DynamicRowEnumeratorNonGeneric : IEnumerator
    {
        private readonly DynamicRow.DynamicColumnEnumerator Enumerator;

        public object Current { get; private set; }

        internal DynamicRowEnumeratorNonGeneric(DynamicRow row)
        {
            Enumerator = new DynamicRow.DynamicColumnEnumerator(row);
        }

        public bool MoveNext()
        {
            if (!Enumerator.MoveNext())
            {
                Current = null;
                return false;
            }

            var col = Enumerator.Current;

            Current = Enumerator.Row.GetCellAt(col.Index);

            return true;
        }

        public void Reset()
        {
            Enumerator.Reset();
            Current = null;
        }

        public override string ToString()
        => $"{nameof(DynamicRowEnumeratorNonGeneric)} bound to {Enumerator}";
    }
}
