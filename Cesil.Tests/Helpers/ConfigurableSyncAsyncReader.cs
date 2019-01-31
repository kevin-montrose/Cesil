using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil.Tests
{
    internal sealed class ConfigurableSyncAsyncReader: TextReader
    {
        private readonly TextReader Inner;

        private int CurrentCall;
        private readonly bool[] Config;

        public ConfigurableSyncAsyncReader(bool[] config, TextReader inner)
        {
            Inner = inner;
            CurrentCall = 0;
            Config = config;
        }

        private bool ShouldBeAsync()
        {
            if(CurrentCall >= Config.Length)
            {
                throw new InvalidOperationException("Unexpected number of async calls");
            }

            var ret = Config[CurrentCall];
            CurrentCall++;

            return ret;
        }

        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            await Task.Yield();
            return await Inner.ReadAsync(buffer, index, count);
        }

        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!ShouldBeAsync())
            {
                return Inner.ReadAsync(buffer, cancellationToken);
            }

            return Async();

            async ValueTask<int> Async()
            {
                await Task.Yield();
                return await Inner.ReadAsync(buffer, cancellationToken);
            }
        }

        public override Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        {
            if (!ShouldBeAsync())
            {
                return Inner.ReadBlockAsync(buffer, index, count);
            }

            return Async();

            async Task<int> Async()
            {
                await Task.Yield();
                return await Inner.ReadBlockAsync(buffer, index, count);
            }
        }

        public override Task<string> ReadLineAsync()
        {
            if (!ShouldBeAsync())
            {
                return Inner.ReadLineAsync();
            }

            return Async();

            async Task<string> Async()
            {
                await Task.Yield();
                return await Inner.ReadLineAsync();
            }
        }

        public override ValueTask<int> ReadBlockAsync(Memory<char> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!ShouldBeAsync())
            {
                return Inner.ReadBlockAsync(buffer, cancellationToken);
            }

            return Async();

            async ValueTask<int> Async()
            {
                await Task.Yield();
                return await Inner.ReadBlockAsync(buffer, cancellationToken);
            }
        }

        public override Task<string> ReadToEndAsync()
        {
            if (!ShouldBeAsync())
            {
                return Inner.ReadToEndAsync();
            }

            return Async();

            async Task<string> Async()
            {
                await Task.Yield();
                return await Inner.ReadToEndAsync();
            }
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
