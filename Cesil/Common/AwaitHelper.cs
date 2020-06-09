using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal static class AwaitHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CheckCancellation<T>(T provider, CancellationToken token)
            where T : class
        {
#if DEBUG
            var c = (ITestableCancellableProvider)provider;
            c.CancelCounter++;

            if (c.CancelAfter != null)
            {
                if (c.CancelCounter >= c.CancelAfter)
                {
                    Throw.OperationCanceledException<object>();
                    return;
                }
            }
#endif
            token.ThrowIfCancellationRequested();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ConfiguredValueTaskAwaitable ConfigureCancellableAwait<T>(T p, ValueTask task, CancellationToken token)
            where T : class
        {
            CheckCancellation(p, token);
            return task.ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ConfiguredValueTaskAwaitable<V> ConfigureCancellableAwait<T, V>(T p, ValueTask<V> task, CancellationToken token)
            where T : class
        {
            CheckCancellation(p, token);
            return task.ConfigureAwait(false);
        }
    }
}
