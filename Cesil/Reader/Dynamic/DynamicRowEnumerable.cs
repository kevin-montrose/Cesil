using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    internal sealed class DynamicRowEnumerable<T> : IEnumerable<T>
    {
        private readonly uint Generation;
        private readonly DynamicRow Row;
        private readonly int? Offset;
        private readonly int? Length;

        internal DynamicRowEnumerable(object row)
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
            }

            Generation = Row.Generation;
        }

        public IEnumerator<T> GetEnumerator()
        {
            Row.AssertGenerationMatch(Generation);

            return new DynamicRowEnumerator<T>(Row, Offset, Length);
        }

        [ExcludeFromCoverage("Trivial, and covered by IEnumerable<T>.GetEnumerator()")]
        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public override string ToString()
        => $"{nameof(DynamicRowEnumerable<T>)} bound to {Row}";
    }
}
