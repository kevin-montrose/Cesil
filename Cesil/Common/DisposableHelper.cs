using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Cesil
{
    internal static class DisposableHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertNotDisposed(ITestableDisposable s)
        => s.AssertNotDisposed();

        // todo: disposable tests need to know what types shouldn't be tested when in RELEASE mode

        // only for internal interfaces, where we want DEBUG to quickly catch use after dispose,
        //      but for RELEASE we don't want to pay the cost of the method call
        [Conditional("DEBUG")]
        public static void AssertNotDisposedInternal(ITestableDisposable s)
        => AssertNotDisposed(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertNotDisposed(ITestableAsyncDisposable s)
        => s.AssertNotDisposed();

        // only for internal interfaces, where we want DEBUG to quickly catch use after dispose,
        //      but for RELEASE we don't want to pay the cost of the method call
        [Conditional("DEBUG")]
        public static void AssertNotDisposedInternal(ITestableAsyncDisposable s)
        => AssertNotDisposed(s);
    }
}
