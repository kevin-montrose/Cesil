using System;
using System.Buffers;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class BufferWriterCharAdapter : IWriterAdapter
    {
        public bool IsDisposed { get; private set; }

        private int NextIndex;
        private Memory<char> Memory;
        private readonly IBufferWriter<char> Writer;

        public BufferWriterCharAdapter(IBufferWriter<char> writer)
        {
            Writer = writer;
            Memory = default;
            NextIndex = 0;
        }

        public void Write(char c)
        {
            AssertNotDisposedInternal(this);

            if (NextIndex == Memory.Length)
            {
                if (!Memory.IsEmpty)
                {
                    Writer.Advance(Memory.Length);
                }

                Memory = Writer.GetMemory();
                NextIndex = 0;
            }

            Memory.Span[NextIndex] = c;
            NextIndex++;
        }

        public void Write(ReadOnlySpan<char> chars)
        {
            AssertNotDisposedInternal(this);

            while (!chars.IsEmpty)
            {
                if (Memory.Length == NextIndex)
                {
                    if (!Memory.IsEmpty)
                    {
                        Writer.Advance(Memory.Length);
                    }

                    Memory = Writer.GetMemory();
                    NextIndex = 0;
                }

                var copyLen = Math.Min(chars.Length, Memory.Length - NextIndex);
                chars.Slice(0, copyLen).CopyTo(Memory.Span.Slice(NextIndex));

                chars = chars.Slice(copyLen);
                NextIndex += copyLen;
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                if (NextIndex != 0)
                {
                    Writer.Advance(NextIndex);
                    NextIndex = 0;
                }

                Memory = default;
                IsDisposed = true;
            }
        }
    }
}
