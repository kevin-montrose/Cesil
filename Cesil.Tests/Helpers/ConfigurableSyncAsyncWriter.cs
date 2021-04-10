using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil.Tests
{
    internal sealed class ConfigurableSyncAsyncWriter : TextWriter
    {
        private readonly TextWriter Inner;

        private int CurrentCall;
        private readonly bool[] Config;

        public override Encoding Encoding => Inner.Encoding;
        public override IFormatProvider FormatProvider => Inner.FormatProvider;
        public override string NewLine
        {
            get { return Inner.NewLine; }
            set { Inner.NewLine = value; }
        }

        public ConfigurableSyncAsyncWriter(bool[] config, TextWriter inner)
        {
            Inner = inner;
            Config = config;
        }

        private bool ShouldBeAsync()
        {
            if (CurrentCall >= Config.Length)
            {
                throw new InvalidOperationException("Unexpected number of async calls");
            }

            var ret = Config[CurrentCall];
            CurrentCall++;

            return ret;
        }

        public override void Close()
        => Inner.Close();

        protected override void Dispose(bool disposing)
        => Inner.Dispose();

        public override void Flush()
        => throw new NotImplementedException();

        public override Task FlushAsync()
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.FlushAsync();

            async Task Async()
            {
                await Task.Yield();
                await Inner.FlushAsync();
            }
        }

        public override Task WriteAsync(char value)
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.WriteAsync(value);

            async Task Async()
            {
                await Task.Yield();
                await Inner.WriteAsync(value);
            }
        }

        public override Task WriteAsync(char[] buffer, int index, int count)
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.WriteAsync(buffer, index, count);

            async Task Async()
            {
                await Task.Yield();
                await Inner.WriteAsync(buffer, index, count);
            }
        }

        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.WriteAsync(buffer, cancellationToken);

            async Task Async()
            {
                await Task.Yield();
                await Inner.WriteAsync(buffer, cancellationToken);
            }
        }

        public override Task WriteAsync(string value)
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.WriteAsync(value);

            async Task Async()
            {
                await Task.Yield();
                await Inner.WriteAsync(value);
            }
        }


        public override Task WriteLineAsync()
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.WriteLineAsync();

            async Task Async()
            {
                await Task.Yield();
                await Inner.WriteLineAsync();
            }
        }

        public override Task WriteLineAsync(char value)
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.WriteLineAsync(value);

            async Task Async()
            {
                await Task.Yield();
                await Inner.WriteLineAsync(value);
            }
        }

        public override Task WriteLineAsync(char[] buffer, int index, int count)
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.WriteLineAsync(buffer, index, count);

            async Task Async()
            {
                await Task.Yield();
                await Inner.WriteLineAsync(buffer, index, count);
            }
        }

        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.WriteLineAsync(buffer, cancellationToken);

            async Task Async()
            {
                await Task.Yield();
                await Inner.WriteLineAsync(buffer, cancellationToken);
            }
        }

        public override Task WriteLineAsync(string value)
        {
            if (ShouldBeAsync())
            {
                return Async();
            }

            return Inner.WriteLineAsync(value);

            async Task Async()
            {
                await Task.Yield();
                await Inner.WriteLineAsync(value);
            }
        }


        public override void Write(bool value)
        => throw new NotImplementedException();

        public override void Write(char value)
        => throw new NotImplementedException();

        public override void Write(char[] buffer)
        => throw new NotImplementedException();

        public override void Write(char[] buffer, int index, int count)
        => throw new NotImplementedException();

        public override void Write(decimal value)
        => throw new NotImplementedException();

        public override void Write(double value)
        => throw new NotImplementedException();

        public override void Write(float value)
        => throw new NotImplementedException();

        public override void Write(int value)
        => throw new NotImplementedException();

        public override void Write(long value)
        => throw new NotImplementedException();

        public override void Write(object value)
        => throw new NotImplementedException();

        public override void Write(ReadOnlySpan<char> buffer)
        => throw new NotImplementedException();

        public override void Write(string format, object arg0)
        => throw new NotImplementedException();

        public override void Write(string format, object arg0, object arg1)
        => throw new NotImplementedException();

        public override void Write(string format, object arg0, object arg1, object arg2)
        => throw new NotImplementedException();

        public override void Write(string format, params object[] arg)
        => throw new NotImplementedException();

        public override void Write(string value)
        => throw new NotImplementedException();

        public override void Write(uint value)
        => throw new NotImplementedException();

        public override void Write(ulong value)
        => throw new NotImplementedException();


        public override void WriteLine()
        => throw new NotImplementedException();

        public override void WriteLine(bool value)
        => throw new NotImplementedException();

        public override void WriteLine(char value)
        => throw new NotImplementedException();

        public override void WriteLine(char[] buffer)
        => throw new NotImplementedException();

        public override void WriteLine(char[] buffer, int index, int count)
        => throw new NotImplementedException();

        public override void WriteLine(decimal value)
        => throw new NotImplementedException();

        public override void WriteLine(double value)
        => throw new NotImplementedException();

        public override void WriteLine(float value)
        => throw new NotImplementedException();

        public override void WriteLine(int value)
        => throw new NotImplementedException();

        public override void WriteLine(long value)
        => throw new NotImplementedException();

        public override void WriteLine(object value)
        => throw new NotImplementedException();

        public override void WriteLine(ReadOnlySpan<char> buffer)
        => throw new NotImplementedException();

        public override void WriteLine(string format, object arg0)
        => throw new NotImplementedException();

        public override void WriteLine(string format, object arg0, object arg1)
        => throw new NotImplementedException();

        public override void WriteLine(string format, object arg0, object arg1, object arg2)
        => throw new NotImplementedException();

        public override void WriteLine(string format, params object[] arg)
        => throw new NotImplementedException();

        public override void WriteLine(string value)
        => throw new NotImplementedException();

        public override void WriteLine(uint value)
        => throw new NotImplementedException();

        public override void WriteLine(ulong value)
        => throw new NotImplementedException();
    }
}
