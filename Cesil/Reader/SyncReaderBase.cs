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

                while (true)
                {
#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
                    T _ = default!;
#pragma warning restore CES0005
                    var res = TryReadInner(false, false, ref _);
                    if (!res.HasValue)
                    {
                        break;
                    }

                    into.Add(res.Value);
                }

                return into;
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow(this, e);

                return default;
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

#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
            record = default!;
#pragma warning restore CES0005

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
                Throw.PoisonAndRethrow(this, e);

                return default;
            }
        }

        public bool TryReadWithReuse(ref T record)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                HandleRowEndingsAndHeaders();

                var res = TryReadInner(false, true, ref record);
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
                Throw.PoisonAndRethrow(this, e);

                return default;
            }
        }

        public ReadWithCommentResult<T> TryReadWithComment()
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                HandleRowEndingsAndHeaders();

#pragma warning disable CES0005 // T is generic, and null is legal, but since T isn't known to be a class we have to forgive null here
                T record = default!;
#pragma warning restore CES0005
                return TryReadInner(true, false, ref record);
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow(this, e);

                return default;
            }
        }

        public ReadWithCommentResult<T> TryReadWithCommentReuse(ref T record)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {
                HandleRowEndingsAndHeaders();

                return TryReadInner(true, true, ref record);
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow(this, e);

                return default;
            }
        }

        internal abstract ReadWithCommentResult<T> TryReadInner(bool returnComments, bool checkRecord, ref T record);

        internal abstract void HandleRowEndingsAndHeaders();

        public abstract void Dispose();
    }
}
