using System.Collections;
using System.Collections.Generic;

namespace Cesil
{
    internal sealed class Enumerator<T> : IEnumerator<T>, ITestableDisposable
    {
        private Reader<T> Reader;

        public bool IsDisposed => Reader == null;

        private T _Current;
        public T Current
        {
            get
            {
                AssertNotDisposed();

                return _Current;
            }
        }

        object IEnumerator.Current => Current;

        public Enumerator(Reader<T> reader)
        {
            Reader = reader;
        }

        public bool MoveNext()
        {
            AssertNotDisposed();

            return Reader.TryRead(out _Current);
        }

        public void Reset()
        {
            AssertNotDisposed();

            Throw.NotSupportedException(nameof(Enumerator<T>), nameof(Reset));
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(Enumerator<T>));
            }
        }

        public void Dispose()
        {
            if (IsDisposed) return;

            Reader = null;
        }

        public override string ToString()
        => $"{nameof(Enumerator<T>)} bound to {Reader}";
    }
}
