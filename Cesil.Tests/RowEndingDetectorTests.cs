using System;
using System.Buffers;
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
        [InlineData("foo", RowEnding.CarriageReturnLineFeed)]

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

        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r", RowEnding.CarriageReturn, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\n", RowEnding.LineFeed, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r\n", RowEnding.CarriageReturnLineFeed, "###")]

        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r123", RowEnding.CarriageReturn, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\n123", RowEnding.LineFeed, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r\n123", RowEnding.CarriageReturnLineFeed, "###")]
        public void Sync(string csv, RowEnding expected, string valueSep = ",")
        {
            var config = 
                (ConcreteBoundConfiguration<_Test>)
                    Configuration.For<_Test>(
                        Options.CreateBuilder(Options.Default)
                        .WithRowEnding(RowEnding.Detect)
                        .WithValueSeparator(valueSep)
                        .BuildInternal()
                    );

            using (var str = new StringReader(csv))
            {
                using (var charLookup = CharacterLookup.MakeCharacterLookup(config.Options, MemoryPool<char>.Shared, out _))
                {
                    var detector = new RowEndingDetector(new ReaderStateMachine(), config.Options, MemoryPool<char>.Shared, charLookup, new TextReaderAdapter(str), config.Options.ValueSeparator.AsMemory());
                    var detect = detector.Detect();
                    Assert.True(detect.HasValue);
                    Assert.Equal(expected, detect.Value.Ending);
                }
            }
        }

        [Theory]
        [InlineData("foo", RowEnding.CarriageReturnLineFeed)]

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

        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r", RowEnding.CarriageReturn, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\n", RowEnding.LineFeed, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r\n", RowEnding.CarriageReturnLineFeed, "###")]

        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r123", RowEnding.CarriageReturn, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\n123", RowEnding.LineFeed, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r\n123", RowEnding.CarriageReturnLineFeed, "###")]
        public async Task Async(string csv, RowEnding expected, string valueSep = ",")
        {
            var opts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.Detect).WithValueSeparator(valueSep).BuildInternal();

            await RunAsyncReaderVariants<_Test>(
                    opts,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_Test>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_Test>;
                        var configCancel = config as AsyncCancelControlConfig<_Test>;
                        var cInner = (ConcreteBoundConfiguration<_Test>)(configUnpin?.Inner ?? configForced?.Inner ?? configCancel?.Inner ?? config);

                        await using (var str = await getReader(csv))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using (var charLookup = CharacterLookup.MakeCharacterLookup(cInner.Options, MemoryPool<char>.Shared, out _))
                            using (var detector = new RowEndingDetector(stateMachine, cInner.Options, MemoryPool<char>.Shared, charLookup, str, cInner.Options.ValueSeparator.AsMemory()))
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
                    },
                    cancellable: false
             );
        }
    }
}
