using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    internal sealed class PassthroughRowEnumerable : IEnumerable<object?>
    {
        private readonly uint Generation;
        private readonly DynamicRow Row;
        private readonly int? Offset;
        private readonly int? Length;

        internal PassthroughRowEnumerable(object row)
        {
            if(row is DynamicRow dynRow)
            {
                Row = dynRow;
                Offset = Length = null;
            }
            else if(row is DynamicRowRange dynRowRange)
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

            Generation = Row.Generation;
        }

        public IEnumerator<object?> GetEnumerator()
        {
            Row.AssertGenerationMatch(Generation);

            return new PassthroughRowEnumerator(Row, Offset, Length);
        }

        [ExcludeFromCoverage("Trivial, and covered by IEnumerable<T>.GetEnumerator()")]
        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public override string ToString()
        => $"{nameof(PassthroughRowEnumerable)} bound to {Row}";
    }
}
