using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class AsyncEnumerator<T> : IAsyncEnumerator<T>, ITestableAsyncDisposable
    {
        private T _Current;
        public T Current
        {
            get
            {
                AssertNotDisposed();
                return _Current;
            }
            private set
            {
                _Current = value;
            }
        }

        public bool IsDisposed => Reader == null;

        private IAsyncReader<T> Reader;
        private readonly CancellationToken Token;

        internal AsyncEnumerator(IAsyncReader<T> reader, CancellationToken token)
        {
            Current = default;
            Reader = reader;
            Token = token;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            AssertNotDisposed();

            var task = Reader.TryReadAsync(Token);
            if (task.IsCompletedSuccessfully)
            {
                var res = task.Result;
                if (!res.HasValue)
                {
                    return new ValueTask<bool>(false);
                }

                Current = res.Value;
                return new ValueTask<bool>(true);
            }

            return MoveNextAsync_ContinueAfterReadAsync(this, task, Token);

            // wait for a read to finish, then continue async
            static async ValueTask<bool> MoveNextAsync_ContinueAfterReadAsync(AsyncEnumerator<T> self, ValueTask<ReadResult<T>> waitFor, CancellationToken cancel)
            {
                var res = await waitFor;

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
                Reader = null;
                return ret;
            }

            return default;
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(AsyncEnumerator<T>));
            }
        }

        public override string ToString()
        => $"{nameof(AsyncEnumerator<T>)} bound to {Reader}";
    }
}
