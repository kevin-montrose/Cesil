﻿using System.Collections;
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

        public Enumerable(IReader<T> reader)
        {
            Reader = reader;
            Enumerated = false;
            _IsDisposed = false;
            _Current = default!;
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

            return Reader.TryRead(out _Current);
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
