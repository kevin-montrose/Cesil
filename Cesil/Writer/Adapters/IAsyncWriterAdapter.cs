using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal interface IAsyncWriterAdapter : ITestableAsyncDisposable
    {
        ValueTask WriteAsync(ReadOnlyMemory<char> chars, CancellationToken cancellationToken);
    }
}
