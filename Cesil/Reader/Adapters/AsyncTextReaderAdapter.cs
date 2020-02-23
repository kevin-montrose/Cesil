using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class AsyncTextReaderAdapter : IAsyncReaderAdapter
    {
        public bool IsDisposed { get; private set; }

        private readonly TextReader Inner;

        public AsyncTextReaderAdapter(TextReader inner)
        {
            Inner = inner;
        }

        public ValueTask<int> ReadAsync(Memory<char> into, CancellationToken cancel)
        {
            AssertNotDisposedInternal(this);

            return Inner.ReadAsync(into, cancel);
        }

        public ValueTask DisposeAsync()
        {
            if (IsDisposed)
            {
                return default;
            }

            if (Inner is IAsyncDisposable iad)
            {
                var disposeTask = iad.DisposeAsync();
                IsDisposed = true;

                return disposeTask;
            }

            Inner.Dispose();
            IsDisposed = true;

            return default;
        }
    }
}
