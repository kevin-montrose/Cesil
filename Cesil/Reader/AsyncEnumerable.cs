using System.Collections.Generic;
using System.Threading;

namespace Cesil
{
    internal sealed class AsyncEnumerable<T>: IAsyncEnumerable<T>
        where T : new()
    {
        private readonly AsyncReader<T> Reader;

        internal AsyncEnumerable(AsyncReader<T> reader)
        {
            Reader = reader;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new AsyncEnumerator<T>(Reader, cancellationToken);
    }
}
