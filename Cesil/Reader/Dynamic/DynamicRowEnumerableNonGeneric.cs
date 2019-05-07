using System.Collections;

namespace Cesil
{
    internal sealed class DynamicRowEnumerableNonGeneric : IEnumerable
    {
        private DynamicRow Row;

        internal DynamicRowEnumerableNonGeneric(object row)
        {
            Row = (DynamicRow)row;
        }

        public IEnumerator GetEnumerator()
        {
            Row.AssertNotDisposed();

            return new DynamicRowEnumeratorNonGeneric(Row);
        }
    }
}
