using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class AsyncTextReaderAdapter : IAsyncReaderAdapter
    {
        public bool IsDisposed => Inner == null;

        private TextReader Inner;

        public AsyncTextReaderAdapter(TextReader inner)
        {
            Inner = inner;
        }

        public ValueTask<int> ReadAsync(Memory<char> into, CancellationToken cancel)
        {
            AssertNotDisposed(this);

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
                Inner = null;

                return disposeTask;
            }

            Inner.Dispose();
            Inner = null;

            return default;
        }
    }
}
