using System;
using System.Buffers;

namespace Cesil
{
    internal sealed class EmptyMemoryOwner : IMemoryOwner<char>
    {
        public static readonly EmptyMemoryOwner Singleton = new EmptyMemoryOwner();

        public Memory<char> Memory => Memory<char>.Empty;

        private EmptyMemoryOwner() { }

        public void Dispose() { }

        public override string ToString()
        => $"{nameof(EmptyMemoryOwner)} Singleton";
    }
}
