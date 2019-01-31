using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class RowEndingDetectorTests
    {
        // just a stub for helper purposes
        class _Foo { public string A { get; set; } }

        private class _Test
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        [Theory]
        [InlineData("foo\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\nfizz,buzz", RowEndings.CarriageReturnLineFeed)]
        [InlineData("\"foo bar\"\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("\"foo\r\nbar\"\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("\"foo\rbar\"\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("\"foo\nbar\"\r\n", RowEndings.CarriageReturnLineFeed)]

        [InlineData("foo\n", RowEndings.LineFeed)]
        [InlineData("foo,bar\n", RowEndings.LineFeed)]
        [InlineData("foo,bar\nfizz,buzz", RowEndings.LineFeed)]
        [InlineData("\"foo bar\"\n", RowEndings.LineFeed)]
        [InlineData("\"foo\r\nbar\"\n", RowEndings.LineFeed)]
        [InlineData("\"foo\rbar\"\n", RowEndings.LineFeed)]
        [InlineData("\"foo\nbar\"\n", RowEndings.LineFeed)]

        [InlineData("foo\r", RowEndings.CarriageReturn)]
        [InlineData("foo,bar\r", RowEndings.CarriageReturn)]
        [InlineData("foo,bar\rfizz,buzz", RowEndings.CarriageReturn)]
        [InlineData("\"foo bar\"\r", RowEndings.CarriageReturn)]
        [InlineData("\"foo\r\nbar\"\r", RowEndings.CarriageReturn)]
        [InlineData("\"foo\rbar\"\r", RowEndings.CarriageReturn)]
        [InlineData("\"foo\nbar\"\r", RowEndings.CarriageReturn)]
        public void Sync(string csv, RowEndings expected)
        {
            var config = Configuration.For<_Test>(Options.Default.NewBuilder().WithReadHeader(ReadHeaders.None).WithRowEnding(RowEndings.Detect).BuildInternal());

            using (var str = new StringReader(csv))
            {
                using (var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar))
                {
                    var detector = new RowEndingDetector<_Test>(config, charLookup, str);
                    var detect = detector.Detect();
                    Assert.True(detect.HasValue);
                    Assert.Equal(expected, detect.Value.Ending);
                }
            }
        }

        [Theory]
        [InlineData("foo\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\nfizz,buzz", RowEndings.CarriageReturnLineFeed)]
        [InlineData("\"foo bar\"\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("\"foo\r\nbar\"\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("\"foo\rbar\"\r\n", RowEndings.CarriageReturnLineFeed)]
        [InlineData("\"foo\nbar\"\r\n", RowEndings.CarriageReturnLineFeed)]

        [InlineData("foo\n", RowEndings.LineFeed)]
        [InlineData("foo,bar\n", RowEndings.LineFeed)]
        [InlineData("foo,bar\nfizz,buzz", RowEndings.LineFeed)]
        [InlineData("\"foo bar\"\n", RowEndings.LineFeed)]
        [InlineData("\"foo\r\nbar\"\n", RowEndings.LineFeed)]
        [InlineData("\"foo\rbar\"\n", RowEndings.LineFeed)]
        [InlineData("\"foo\nbar\"\n", RowEndings.LineFeed)]

        [InlineData("foo\r", RowEndings.CarriageReturn)]
        [InlineData("foo,bar\r", RowEndings.CarriageReturn)]
        [InlineData("foo,bar\rfizz,buzz", RowEndings.CarriageReturn)]
        [InlineData("\"foo bar\"\r", RowEndings.CarriageReturn)]
        [InlineData("\"foo\r\nbar\"\r", RowEndings.CarriageReturn)]
        [InlineData("\"foo\rbar\"\r", RowEndings.CarriageReturn)]
        [InlineData("\"foo\nbar\"\r", RowEndings.CarriageReturn)]
        public async Task Async(string csv, RowEndings expected)
        {
            var config = Configuration.For<_Test>(Options.Default.NewBuilder().WithReadHeader(ReadHeaders.None).WithRowEnding(RowEndings.Detect).BuildInternal());

            await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader(csv))
                        {
                            using (var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar))
                            {
                                var detector = new RowEndingDetector<_Test>(config, charLookup, str);
                                var detect = await detector.DetectAsync(CancellationToken.None);
                                Assert.True(detect.HasValue);
                                Assert.Equal(expected, detect.Value.Ending);
                            }
                        }
                    }
             );
        }
    }
}
