using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil.Tests
{
    internal sealed class ForcedAsyncWriter : TextWriter
    {
        public override Encoding Encoding => Inner.Encoding;
        public override IFormatProvider FormatProvider => Inner.FormatProvider;
        public override string NewLine { get => Inner.NewLine; set => Inner.NewLine = value; }

        private readonly TextWriter Inner;

        public ForcedAsyncWriter(TextWriter inner)
        {
            Inner = inner;
        }

        public override void Close()
        => Inner.Close();

        protected override void Dispose(bool disposing)
        => Inner.Dispose();

        public override void Flush()
        => Inner.Flush();

        public override async Task FlushAsync()
        {
            await Task.Yield();
            await Inner.FlushAsync();
        }

        public override object InitializeLifetimeService()
        => Inner.InitializeLifetimeService();

        public override void Write(bool value)
        => Inner.Write(value);

        public override void Write(char value)
        => Inner.Write(value);

        public override void Write(char[] buffer)
        => Inner.Write(buffer);

        public override void Write(char[] buffer, int index, int count)
        => Inner.Write(buffer, index, count);

        public override void Write(decimal value)
        => Inner.Write(value);

        public override void Write(double value)
        => Inner.Write(value);

        public override void Write(float value)
        => Inner.Write(value);

        public override void Write(int value)
        => Inner.Write(value);

        public override void Write(long value)
        => Inner.Write(value);

        public override void Write(object value)
        => Inner.Write(value);

        public override void Write(ReadOnlySpan<char> buffer)
        => Inner.Write(buffer);

        public override void Write(string format, object arg0)
        => Inner.Write(format, arg0);

        public override void Write(string format, object arg0, object arg1)
        => Inner.Write(format, arg0, arg1);

        public override void Write(string format, object arg0, object arg1, object arg2)
        => Inner.Write(format, arg0, arg1, arg2);

        public override void Write(string format, params object[] arg)
        => Inner.Write(format, arg);

        public override void Write(string value)
        => Inner.Write(value);

        public override void Write(uint value)
        => Inner.Write(value);

        public override void Write(ulong value)
        => Inner.Write(value);

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
        => Inner.WriteLine();

        public override void WriteLine(bool value)
        => Inner.WriteLine(value);

        public override void WriteLine(char value)
        => Inner.WriteLine(value);

        public override void WriteLine(char[] buffer)
        => Inner.WriteLine(buffer);

        public override void WriteLine(char[] buffer, int index, int count)
        => Inner.WriteLine(buffer, index, count);

        public override void WriteLine(decimal value)
        => Inner.WriteLine(value);

        public override void WriteLine(double value)
        => Inner.WriteLine(value);

        public override void WriteLine(float value)
        => Inner.WriteLine(value);

        public override void WriteLine(int value)
        => Inner.WriteLine(value);

        public override void WriteLine(long value)
        => Inner.WriteLine(value);

        public override void WriteLine(object value)
        => Inner.WriteLine(value);

        public override void WriteLine(ReadOnlySpan<char> buffer)
        => Inner.WriteLine(buffer);

        public override void WriteLine(string format, object arg0)
        => Inner.WriteLine(format, arg0);

        public override void WriteLine(string format, object arg0, object arg1)
        => Inner.WriteLine(format, arg0, arg1);

        public override void WriteLine(string format, object arg0, object arg1, object arg2)
        => Inner.WriteLine(format, arg0, arg1, arg2);

        public override void WriteLine(string format, params object[] arg)
        => Inner.WriteLine(format, arg);

        public override void WriteLine(string value)
        => Inner.WriteLine(value);

        public override void WriteLine(uint value)
        => Inner.WriteLine(value);

        public override void WriteLine(ulong value)
        => Inner.WriteLine(value);

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
