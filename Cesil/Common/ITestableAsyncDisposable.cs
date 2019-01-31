using System;

namespace Cesil
{
    internal interface ITestableAsyncDisposable : IAsyncDisposable
    {
        bool IsDisposed { get; }

        void AssertNotDisposed();
    }
}
