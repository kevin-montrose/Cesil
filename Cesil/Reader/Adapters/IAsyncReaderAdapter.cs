using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal interface IAsyncReaderAdapter : ITestableAsyncDisposable
    {
        ValueTask<int> ReadAsync(Memory<char> into, CancellationToken cancel);
    }
}
