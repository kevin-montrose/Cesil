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
        [InlineData("foo", ReadRowEnding.CarriageReturnLineFeed)]

        [InlineData("foo\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\nfizz,buzz", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo bar\"\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\r\nbar\"\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\rbar\"\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\nbar\"\r\n", ReadRowEnding.CarriageReturnLineFeed)]

        [InlineData("foo\n", ReadRowEnding.LineFeed)]
        [InlineData("foo,bar\n", ReadRowEnding.LineFeed)]
        [InlineData("foo,bar\nfizz,buzz", ReadRowEnding.LineFeed)]
        [InlineData("\"foo bar\"\n", ReadRowEnding.LineFeed)]
        [InlineData("\"foo\r\nbar\"\n", ReadRowEnding.LineFeed)]
        [InlineData("\"foo\rbar\"\n", ReadRowEnding.LineFeed)]
        [InlineData("\"foo\nbar\"\n", ReadRowEnding.LineFeed)]

        [InlineData("foo\r", ReadRowEnding.CarriageReturn)]
        [InlineData("foo,bar\r", ReadRowEnding.CarriageReturn)]
        [InlineData("foo,bar\rfizz,buzz", ReadRowEnding.CarriageReturn)]
        [InlineData("\"foo bar\"\r", ReadRowEnding.CarriageReturn)]
        [InlineData("\"foo\r\nbar\"\r", ReadRowEnding.CarriageReturn)]
        [InlineData("\"foo\rbar\"\r", ReadRowEnding.CarriageReturn)]
        [InlineData("\"foo\nbar\"\r", ReadRowEnding.CarriageReturn)]

        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r", ReadRowEnding.CarriageReturn, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\n", ReadRowEnding.LineFeed, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r\n", ReadRowEnding.CarriageReturnLineFeed, "###")]

        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r123", ReadRowEnding.CarriageReturn, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\n123", ReadRowEnding.LineFeed, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r\n123", ReadRowEnding.CarriageReturnLineFeed, "###")]
        public void Sync(string csv, ReadRowEnding expected, string valueSep = ",")
        {
            var config =
                (ConcreteBoundConfiguration<_Test>)
                    Configuration.For<_Test>(
                        Options.CreateBuilder(Options.Default)
                        .WithReadRowEnding(ReadRowEnding.Detect)
                        .WithValueSeparator(valueSep)
                        .BuildInternal()
                    );

            using (var str = new StringReader(csv))
            {
                var charLookup = CharacterLookup.MakeCharacterLookup(config.Options, out _);
                {
                    var detector = new RowEndingDetector(new ReaderStateMachine(), config.Options, MemoryPool<char>.Shared, charLookup, new TextReaderAdapter(str), config.Options.ValueSeparator.AsMemory());
                    var detect = detector.Detect();
                    Assert.True(detect.HasValue);
                    Assert.Equal(expected, detect.Value.Ending);
                }
            }
        }

        [Theory]
        [InlineData("#abcd", ReadRowEnding.CarriageReturnLineFeed)]
        
        [InlineData("#abcd\r", ReadRowEnding.CarriageReturn)]
        [InlineData("#abcd\n", ReadRowEnding.LineFeed)]
        [InlineData("#abcd\r\n", ReadRowEnding.CarriageReturnLineFeed)]

        [InlineData("#abcd\rfoo", ReadRowEnding.CarriageReturn)]
        [InlineData("#abcd\nfoo", ReadRowEnding.LineFeed)]
        [InlineData("#abcd\r\nfoo", ReadRowEnding.CarriageReturnLineFeed)]
        public void CommentsAsFirstRecord(string csv, ReadRowEnding expected)
        {
            Assert.StartsWith("#", csv);

            var config =
                (ConcreteBoundConfiguration<_Test>)
                    Configuration.For<_Test>(
                        Options.CreateBuilder(Options.Default)
                        .WithReadRowEnding(ReadRowEnding.Detect)
                        .WithCommentCharacter('#')
                        .WithValueSeparator(",")
                        .BuildInternal()
                    );

            using (var str = new StringReader(csv))
            {
                var charLookup = CharacterLookup.MakeCharacterLookup(config.Options, out _);
                {
                    var detector = new RowEndingDetector(new ReaderStateMachine(), config.Options, MemoryPool<char>.Shared, charLookup, new TextReaderAdapter(str), config.Options.ValueSeparator.AsMemory());
                    var detect = detector.Detect();
                    Assert.True(detect.HasValue);
                    Assert.Equal(expected, detect.Value.Ending);
                }
            }
        }

        // async tests

        [Theory]
        [InlineData("foo", ReadRowEnding.CarriageReturnLineFeed)]

        [InlineData("foo\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("foo,bar\r\nfizz,buzz", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo bar\"\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\r\nbar\"\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\rbar\"\r\n", ReadRowEnding.CarriageReturnLineFeed)]
        [InlineData("\"foo\nbar\"\r\n", ReadRowEnding.CarriageReturnLineFeed)]

        [InlineData("foo\n", ReadRowEnding.LineFeed)]
        [InlineData("foo,bar\n", ReadRowEnding.LineFeed)]
        [InlineData("foo,bar\nfizz,buzz", ReadRowEnding.LineFeed)]
        [InlineData("\"foo bar\"\n", ReadRowEnding.LineFeed)]
        [InlineData("\"foo\r\nbar\"\n", ReadRowEnding.LineFeed)]
        [InlineData("\"foo\rbar\"\n", ReadRowEnding.LineFeed)]
        [InlineData("\"foo\nbar\"\n", ReadRowEnding.LineFeed)]

        [InlineData("foo\r", ReadRowEnding.CarriageReturn)]
        [InlineData("foo,bar\r", ReadRowEnding.CarriageReturn)]
        [InlineData("foo,bar\rfizz,buzz", ReadRowEnding.CarriageReturn)]
        [InlineData("\"foo bar\"\r", ReadRowEnding.CarriageReturn)]
        [InlineData("\"foo\r\nbar\"\r", ReadRowEnding.CarriageReturn)]
        [InlineData("\"foo\rbar\"\r", ReadRowEnding.CarriageReturn)]
        [InlineData("\"foo\nbar\"\r", ReadRowEnding.CarriageReturn)]

        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r", ReadRowEnding.CarriageReturn, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\n", ReadRowEnding.LineFeed, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r\n", ReadRowEnding.CarriageReturnLineFeed, "###")]

        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r123", ReadRowEnding.CarriageReturn, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\n123", ReadRowEnding.LineFeed, "###")]
        [InlineData("h#ello###wo##rld###\"fiz###z\"###\"buzz###\"\r\n123", ReadRowEnding.CarriageReturnLineFeed, "###")]
        public async Task Async(string csv, ReadRowEnding expected, string valueSep = ",")
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadRowEnding(ReadRowEnding.Detect).WithValueSeparator(valueSep).BuildInternal();

            await RunAsyncReaderVariants<_Test>(
                    opts,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_Test>;
                        var configCancel = config as AsyncCancelControlConfig<_Test>;
                        var cInner = (ConcreteBoundConfiguration<_Test>)(configForced?.Inner ?? configCancel?.Inner ?? config);

                        await using (var str = await getReader(csv))
                        {
                            var stateMachine = new ReaderStateMachine();
                            var charLookup = CharacterLookup.MakeCharacterLookup(cInner.Options, out _);
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

        [Theory]
        [InlineData("#abcd", ReadRowEnding.CarriageReturnLineFeed)]

        [InlineData("#abcd\r", ReadRowEnding.CarriageReturn)]
        [InlineData("#abcd\n", ReadRowEnding.LineFeed)]
        [InlineData("#abcd\r\n", ReadRowEnding.CarriageReturnLineFeed)]

        [InlineData("#abcd\rfoo", ReadRowEnding.CarriageReturn)]
        [InlineData("#abcd\nfoo", ReadRowEnding.LineFeed)]
        [InlineData("#abcd\r\nfoo", ReadRowEnding.CarriageReturnLineFeed)]
        public async Task CommentsAsFirstRecordAsync(string csv, ReadRowEnding expected)
        {
            Assert.StartsWith("#", csv);

            var opts =
                Options
                    .CreateBuilder(Options.Default)
                    .WithReadRowEnding(ReadRowEnding.Detect)
                    .WithCommentCharacter('#')
                    .WithValueSeparator(",")
                    .BuildInternal();

            await RunAsyncReaderVariants<_Test>(
                opts,
                async (config, getReader) =>
                {
                    var configForced = config as AsyncCountingAndForcingConfig<_Test>;
                    var configCancel = config as AsyncCancelControlConfig<_Test>;
                    var cInner = (ConcreteBoundConfiguration<_Test>)(configForced?.Inner ?? configCancel?.Inner ?? config);

                    await using (var str = await getReader(csv))
                    {
                        var stateMachine = new ReaderStateMachine();
                        var charLookup = CharacterLookup.MakeCharacterLookup(cInner.Options, out _);
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
