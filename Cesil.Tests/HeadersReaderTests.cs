using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class HeadersReaderTests
    {
        private static BufferWithPushback MakeBuffer()
        {
            return
                new BufferWithPushback(
                    MemoryPool<char>.Shared,
                    500
                );
        }

        // just a stub for helper purposes
        private class _Foo { public string A { get; set; } }

        private class _JustHeaders
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        private static IEnumerable<ReadOnlyMemory<char>> ToEnumerable<T>(HeadersReader<T>.HeaderEnumerator e)
        {
            using (e)
            {
                while (e.MoveNext())
                {
                    yield return e.Current;
                }
            }
        }

        private class _CommentBeforeHeader
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void CommentBeforeHeader()
        {
            // \r\n
            {
                var config =
                    (ConcreteBoundConfiguration<_CommentBeforeHeader>)
                        Configuration.For<_CommentBeforeHeader>(
                            Options.CreateBuilder(Options.Default)
                                .WithCommentCharacter('#')
                                .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                                .ToOptions()
                        );

                using (var str = new StringReader("#hello\rfoo\nbar\r\nFoo,Bar"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader =
                        new HeadersReader<_CommentBeforeHeader>(
                            new ReaderStateMachine(),
                            config,
                            charLookup,
                            new TextReaderAdapter(str),
                            MakeBuffer()
                        );
                    var res = reader.Read();
                    Assert.True(res.IsHeader);
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Foo", new string(i.Span)),
                        i => Assert.Equal("Bar", new string(i.Span))
                    );
                }
            }

            // \r
            {
                var config =
                    (ConcreteBoundConfiguration<_CommentBeforeHeader>)
                        Configuration.For<_CommentBeforeHeader>(
                            Options.CreateBuilder(Options.Default)
                                .WithCommentCharacter('#')
                                .WithRowEnding(RowEnding.CarriageReturn)
                                .ToOptions()
                        );

                using (var str = new StringReader("#hello\nfoo\n\rFoo,Bar"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_CommentBeforeHeader>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.True(res.IsHeader);
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Foo", new string(i.Span)),
                        i => Assert.Equal("Bar", new string(i.Span))
                    );
                }
            }

            // \n
            {
                var config =
                    (ConcreteBoundConfiguration<_CommentBeforeHeader>)
                        Configuration.For<_CommentBeforeHeader>(
                            Options.CreateBuilder(Options.Default)
                                .WithCommentCharacter('#')
                                .WithRowEnding(RowEnding.LineFeed)
                                .ToOptions()
                        );

                using (var str = new StringReader("#hello\rfoo\r..\nFoo,Bar"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_CommentBeforeHeader>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.True(res.IsHeader);
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Foo", new string(i.Span)),
                        i => Assert.Equal("Bar", new string(i.Span))
                    );
                }
            }
        }

        private class _BufferToLarge
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void BufferToLarge()
        {
            var config = (ConcreteBoundConfiguration<_BufferToLarge>)Configuration.For<_BufferToLarge>(Options.CreateBuilder(Options.Default).WithMemoryPool(new TestMemoryPool<char>(16)).ToOptions());

            // none
            {
                using (var str = new StringReader("foo,fizz,bar,buzz,baz,nope,nada,zilch,what,who,when,where,qwerty,dvorak"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(MemoryPool<char>.Shared, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using (var reader = new HeadersReader<_BufferToLarge>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer()))
                    {
                        Assert.Throws<InvalidOperationException>(() => reader.Read());
                    }
                }
            }
        }

        [Fact]
        public void JustHeaders()
        {
            var config = (ConcreteBoundConfiguration<_JustHeaders>)Configuration.For<_JustHeaders>();

            // none
            {
                using (var str = new StringReader("fizz"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("fizz", new string(i.Span)));
                    Assert.False(res.IsHeader);
                }
            }

            // one, exact
            {
                using (var str = new StringReader("Foo"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("Foo", new string(i.Span)));
                    Assert.True(res.IsHeader);
                }
            }

            // one, inexact
            {
                using (var str = new StringReader("Foo,fizz"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Foo", new string(i.Span)),
                        i => Assert.Equal("fizz", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }

                using (var str = new StringReader("fizz,Bar"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("fizz", new string(i.Span)),
                        i => Assert.Equal("Bar", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }
            }

            // two, exact
            {
                using (var str = new StringReader("Foo,Bar"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Foo", new string(i.Span)),
                        i => Assert.Equal("Bar", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }

                using (var str = new StringReader("Bar,Foo"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Bar", new string(i.Span)),
                        i => Assert.Equal("Foo", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }
            }

            // two, inexact
            {
                using (var str = new StringReader("Foo,Bar,Fizz"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Foo", new string(i.Span)),
                        i => Assert.Equal("Bar", new string(i.Span)),
                        i => Assert.Equal("Fizz", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }

                using (var str = new StringReader("Bar,Fizz,Foo"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Bar", new string(i.Span)),
                        i => Assert.Equal("Fizz", new string(i.Span)),
                        i => Assert.Equal("Foo", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }
            }
        }

        private class _ManyHeaders
        {
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader1 { get; set; }
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader2 { get; set; }
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader3 { get; set; }
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader4 { get; set; }
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader5 { get; set; }
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader6 { get; set; }
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader7 { get; set; }
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader8 { get; set; }
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader9 { get; set; }
            public string LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader10 { get; set; }

        }

        [Fact]
        public void ManyHeaders()
        {
            var csv =
                string.Join(
                    ",",
                    new[]
                    {
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader1),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader2),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader3),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader4),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader5),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader6),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader7),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader8),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader9),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader10)
                    }
                );

            var config = (ConcreteBoundConfiguration<_ManyHeaders>)Configuration.For<_ManyHeaders>(Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions());

            using (var str = new StringReader(csv))
            {
                using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                using var reader = new HeadersReader<_ManyHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                var res = reader.Read();
                Assert.Collection(
                    ToEnumerable(res.Headers),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader1), new string(i.Span)),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader2), new string(i.Span)),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader3), new string(i.Span)),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader4), new string(i.Span)),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader5), new string(i.Span)),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader6), new string(i.Span)),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader7), new string(i.Span)),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader8), new string(i.Span)),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader9), new string(i.Span)),
                    i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader10), new string(i.Span))
                );
                Assert.True(res.IsHeader);
            }
        }

        [Fact]
        public void TrailingRecords()
        {
            var config = (ConcreteBoundConfiguration<_JustHeaders>)Configuration.For<_JustHeaders>(Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions());

            // none
            {
                using (var str = new StringReader("fizz\r\n0\r\n"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("fizz", new string(i.Span)));
                    Assert.False(res.IsHeader);
                }
            }

            // one, exact
            {
                using (var str = new StringReader("Foo\r\nfoo"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("Foo", new string(i.Span)));
                    Assert.True(res.IsHeader);
                }
            }

            // one, inexact
            {
                using (var str = new StringReader("Foo,fizz\r\n1,2"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Foo", new string(i.Span)),
                        i => Assert.Equal("fizz", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }

                using (var str = new StringReader("fizz,Bar\r\n2,blah\r\n"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("fizz", new string(i.Span)),
                        i => Assert.Equal("Bar", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }
            }

            // two, exact
            {
                using (var str = new StringReader("Foo,Bar\r\nwhatever,something"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Foo", new string(i.Span)),
                        i => Assert.Equal("Bar", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }

                using (var str = new StringReader("Bar,Foo\r\n3,4"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Bar", new string(i.Span)),
                        i => Assert.Equal("Foo", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }
            }

            // two, inexact
            {
                using (var str = new StringReader("Foo,Bar,Fizz\r\na,b,c\r\n"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Foo", new string(i.Span)),
                        i => Assert.Equal("Bar", new string(i.Span)),
                        i => Assert.Equal("Fizz", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }

                using (var str = new StringReader("Bar,Fizz,Foo\r\n1,2,3"))
                {
                    using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                    using var reader = new HeadersReader<_JustHeaders>(new ReaderStateMachine(), config, charLookup, new TextReaderAdapter(str), MakeBuffer());
                    var res = reader.Read();
                    Assert.Collection(
                        ToEnumerable(res.Headers),
                        i => Assert.Equal("Bar", new string(i.Span)),
                        i => Assert.Equal("Fizz", new string(i.Span)),
                        i => Assert.Equal("Foo", new string(i.Span))
                    );
                    Assert.True(res.IsHeader);
                }
            }
        }

        [Fact]
        public async Task CommentBeforeHeaderAsync()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.Default)
                        .WithCommentCharacter('#')
                        .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                        .ToOptions();

                await RunAsyncReaderVariants<_CommentBeforeHeader>(
                    opts,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_CommentBeforeHeader>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_CommentBeforeHeader>;
                        var cInner = (ConcreteBoundConfiguration<_CommentBeforeHeader>)(configUnpin?.Inner ?? configForced?.Inner ?? config);



                        await using (var str = await getReader("#hello\rfoo\nbar\r\nFoo,Bar"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();

                            using (var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _))
                            using (var reader = new HeadersReader<_CommentBeforeHeader>(stateMachine, cInner, charLookup, str, MakeBuffer()))
                            {
                                if (configForced != null)
                                {
                                    configForced.Set(reader);
                                }

                                var res = await reader.ReadAsync(default);
                                Assert.True(res.IsHeader);
                                Assert.Collection(
                                    ToEnumerable(res.Headers),
                                    i => Assert.Equal("Foo", new string(i.Span)),
                                    i => Assert.Equal("Bar", new string(i.Span))
                                );
                            }
                        }
                    }
                );
            }

            // \r
            {
                var opts =
                   Options.CreateBuilder(Options.Default)
                        .WithCommentCharacter('#')
                        .WithRowEnding(RowEnding.CarriageReturn)
                        .ToOptions();

                await RunAsyncReaderVariants<_CommentBeforeHeader>(
                    opts,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_CommentBeforeHeader>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_CommentBeforeHeader>;
                        var cInner = (ConcreteBoundConfiguration<_CommentBeforeHeader>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("#hello\nfoo\n\rFoo,Bar"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();

                            using (var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _))
                            using (var reader = new HeadersReader<_CommentBeforeHeader>(stateMachine, cInner, charLookup, str, MakeBuffer()))
                            {
                                if (configForced != null)
                                {
                                    configForced.Set(reader);
                                }

                                var res = await reader.ReadAsync(default);
                                Assert.True(res.IsHeader);
                                Assert.Collection(
                                    ToEnumerable(res.Headers),
                                    i => Assert.Equal("Foo", new string(i.Span)),
                                    i => Assert.Equal("Bar", new string(i.Span))
                                );
                            }
                        }
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.Default)
                        .WithCommentCharacter('#')
                        .WithRowEnding(RowEnding.LineFeed)
                        .ToOptions();

                await RunAsyncReaderVariants<_CommentBeforeHeader>(
                    opts,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_CommentBeforeHeader>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_CommentBeforeHeader>;
                        var cInner = (ConcreteBoundConfiguration<_CommentBeforeHeader>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("#hello\rfoo\r..\nFoo,Bar"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();

                            using (var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _))
                            using (var reader = new HeadersReader<_CommentBeforeHeader>(stateMachine, cInner, charLookup, str, MakeBuffer()))
                            {
                                if (configForced != null)
                                {
                                    configForced.Set(reader);
                                }

                                var res = await reader.ReadAsync(default);
                                Assert.True(res.IsHeader);
                                Assert.Collection(
                                    ToEnumerable(res.Headers),
                                    i => Assert.Equal("Foo", new string(i.Span)),
                                    i => Assert.Equal("Bar", new string(i.Span))
                                );
                            }
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task JustHeadersAsync()
        {
            // none
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var cInner = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("fizz"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, cInner, charLookup, str, MakeBuffer());

                            if (configForced != null)
                            {
                                configForced.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("fizz", new string(i.Span)));
                            Assert.False(res.IsHeader);
                        }
                    }
                );
            }

            // one, exact
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var cInner = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("Foo"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, cInner, charLookup, str, MakeBuffer());

                            if (configForced != null)
                            {
                                configForced.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("Foo", new string(i.Span)));
                            Assert.True(res.IsHeader);
                        }
                    }
                );
            }

            // one, inexact
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var cInner = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("Foo,fizz"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, cInner, charLookup, str, MakeBuffer());

                            if (configForced != null)
                            {
                                configForced.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Foo", new string(i.Span)),
                                i => Assert.Equal("fizz", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );

                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var cInner = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("fizz,Bar"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, cInner, charLookup, str, MakeBuffer());

                            if (configForced != null)
                            {
                                configForced.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("fizz", new string(i.Span)),
                                i => Assert.Equal("Bar", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );
            }

            // two, exact
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var cInner = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("Foo,Bar"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, cInner, charLookup, str, MakeBuffer());

                            if (configForced != null)
                            {
                                configForced.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Foo", new string(i.Span)),
                                i => Assert.Equal("Bar", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );

                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var cInner = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("Bar,Foo"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, cInner, charLookup, str, MakeBuffer());

                            if (configForced != null)
                            {
                                configForced.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Bar", new string(i.Span)),
                                i => Assert.Equal("Foo", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );
            }

            // two, inexact
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var cInner = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("Foo,Bar,Fizz"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, cInner, charLookup, str, MakeBuffer());

                            if (configForced != null)
                            {
                                configForced.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Foo", new string(i.Span)),
                                i => Assert.Equal("Bar", new string(i.Span)),
                                i => Assert.Equal("Fizz", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );

                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (config, getReader) =>
                    {
                        var configForced = config as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = config as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var cInner = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? configForced?.Inner ?? config);

                        await using (var str = await getReader("Bar,Fizz,Foo"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(cInner.MemoryPool, cInner.EscapedValueStartAndStop, cInner.ValueSeparator, cInner.EscapeValueEscapeChar, cInner.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, cInner, charLookup, str, MakeBuffer());

                            if (configForced != null)
                            {
                                configForced.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Bar", new string(i.Span)),
                                i => Assert.Equal("Fizz", new string(i.Span)),
                                i => Assert.Equal("Foo", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task ManyHeadersAsync()
        {
            var config = Configuration.For<_ManyHeaders>(Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions());

            var csv =
                string.Join(
                    ",",
                    new[]
                    {
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader1),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader2),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader3),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader4),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader5),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader6),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader7),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader8),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader9),
                        nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader10)
                    }
                );

            await RunAsyncReaderVariants<_ManyHeaders>(
                Options.Default,
                async (config, makeReader) =>
                {
                    var forcedConfig = config as AsyncCountingAndForcingConfig<_ManyHeaders>;
                    var configUnpin = config as AsyncInstrumentedPinConfig<_ManyHeaders>;
                    var c = (ConcreteBoundConfiguration<_ManyHeaders>)(configUnpin?.Inner ?? forcedConfig?.Inner ?? config);

                    await using (var str = await makeReader(csv))
                    await using (configUnpin?.CreateAsyncReader(str))
                    {
                        var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                        using var charLookup = CharacterLookup.MakeCharacterLookup(c.MemoryPool, c.EscapedValueStartAndStop, c.ValueSeparator, c.EscapeValueEscapeChar, c.CommentChar, out _);
                        using var reader = new HeadersReader<_ManyHeaders>(stateMachine, c, charLookup, str, MakeBuffer());

                        if (forcedConfig != null)
                        {
                            forcedConfig.Set(reader);
                        }

                        var res = await reader.ReadAsync(default);
                        Assert.Collection(
                            ToEnumerable(res.Headers),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader1), new string(i.Span)),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader2), new string(i.Span)),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader3), new string(i.Span)),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader4), new string(i.Span)),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader5), new string(i.Span)),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader6), new string(i.Span)),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader7), new string(i.Span)),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader8), new string(i.Span)),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader9), new string(i.Span)),
                            i => Assert.Equal(nameof(_ManyHeaders.LoooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooongHeader10), new string(i.Span))
                        );
                        Assert.True(res.IsHeader);
                    }
                }
            );
        }

        [Fact]
        public async Task TrailingRecordsAsync()
        {
            var columns =
                new[]
                {
                    new Column(nameof(_JustHeaders.Foo), null, null, false),
                    new Column(nameof(_JustHeaders.Bar), null, null, false),
                };

            // none
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (c, getReader) =>
                    {
                        var forcedConfig = c as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = c as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var config = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? forcedConfig?.Inner ?? c);

                        await using (var str = await getReader("fizz\r\n0\r\n"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, config, charLookup, str, MakeBuffer());

                            if (forcedConfig != null)
                            {
                                forcedConfig.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("fizz", new string(i.Span)));
                            Assert.False(res.IsHeader);
                        }
                    }
                );
            }

            // one, exact
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (c, getReader) =>
                    {
                        var forcedConfig = c as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = c as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var config = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? forcedConfig?.Inner ?? c);

                        await using (var str = await getReader("Foo\r\nfoo"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, config, charLookup, str, MakeBuffer());

                            if (forcedConfig != null)
                            {
                                forcedConfig.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("Foo", new string(i.Span)));
                            Assert.True(res.IsHeader);
                        }
                    }
                );
            }

            // one, inexact
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (c, getReader) =>
                    {
                        var forcedConfig = c as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = c as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var config = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? forcedConfig?.Inner ?? c);

                        await using (var str = await getReader("Foo,fizz\r\n1,2"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, config, charLookup, str, MakeBuffer());

                            if (forcedConfig != null)
                            {
                                forcedConfig.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Foo", new string(i.Span)),
                                i => Assert.Equal("fizz", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );

                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (c, getReader) =>
                    {
                        var forcedConfig = c as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = c as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var config = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? forcedConfig?.Inner ?? c);

                        await using (var str = await getReader("fizz,Bar\r\n2,blah\r\n"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, config, charLookup, str, MakeBuffer());

                            if (forcedConfig != null)
                            {
                                forcedConfig.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("fizz", new string(i.Span)),
                                i => Assert.Equal("Bar", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );
            }

            // two, exact
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (c, getReader) =>
                    {
                        var forcedConfig = c as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = c as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var config = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? forcedConfig?.Inner ?? c);

                        await using (var str = await getReader("Foo,Bar\r\nwhatever,something"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, config, charLookup, str, MakeBuffer());

                            if (forcedConfig != null)
                            {
                                forcedConfig.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Foo", new string(i.Span)),
                                i => Assert.Equal("Bar", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );

                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (c, getReader) =>
                    {
                        var forcedConfig = c as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = c as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var config = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? forcedConfig?.Inner ?? c);

                        await using (var str = await getReader("Bar,Foo\r\n3,4"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, config, charLookup, str, MakeBuffer());

                            if (forcedConfig != null)
                            {
                                forcedConfig.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Bar", new string(i.Span)),
                                i => Assert.Equal("Foo", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
               );
            }

            // two, inexact
            {
                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (c, getReader) =>
                    {
                        var forcedConfig = c as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = c as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var config = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? forcedConfig?.Inner ?? c);

                        await using (var str = await getReader("Foo,Bar,Fizz\r\na,b,c\r\n"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, config, charLookup, str, MakeBuffer());

                            if (forcedConfig != null)
                            {
                                forcedConfig.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Foo", new string(i.Span)),
                                i => Assert.Equal("Bar", new string(i.Span)),
                                i => Assert.Equal("Fizz", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );

                await RunAsyncReaderVariants<_JustHeaders>(
                    Options.Default,
                    async (c, getReader) =>
                    {
                        var forcedConfig = c as AsyncCountingAndForcingConfig<_JustHeaders>;
                        var configUnpin = c as AsyncInstrumentedPinConfig<_JustHeaders>;
                        var config = (ConcreteBoundConfiguration<_JustHeaders>)(configUnpin?.Inner ?? forcedConfig?.Inner ?? c);

                        await using (var str = await getReader("Bar,Fizz,Foo\r\n1,2,3"))
                        await using (configUnpin?.CreateAsyncReader(str))
                        {
                            var stateMachine = configUnpin?.StateMachine ?? new ReaderStateMachine();
                            using var charLookup = CharacterLookup.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar, out _);
                            using var reader = new HeadersReader<_JustHeaders>(stateMachine, config, charLookup, str, MakeBuffer());

                            if (forcedConfig != null)
                            {
                                forcedConfig.Set(reader);
                            }

                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(
                                ToEnumerable(res.Headers),
                                i => Assert.Equal("Bar", new string(i.Span)),
                                i => Assert.Equal("Fizz", new string(i.Span)),
                                i => Assert.Equal("Foo", new string(i.Span))
                            );
                            Assert.True(res.IsHeader);
                        }
                    }
                );
            }
        }
    }
}
