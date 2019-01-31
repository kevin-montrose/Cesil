using System;

namespace Cesil
{
    internal interface ITestableDisposable: IDisposable
    {
        bool IsDisposed { get; }

        void AssertNotDisposed();
    }
}
