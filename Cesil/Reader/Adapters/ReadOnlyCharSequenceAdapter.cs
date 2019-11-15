using System;
using System.Buffers;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class ReadOnlyCharSequenceAdapter : IReaderAdapter
    {
        public bool IsDisposed { get; private set; }

        private ReadOnlyMemory<char> CurrentSegment;
        private ReadOnlySequence<char>.Enumerator Enumerator;
        private bool IsComplete;

        public ReadOnlyCharSequenceAdapter(ReadOnlySequence<char> sequence)
        {
            Enumerator = sequence.GetEnumerator();
            CurrentSegment = ReadOnlyMemory<char>.Empty;
            IsComplete = false;
        }

        public int Read(Span<char> into)
        {
            AssertNotDisposed(this);

            if (IsComplete)
            {
                return 0;
            }

tryAgain:
            if (CurrentSegment.IsEmpty)
            {
                if (!Enumerator.MoveNext())
                {
                    IsComplete = true;
                    return 0;
                }

                CurrentSegment = Enumerator.Current;
                goto tryAgain;
            }

            var intoLen = into.Length;
            var curSegLen = CurrentSegment.Length;

            if (intoLen > curSegLen)
            {
                // current segment can't fill into

                CurrentSegment.Span.CopyTo(into);
                CurrentSegment = ReadOnlyMemory<char>.Empty;

                var remainingInto = into.Slice(curSegLen);

                var nextChunkRs = Read(remainingInto);
                return curSegLen + nextChunkRs;
            }
            else
            {
                // current segment will fill into

                CurrentSegment.Span.Slice(0, intoLen).CopyTo(into);
                CurrentSegment = CurrentSegment.Slice(intoLen);
                return intoLen;
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                CurrentSegment = default;
                Enumerator = default;
            }
        }
    }
}
