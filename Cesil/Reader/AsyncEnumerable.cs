using System.Collections.Generic;
using System.Threading;

namespace Cesil
{
    internal sealed class AsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly IAsyncReader<T> Reader;

        internal AsyncEnumerable(IAsyncReader<T> reader)
        {
            Reader = reader;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new AsyncEnumerator<T>(Reader, cancellationToken);

        public override string ToString()
        => $"{nameof(AsyncEnumerable<T>)} bound to {Reader}";
    }
}
