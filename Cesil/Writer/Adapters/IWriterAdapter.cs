using System;

namespace Cesil
{
    internal interface IWriterAdapter : ITestableDisposable
    {
        void Write(char c);
        void Write(ReadOnlySpan<char> chars);
    }
}
