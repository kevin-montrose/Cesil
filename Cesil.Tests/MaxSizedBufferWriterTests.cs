using System;
using System.Buffers;
using Xunit;

namespace Cesil.Tests
{
    public class MaxSizedBufferWriterTests
    {
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
