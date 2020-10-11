using System.Collections;
using System.Reflection;
using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRowEnumerableNonGeneric : IEnumerable
    {
        private readonly DynamicRow Row;
        private readonly ITestableDisposable DependsOn;
        private readonly int? Offset;
        private readonly int? Length;

        internal DynamicRowEnumerableNonGeneric(object row)
        {
            if (row is DynamicRow dynRow)
            {
                Row = dynRow;
                DependsOn = dynRow;
                Offset = Length = null;
            }
            else if (row is DynamicRowRange dynRowRange)
            {
                Row = dynRowRange.Parent;
                DependsOn = dynRowRange;
                Offset = dynRowRange.Offset;
                Length = dynRowRange.Length;
            }
            else
            {
                DependsOn = Row = Throw.ImpossibleException<DynamicRow>($"Unexpected dynamic row type ({row.GetType().GetTypeInfo()})");
                return;
            }
        }

        public IEnumerator GetEnumerator()
        {
            AssertNotDisposed(Row);

            return new DynamicRowEnumeratorNonGeneric(Row, DependsOn, Offset, Length);
        }

        public override string ToString()
        => $"{nameof(DynamicRowEnumerableNonGeneric)} bound to {Row}";
    }
}
