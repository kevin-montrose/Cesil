using System;
using System.IO;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class TextWriterAdapter : IWriterAdapter
    {
        public bool IsDisposed { get; private set; }

        private readonly TextWriter Inner;

        internal TextWriterAdapter(TextWriter inner)
        {
            Inner = inner;
        }

        public void Write(char c)
        {
            AssertNotDisposedInternal(this);

            Inner.Write(c);
        }

        public void Write(ReadOnlySpan<char> chars)
        {
            AssertNotDisposedInternal(this);

            Inner.Write(chars);
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                Inner.Dispose();
                IsDisposed = true;
            }
        }
    }
}
