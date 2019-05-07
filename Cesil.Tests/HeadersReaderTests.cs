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
        // just a stub for helper purposes
        class _Foo { public string A { get; set; } }

        class _JustHeaders
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

        class _BufferToLarge
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void BufferToLarge()
        {
            var config = (ConcreteBoundConfiguration<_BufferToLarge>)Configuration.For<_BufferToLarge>(Options.Default.NewBuilder().WithMemoryPool(new TestMemoryPool<char>(16)).Build());

            // none
            {
                using (var str = new StringReader("foo,fizz,bar,buzz,baz,nope,nada,zilch,what,who,when,where,qwerty,dvorak"))
                {
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(MemoryPool<char>.Shared, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using (var reader = new HeadersReader<_BufferToLarge>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500)))
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
                    var res = reader.Read();
                    Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("fizz", new string(i.Span)));
                    Assert.False(res.IsHeader);
                }
            }

            // one, exact
            {
                using (var str = new StringReader("Foo"))
                {
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
                    var res = reader.Read();
                    Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("Foo", new string(i.Span)));
                    Assert.True(res.IsHeader);
                }
            }

            // one, inexact
            {
                using (var str = new StringReader("Foo,fizz"))
                {
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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

        class _ManyHeaders
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

            var config = (ConcreteBoundConfiguration<_ManyHeaders>)Configuration.For<_ManyHeaders>(Options.Default.NewBuilder().WithRowEnding(RowEndings.CarriageReturnLineFeed).Build());

            using (var str = new StringReader(csv))
            {
                using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                using var reader = new HeadersReader<_ManyHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
            var config = (ConcreteBoundConfiguration<_JustHeaders>)Configuration.For<_JustHeaders>(Options.Default.NewBuilder().WithRowEnding(RowEndings.CarriageReturnLineFeed).Build());

            // none
            {
                using (var str = new StringReader("fizz\r\n0\r\n"))
                {
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
                    var res = reader.Read();
                    Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("fizz", new string(i.Span)));
                    Assert.False(res.IsHeader);
                }
            }

            // one, exact
            {
                using (var str = new StringReader("Foo\r\nfoo"))
                {
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
                    var res = reader.Read();
                    Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("Foo", new string(i.Span)));
                    Assert.True(res.IsHeader);
                }
            }

            // one, inexact
            {
                using (var str = new StringReader("Foo,fizz\r\n1,2"))
                {
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                    using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                    using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
        public async Task JustHeadersAsync()
        {
            var config = (ConcreteBoundConfiguration<_JustHeaders>)Configuration.For<_JustHeaders>(Options.Default.NewBuilder().WithRowEnding(RowEndings.CarriageReturnLineFeed).Build());

            // none
            {
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("fizz"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("fizz", new string(i.Span)));
                            Assert.False(res.IsHeader);
                        }
                    }
                );
            }

            // one, exact
            {
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Foo"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("Foo", new string(i.Span)));
                            Assert.True(res.IsHeader);
                        }
                    }
                );
            }

            // one, inexact
            {
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Foo,fizz"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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

                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("fizz,Bar"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Foo,Bar"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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

                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Bar,Foo"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config,charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Foo,Bar,Fizz"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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

                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Bar,Fizz,Foo"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
            var config = Configuration.For<_ManyHeaders>(Options.Default.NewBuilder().WithRowEnding(RowEndings.CarriageReturnLineFeed).Build());

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
                    var c = (ConcreteBoundConfiguration<_ManyHeaders>)config;

                    using (var str = makeReader(csv))
                    {
                        using var charLookup = ReaderStateMachine.MakeCharacterLookup(c.MemoryPool, c.EscapedValueStartAndStop, c.ValueSeparator, c.EscapeValueEscapeChar, c.CommentChar);
                        using var reader = new HeadersReader<_ManyHeaders>(c, charLookup,  str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
            var config = (ConcreteBoundConfiguration<_JustHeaders>)Configuration.For<_JustHeaders>(Options.Default.NewBuilder().WithRowEnding(RowEndings.CarriageReturnLineFeed).Build());

            var columns =
                new[]
                {
                    new Column(nameof(_JustHeaders.Foo), null, null, false),
                    new Column(nameof(_JustHeaders.Bar), null, null, false),
                };

            // none
            {
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("fizz\r\n0\r\n"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("fizz", new string(i.Span)));
                            Assert.False(res.IsHeader);
                        }
                    }
                );
            }

            // one, exact
            {
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Foo\r\nfoo"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
                            var res = await reader.ReadAsync(CancellationToken.None);
                            Assert.Collection(ToEnumerable(res.Headers), i => Assert.Equal("Foo", new string(i.Span)));
                            Assert.True(res.IsHeader);
                        }
                    }
                );
            }

            // one, inexact
            {
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Foo,fizz\r\n1,2"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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

                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("fizz,Bar\r\n2,blah\r\n"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Foo,Bar\r\nwhatever,something"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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

                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Bar,Foo\r\n3,4"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Foo,Bar,Fizz\r\na,b,c\r\n"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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

                await RunAsyncReaderVariants<_Foo>(
                    Options.Default,
                    async (_, getReader) =>
                    {
                        using (var str = getReader("Bar,Fizz,Foo\r\n1,2,3"))
                        {
                            using var charLookup = ReaderStateMachine.MakeCharacterLookup(config.MemoryPool, config.EscapedValueStartAndStop, config.ValueSeparator, config.EscapeValueEscapeChar, config.CommentChar);
                            using var reader = new HeadersReader<_JustHeaders>(config, charLookup, str, new BufferWithPushback(MemoryPool<char>.Shared, 500));
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
