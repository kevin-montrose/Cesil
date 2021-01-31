using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class BufferWithPushbackTests
    {
        [Fact]
        public void Read()
        {
            const string TEXT = "hello world";

            using (var str = new StringReader(TEXT))
            {
                var buf =
                    new BufferWithPushback(
                        MemoryPool<char>.Shared,
                        500
                    );
                var bytes = buf.Read(new TextReaderAdapter(str), true);
                Assert.Equal(TEXT.Length, bytes);

                var shouldMatch = new string(buf.Buffer.Span.Slice(0, bytes));
                Assert.Equal(TEXT, shouldMatch);
            }
        }

        // just a stub for helper purposes
        private class _Foo { public string A { get; set; } }

        [Fact]
        public async Task ReadAsync()
        {
            const string TEXT = "hello world";

            await RunAsyncReaderVariants<_Foo>(
                Options.Default,
                async (_, getReader) =>
                {
                    await using (var str = await getReader(TEXT))
                    {
                        var buf =
                            new BufferWithPushback(
                                MemoryPool<char>.Shared,
                                500
                            );
                        var bytes = await buf.ReadAsync(str, true, CancellationToken.None);
                        Assert.Equal(TEXT.Length, bytes);

                        var shouldMatch = new string(buf.Buffer.Span.Slice(0, bytes));
                        Assert.Equal(TEXT, shouldMatch);
                    }
                },
                cancellable: false
            );
        }

        [Fact]
        public void ReadAndPushback()
        {
            const string FIRST_TEXT = "hello ";
            const string SECOND_TEXT = "world";
            const string TEXT = FIRST_TEXT + SECOND_TEXT;

            using (var str = new StringReader(TEXT))
            {
                var buf =
                    new BufferWithPushback(
                        MemoryPool<char>.Shared,
                        500
                    );
                var bytes = buf.Read(new TextReaderAdapter(str), true);
                Assert.Equal(TEXT.Length, bytes);

                var shouldMatch = new string(buf.Buffer.Span.Slice(0, bytes));
                Assert.Equal(TEXT, shouldMatch);

                buf.PushBackFromBuffer(bytes, SECOND_TEXT.Length);

                var bytes2 = buf.Read(new TextReaderAdapter(str), true);

                Assert.Equal(SECOND_TEXT.Length, bytes2);

                var shouldMatch2 = new string(buf.Buffer.Span.Slice(0, bytes2));
                Assert.Equal(SECOND_TEXT, shouldMatch2);
            }
        }

        [Fact]
        public async Task ReadAsyncAndPushback()
        {
            const string FIRST_TEXT = "hello ";
            const string SECOND_TEXT = "world";
            const string TEXT = FIRST_TEXT + SECOND_TEXT;

            await RunAsyncReaderVariants<_Foo>(
                Options.Default,
                async (_, getReader) =>
                {
                    await using (var str = await getReader(TEXT))
                    {
                        var buf =
                            new BufferWithPushback(
                                MemoryPool<char>.Shared,
                                500
                            );
                        var bytes = await buf.ReadAsync(str, true, CancellationToken.None);
                        Assert.Equal(TEXT.Length, bytes);

                        var shouldMatch = new string(buf.Buffer.Span.Slice(0, bytes));
                        Assert.Equal(TEXT, shouldMatch);

                        buf.PushBackFromBuffer(bytes, SECOND_TEXT.Length);

                        var bytes2 = await buf.ReadAsync(str, true, CancellationToken.None);

                        Assert.Equal(SECOND_TEXT.Length, bytes2);

                        var shouldMatch2 = new string(buf.Buffer.Span.Slice(0, bytes2));
                        Assert.Equal(SECOND_TEXT, shouldMatch2);
                    }
                },
                cancellable: false
            );
        }
    }
#pragma warning restore IDE1006
}
