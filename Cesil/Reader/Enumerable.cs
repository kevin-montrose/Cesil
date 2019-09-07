using System.Collections;
using System.Collections.Generic;

namespace Cesil
{
    internal sealed class Enumerable<T> : IEnumerable<T>
    {
        private readonly IReader<T> Reader;

        public Enumerable(IReader<T> reader)
        {
            Reader = reader;
        }

        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public IEnumerator<T> GetEnumerator()
        => new Enumerator<T>(Reader);

        public override string ToString()
        => $"{nameof(Enumerable<T>)} bound to {Reader}";
    }
}
