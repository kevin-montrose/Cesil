using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal static class AwaitHelper
    {
        // todo: add infrastucture for testing CancellationToken points

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckCancellation<T>(T provider, CancellationToken token)
        {
#if DEBUG
            var c = (ITestableCancellableProvider)provider!;
            c.CancelCounter++;

            if (c.CancelAfter != null)
            {    
                if (c.CancelCounter >= c.CancelAfter)
                {
                    throw new OperationCanceledException();
                }
            }
#endif

            token.ThrowIfCancellationRequested();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ConfiguredValueTaskAwaitable ConfigureCancellableAwait<T>(T p, ValueTask task, CancellationToken token)
        {
            CheckCancellation(p, token);
            return task.ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ConfiguredValueTaskAwaitable<V> ConfigureCancellableAwait<T, V>(T p, ValueTask<V> task, CancellationToken token)
        {
            CheckCancellation(p, token);
            return task.ConfigureAwait(false);
        }
    }
}
