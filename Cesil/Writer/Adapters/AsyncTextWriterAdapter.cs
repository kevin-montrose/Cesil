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

        public ValueTask WriteAsync(ReadOnlyMemory<char> chars, CancellationToken cancellationToken)
        {
            AssertNotDisposedInternal(this);

#pragma warning disable CES0001 // this is a simple wrapper, don't need to introduce a transition point
            var ret = Inner.WriteAsync(chars, cancellationToken);
            if (ret.IsCompletedSuccessfully)
            {
                return default;
            }
#pragma warning restore CES0001

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
