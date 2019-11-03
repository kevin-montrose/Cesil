using System.Collections;
using System.Collections.Generic;

namespace Cesil
{
    internal sealed class DynamicRowEnumerable<T> : IEnumerable<T>
    {
        private readonly uint Generation;
        private readonly DynamicRow Row;

        internal DynamicRowEnumerable(object row)
        {
            Row = (DynamicRow)row;
            Generation = Row.Generation;
        }

        public IEnumerator<T> GetEnumerator()
        {
            Row.AssertGenerationMatch(Generation);

            return new DynamicRowEnumerator<T>(Row);
        }

        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public override string ToString()
        => $"{nameof(DynamicRowEnumerable<T>)} bound to {Row}";
    }
}
