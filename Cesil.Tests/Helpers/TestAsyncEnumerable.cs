using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil.Tests
{
    public class TestAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private class TestAsyncEnumerator<T2> : IAsyncEnumerator<T2>
        {
            private readonly bool AlwaysAsync;
            private readonly IEnumerator<T2> Raw;

            public T2 Current { get; private set; }

            public TestAsyncEnumerator(IEnumerator<T2> raw, bool alwaysAsync)
            {
                Raw = raw;
                AlwaysAsync = alwaysAsync;
            }

            public async ValueTask DisposeAsync()
            {
                if (AlwaysAsync)
                {
                    await Task.Yield();
                }

                Raw.Dispose();
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                if (AlwaysAsync)
                {
                    await Task.Yield();
                }

                var ret = Raw.MoveNext();
                if (ret)
                {
                    Current = Raw.Current;
                }

                return ret;
            }
        }

        private readonly bool AlwaysAsync;
        private readonly IEnumerable<T> Raw;
        public TestAsyncEnumerable(IEnumerable<T> raw, bool alwaysAsync)
        {
            Raw = raw;
            AlwaysAsync = alwaysAsync;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancel = default)
        => new TestAsyncEnumerator<T>(Raw.GetEnumerator(), AlwaysAsync);
    }
}
