using System;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal abstract class SyncReaderBase<T> :
        ReaderBase<T>,
        IReader<T>,
        ITestableDisposable
    {
        public bool IsDisposed { get; internal set; }

        internal readonly IReaderAdapter Inner;

        internal SyncReaderBase(IReaderAdapter inner, BoundConfigurationBase<T> config, object? context, IRowConstructor<T> rowBuilder, ExtraColumnTreatment extraTreatment) : base(config, context, rowBuilder, extraTreatment)
        {
            Inner = inner;
        }

        public TCollection ReadAll<TCollection>(TCollection into)
            where TCollection : class, ICollection<T>
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            Utils.CheckArgumentNull(into, nameof(into));
            Utils.CheckImmutableReadInto<TCollection, T>(into, nameof(into));

            try
            {

                HandleRowEndingsAndHeaders();

                using (StateMachine.Pin())
                {
                    while (true)
                    {
                        T _ = default!;
                        var res = TryReadInner(false, true, ref _);
                        if (!res.HasValue)
                        {
                            break;
                        }

                        into.Add(res.Value);
                    }
                }

                return into;
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<TCollection>(this, e);
            }
        }

        public List<T> ReadAll()
        => ReadAll(new List<T>());

        public IEnumerable<T> EnumerateAll()
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            return new Enumerable<T>(this);
        }

        public bool TryRead(out T record)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            record = default!;
            return TryReadWithReuse(ref record);
        }

        public bool TryReadWithReuse(ref T record)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                HandleRowEndingsAndHeaders();

                var res = TryReadInner(false, false, ref record);
                if (res.ResultType == ReadWithCommentResultType.HasValue)
                {
                    record = res.Value;
                    return true;
                }

                // intentionally not clearing record here
                return false;
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<bool>(this, e);
            }
        }

        public ReadWithCommentResult<T> TryReadWithComment()
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            var record = default(T)!;
            return TryReadWithCommentReuse(ref record);
        }

        public ReadWithCommentResult<T> TryReadWithCommentReuse(ref T record)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                HandleRowEndingsAndHeaders();

                return TryReadInner(true, false, ref record);
            }
            catch (Exception e)
            {
                return Throw.PoisonAndRethrow<ReadWithCommentResult<T>>(this, e);
            }
        }

        internal abstract ReadWithCommentResult<T> TryReadInner(bool returnComments, bool pinAcquired, ref T record);

        internal abstract void HandleRowEndingsAndHeaders();

        public abstract void Dispose();
    }
}
