using System.Collections.Generic;
using System.IO;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal abstract class SyncReaderBase<T> :
        ReaderBase<T>,
        IReader<T>,
        ITestableDisposable
    {
        internal IReaderAdapter Inner;

        public bool IsDisposed => Inner == null;

        internal SyncReaderBase(IReaderAdapter inner, BoundConfigurationBase<T> config, object context) : base(config, context)
        {
            Inner = inner;
        }

        public TCollection ReadAll<TCollection>(TCollection into)
            where TCollection : class, ICollection<T>
        {
            AssertNotDisposed(this);

            if (into == null)
            {
                return Throw.ArgumentNullException<TCollection>(nameof(into));
            }

            bool prePinned;
            if (!StateMachineInitialized)
            {
                prePinned = false;
            }
            else
            {
                StateMachine.Pin();
                prePinned = true;
            }

            while (true)
            {
                T _ = default;
                var res = TryReadInner(false, prePinned, ref _);
                if (!res.HasValue)
                {
                    break;
                }

                into.Add(res.Value);

                if(!prePinned && StateMachineInitialized)
                {
                    StateMachine.Pin();
                    prePinned = true;
                }
            }

            if (prePinned)
            {
                StateMachine.Unpin();
            }

            return into;
        }

        public List<T> ReadAll()
        => ReadAll(new List<T>());

        public IEnumerable<T> EnumerateAll()
        {
            AssertNotDisposed(this);

            return new Enumerable<T>(this);
        }

        public bool TryRead(out T record)
        {
            AssertNotDisposed(this);

            record = default;
            return TryReadWithReuse(ref record);
        }

        public bool TryReadWithReuse(ref T record)
        {
            AssertNotDisposed(this);

            var res = TryReadInner(false, false, ref record);
            if (res.ResultType == ReadWithCommentResultType.HasValue)
            {
                record = res.Value;
                return true;
            }

            // intentionally not clearing record here
            return false;
        }

        public ReadWithCommentResult<T> TryReadWithComment()
        {
            AssertNotDisposed(this);

            var record = default(T);
            return TryReadWithCommentReuse(ref record);
        }

        public ReadWithCommentResult<T> TryReadWithCommentReuse(ref T record)
        {
            AssertNotDisposed(this);

            return TryReadInner(true, false, ref record);
        }

        internal abstract ReadWithCommentResult<T> TryReadInner(bool returnComments, bool pinAcquired, ref T record);

        public abstract void Dispose();
    }
}
