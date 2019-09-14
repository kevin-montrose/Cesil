﻿using System.Collections;
using System.Collections.Generic;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRowEnumerator<T> : IEnumerator<T>, ITestableDisposable
    {
        private DynamicRow.DynamicColumnEnumerator Enumerator;

        public bool IsDisposed
        {
            get
            {
                var e = Enumerator;
                if (e == null) return true;

                return e.IsDisposed;
            }
        }

        private T _Current;
        public T Current
        {
            get
            {
                AssertNotDisposed(this);
                return _Current;
            }
        }

        object IEnumerator.Current => Current;

        internal DynamicRowEnumerator(DynamicRow row)
        {
            Enumerator = new DynamicRow.DynamicColumnEnumerator(row);
        }

        public bool MoveNext()
        {
            AssertNotDisposed(this);

            if (!Enumerator.MoveNext())
            {
                _Current = default;
                return false;
            }

            var col = Enumerator.Current;

            dynamic val = Enumerator.Row.GetCellAt(col.Index);
            _Current = val;

            return true;
        }

        public void Reset()
        {
            AssertNotDisposed(this);

            _Current = default;
            Enumerator.Reset();
        }


        public void Dispose()
        {
            Enumerator?.Dispose();
            Enumerator = null;
        }

        public override string ToString()
        => $"{nameof(DynamicRowEnumerator<T>)} bound to {Enumerator}";
    }
}
