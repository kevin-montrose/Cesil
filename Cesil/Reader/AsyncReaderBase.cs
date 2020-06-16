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

        public ValueTask<TCollection> ReadAllAsync<TCollection>(TCollection into, CancellationToken cancellationToken = default)
            where TCollection : class, ICollection<T>
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            return ReadAllIntoCollectionAsync(into, cancellationToken);
        }

        public ValueTask<List<T>> ReadAllAsync(CancellationToken cancellationToken = default)
        => ReadAllAsync(new List<T>(), cancellationToken);

        private ValueTask<TCollection> ReadAllIntoCollectionAsync<TCollection>(TCollection into, CancellationToken cancellationToken)
        where TCollection : class, ICollection<T>
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            Utils.CheckArgumentNull(into, nameof(into));
            Utils.CheckImmutableReadInto<TCollection, T>(into, nameof(into));

            try
            {

                var headersAndRowEndingsTask = HandleRowEndingsAndHeadersAsync(cancellationToken);
                if (!headersAndRowEndingsTask.IsCompletedSuccessfully(this))
                {
                    return ReadAllAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(this, headersAndRowEndingsTask, into, cancellationToken);
                }

                using (StateMachine.Pin())
                {
                    while (true)
                    {
#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
                        T _ = default!;
#pragma warning restore CES0005
                        var resTask = TryReadInnerAsync(false, true, false, ref _, cancellationToken);
                        if (!resTask.IsCompletedSuccessfully(this))
                        {
                            return ReadAllAsync_ContinueAfterTryReadAsync(this, resTask, into, cancellationToken);
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
            static async ValueTask<TCollection> ReadAllAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(AsyncReaderBase<T> self, ValueTask waitFor, TCollection into, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    using (self.StateMachine.Pin())
                    {
                        while (true)
                        {
#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
                            T _ = default!;
#pragma warning restore CES0005

                            var resTask = self.TryReadInnerAsync(false, true, false, ref _, cancellationToken);
                            ReadWithCommentResult<T> res;
                            self.StateMachine.ReleasePinForAsync(resTask);
                            {
                                res = await ConfigureCancellableAwait(self, resTask, cancellationToken);
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
            static async ValueTask<TCollection> ReadAllAsync_ContinueAfterTryReadAsync(AsyncReaderBase<T> self, ValueTask<ReadWithCommentResult<T>> waitFor, TCollection ret, CancellationToken cancellationToken)
            {
                try
                {
                    var other = await ConfigureCancellableAwait(self, waitFor, cancellationToken);

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
#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
                        T _ = default!;
#pragma warning restore CES0005
                        var resTask = self.TryReadInnerAsync(false, true, false, ref _, cancellationToken);
                        ReadWithCommentResult<T> res;
                        self.StateMachine.ReleasePinForAsync(resTask);
                        {
                            res = await ConfigureCancellableAwait(self, resTask, cancellationToken);
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

        public ValueTask<ReadResult<T>> TryReadWithReuseAsync(ref T row, CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {

                var handleRowEndingsAndHeadersTask = HandleRowEndingsAndHeadersAsync(cancellationToken);
                if (!handleRowEndingsAndHeadersTask.IsCompletedSuccessfully(this))
                {
                    TryPreAllocateRow(true, ref row);
                    return TryReadWithReuseAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(this, handleRowEndingsAndHeadersTask, row, cancellationToken);
                }

                var tryReadTask = TryReadInnerAsync(false, false, true, ref row, cancellationToken);
                if (!tryReadTask.IsCompletedSuccessfully(this))
                {
                    return TryReadWithReuseAsync_ContinueAfterTryReadInnerAsync(this, tryReadTask, cancellationToken);
                }

                var res = tryReadTask.Result;
                return
                    res.ResultType switch
                    {
                        ReadWithCommentResultType.HasValue => new ValueTask<ReadResult<T>>(new ReadResult<T>(res.Value)),
                        ReadWithCommentResultType.NoValue => new ValueTask<ReadResult<T>>(ReadResult<T>.Empty),
                        _ => Throw.InvalidOperationException<ValueTask<ReadResult<T>>>($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}")
                    };
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask<ReadResult<T>>>(this, e);
            }

            // wait for HandleRowEndingsAndHeadersAsync to finish, then continue
            static async ValueTask<ReadResult<T>> TryReadWithReuseAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(AsyncReaderBase<T> self, ValueTask waitFor, T row, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    var readTask = self.TryReadInnerAsync(false, false, true, ref row, cancellationToken);
                    var res = await ConfigureCancellableAwait(self, readTask, cancellationToken);

                    return
                        res.ResultType switch
                        {
                            ReadWithCommentResultType.HasValue => new ReadResult<T>(res.Value),
                            ReadWithCommentResultType.NoValue => ReadResult<T>.Empty,
                            _ => Throw.InvalidOperationException<ReadResult<T>>($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}")
                        };
                }
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadResult<T>>(self, e);
                }
            }

            // wait for the inner call to finish
            static async ValueTask<ReadResult<T>> TryReadWithReuseAsync_ContinueAfterTryReadInnerAsync(AsyncReaderBase<T> self, ValueTask<ReadWithCommentResult<T>> waitFor, CancellationToken cancellationToken)
            {
                try
                {
                    var res = await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    return
                        res.ResultType switch
                        {
                            ReadWithCommentResultType.HasValue => new ReadResult<T>(res.Value),
                            ReadWithCommentResultType.NoValue => ReadResult<T>.Empty,
                            _ => Throw.InvalidOperationException<ReadResult<T>>($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}")
                        };
                }
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadResult<T>>(self, e);
                }
            }
        }

        public ValueTask<ReadResult<T>> TryReadAsync(CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
                T row = default!;
#pragma warning restore CES0005
                var handleRowEndingsAndHeadersTask = HandleRowEndingsAndHeadersAsync(cancellationToken);
                if (!handleRowEndingsAndHeadersTask.IsCompletedSuccessfully(this))
                {
                    return TryReadAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(this, handleRowEndingsAndHeadersTask, row, cancellationToken);
                }

                var tryReadTask = TryReadInnerAsync(false, false, false, ref row, cancellationToken);
                if (!tryReadTask.IsCompletedSuccessfully(this))
                {
                    return TryReadAsync_ContinueAfterTryReadInnerAsync(this, tryReadTask, cancellationToken);
                }

                var res = tryReadTask.Result;
                return
                    res.ResultType switch
                    {
                        ReadWithCommentResultType.HasValue => new ValueTask<ReadResult<T>>(new ReadResult<T>(res.Value)),
                        ReadWithCommentResultType.NoValue => new ValueTask<ReadResult<T>>(ReadResult<T>.Empty),
                        _ => Throw.InvalidOperationException<ValueTask<ReadResult<T>>>($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}")
                    };
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask<ReadResult<T>>>(this, e);
            }

            // wait for HandleRowEndingsAndHeadersAsync to finish, then continue
            static async ValueTask<ReadResult<T>> TryReadAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(AsyncReaderBase<T> self, ValueTask waitFor, T row, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    var readTask = self.TryReadInnerAsync(false, false, false, ref row, cancellationToken);
                    var res = await ConfigureCancellableAwait(self, readTask, cancellationToken);

                    return
                        res.ResultType switch
                        {
                            ReadWithCommentResultType.HasValue => new ReadResult<T>(res.Value),
                            ReadWithCommentResultType.NoValue => ReadResult<T>.Empty,
                            _ => Throw.InvalidOperationException<ReadResult<T>>($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}")
                        };
                }
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadResult<T>>(self, e);
                }
            }

            // wait for the inner call to finish
            static async ValueTask<ReadResult<T>> TryReadAsync_ContinueAfterTryReadInnerAsync(AsyncReaderBase<T> self, ValueTask<ReadWithCommentResult<T>> waitFor, CancellationToken cancellationToken)
            {
                try
                {
                    var res = await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    return
                        res.ResultType switch
                        {
                            ReadWithCommentResultType.HasValue => new ReadResult<T>(res.Value),
                            ReadWithCommentResultType.NoValue => ReadResult<T>.Empty,
                            _ => Throw.InvalidOperationException<ReadResult<T>>($"Unexpected {nameof(ReadWithCommentResultType)}: {res.ResultType}")
                        };
                }
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadResult<T>>(self, e);
                }
            }
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentAsync(CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
                T record = default!;
#pragma warning restore CES0005

                var handleRowEndingsAndHeadersTask = HandleRowEndingsAndHeadersAsync(cancellationToken);
                if (!handleRowEndingsAndHeadersTask.IsCompletedSuccessfully(this))
                {
                    return TryReadWithCommentAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(this, handleRowEndingsAndHeadersTask, record, cancellationToken);
                }

                return TryReadInnerAsync(true, false, false, ref record, cancellationToken);
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask<ReadWithCommentResult<T>>>(this, e);
            }

            // wait for HandleRowEndingsAndHeadersAsync and continue
            static async ValueTask<ReadWithCommentResult<T>> TryReadWithCommentAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(AsyncReaderBase<T> self, ValueTask waitFor, T record, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    var readTask = self.TryReadInnerAsync(true, false, false, ref record, cancellationToken);
                    var ret = await ConfigureCancellableAwait(self, readTask, cancellationToken);

                    return ret;
                }
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadWithCommentResult<T>>(self, e);
                }
            }
        }

        public ValueTask<ReadWithCommentResult<T>> TryReadWithCommentReuseAsync(ref T record, CancellationToken cancellationToken = default)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                var handleRowEndingsAndHeadersTask = HandleRowEndingsAndHeadersAsync(cancellationToken);
                if (!handleRowEndingsAndHeadersTask.IsCompletedSuccessfully(this))
                {
                    return TryReadWithCommentReuseAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(this, handleRowEndingsAndHeadersTask, record, cancellationToken);
                }

                return TryReadInnerAsync(true, false, true, ref record, cancellationToken);
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ValueTask<ReadWithCommentResult<T>>>(this, e);
            }

            // wait for HandleRowEndingsAndHeadersAsync and continue
            static async ValueTask<ReadWithCommentResult<T>> TryReadWithCommentReuseAsync_ContinueAfterHandleRowEndingsAndHeadersAsync(AsyncReaderBase<T> self, ValueTask waitFor, T record, CancellationToken cancellationToken)
            {
                try
                {
                    await ConfigureCancellableAwait(self, waitFor, cancellationToken);

                    var readTask = self.TryReadInnerAsync(true, false, true, ref record, cancellationToken);
                    var ret = await ConfigureCancellableAwait(self, readTask, cancellationToken);

                    return ret;
                }
                catch (Exception e)
                {
                    return Throw.PoisonAndRethrow<ReadWithCommentResult<T>>(self, e);
                }
            }
        }

        internal abstract ValueTask<ReadWithCommentResult<T>> TryReadInnerAsync(bool returnComments, bool pinAcquired, bool checkRecord, ref T record, CancellationToken cancellationToken);

        internal abstract ValueTask HandleRowEndingsAndHeadersAsync(CancellationToken cancellationToken);

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
