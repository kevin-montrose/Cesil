using System;
using System.Collections;
using System.Collections.Generic;

namespace Cesil
{
    internal partial class MemberOrderHelper<T> : IEnumerator<T>
    {
        private bool AlreadyEnumerated;

        private int CurrentIndex;
        T IEnumerator<T>.Current => GetAt(CurrentIndex);

        [ExcludeFromCoverage("Trivial, and covered by IEnumerator<T>.Current")]
        object? IEnumerator.Current => GetAt(CurrentIndex);

        bool IEnumerator.MoveNext()
        {
            CurrentIndex++;
            return CurrentIndex < Data.Count;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            // we expect that, most of the time, we'll
            //    only enumerate a single time so optimize
            //    for that by avoiding an allocation
            //    unless GetEnumerator() is called twice
            if (!AlreadyEnumerated)
            {
                AlreadyEnumerated = true;
                return this;
            }

            return GetEnumeratorImpl(this);

            static IEnumerator<T> GetEnumeratorImpl(MemberOrderHelper<T> self)
            {
                for (var i = 0; i < self.Data.Count; i++)
                {
                    yield return self.GetAt(i);
                }
            }
        }

        [ExcludeFromCoverage("Trivial, and covered by IEnumerable<T>.GetEnumerator()")]
        IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable<T>)this).GetEnumerator();

        void IEnumerator.Reset()
        => Throw.NotSupportedException<object>(nameof(MemberOrderHelper<T>), nameof(Reset));

        void IDisposable.Dispose()
        {
            // you might be thinking you could cleverly 
            //   set AlreadyEnumerated = false here
            //   but odds are you are introducing a race
            //   condition if you do so
            //
            // if you can think of a clever way to prove
            //    that isn't happening... PRs are welcome
        }
    }
}
