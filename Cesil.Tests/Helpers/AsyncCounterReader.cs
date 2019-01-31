using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil.Tests
{
    public sealed class AsyncCounterReader: TextReader
    {
        private int _Count;
        public int Count => _Count;

        private readonly TextReader Inner;
        public AsyncCounterReader(TextReader inner)
        {
            _Count = 0;
            Inner = inner;
        }

        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            Interlocked.Increment(ref _Count);
            await Task.Yield();
            return await Inner.ReadAsync(buffer, index, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            Interlocked.Increment(ref _Count);
            await Task.Yield();
            return await Inner.ReadAsync(buffer, cancellationToken);
        }

        public override async Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        {
            Interlocked.Increment(ref _Count);
            await Task.Yield();
            return await Inner.ReadBlockAsync(buffer, index, count);
        }

        public override async Task<string> ReadLineAsync()
        {
            Interlocked.Increment(ref _Count);
            await Task.Yield();
            return await Inner.ReadLineAsync();
        }

        public override async ValueTask<int> ReadBlockAsync(Memory<char> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            Interlocked.Increment(ref _Count);
            await Task.Yield();
            return await Inner.ReadBlockAsync(buffer, cancellationToken);
        }

        public override async Task<string> ReadToEndAsync()
        {
            Interlocked.Increment(ref _Count);
            await Task.Yield();
            return await Inner.ReadToEndAsync();
        }

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

        protected override void Dispose(bool disposing)
        => Inner.Dispose();
    }
}
