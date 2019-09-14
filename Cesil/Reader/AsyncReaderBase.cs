using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal abstract partial class AsyncReaderBase<T> :
        ReaderBase<T>,
        IAsyncReader<T>,
        ITestableAsyncDisposable
    {
        public bool IsDisposed => Inner == null;

        internal IAsyncReaderAdapter Inner;

        internal AsyncReaderBase(IAsyncReaderAdapter reader, BoundConfigurationBase<T> config, object context) : base(config, context)
        {
            Inner = reader;
        }

        public ValueTask<TCollection> ReadAllAsync<TCollection>(TCollection into, CancellationToken cancel = default)
        where TCollection: class, ICollection<T>
        {
            AssertNotDisposed(this);

            return ReadAllIntoCollectionAsync(into, cancel);
        }

        public ValueTask<List<T>> ReadAllAsync(CancellationToken cancel = default)
        => ReadAllAsync(new List<T>(), cancel);

        private ValueTask<TCollection> ReadAllIntoCollectionAsync<TCollection>(TCollection into, CancellationToken cancel)
        where TCollection : class, ICollection<T>
        {
            if (into == null)
            {
                return Throw.ArgumentNullException<ValueTask<TCollection>>(nameof(into));
            }

            bool pinAcquired;
            if(!StateMachineInitialized)
            {
                pinAcquired = false;
            }
            else
            {
                StateMachine.Pin();
                pinAcquired = true;
            }

            while (true)
            {
                T _ = default;
                var resTask = TryReadInnerAsync(false, pinAcquired, ref _, cancel);
                if (!resTask.IsCompletedSuccessfully(this))
                {
                    return ReadAllAsync_ContinueAfterTryReadAsync(this, resTask, pinAcquired, into, cancel);
                }

                var res = resTask.Result;
                if (res.HasValue)
                {
                    into.Add(res.Value);
                }
                else
                {
                    break;
                }

                if (!pinAcquired && StateMachineInitialized)
                {
                    StateMachine.Pin();
                    pinAcquired = true;
                }
            }

            if (pinAcquired)
            {
                StateMachine.Unpin();
            }

            return new ValueTask<TCollection>(into);

            // wait for a tryreadasync to finish, then continue async
            static async ValueTask<TCollection> ReadAllAsync_ContinueAfterTryReadAsync(AsyncReaderBase<T> self, ValueTask<ReadWithCommentResult<T>> waitFor, bool pinAcquired, TCollection ret, CancellationToken cancel)
            {
                var other = await waitFor;
                if (other.HasValue)
                {
                    ret.Add(other.Value);
                }
                else
                {
                    return ret;
                }

                while (true)
                {
                    T _ = default;
                    var tryReadTask = self.TryReadInnerAsync(false, pinAcquired, ref _, cancel);
                    var res = await tryReadTask;
                    if (res.HasValue)
                    {
                        ret.Add(res.Value);
                    }
                    else
                    {
                        break;
                    }

                    if(!pinAcquired && self.StateMachineInitialized)
                    {
                        self.StateMachine.Pin();
                        pinAcquired = true;
                    }
                }

                return ret;
            }
        }

        public IAsyncEnumerable<T> EnumerateAllAsync()
        {
            AssertNotDisposed(this);

            return new AsyncEnumerable<T>(this);
        }

        public ValueTask<ReadResult<T>> TryReadWithReuseAsync(ref T row, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);

            var tryReadTask = TryReadInnerAsync(false, false, ref row, cancel);
            if (!tryReadTask.IsCompletedSuccessfully(this))
            {
                return TryReadWithReuseAsync_ContinueAfterTryReadInnerAsync(tryReadTask, cancel);
            }

            var res = tryReadTask.Result;
            switch (res.ResultType)
            {
                case ReadWithCommentResultType.HasValue:
                    return new ValueTask<ReadResult<T>>(new ReadResult<T>(res.Value));
                case ReadWithCommentResultType.NoValue:
                    return new ValueTask<ReadResult<T>>(ReadResult<T>.Empty);
                default:
                    return Throw.InvalidOperationException<ValueTask<ReadResult<T>>>($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}");
            }

            // wait for the inner call to finish
            static async ValueTask<ReadResult<T>> TryReadWithReuseAsync_ContinueAfterTryReadInnerAsync(ValueTask<ReadWithCommentResult<T>> waitFor, CancellationToken cancel)
            {
                var res = await waitFor;

                switch (res.ResultType)
                {
                    case ReadWithCommentResultType.HasValue:
                        return new ReadResult<T>(res.Value);
                    case ReadWithCommentResultType.NoValue:
                        return ReadResult<T>.Empty;
                    default:
                        return Throw.InvalidOperationException<ReadResult<T>>($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}");
                }
            }
        }

        public ValueTask<ReadResult<T>> TryReadAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed(this);

            var record = default(T);
            return TryReadWithReuseAsync(ref record, cancel);
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed(this);

            var record = default(T);
            return TryReadWithCommentReuseAsync(ref record, cancel);
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentReuseAsync(ref T record, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);

            return TryReadInnerAsync(true, false, ref record, cancel);
        }

        internal abstract ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync(bool returnComments, bool pinAcquired, ref T record, CancellationToken cancel);

        public abstract ValueTask DisposeAsync();
    }

#if DEBUG
    // this is only implemented in DEBUG builds, so tests (and only tests) can force
    //    particular async paths
    internal abstract partial class AsyncReaderBase<T> : ITestableAsyncProvider
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
