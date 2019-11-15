using System;
using System.Buffers;
using System.Text;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class ReadOnlyByteSequenceAdapter : ByteSequenceAdapterBase, IReaderAdapter
    {
        public bool IsDisposed { get; private set; }

        private ReadOnlySequence<byte> Sequence;
        internal ReadOnlyByteSequenceAdapter(ReadOnlySequence<byte> sequence, Encoding encoding) : base(encoding)
        {
            Sequence = sequence;
        }

        public int Read(Span<char> into)
        {
            AssertNotDisposed(this);

            if (IsComplete)
            {
                return 0;
            }

            MapByteSequenceToChar(Sequence, true, into, out var processed, out _, out var chars);

            Sequence = Sequence.Slice(processed);

            return chars;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                Sequence = ReadOnlySequence<byte>.Empty;
                IsDisposed = true;
            }
        }
    }
}
