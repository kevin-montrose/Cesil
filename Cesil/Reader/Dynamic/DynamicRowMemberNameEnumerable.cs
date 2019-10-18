using System.Collections;
using System.Collections.Generic;

namespace Cesil
{
    internal sealed class DynamicRowMemberNameEnumerable : IEnumerable<string>
    {
        private readonly DynamicRow Inner;
        private readonly uint Generation;

        internal DynamicRowMemberNameEnumerable(DynamicRow row)
        {
            Inner = row;
            Generation = Inner.Generation;
        }

        public IEnumerator<string> GetEnumerator()
        {
            Inner.AssertGenerationMatch(Generation);
            return new DynamicRowMemberNameEnumerator(Inner);
        }

        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public override string ToString()
        => $"{nameof(DynamicRowMemberNameEnumerable)} bound to {Inner}";
    }
}
