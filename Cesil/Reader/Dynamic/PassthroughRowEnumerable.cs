using System.Collections;
using System.Collections.Generic;

namespace Cesil
{
    internal sealed class PassthroughRowEnumerable : IEnumerable<object?>
    {
        private readonly uint Generation;
        private readonly DynamicRow Row;

        internal PassthroughRowEnumerable(object row)
        {
            Row = (DynamicRow)row;
            Generation = Row.Generation;
        }

        public IEnumerator<object?> GetEnumerator()
        {
            Row.AssertGenerationMatch(Generation);

            return new PassthroughRowEnumerator(Row);
        }

        [ExcludeFromCoverage("Trivial, and covered by IEnumerable<T>.GetEnumerator()")]
        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public override string ToString()
        => $"{nameof(PassthroughRowEnumerable)} bound to {Row}";
    }
}
