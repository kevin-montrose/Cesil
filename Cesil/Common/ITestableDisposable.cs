using System;

namespace Cesil
{
    internal interface ITestableDisposable : IDisposable
    {
        bool IsDisposed { get; }

        void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                var name = GetType().Name;

                Throw.ObjectDisposedException<object>(name);
            }
        }
    }
}
