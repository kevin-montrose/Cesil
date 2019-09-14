using System.Runtime.CompilerServices;

namespace Cesil
{
    internal static class DisposableHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertNotDisposed(ITestableDisposable s)
        => s.AssertNotDisposed();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertNotDisposed(ITestableAsyncDisposable s)
        => s.AssertNotDisposed();
    }
}
