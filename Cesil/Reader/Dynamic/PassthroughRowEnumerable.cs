using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    internal sealed class PassthroughRowEnumerable : IEnumerable<object?>
    {
        private readonly uint Generation;
        private readonly DynamicRow Row;
        private readonly ITestableDisposable DependsOn;
        private readonly int? Offset;
        private readonly int? Length;

        internal PassthroughRowEnumerable(object row)
        {
            if (row is DynamicRow dynRow)
            {
                Row = dynRow;
                DependsOn = dynRow;
                Offset = Length = null;
            }
            else
            {
                var dynRowRange = Utils.NonNull(row as DynamicRowRange);

                Row = dynRowRange.Parent;
                DependsOn = dynRowRange;
                Offset = dynRowRange.Offset;
                Length = dynRowRange.Length;
            }

            Generation = Row.Generation;
        }

        public IEnumerator<object?> GetEnumerator()
        {
            Row.AssertGenerationMatch(Generation);

            return new PassthroughRowEnumerator(Row, DependsOn, Offset, Length);
        }

        [ExcludeFromCoverage("Trivial, and covered by IEnumerable<T>.GetEnumerator()")]
        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public override string ToString()
        => $"{nameof(PassthroughRowEnumerable)} bound to {Row}";
    }
}
