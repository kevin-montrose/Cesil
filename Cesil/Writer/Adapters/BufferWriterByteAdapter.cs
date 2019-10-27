using System;
using System.Buffers;
using System.Text;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class BufferWriterByteAdapter : IWriterAdapter
    {
        public bool IsDisposed { get; private set; }

        private readonly IBufferWriter<byte> Writer;
        private readonly Encoding Encoding;

        internal BufferWriterByteAdapter(IBufferWriter<byte> writer, Encoding encoding)
        {
            Writer = writer;
            Encoding = encoding;
        }

        public void Write(ReadOnlySpan<char> chars)
        {
            AssertNotDisposed(this);

            if (chars.IsEmpty)
            {
                return;
            }

            var neededBytes = Encoding.GetByteCount(chars);

            var into = Writer.GetSpan(neededBytes);
            Encoding.GetBytes(chars, into);

            Writer.Advance(neededBytes);
        }

        public void Write(char c)
        {
            Span<char> data = stackalloc char[1];
            data[0] = c;

            Write(data);
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
            }
        }
    }
}
