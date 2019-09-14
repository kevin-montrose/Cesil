using System;
using System.Buffers;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class BufferWriterAdapter : IWriterAdapter
    {
        public bool IsDisposed => Writer == null;

        private int NextIndex;
        private Memory<char> Memory;
        private IBufferWriter<char> Writer;

        public BufferWriterAdapter(IBufferWriter<char> writer)
        {
            Writer = writer;
            Memory = default;
            NextIndex = 0;
        }

        public void Write(char c)
        {
            AssertNotDisposed(this);

            if(NextIndex == Memory.Length)
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
            AssertNotDisposed(this);

            while (!chars.IsEmpty)
            {
                if(Memory.Length == NextIndex)
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
                if(NextIndex != 0)
                {
                    Writer.Advance(NextIndex);
                    NextIndex = 0;
                }

                Memory = default;
                Writer = null;
            }
        }
    }
}
