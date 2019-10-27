using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed partial class AsyncEnumerator<T> : IAsyncEnumerator<T>, ITestableAsyncDisposable
    {
        private T _Current;
        public T Current
        {
            get
            {
                AssertNotDisposed(this);
                return _Current;
            }
            private set
            {
                _Current = value;
            }
        }

        public bool IsDisposed { get; private set; }

        private readonly IAsyncReader<T> Reader;
        private readonly CancellationToken Token;

        internal AsyncEnumerator(IAsyncReader<T> reader, CancellationToken token)
        {
            _Current = default!;
            Reader = reader;
            Token = token;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            AssertNotDisposed(this);
            Token.ThrowIfCancellationRequested();

            var task = Reader.TryReadAsync(Token);
            if (!task.IsCompletedSuccessfully(this))
            {
                return MoveNextAsync_ContinueAfterReadAsync(this, task, Token);
            }
            
            var res = task.Result;
            if (!res.HasValue)
            {
                return new ValueTask<bool>(false);
            }

            Current = res.Value;
            return new ValueTask<bool>(true);
            
            // wait for a read to finish, then continue async
            static async ValueTask<bool> MoveNextAsync_ContinueAfterReadAsync(AsyncEnumerator<T> self, ValueTask<ReadResult<T>> waitFor, CancellationToken cancel)
            {
                var res = await waitFor;
                self.Token.ThrowIfCancellationRequested();

                if (!res.HasValue)
                {
                    return false;
                }

                self.Current = res.Value;
                return true;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                var ret = Reader.DisposeAsync();
                IsDisposed = true;
                return ret;
            }

            return default;
        }

        public override string ToString()
        => $"{nameof(AsyncEnumerator<T>)} bound to {Reader}";
    }

#if DEBUG
    // this is only implemented in DEBUG builds, so tests (and only tests) can force
    //    particular async paths
    internal sealed partial class AsyncEnumerator<T> : ITestableAsyncProvider
    {
        private int _GoAsyncAfter;
        int ITestableAsyncProvider.GoAsyncAfter { set { _GoAsyncAfter = value; } }

        private int _AsyncCounter;
        int ITestableAsyncProvider.AsyncCounter => _AsyncCounter;

        bool ITestableAsyncProvider.ShouldGoAsync()
        {
            lock (this)
            {
                _AsyncCounter++;

                var ret = _AsyncCounter >= _GoAsyncAfter;

                return ret;
            }
        }
    }
#endif
}
