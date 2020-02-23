using System;
using System.Runtime.CompilerServices;

namespace Cesil
{
    internal interface ITestableDisposable : IDisposable
    {
        bool IsDisposed { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
