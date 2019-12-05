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
        [InlineData("foo\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\nfizz,buzz", RowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo bar\"\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\r\nbar\"\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\rbar\"\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\nbar\"\r\n", RowEnding.CarriageReturnLineFeed)]

        [InlineData("foo\n", RowEnding.LineFeed)]
        [InlineData("foo,bar\n", RowEnding.LineFeed)]
        [InlineData("foo,bar\nfizz,buzz", RowEnding.LineFeed)]
        [InlineData("\"foo bar\"\n", RowEnding.LineFeed)]
        [InlineData("\"foo\r\nbar\"\n", RowEnding.LineFeed)]
        [InlineData("\"foo\rbar\"\n", RowEnding.LineFeed)]
        [InlineData("\"foo\nbar\"\n", RowEnding.LineFeed)]

        [InlineData("foo\r", RowEnding.CarriageReturn)]
        [InlineData("foo,bar\r", RowEnding.CarriageReturn)]
        [InlineData("foo,bar\rfizz,buzz", RowEnding.CarriageReturn)]
        [InlineData("\"foo bar\"\r", RowEnding.CarriageReturn)]
        [InlineData("\"foo\r\nbar\"\r", RowEnding.CarriageReturn)]
        [InlineData("\"foo\rbar\"\r", RowEnding.CarriageReturn)]
        [InlineData("\"foo\nbar\"\r", RowEnding.CarriageReturn)]
        public void Sync(string csv, RowEnding expected)
        {
            var config = (ConcreteBoundConfiguration<_Test>)Configuration.For<_Test>(Options.CreateBuilder(Options.Default).WithReadHeaderInternal(default).WithRowEnding(RowEnding.Detect).BuildInternal());

            using (var str = new StringReader(csv))
            {
                using (var charLookup = CharacterLookup.MakeCharacterLookup(config.Options.MemoryPool, config.Options.EscapedValueStartAndEnd, config.Options.ValueSeparator, config.Options.EscapedValueEscapeCharacter, config.Options.CommentCharacter, false, out _))
                {
                    var detector = new RowEndingDetector(new ReaderStateMachine(), config.Options, charLookup, new TextReaderAdapter(str));
                    var detect = detector.Detect();
                    Assert.True(detect.HasValue);
                    Assert.Equal(expected, detect.Value.Ending);
                }
            }
        }

        [Theory]
        [InlineData("foo\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\nfizz,buzz", RowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo bar\"\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\r\nbar\"\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\rbar\"\r\n", RowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\nbar\"\r\n", RowEnding.CarriageReturnLineFeed)]

        [InlineData("foo\n", RowEnding.LineFeed)]
        [InlineData("foo,bar\n", RowEnding.LineFeed)]
        [InlineData("foo,bar\nfizz,buzz", RowEnding.LineFeed)]
        [InlineData("\"foo bar\"\n", RowEnding.LineFeed)]
        [InlineData("\"foo\r\nbar\"\n", RowEnding.LineFeed)]
        [InlineData("\"foo\rbar\"\n", RowEnding.LineFeed)]
        [InlineData("\"foo\nbar\"\n", RowEnding.LineFeed)]

        [InlineData("foo\r", RowEnding.CarriageReturn)]
        [InlineData("foo,bar\r", RowEnding.CarriageReturn)]
        [InlineData("foo,bar\rfizz,buzz", RowEnding.CarriageReturn)]
        [InlineData("\"foo bar\"\r", RowEnding.CarriageReturn)]
        [InlineData("\"foo\r\nbar\"\r", RowEnding.CarriageReturn)]
        [InlineData("\"foo\rbar\"\r", RowEnding.CarriageReturn)]
        [InlineData("\"foo\nbar\"\r", RowEnding.CarriageReturn)]
        public async Task Async(string csv, RowEnding expected)
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeaderInternal(default).WithRowEnding(RowEnding.Detect).BuildInternal();

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
                            using (var charLookup = CharacterLookup.MakeCharacterLookup(cInner.Options.MemoryPool, cInner.Options.EscapedValueStartAndEnd, cInner.Options.ValueSeparator, cInner.Options.EscapedValueEscapeCharacter, cInner.Options.CommentCharacter, false, out _))
                            using (var detector = new RowEndingDetector(stateMachine, cInner.Options, charLookup, str))
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
