using System.Collections;

namespace Cesil
{
    internal sealed class DynamicRowEnumeratorNonGeneric : IEnumerator
    {
        private readonly DynamicRow.DynamicColumnEnumerator Enumerator;
        private readonly ITestableDisposable DependsOn;
        private readonly int? Offset;
        private readonly int? Length;

        public object? Current { get; private set; }

        internal DynamicRowEnumeratorNonGeneric(DynamicRow row, ITestableDisposable dependsOn, int? offset, int? length)
        {
            Enumerator = new DynamicRow.DynamicColumnEnumerator(row, offset, length);
            DependsOn = dependsOn;
            Offset = offset;
            Length = length;
        }

        public bool MoveNext()
        {
            if (!Enumerator.MoveNext())
            {
                Current = null;
                return false;
            }

            var col = Enumerator.Current;

            var trueIx = col.Index + (Offset ?? 0);

            Current = Enumerator.Row.GetCellAt(DependsOn, trueIx);

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
