using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal abstract class AsyncReaderBase<T> :
        ReaderBase<T>,
        IAsyncReader<T>,
        ITestableAsyncDisposable
    {
        public bool IsDisposed => Inner == null;

        internal TextReader Inner;

        internal AsyncReaderBase(TextReader reader, BoundConfigurationBase<T> config, object context) : base(config, context)
        {
            Inner = reader;
        }

        public ValueTask<List<T>> ReadAllAsync(List<T> into, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            if (into == null)
            {
                Throw.ArgumentNullException(nameof(into));
            }

            return ReadAllIntoListAsync(into, cancel);
        }

        public ValueTask<List<T>> ReadAllAsync(CancellationToken cancel = default)
        => ReadAllAsync(new List<T>(), cancel);

        private ValueTask<List<T>> ReadAllIntoListAsync(List<T> into, CancellationToken cancel)
        {
            AssertNotDisposed();

            if (into == null)
            {
                Throw.ArgumentNullException(nameof(into));
            }

            while (true)
            {
                var resTask = TryReadAsync(cancel);
                if (resTask.IsCompletedSuccessfully)
                {
                    var res = resTask.Result;

                    if (res.HasValue)
                    {
                        into.Add(res.Value);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    return ReadAllAsync_ContinueAfterTryReadAsync(this, resTask, into, cancel);
                }
            }

            return new ValueTask<List<T>>(into);

            // wait for a tryreadasync to finish, then continue async
            static async ValueTask<List<T>> ReadAllAsync_ContinueAfterTryReadAsync(AsyncReaderBase<T> self, ValueTask<ReadResult<T>> waitFor, List<T> ret, CancellationToken cancel)
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
                    var res = await self.TryReadAsync(cancel);
                    if (res.HasValue)
                    {
                        ret.Add(res.Value);
                    }
                    else
                    {
                        break;
                    }
                }

                return ret;
            }
        }

        public IAsyncEnumerable<T> EnumerateAllAsync()
        {
            AssertNotDisposed();

            return new AsyncEnumerable<T>(this);
        }

        public ValueTask<ReadResult<T>> TryReadWithReuseAsync(ref T row, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            var tryReadTask = TryReadInnerAsync(false, ref row, cancel);
            if (!tryReadTask.IsCompletedSuccessfully)
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
                    Throw.InvalidOperationException($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}");
                    // just for control flow
                    return default;
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
                        Throw.InvalidOperationException($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}");
                        // just for control flow
                        return default;
                }
            }
        }

        public ValueTask<ReadResult<T>> TryReadAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed();

            var record = default(T);
            return TryReadWithReuseAsync(ref record, cancel);
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed();

            var record = default(T);
            return TryReadWithCommentReuseAsync(ref record, cancel);
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentReuseAsync(ref T record, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            return TryReadInnerAsync(true, ref record, cancel);
        }

        internal abstract ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync(bool returnComments, ref T record, CancellationToken cancel);

        public abstract ValueTask DisposeAsync();

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                var name = this.GetType().Name;

                Throw.ObjectDisposedException(name);
            }
        }
    }
}
