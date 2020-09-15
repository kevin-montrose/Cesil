using System.Collections;
using System.Reflection;
using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRowEnumerableNonGeneric : IEnumerable
    {
        private readonly DynamicRow Row;
        private readonly int? Offset;
        private readonly int? Length;

        internal DynamicRowEnumerableNonGeneric(object row)
        {
            if (row is DynamicRow dynRow)
            {
                Row = dynRow;
                Offset = Length = null;
            }
            else if (row is DynamicRowRange dynRowRange)
            {
                Row = dynRowRange.Parent;
                Offset = dynRowRange.Offset;
                Length = dynRowRange.Length;
            }
            else
            {
                Row = Throw.ImpossibleException<DynamicRow>($"Unexpected dynamic row type ({row.GetType().GetTypeInfo()})");
                return;
            }
        }

        public IEnumerator GetEnumerator()
        {
            AssertNotDisposed(Row);

            return new DynamicRowEnumeratorNonGeneric(Row, Offset, Length);
        }

        public override string ToString()
        => $"{nameof(DynamicRowEnumerableNonGeneric)} bound to {Row}";
    }
}
