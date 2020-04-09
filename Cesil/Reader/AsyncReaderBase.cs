using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static Cesil.AwaitHelper;
using static Cesil.DisposableHelper;

namespace Cesil
{
    internal abstract partial class AsyncReaderBase<T> :
        ReaderBase<T>,
        IAsyncReader<T>,
        ITestableAsyncDisposable
    {
        public bool IsDisposed { get; internal set; }

        internal readonly IAsyncReaderAdapter Inner;

        internal AsyncReaderBase(IAsyncReaderAdapter reader, BoundConfigurationBase<T> config, object? context, IRowConstructor<T> rowBuilder, ExtraColumnTreatment extraTreatment) : base(config, context, rowBuilder, extraTreatment)
        {
            Inner = reader;
        }

        public ValueTask<TCollection> ReadAllAsync<TCollection>(TCollection into, CancellationToken cancel = default)
        where TCollection : class, ICollection<T>
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            return ReadAllIntoCollectionAsync(into, cancel);
        }

        public ValueTask<List<T>> ReadAllAsync(CancellationToken cancel = default)
        => ReadAllAsync(new List<T>(), cancel);

        private ValueTask<TCollection> ReadAllIntoCollectionAsync<TCollection>(TCollection into, CancellationToken cancel)
        where TCollection : class, ICollection<T>
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            Utils.CheckArgumentNull(into, nameof(into));
            Utils.CheckImmutableReadInto<TCollection, T>(into, nameof(into));

            try
            {

                var headersAndRowEndingsTask = HandleRowEndingsAndHeadersAsync(cancel);
                if (!headersAndRowEndingsTask.IsCompletedSuccessfully(this))
                {
                    return ReadAllAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(this, headersAndRowEndingsTask, into, cancel);
                }

                using (StateMachine.Pin())
                {
                    while (true)
                    {
                        T _ = default!;
                        var resTask = TryReadInnerAsync(false, true, ref _, cancel);
                        if (!resTask.IsCompletedSuccessfully(this))
                        {
                            return ReadAllAsync_ContinueAfterTryReadAsync(this, resTask, into, cancel);
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
                    }
                }

                return new ValueTask<TCollection>(into);
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask<TCollection>>(this, e);
            }

            // continue after HandleRowEndingsAndHeadersAsync completes
            static async ValueTask<TCollection> ReadAllAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(AsyncReaderBase<T> self, ValueTask waitFor, TCollection into, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    using (self.StateMachine.Pin())
                    {
                        while (true)
                        {
                            T _ = default!;

                            var resTask = self.TryReadInnerAsync(false, true, ref _, cancel);
                            ReadWithCommentResult<T> res;
                            self.StateMachine.ReleasePinForAsync(resTask);
                            {
                                res = await ConfigureCancellableAwait(self, resTask, cancel);
                                CheckCancellation(self, cancel);
                            }

                            if (res.HasValue)
                            {
                                into.Add(res.Value);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    return into;
                }
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<TCollection>(self, e);
                }
            }

            // wait for a tryreadasync to finish, then continue async
            static async ValueTask<TCollection> ReadAllAsync_ContinueAfterTryReadAsync(AsyncReaderBase<T> self, ValueTask<ReadWithCommentResult<T>> waitFor, TCollection ret, CancellationToken cancel)
            {
                try
                {

                    var other = await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

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
                        T _ = default!;
                        var resTask = self.TryReadInnerAsync(false, true, ref _, cancel);
                        ReadWithCommentResult<T> res;
                        self.StateMachine.ReleasePinForAsync(resTask);
                        {
                            res = await ConfigureCancellableAwait(self, resTask, cancel);
                            CheckCancellation(self, cancel);
                        }
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
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<TCollection>(self, e);
                }
            }
        }

        public IAsyncEnumerable<T> EnumerateAllAsync()
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            return new AsyncEnumerable<T>(this);
        }

        public ValueTask<ReadResult<T>> TryReadWithReuseAsync(ref T row, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {

                var handleRowEndingsAndHeadersTask = HandleRowEndingsAndHeadersAsync(cancel);
                if (!handleRowEndingsAndHeadersTask.IsCompletedSuccessfully(this))
                {
                    TryPreAllocateRow(ref row);
                    return TryReadWithReuseAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(this, handleRowEndingsAndHeadersTask, row, cancel);
                }

                var tryReadTask = TryReadInnerAsync(false, false, ref row, cancel);
                if (!tryReadTask.IsCompletedSuccessfully(this))
                {
                    return TryReadWithReuseAsync_ContinueAfterTryReadInnerAsync(this, tryReadTask, cancel);
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
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask<ReadResult<T>>>(this, e);
            }

            // wait for HandleRowEndingsAndHeadersAsync to finish, then continue
            static async ValueTask<ReadResult<T>> TryReadWithReuseAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(AsyncReaderBase<T> self, ValueTask waitFor, T row, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    var res = await ConfigureCancellableAwait(self, self.TryReadInnerAsync(false, false, ref row, cancel), cancel);
                    CheckCancellation(self, cancel);

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
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadResult<T>>(self, e);
                }
            }

            // wait for the inner call to finish
            static async ValueTask<ReadResult<T>> TryReadWithReuseAsync_ContinueAfterTryReadInnerAsync(AsyncReaderBase<T> self, ValueTask<ReadWithCommentResult<T>> waitFor, CancellationToken cancel)
            {
                try
                {
                    var res = await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

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
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadResult<T>>(self, e);
                }
            }
        }

        public ValueTask<ReadResult<T>> TryReadAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            var record = default(T)!;
            return TryReadWithReuseAsync(ref record, cancel);
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            var record = default(T)!;
            return TryReadWithCommentReuseAsync(ref record, cancel);
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentReuseAsync(ref T record, CancellationToken cancel = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                var handleRowEndingsAndHeadersTask = HandleRowEndingsAndHeadersAsync(cancel);
                if (!handleRowEndingsAndHeadersTask.IsCompletedSuccessfully(this))
                {
                    return TryReadWithCommentReuseAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(this, handleRowEndingsAndHeadersTask, record, cancel);
                }

                return TryReadInnerAsync(true, false, ref record, cancel);
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask<ReadWithCommentResult<T>>>(this, e);
            }

            // wait for HandleRowEndingsAndHeadersAsync and continue
            static async ValueTask<ReadWithCommentResult<T>> TryReadWithCommentReuseAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(AsyncReaderBase<T> self, ValueTask waitFor, T record, CancellationToken cancel)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancel);
                    CheckCancellation(self, cancel);

                    var ret = await ConfigureCancellableAwait(self, self.TryReadInnerAsync(true, false, ref record, cancel), cancel);
                    CheckCancellation(self, cancel);

                    return ret;
                }
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadWithCommentResult<T>>(self, e);
                }
            }
        }

        internal abstract ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync(bool returnComments, bool pinAcquired, ref T record, CancellationToken cancel);

        internal abstract ValueTask HandleRowEndingsAndHeadersAsync(CancellationToken cancel);

        public abstract ValueTask DisposeAsync();
    }

#if DEBUG
    // only available in DEBUG builds, so tests can force cancellation at arbitrary points
    internal abstract partial class AsyncReaderBase<T> : ITestableCancellableProvider
    {
        int? ITestableCancellableProvider.CancelAfter { get; set; }
        int ITestableCancellableProvider.CancelCounter { get; set; }
    }

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
