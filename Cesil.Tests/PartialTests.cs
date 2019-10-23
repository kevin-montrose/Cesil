using System;
using System.Buffers;
using Xunit;

namespace Cesil.Tests
{
    public class PartialTests
    {
        [Fact]
        public void Empty()
        {
            var partial = new Partial<object>(MemoryPool<char>.Shared);

            var s = partial.PendingAsString(ReadOnlyMemory<char>.Empty);

            Assert.Equal("", s);
        }
    }
}
