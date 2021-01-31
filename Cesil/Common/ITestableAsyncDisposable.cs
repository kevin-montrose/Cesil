using System;
using System.Runtime.CompilerServices;

namespace Cesil
{
    internal interface ITestableAsyncDisposable : IAsyncDisposable
    {
        bool IsDisposed { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                var name = GetType().Name;

                Throw.ObjectDisposedException(name);
            }
        }
    }
}
