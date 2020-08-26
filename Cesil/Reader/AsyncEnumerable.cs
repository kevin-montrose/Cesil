using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static Cesil.AwaitHelper;
using static Cesil.DisposableHelper;

namespace Cesil
{
    // this can only be enumerated once, so implementing both interfaces on a single class
    internal sealed partial class AsyncEnumerable<T> :
        IAsyncEnumerable<T>,
        IAsyncEnumerator<T>,
        ITestableAsyncDisposable
    {
        private readonly IAsyncReader<T> Reader;

        private bool Enumerated;

        private CancellationToken Token;

        private T _Current;
        T IAsyncEnumerator<T>.Current
        {
            get
            {
                AssertNotDisposed(this);
                return _Current;
            }
        }

        private bool _IsDisposed;
        bool ITestableAsyncDisposable.IsDisposed => _IsDisposed;

        internal AsyncEnumerable(IAsyncReader<T> reader)
        {
            Reader = reader;
#pragma warning disable CES0005 // T is generic, and we'll overwrite it before it's used, so default! is needed
            _Current = default!;
#pragma warning restore CES0005
            Token = CancellationToken.None;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);

            if (Enumerated)
            {
                return Throw.InvalidOperationException<IAsyncEnumerator<T>>("Cannot enumerate this enumerable multiple times");
            }

            Enumerated = true;
            Token = cancellationToken;

            return this;
        }

        ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync()
        {
            AssertNotDisposed(this);

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

            _Current = res.Value;
            return new ValueTask<bool>(true);

            // wait for a read to finish, then continue async
            static async ValueTask<bool> MoveNextAsync_ContinueAfterReadAsync(AsyncEnumerable<T> self, ValueTask<ReadResult<T>> waitFor, CancellationToken cancellationToken)
            {
                var res = await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                if (!res.HasValue)
                {
                    return false;
                }

                self._Current = res.Value;
                return true;
            }
        }

        // not explicit for testing purposes
        public ValueTask DisposeAsync()
        {
            if (!_IsDisposed)
            {
                var ret = Reader.DisposeAsync();
                _IsDisposed = true;
                return ret;
            }

            return default;
        }

        public override string ToString()
        => $"{nameof(AsyncEnumerable<T>)} bound to {Reader}, {nameof(Enumerated)}={Enumerated}";
    }

#if DEBUG
    // only available in DEBUG builds, so tests can force cancellation at arbitrary points
    internal sealed partial class AsyncEnumerable<T> : ITestableCancellableProvider
    {
        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
        int? ITestableCancellableProvider.CancelAfter { get; set; }
        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
        int ITestableCancellableProvider.CancelCounter { get; set; }
    }

    // this is only implemented in DEBUG builds, so tests (and only tests) can force
    //    particular async paths
    internal sealed partial class AsyncEnumerable<T> : ITestableAsyncProvider
    {
        private int _GoAsyncAfter;
        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
        int ITestableAsyncProvider.GoAsyncAfter { set { _GoAsyncAfter = value; } }

        private int _AsyncCounter;
        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
        int ITestableAsyncProvider.AsyncCounter => _AsyncCounter;

        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
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
