using System.Collections.Generic;
using System.IO;

namespace Cesil
{
    internal abstract class SyncReaderBase<T> :
        ReaderBase<T>,
        IReader<T>,
        ITestableDisposable
    {
        internal TextReader Inner;

        public bool IsDisposed => Inner == null;

        internal SyncReaderBase(TextReader inner, BoundConfigurationBase<T> config, object context) : base(config, context)
        {
            Inner = inner;
        }

        public List<T> ReadAll(List<T> into)
        {
            AssertNotDisposed();

            if (into == null)
            {
                Throw.ArgumentNullException(nameof(into));
            }

            while (TryRead(out var t))
            {
                into.Add(t);
            }

            return into;
        }

        public List<T> ReadAll()
        => ReadAll(new List<T>());

        public IEnumerable<T> EnumerateAll()
        {
            AssertNotDisposed();

            return new Enumerable<T>(this);
        }

        public bool TryRead(out T record)
        {
            AssertNotDisposed();

            record = default;
            return TryReadWithReuse(ref record);
        }

        public bool TryReadWithReuse(ref T record)
        {
            AssertNotDisposed();

            var res = TryReadInner(false, ref record);
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
            AssertNotDisposed();

            var record = default(T);
            return TryReadWithCommentReuse(ref record);
        }

        public ReadWithCommentResult<T> TryReadWithCommentReuse(ref T record)
        {
            AssertNotDisposed();

            return TryReadInner(true, ref record);
        }

        internal abstract ReadWithCommentResult<T> TryReadInner(bool returnComments, ref T record);

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                var name = this.GetType().Name;
                Throw.ObjectDisposedException(name);
            }
        }

        public abstract void Dispose();
    }
}
