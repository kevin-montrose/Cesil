using System;

namespace Cesil
{
    internal interface IReaderAdapter : ITestableDisposable
    {
        int Read(Span<char> into);
    }
}
