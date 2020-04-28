using System.Collections;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRowEnumerableNonGeneric : IEnumerable
    {
        private readonly DynamicRow Row;

        internal DynamicRowEnumerableNonGeneric(object row)
        {
            Row = (DynamicRow)row;
        }

        public IEnumerator GetEnumerator()
        {
            AssertNotDisposed(Row);

            return new DynamicRowEnumeratorNonGeneric(Row);
        }

        public override string ToString()
        => $"{nameof(DynamicRowEnumerableNonGeneric)} bound to {Row}";
    }
}
