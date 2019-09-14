using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Cesil
{
    internal static class AsyncTestHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCompletedSuccessfully(this ref ValueTask t, object selfObj)
        {
#if DEBUG
            var self = (ITestableAsyncProvider)selfObj;
            
            // act like we haven't completed, so we can try out the "slow" path
            if (self.ShouldGoAsync())
            {
                return false;
            }

            // force "fast" path
            var tRef = t.AsTask();
            tRef.Wait();
            t = default;
            return t.IsCompletedSuccessfully;
#else
            return t.IsCompletedSuccessfully;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCompletedSuccessfully<T>(this ref ValueTask<T> t, object selfObj)
        {
#if DEBUG
            var self = (ITestableAsyncProvider)selfObj;

            // act like we haven't completed, so we can try out the "slow" path
            if (self.ShouldGoAsync())
            {
                return false;
            }

            // force "fast" path
            var tRef = t.AsTask();
            tRef.Wait();
            t = new ValueTask<T>(tRef.Result);
            return t.IsCompletedSuccessfully;
#else
            return t.IsCompletedSuccessfully;
#endif
        }
    }
}
