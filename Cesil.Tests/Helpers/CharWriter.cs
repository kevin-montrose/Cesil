using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Cesil.Tests
{
    public class CharWriter : IBufferWriter<char>
    {
        private readonly PipeWriter Inner;

        public CharWriter(PipeWriter inner)
        {
            Inner = inner;
        }

        public void Advance(int count)
        => Inner.Advance(count * sizeof(char));

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            var bytes = Inner.GetSpan(sizeHint * sizeof(char));
            var chars = MemoryMarshal.Cast<byte, char>(bytes);

            return chars;
        }

        public ValueTask<FlushResult> FlushAsync()
        => Inner.FlushAsync();
    }
}
