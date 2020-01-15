using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.AwaitHelper;
using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed partial class AsyncEnumerableAdapter<T> :
        IAsyncEnumerable<T>,
        IAsyncEnumerator<T>,
        ITestableAsyncDisposable
    {
        private static readonly ValueTask<bool> TRUE = new ValueTask<bool>(true);
        private static readonly ValueTask<bool> FALSE = new ValueTask<bool>(false);

        private bool Enumerated;
        private readonly IEnumerable<T> Rows;
        private IEnumerator<T> Enumerator;
        private CancellationToken Token;

        private bool _IsDisposed;
        bool ITestableAsyncDisposable.IsDisposed => _IsDisposed;

        private T _Current;
        public T Current
        {
            get
            {
                AssertNotDisposed(this);

                return _Current;
            }
        }

        public AsyncEnumerableAdapter(IEnumerable<T> rows)
        {
            Rows = rows;
            Enumerated = false;
            Enumerator = Rows.GetEnumerator();
            _Current = default!;
            _IsDisposed = false;
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

        public ValueTask<bool> MoveNextAsync()
        {
            AssertNotDisposed(this);

            CheckCancellation(this, Token);

            if (Enumerator.MoveNext())
            {
                _Current = Enumerator.Current;
                return TRUE;
            }

            return FALSE;
        }

        public ValueTask DisposeAsync()
        {
            if (!_IsDisposed)
            {
                _IsDisposed = true;
                Enumerator.Dispose();
            }

            return default;
        }

        public override string ToString()
        => $"{nameof(AsyncEnumerableAdapter<object>)} backed by {Enumerator}";
    }

#if DEBUG
    internal sealed partial class AsyncEnumerableAdapter<T> : ITestableCancellableProvider
    {
        int? ITestableCancellableProvider.CancelAfter { get; set; }
        int ITestableCancellableProvider.CancelCounter { get; set; }
    }
#endif
}
