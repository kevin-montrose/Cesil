using System;

namespace Cesil
{
    internal interface ITestableAsyncDisposable : IAsyncDisposable
    {
        bool IsDisposed { get; }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                var name = GetType().Name;

                Throw.ObjectDisposedException<object>(name);
            }
        }
    }
}
