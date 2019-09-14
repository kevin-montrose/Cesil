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
        private class _Foo { public string A { get; set; } }

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
            var config = (ConcreteBoundConfiguration<_Test>)Configuration.For<_Test>(Options.Default.NewBuilder().WithReadHeaderInternal(default).WithRowEnding(RowEndings.Detect).BuildInternal());

            using (var str = new StringReader(csv))
            {
                using (var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _))
                {
                    var detector = new RowEndingDetector<_Test>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str));
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
            var opts = Options.Default.NewBuilder().WithReadHeaderInternal(default).WithRowEnding(RowEndings.Detect).BuildInternal();

            await RunAsyncReaderVariants<_Test>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_Test>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_Test>;
                        var cInner = (ConcreteBoundConfiguration<_Test>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader(csv))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using (var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _))
                            using (var detector = new RowEndingDetector<_Test>(stateMachine, cInner, charLookup, str))
                            {
                                if (configForced != null)
                                {
                                    configForced.Set(detector);
                                }

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
