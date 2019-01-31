using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil.Tests
{
    internal sealed class ForcedAsyncReader: TextReader
    {
        private readonly TextReader Inner;
        public ForcedAsyncReader(TextReader inner)
        {
            Inner = inner;
        }

        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            await Task.Yield();
            return await Inner.ReadAsync(buffer, index, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.Yield();
            return await Inner.ReadAsync(buffer, cancellationToken);
        }

        public override async Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        {
            await Task.Yield();
            return await Inner.ReadBlockAsync(buffer, index, count);
        }

        public override async Task<string> ReadLineAsync()
        {
            await Task.Yield();
            return await Inner.ReadLineAsync();
        }

        public override async ValueTask<int> ReadBlockAsync(Memory<char> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.Yield();
            return await Inner.ReadBlockAsync(buffer, cancellationToken);
        }

        public override async Task<string> ReadToEndAsync()
        {
            await Task.Yield();
            return await Inner.ReadToEndAsync();
        }

        protected override void Dispose(bool disposing)
        => Inner.Dispose();

        public override int Read()
        => throw new NotImplementedException();

        public override int Read(char[] buffer, int index, int count)
        => throw new NotImplementedException();

        public override int Read(Span<char> buffer)
        => throw new NotImplementedException();


        public override int ReadBlock(char[] buffer, int index, int count)
        => throw new NotImplementedException();


        public override int ReadBlock(Span<char> buffer)
        => throw new NotImplementedException();


        public override string ReadLine()
        => throw new NotImplementedException();

        public override string ReadToEnd()
        => throw new NotImplementedException();
    }
}
