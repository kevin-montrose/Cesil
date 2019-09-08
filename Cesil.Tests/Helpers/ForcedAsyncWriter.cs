using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil.Tests
{
    internal sealed class ForcedAsyncWriter : TextWriter, IAsyncDisposable
    {
        public override Encoding Encoding => Inner.Encoding;
        public override IFormatProvider FormatProvider => Inner.FormatProvider;
        public override string NewLine { get => Inner.NewLine; set => Inner.NewLine = value; }

        private readonly TextWriter Inner;

        public ForcedAsyncWriter(TextWriter inner)
        {
            Inner = inner;
        }

        public override async ValueTask DisposeAsync()
        {
            await Task.Yield();

            await Inner.DisposeAsync();
        }

        public override void Close()
        => Inner.Close();

        protected override void Dispose(bool disposing)
        => Inner.Dispose();

        public override void Flush()
        => throw new NotImplementedException();

        public override async Task FlushAsync()
        {
            await Task.Yield();
            await Inner.FlushAsync();
        }

        public override object InitializeLifetimeService()
        => Inner.InitializeLifetimeService();

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

        public override async Task WriteAsync(char value)
        {
            await Task.Yield();
            await Inner.WriteAsync(value);
        }

        public override async Task WriteAsync(char[] buffer, int index, int count)
        {
            await Task.Yield();
            await Inner.WriteAsync(buffer, index, count);
        }

        public override async Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            await Inner.WriteAsync(buffer, cancellationToken);
        }

        public override async Task WriteAsync(string value)
        {
            await Task.Yield();
            await Inner.WriteAsync(value);
        }

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

        public override async Task WriteLineAsync()
        {
            await Task.Yield();
            await Inner.WriteLineAsync();
        }

        public override async Task WriteLineAsync(char value)
        {
            await Task.Yield();
            await Inner.WriteLineAsync(value);
        }

        public override async Task WriteLineAsync(char[] buffer, int index, int count)
        {
            await Task.Yield();
            await Inner.WriteLineAsync(buffer, index, count);
        }

        public override async Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            await Inner.WriteLineAsync(buffer, cancellationToken);
        }

        public override async Task WriteLineAsync(string value)
        {
            await Task.Yield();
            await Inner.WriteLineAsync(value);
        }
    }
}
