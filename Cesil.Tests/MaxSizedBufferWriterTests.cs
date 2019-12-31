using System;
using System.Buffers;
using Xunit;

namespace Cesil.Tests
{
    public class MaxSizedBufferWriterTests
    {
        private sealed class FixSizeMemoryPool : MemoryPool<char>
        {
            private sealed class Owner : IMemoryOwner<char>
            {
                public Memory<char> Memory { get; }

                public Owner(Memory<char> mem)
                {
                    Memory = mem;
                }

                public void Dispose() { }
            }

            private readonly char[] Buffer;

            public override int MaxBufferSize { get; }

            public FixSizeMemoryPool(int size)
            {
                MaxBufferSize = size;
                Buffer = new char[MaxBufferSize];
            }

            public override IMemoryOwner<char> Rent(int minBufferSize = -1)
            {
                return new Owner(Buffer.AsMemory());
            }

            protected override void Dispose(bool disposing) { }
        }

        [Fact]
        public void Errors()
        {
            // out of range
            {
                var writer = new MaxSizedBufferWriter(MemoryPool<char>.Shared, null);

                // advance
                Assert.Throws<ArgumentException>(() => writer.Advance(-1));

                // get memory
                Assert.Throws<ArgumentException>(() => writer.GetMemory(-1));
            }

            // too large request
            {
                var writer = new MaxSizedBufferWriter(new FixSizeMemoryPool(16), null);

                Assert.Throws<InvalidOperationException>(() => writer.GetMemory(17));
            }
        }

        [Fact]
        public void Empty()
        {
            var writer = new MaxSizedBufferWriter(MemoryPool<char>.Shared, null);
            var buff = writer.Buffer;
            Assert.True(buff.IsEmpty);
        }

        [Fact]
        public void SingleSegment()
        {
            // one advance call
            {
                var writer = new MaxSizedBufferWriter(MemoryPool<char>.Shared, null);

                {
                    var charSpan = writer.GetSpan(8);
                    "01234567".AsSpan().CopyTo(charSpan);
                    writer.Advance(8);
                }

                var buff = writer.Buffer;
                Assert.False(buff.IsEmpty);
                Assert.True(buff.IsSingleSegment);

                var resChars = buff.ToArray();
                var resCharSpan = new ReadOnlySpan<char>(resChars);
                var str = new string(resCharSpan);

                Assert.Equal("01234567", str);
            }

            // two advance calls
            {
                var writer = new MaxSizedBufferWriter(MemoryPool<char>.Shared, null);

                {
                    var charSpan = writer.GetSpan(16);
                    "01234567".AsSpan().CopyTo(charSpan);
                    writer.Advance(8);
                }

                {
                    var charSpan = writer.GetSpan(4);
                    "89AB".AsSpan().CopyTo(charSpan);
                    writer.Advance(4);
                }

                var buff = writer.Buffer;
                Assert.False(buff.IsEmpty);
                Assert.True(buff.IsSingleSegment);

                var resChars = buff.ToArray();
                var resCharSpan = new ReadOnlySpan<char>(resChars);
                var str = new string(resCharSpan);

                Assert.Equal("0123456789AB", str);
            }

        }

        [Fact]
        public void MulitSegment()
        {
            var writer = new MaxSizedBufferWriter(MemoryPool<char>.Shared, null);

            {
                var charSpan = writer.GetSpan(10);
                "01234567".AsSpan().CopyTo(charSpan);
                writer.Advance(8);
            }

            {
                var charSpan = writer.GetSpan(10_000);
                "abc".AsSpan().CopyTo(charSpan);
                writer.Advance(3);
            }

            var buff = writer.Buffer;
            Assert.False(buff.IsEmpty);
            Assert.False(buff.IsSingleSegment);

            var resChars = buff.ToArray();
            var resCharSpan = new ReadOnlySpan<char>(resChars);
            var str = new string(resCharSpan);

            Assert.Equal("01234567abc", str);
        }

        [Fact]
        public void Reset()
        {
            var writer = new MaxSizedBufferWriter(MemoryPool<char>.Shared, null);

            {
                var charSpan = writer.GetSpan(10);
                "01234567".AsSpan().CopyTo(charSpan);
                writer.Advance(8);
            }

            writer.Reset();

            {
                var charSpan = writer.GetSpan(3);
                "abc".AsSpan().CopyTo(charSpan);
                writer.Advance(3);
            }

            var buff = writer.Buffer;
            Assert.False(buff.IsEmpty);
            Assert.True(buff.IsSingleSegment);

            var resChars = buff.ToArray();
            var resCharSpan = new ReadOnlySpan<char>(resChars);
            var str = new string(resCharSpan);

            Assert.Equal("abc", str);

            writer.Reset();
            Assert.True(writer.Buffer.IsEmpty);
        }
    }
}
