using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class AsyncTextWriterAdapter : IAsyncWriterAdapter
    {
        public bool IsDisposed => Inner == null;

        private TextWriter Inner;

        public AsyncTextWriterAdapter(TextWriter inner)
        {
            Inner = inner;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<char> chars, CancellationToken cancel)
        {
            AssertNotDisposed(this);

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
                Inner = null;

                return ret;
            }

            return default;
        }
    }
}
