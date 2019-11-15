using System;
using System.Buffers;
using Xunit;

namespace Cesil.Tests
{
    public class MaybeInPlaceBufferTests
    {
        [Fact]
        public void InPlace()
        {
            var mem = "hello world".AsMemory();
            var span = mem.Span;

            using (var buffer = new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared))
            {
                Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Uninitialized, buffer.CurrentMode);
                buffer.Append(span, 0, 2);
                Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);
                buffer.Append(span, 2, 3);
                Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);
                buffer.Append(span, 5, 4);
                Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);
                buffer.Append(span, 9, 2);
                Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);

                var res = buffer.AsMemory(mem);
                var str = new string(res.Span);
                Assert.Equal("hello world", str);
            }
        }

        [Fact]
        public void Skip()
        {
            // from InPlace
            {
                var mem = "hello world".AsMemory();
                var span = mem.Span;

                using (var buffer = new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared))
                {
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Uninitialized, buffer.CurrentMode);
                    buffer.Append(span, 0, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);
                    buffer.Skipped();
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.CopyOnNextAppend, buffer.CurrentMode);
                    buffer.Append(span, 6, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);

                    var res = buffer.AsMemory(mem);
                    var str = new string(res.Span);
                    Assert.Equal("helloworld", str);
                }
            }

            // from CopyOnNextAppend
            {
                var mem = "hello world".AsMemory();
                var span = mem.Span;

                using (var buffer = new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared))
                {
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Uninitialized, buffer.CurrentMode);
                    buffer.Append(span, 0, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);
                    buffer.Skipped();
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.CopyOnNextAppend, buffer.CurrentMode);
                    buffer.Skipped();
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.CopyOnNextAppend, buffer.CurrentMode);
                    buffer.Append(span, 6, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);

                    var res = buffer.AsMemory(mem);
                    var str = new string(res.Span);
                    Assert.Equal("helloworld", str);
                }
            }

            // from Copy
            {
                var mem = "hello world".AsMemory();
                var span = mem.Span;

                using (var buffer = new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared))
                {
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Uninitialized, buffer.CurrentMode);
                    buffer.Append(span, 0, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);
                    buffer.Skipped();
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.CopyOnNextAppend, buffer.CurrentMode);
                    buffer.Skipped();
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.CopyOnNextAppend, buffer.CurrentMode);
                    buffer.Append(span, 6, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);
                    buffer.Skipped();
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);

                    var res = buffer.AsMemory(mem);
                    var str = new string(res.Span);
                    Assert.Equal("helloworld", str);
                }
            }
        }

        [Fact]
        public void AppendSingle()
        {
            var mem = "hello world".AsMemory();
            var span = mem.Span;

            // from Uninitialized
            {
                using (var buffer = new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared))
                {
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Uninitialized, buffer.CurrentMode);
                    buffer.AppendSingle(ReadOnlySpan<char>.Empty, 'r');
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);
                    buffer.AppendSingle(ReadOnlySpan<char>.Empty, 'e');
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);
                    buffer.Append(span, 0, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);

                    var res = buffer.AsMemory(mem);
                    var str = new string(res.Span);
                    Assert.Equal("rehello", str);
                }
            }

            // from InPlace
            {
                using (var buffer = new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared))
                {
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Uninitialized, buffer.CurrentMode);
                    buffer.Append(span, 0, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);
                    buffer.AppendSingle(span, '-');
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);
                    buffer.Append(span, 6, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);

                    var res = buffer.AsMemory(mem);
                    var str = new string(res.Span);
                    Assert.Equal("hello-world", str);
                }

                // at length
                using (var buffer = new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared))
                {
                    var expectedLength = 0;

                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Uninitialized, buffer.CurrentMode);
                    buffer.AppendSingle(ReadOnlySpan<char>.Empty, 'r');
                    expectedLength++;
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);
                    while (buffer.Length < buffer.Copy.Length)
                    {
                        buffer.AppendSingle(ReadOnlySpan<char>.Empty, 'e');
                        Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);
                        expectedLength++;
                    }

                    buffer.AppendSingle(ReadOnlySpan<char>.Empty, 'd');
                    expectedLength++;
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);

                    var res = buffer.AsMemory(mem);
                    var str = new string(res.Span);
                    Assert.Equal(expectedLength, str.Length);
                    var expected = "r" + string.Join("", System.Linq.Enumerable.Repeat('e', expectedLength - 2)) + "d";
                    Assert.Equal(expected, str);
                }
            }

            // from CopyOnNextAppend
            {
                using (var buffer = new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared))
                {
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Uninitialized, buffer.CurrentMode);
                    buffer.Append(span, 0, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);
                    buffer.Skipped();
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.CopyOnNextAppend, buffer.CurrentMode);
                    buffer.AppendSingle(span, '-');
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);
                    buffer.Append(span, 6, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);

                    var res = buffer.AsMemory(mem);
                    var str = new string(res.Span);
                    Assert.Equal("hello-world", str);
                }
            }

            // from Copy
            {
                using (var buffer = new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared))
                {
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Uninitialized, buffer.CurrentMode);
                    buffer.Append(span, 0, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.InPlace, buffer.CurrentMode);
                    buffer.Skipped();
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.CopyOnNextAppend, buffer.CurrentMode);
                    buffer.AppendSingle(span, '-');
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);
                    buffer.Append(span, 6, 5);
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);
                    buffer.AppendSingle(span, '!');
                    Assert.Equal(MaybeInPlaceBuffer<char>.Mode.Copy, buffer.CurrentMode);

                    var res = buffer.AsMemory(mem);
                    var str = new string(res.Span);
                    Assert.Equal("hello-world!", str);
                }
            }
        }
    }
}
