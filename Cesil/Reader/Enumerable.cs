using System.Collections;
using System.Collections.Generic;

namespace Cesil
{
    internal sealed class Enumerable<T> : IEnumerable<T>
    {
        private readonly Reader<T> Reader;

        public Enumerable(Reader<T> reader)
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
