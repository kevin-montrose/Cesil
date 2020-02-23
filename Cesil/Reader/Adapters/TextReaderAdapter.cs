using System;
using System.IO;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class TextReaderAdapter : IReaderAdapter
    {
        public bool IsDisposed { get; private set; }

        private readonly TextReader Inner;

        public TextReaderAdapter(TextReader inner)
        {
            Inner = inner;
        }

        public int Read(Span<char> into)
        {
            AssertNotDisposedInternal(this);

            return Inner.Read(into);
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
