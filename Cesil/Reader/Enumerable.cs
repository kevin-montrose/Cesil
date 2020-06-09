using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    // this can only be enumerated once, so implementing both interfaces on a single class
    internal sealed class Enumerable<T> :
        IEnumerable<T>,
        IEnumerator<T>,
        ITestableDisposable
    {
        private readonly IReader<T> Reader;

        private bool Enumerated;

        private bool _IsDisposed;
        bool ITestableDisposable.IsDisposed => _IsDisposed;

        private T _Current;
        T IEnumerator<T>.Current
        {
            get
            {
                AssertNotDisposed(this);

                return _Current;
            }
        }

        object? IEnumerator.Current
        {
            get
            {
                AssertNotDisposed(this);

                return _Current;
            }
        }

        internal Enumerable(IReader<T> reader)
        {
            Reader = reader;
            Enumerated = false;
            _IsDisposed = false;
#pragma warning disable CES0005 // T is generic, and we'll overwrite it before it's used, so default! is needed
            _Current = default!;
#pragma warning restore CES0005
        }

        IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

        public IEnumerator<T> GetEnumerator()
        {
            AssertNotDisposed(this);

            if (Enumerated)
            {
                return Throw.InvalidOperationException<IEnumerator<T>>("Cannot enumerate this enumerable multiple times");
            }

            Enumerated = true;

            return this;
        }

        bool IEnumerator.MoveNext()
        {
            AssertNotDisposed(this);

            if(Reader.TryRead(out var c))
            {
                _Current = c;
                return true;
            }

            return false;
        }

        void IEnumerator.Reset()
        {
            AssertNotDisposed(this);

            Throw.NotSupportedException<object>(nameof(Enumerable<T>), nameof(Reset)); ;
        }

        // not explicit for testing purposes
        public void Dispose()
        {
            if (_IsDisposed) return;

            _IsDisposed = true;
        }

        public override string ToString()
        => $"{nameof(Enumerable<T>)} bound to {Reader}, {nameof(Enumerated)}={Enumerated}";
    }
}
