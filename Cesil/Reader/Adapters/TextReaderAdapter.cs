using System;
using System.IO;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class TextReaderAdapter : IReaderAdapter
    {
        public bool IsDisposed => Inner == null;

        private TextReader Inner;

        public TextReaderAdapter(TextReader inner)
        {
            Inner = inner;
        }

        public int Read(Span<char> into)
        {
            AssertNotDisposed(this);

            return Inner.Read(into);
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                Inner.Dispose();
                Inner = null;
            }
        }
    }
}
