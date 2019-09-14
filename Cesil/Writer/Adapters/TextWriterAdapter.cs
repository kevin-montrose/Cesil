using System;
using System.IO;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class TextWriterAdapter : IWriterAdapter
    {
        public bool IsDisposed => Inner == null;

        private TextWriter Inner;

        public TextWriterAdapter(TextWriter inner)
        {
            Inner = inner;
        }

        public void Write(char c)
        {
            AssertNotDisposed(this);

            Inner.Write(c);
        }

        public void Write(ReadOnlySpan<char> chars)
        {
            AssertNotDisposed(this);

            Inner.Write(chars);
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
