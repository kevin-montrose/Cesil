using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class AsyncTextWriterAdapter : IAsyncWriterAdapter
    {
        public bool IsDisposed { get; private set; }

        private readonly TextWriter Inner;

        internal AsyncTextWriterAdapter(TextWriter inner)
        {
            Inner = inner;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<char> chars, CancellationToken cancel)
        {
            AssertNotDisposedInternal(this);

            var ret = Inner.WriteAsync(chars, cancel);
            if (ret.IsCompletedSuccessfully)
            {
                return default;
            }

            return new ValueTask(ret);
        }

        public ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                var ret = Inner.DisposeAsync();
                IsDisposed = true;

                return ret;
            }

            return default;
        }
    }
}
