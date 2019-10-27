using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class Enumerator<T> : IEnumerator<T>, ITestableDisposable
    {
        private readonly IReader<T> Reader;

        public bool IsDisposed { get; private set; }

        private T _Current;
        public T Current
        {
            get
            {
                AssertNotDisposed(this);

                return _Current;
            }
        }

        object? IEnumerator.Current => Current;

        public Enumerator(IReader<T> reader)
        {
            _Current = default!;
            Reader = reader;
        }

        public bool MoveNext()
        {
            AssertNotDisposed(this);

            return Reader.TryRead(out _Current);
        }

        public void Reset()
        {
            AssertNotDisposed(this);

            Throw.NotSupportedException<object>(nameof(Enumerator<T>), nameof(Reset));
        }

        public void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
        }

        public override string ToString()
        => $"{nameof(Enumerator<T>)} bound to {Reader}";
    }
}
