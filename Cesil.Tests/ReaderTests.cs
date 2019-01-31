using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class ReaderTests
    {
        [Fact]
        public void TransitionMatrixConstants()
        {
            var maxStateVal = Enum.GetValues(typeof(ReaderStateMachine.State)).Cast<ReaderStateMachine.State>().Select(b => (byte)b).Max();

            // making these consts is a win, but we want to make sure we don't break them
            Assert.Equal(maxStateVal + 1, ReaderStateMachine.RuleCacheStateCount);
            Assert.Equal(Enum.GetValues(typeof(ReaderStateMachine.CharacterType)).Length, ReaderStateMachine.RuleCacheCharacterCount);
            Assert.Equal((maxStateVal + 1) * Enum.GetValues(typeof(ReaderStateMachine.CharacterType)).Length, ReaderStateMachine.RuleCacheConfigSize);
            Assert.Equal(Enum.GetValues(typeof(RowEndings)).Length, ReaderStateMachine.RuleCacheRowEndingCount);
            Assert.Equal(Enum.GetValues(typeof(RowEndings)).Length * 2, ReaderStateMachine.RuleCacheConfigCount);
        }

        [Fact]
        public void StateMasks()
        {
            foreach(ReaderStateMachine.State state in Enum.GetValues(typeof(ReaderStateMachine.State)))
            {
                var wasSpecial = false;

                var inComment = (((byte)state) & ReaderStateMachine.IN_COMMENT_MASK) == ReaderStateMachine.IN_COMMENT_MASK;
                if (inComment)
                {
                    Assert.True(
                        state == ReaderStateMachine.State.Comment_BeforeHeader ||
                        state == ReaderStateMachine.State.Comment_BeforeHeader_ExpectingEndOfComment ||
                        state == ReaderStateMachine.State.Comment_BeforeRecord ||
                        state == ReaderStateMachine.State.Comment_BeforeRecord_ExpectingEndOfComment
                    );
                    wasSpecial = true;
                }

                var inEscapedValue = (((byte)state) & ReaderStateMachine.IN_ESCAPED_VALUE_MASK) == ReaderStateMachine.IN_ESCAPED_VALUE_MASK;
                if (inEscapedValue)
                {
                    Assert.True(
                        state == ReaderStateMachine.State.Header_InEscapedValue ||
                        state == ReaderStateMachine.State.Header_InEscapedValueWithPendingEscape ||
                        state == ReaderStateMachine.State.Record_InEscapedValue ||
                        state == ReaderStateMachine.State.Record_InEscapedValueWithPendingEscape
                    );
                    wasSpecial = true;
                }

                var canEndRecord = (((byte)state) & ReaderStateMachine.CAN_END_RECORD_MASK) == ReaderStateMachine.CAN_END_RECORD_MASK;
                if (canEndRecord)
                {
                    Assert.True(
                            state == ReaderStateMachine.State.Record_InEscapedValueWithPendingEscape ||
                            state == ReaderStateMachine.State.Record_Unescaped_NoValue ||
                            state == ReaderStateMachine.State.Record_Unescaped_WithValue
                    );
                    wasSpecial = true;
                }

                if (!wasSpecial)
                {
                    Assert.True(
                        state == ReaderStateMachine.State.NONE ||
                        state == ReaderStateMachine.State.Header_Start ||
                        state == ReaderStateMachine.State.Header_InEscapedValue_ExpectingEndOfValueOrRecord ||
                        state == ReaderStateMachine.State.Header_Unescaped_NoValue ||
                        state == ReaderStateMachine.State.Header_Unescaped_WithValue ||
                        state == ReaderStateMachine.State.Header_ExpectingEndOfRecord ||
                        state == ReaderStateMachine.State.Record_Start ||
                        state == ReaderStateMachine.State.Record_InEscapedValue_ExpectingEndOfValueOrRecord ||
                        state == ReaderStateMachine.State.Record_ExpectingEndOfRecord ||
                        state == ReaderStateMachine.State.Invalid
                    );
                }
            }
        }

        class _TabSeparator
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
        }

        [Fact]
        public void TabSeparator()
        {
            const string TSV = @"Foo	Bar
""hello""""world""	123
";
            var opts = Options.Default.NewBuilder().WithEscapedValueStartAndEnd('"').WithValueSeparator('\t').Build();

            RunSyncReaderVariants<_TabSeparator>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader(TSV))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello\"world", a.Foo); Assert.Equal(123, a.Bar); }
                        );
                    }
                }
            );
        }

        class _DifferentEscapes
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void DifferentEscapes()
        {
            var opts = Options.Default.NewBuilder().WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter('\\').Build();

            // simple
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\nhello,world"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\n\"hello\",\"world\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped with quotes
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\n\"he\\\"llo\",\"world\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("he\"llo", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped with slash
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\n\"hello\",\"w\\\\orld\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("w\\orld", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escape char outside of quotes
            {
                RunSyncReaderVariants<_DifferentEscapes>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("Foo,Bar\r\n\\,\\ooo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("\\", a.Foo); Assert.Equal("\\ooo", a.Bar); }
                            );
                        }
                    }
                );
            }
        }

        class _BadEscape
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        [Fact]
        public void BadEscape()
        {
            var opts = Options.Default;
            var CSV = @"h""ello"",world";

            RunSyncReaderVariants<_BadEscape>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader(CSV))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.Throws<InvalidOperationException>(() => csv.ReadAll());
                    }
                }
            );
        }

        class _TryReadWithReuse
        {
            public string Bar { get; set; }
        }

        [Fact]
        public void TryReadWithReuse()
        {
            const string CSV = "hello\r\nworld\r\nfoo\r\n";

            RunSyncReaderVariants<_TryReadWithReuse>(
                Options.Default,
                (config, getReader) =>
                {
                    _TryReadWithReuse pre = null;

                    using (var reader = getReader(CSV))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryReadWithReuse(ref pre));
                        Assert.Equal("hello", pre.Bar);

                        var oldPre = pre;
                        Assert.True(csv.TryReadWithReuse(ref pre));
                        Assert.Equal("world", pre.Bar);
                        Assert.Same(oldPre, pre);

                        Assert.True(csv.TryReadWithReuse(ref pre));
                        Assert.Equal("foo", pre.Bar);
                        Assert.Same(oldPre, pre);

                        Assert.False(csv.TryReadWithReuse(ref pre));
                        Assert.Same(oldPre, pre);
                    }
                }
            );
        }

        class _ReadAll
        {
            public string Foo { get; set; }
            public int? Bar { get; set; }
            public DateTime Fizz { get; set; }
            public double? Buzz { get; set; }
        }

        [Fact]
        public void ReadAll()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            RunSyncReaderVariants<_ReadAll>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();
                            Assert.Collection(
                                read,
                                r1 =>
                                {

                                    Assert.Equal("hello\nworld", r1.Foo);
                                    Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                    Assert.Equal((int?)null, r1.Bar);
                                    Assert.Equal(123.45, r1.Buzz);
                                },
                                r2 =>
                                {

                                    Assert.Equal("", r2.Foo);
                                    Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                    Assert.Equal((int?)null, r2.Bar);
                                    Assert.Equal((double?)null, r2.Buzz);
                                },
                                r3 =>
                                {

                                    Assert.Equal("mkay", r3.Foo);
                                    Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                    Assert.Equal((int?)8675309, r3.Bar);
                                    Assert.Equal((double?)987654321.012345, r3.Buzz);
                                }
                            );
                        }
                    }
                );
        }

        [Fact]
        public void ReadAll_PreAllocated()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            RunSyncReaderVariants<_ReadAll>(
                    opts,
                    (config, getReader) =>
                    {
                        var pre = new List<_ReadAll>();
                        pre.Add(new _ReadAll { Bar = 1, Buzz = 2.2, Fizz = new DateTime(3, 3, 3), Foo = "4" });

                        using (var reader = getReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll(pre);
                            Assert.Collection(
                                read,
                                r1 =>
                                {
                                    Assert.Equal("4", r1.Foo);
                                    Assert.Equal(new DateTime(3, 3, 3), r1.Fizz);
                                    Assert.Equal(1, r1.Bar);
                                    Assert.Equal(2.2, r1.Buzz);
                                },
                                r2 =>
                                {

                                    Assert.Equal("hello\nworld", r2.Foo);
                                    Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r2.Fizz);
                                    Assert.Equal((int?)null, r2.Bar);
                                    Assert.Equal(123.45, r2.Buzz);
                                },
                                r3 =>
                                {

                                    Assert.Equal("", r3.Foo);
                                    Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r3.Fizz);
                                    Assert.Equal((int?)null, r3.Bar);
                                    Assert.Equal((double?)null, r3.Buzz);
                                },
                                r4 =>
                                {

                                    Assert.Equal("mkay", r4.Foo);
                                    Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r4.Fizz);
                                    Assert.Equal((int?)8675309, r4.Bar);
                                    Assert.Equal((double?)987654321.012345, r4.Buzz);
                                }
                            );
                        }
                    }
                );
        }

        [Fact]
        public void EnumerateAll()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            RunSyncReaderVariants<_ReadAll>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.EnumerateAll();
                            Assert.Collection(
                                read,
                                r1 =>
                                {

                                    Assert.Equal("hello\nworld", r1.Foo);
                                    Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                    Assert.Equal((int?)null, r1.Bar);
                                    Assert.Equal(123.45, r1.Buzz);
                                },
                                r2 =>
                                {

                                    Assert.Equal("", r2.Foo);
                                    Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                    Assert.Equal((int?)null, r2.Bar);
                                    Assert.Equal((double?)null, r2.Buzz);
                                },
                                r3 =>
                                {

                                    Assert.Equal("mkay", r3.Foo);
                                    Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                    Assert.Equal((int?)8675309, r3.Bar);
                                    Assert.Equal((double?)987654321.012345, r3.Buzz);
                                }
                            );
                        }
                    }
                );
        }

        private class _OneColumnOneRow
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void OneColumnOneRow()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // normal
            RunSyncReaderVariants<_OneColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("hello"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var record));
                        Assert.NotNull(record);
                        Assert.Equal("hello", record.Foo);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // quoted
            RunSyncReaderVariants<_OneColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello world\""))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var record));
                        Assert.NotNull(record);
                        Assert.Equal("hello world", record.Foo);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // escaped
            RunSyncReaderVariants<_OneColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello \"\" world\""))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var record));
                        Assert.NotNull(record);
                        Assert.Equal("hello \" world", record.Foo);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );
        }

        private class _TwoColumnOneRow
        {
            public string One { get; set; }
            public string Two { get; set; }
        }

        [Fact]
        public void TwoColumnOneRow()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // normal
            RunSyncReaderVariants<_TwoColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("hello,world"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));

                        Assert.Equal("hello", t.One);
                        Assert.Equal("world", t.Two);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // quoted
            RunSyncReaderVariants<_TwoColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello,world\",\"fizz,buzz\""))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));

                        Assert.Equal("hello,world", t.One);
                        Assert.Equal("fizz,buzz", t.Two);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // escaped
            RunSyncReaderVariants<_TwoColumnOneRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello\"\"world\",\"fizz\"\"buzz\""))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));

                        Assert.Equal("hello\"world", t.One);
                        Assert.Equal("fizz\"buzz", t.Two);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );
        }

        private class _TwoColumnTwoRow
        {
            public string Fizz { get; set; }
            public string Buzz { get; set; }
        }

        [Fact]
        public void TwoColumnTwoRow()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

            // normal
            RunSyncReaderVariants<_TwoColumnTwoRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("hello,world\r\nfoo,bar"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));
                        Assert.Equal("hello", t.Fizz);
                        Assert.Equal("world", t.Buzz);

                        Assert.True(reader.TryRead(out t));
                        Assert.Equal("foo", t.Fizz);
                        Assert.Equal("bar", t.Buzz);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // quoted
            RunSyncReaderVariants<_TwoColumnTwoRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello,world\",whatever\r\n\"foo,bar\",whoever"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));
                        Assert.Equal("hello,world", t.Fizz);
                        Assert.Equal("whatever", t.Buzz);

                        Assert.True(reader.TryRead(out t));
                        Assert.Equal("foo,bar", t.Fizz);
                        Assert.Equal("whoever", t.Buzz);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // escaped
            RunSyncReaderVariants<_TwoColumnTwoRow>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("\"hello\"\"world\",whatever\r\n\"foo\"\"bar\",whoever"))
                    using (var reader = config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));
                        Assert.Equal("hello\"world", t.Fizz);
                        Assert.Equal("whatever", t.Buzz);

                        Assert.True(reader.TryRead(out t));
                        Assert.Equal("foo\"bar", t.Fizz);
                        Assert.Equal("whoever", t.Buzz);

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );
        }

        class _DetectLineEndings
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
            public string Fizz { get; set; }
        }

        [Fact]
        public void DetectLineEndings()
        {
            var opts = Options.Default.NewBuilder().WithRowEnding(RowEndings.Detect).WithReadHeader(ReadHeaders.Never).Build();

            // normal
            {
                // \r\n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\r\ndddd,eeeee,ffffff\r\n1,2,3\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("eeeee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\rdddd,eeeee,ffffff\r1,2,3\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("eeeee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\ndddd,eeeee,ffffff\n1,2,3\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("eeeee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // quoted
            {
                // \r\n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\r\ndddd,\"ee\neee\",ffffff\r\n1,2,\"3\r\n\"\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\rdddd,\"ee\neee\",ffffff\r1,2,\"3\r\n\"\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\ndddd,\"ee\neee\",ffffff\n1,2,\"3\r\n\"\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("bb", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("2", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // escaped
            {
                // \r\n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\n\"\"\"dddd\",\"ee\neee\",ffffff\r\n1,\"\"\"2\"\"\",\"3\r\n\"\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("b\"b", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("\"2\"", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\"\"\"dddd\",\"ee\neee\",ffffff\r1,\"\"\"2\"\"\",\"3\r\n\"\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("b\"b", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("\"2\"", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectLineEndings>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\n\"\"\"dddd\",\"ee\neee\",ffffff\n1,\"\"\"2\"\"\",\"3\r\n\"\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", t1.Foo);
                            Assert.Equal("b\"b", t1.Bar);
                            Assert.Equal("ccc", t1.Fizz);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", t2.Foo);
                            Assert.Equal("ee\neee", t2.Bar);
                            Assert.Equal("ffffff", t2.Fizz);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal("1", t3.Foo);
                            Assert.Equal("\"2\"", t3.Bar);
                            Assert.Equal("3\r\n", t3.Fizz);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }
        }

        class _DetectHeaders
        {
            public int Hello { get; set; }
            public double World { get; set; }
        }

        [Fact]
        public void DetectHeaders()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Detect).WithRowEnding(RowEndings.Detect).Build();

            // no headers
            RunSyncReaderVariants<_DetectHeaders>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader("123,4.56"))
                    using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                    {
                        Assert.True(reader.TryRead(out var t));
                        Assert.Equal(123, t.Hello);
                        Assert.Equal(4.56, t.World);

                        Assert.Equal(ReadHeaders.Never, reader.ReadHeaders.Value);

                        Assert.Collection(
                            reader.Columns,
                            c => Assert.Equal("Hello", c.Name),
                            c => Assert.Equal("World", c.Name)
                        );

                        Assert.False(reader.TryRead(out _));
                    }
                }
            );

            // headers
            {
                // \r\n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("Hello,World\r\n123,4.56\r\n789,0.12\r\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("Hello", c.Name),
                                c => Assert.Equal("World", c.Name)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("Hello,World\n123,4.56\n789,0.12\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("Hello", c.Name),
                                c => Assert.Equal("World", c.Name)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("Hello,World\r123,4.56\r789,0.12\r"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("Hello", c.Name),
                                c => Assert.Equal("World", c.Name)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // headers, different order
            {
                // \r\n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Hello\r\n4.56,123\r\n0.12,789\r\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Equal("Hello", c.Name)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Hello\n4.56,123\n0.12,789\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Equal("Hello", c.Name)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Hello\r4.56,123\r0.12,789\r"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(123, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Equal("Hello", c.Name)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(789, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // headers, missing
            {
                // \r\n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Foo\r\n4.56,123\r\n0.12,789\r\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Same(Column.Ignored, c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Foo\n4.56,123\n0.12,789\n"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Same(Column.Ignored, c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncReaderVariants<_DetectHeaders>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("World,Foo\r4.56,123\r0.12,789\r"))
                        using (var reader = (Reader<_DetectHeaders>)config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(4.56, t.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Same(Column.Ignored, c)
                            );

                            Assert.True(reader.TryRead(out t));
                            Assert.Equal(0, t.Hello);
                            Assert.Equal(0.12, t.World);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }
        }

        class _IsRequiredMissing
        {
            public string A { get; set; }
            [DataMember(IsRequired = true)]
            public string B { get; set; }
        }

        [Fact]
        public void IsRequiredNotInHeader()
        {
            var opts = Options.Default;
            var CSV = "A,C\r\nhello,world";

            RunSyncReaderVariants<_IsRequiredMissing>(
                opts,
                (config, getReader) =>
                {
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        Assert.Throws<SerializationException>(() => csv.ReadAll());
                    }
                }
            );
        }

        [Fact]
        public void IsRequiredNotInRow()
        {
            var opts = Options.Default;

            // beginning
            RunSyncReaderVariants<_IsRequiredMissing>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "B,C\r\nhello,world\r\n,";

                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        Assert.Throws<SerializationException>(() => csv.ReadAll());
                    }
                }
            );

            // middle
            RunSyncReaderVariants<_IsRequiredMissing>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "A,B,C\r\nhello,world,foo\r\n,,";

                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        Assert.Throws<SerializationException>(() => csv.ReadAll());
                    }
                }
            );

            // end
            RunSyncReaderVariants<_IsRequiredMissing>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "A,B\r\nhello,world\r\n,";

                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        Assert.Throws<SerializationException>(() => csv.ReadAll());
                    }
                }
            );
        }

        class _Comment
        {
            [DataMember(Name = "hello")]
            public string Hello { get; set; }
            [DataMember(Name = "world")]
            public string World { get; set; }
        }

        [Fact]
        public void Comments()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').Build();

            // comment first line
            RunSyncReaderVariants<_Comment>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "#this is a test comment!\r\nhello,world\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                    }
                }
            );

            // comment after header
            RunSyncReaderVariants<_Comment>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\n#this is a test comment\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                    }
                }
            );

            // comment between rows
            RunSyncReaderVariants<_Comment>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!\r\nfizz,buzz";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); },
                            b => { Assert.Equal("fizz", b.Hello); Assert.Equal("buzz", b.World); }
                        );
                    }
                }
            );

            // comment at end
            RunSyncReaderVariants<_Comment>(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                    }
                }
            );
        }

        [Fact]
        public async Task BadEscapeAsync()
        {
            var opts = Options.Default;
            var CSV = @"h""ello"",world";

            await RunAsyncReaderVariants<_BadEscape>(
                opts,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.ReadAllAsync());
                    }
                }
            );
        }

        [Fact]
        public async Task TryReadWithReuseAsync()
        {
            const string CSV = "hello\r\nworld\r\nfoo\r\n";

            await RunAsyncReaderVariants<_TryReadWithReuse>(
                Options.Default,
                async (config, getReader) =>
                {
                    _TryReadWithReuse pre = null;

                    using (var reader = getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var ret1 = await csv.TryReadWithReuseAsync(ref pre);
                        Assert.True(ret1.HasValue);
                        Assert.Equal("hello", ret1.Value.Bar);

                        var ret2 = await csv.TryReadWithReuseAsync(ref pre);
                        Assert.True(ret2.HasValue);
                        Assert.Equal("world", ret2.Value.Bar);
                        Assert.Same(ret1.Value, ret2.Value);

                        var ret3 = await csv.TryReadWithReuseAsync(ref pre);
                        Assert.True(ret3.HasValue);
                        Assert.Equal("foo", ret3.Value.Bar);
                        Assert.Same(ret1.Value, ret2.Value);
                        Assert.Same(ret2.Value, ret3.Value);

                        var ret4 = await csv.TryReadWithReuseAsync(ref pre);
                        Assert.False(ret4.HasValue);
                    }
                }
            );
        }

        [Fact]
        public async Task ReadAllAsync()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            await RunAsyncReaderVariants<_ReadAll>(
                opts,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();
                        Assert.Collection(
                            read,
                            r1 =>
                            {

                                Assert.Equal("hello\nworld", r1.Foo);
                                Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                Assert.Equal((int?)null, r1.Bar);
                                Assert.Equal(123.45, r1.Buzz);
                            },
                            r2 =>
                            {

                                Assert.Equal("", r2.Foo);
                                Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                Assert.Equal((int?)null, r2.Bar);
                                Assert.Equal((double?)null, r2.Buzz);
                            },
                            r3 =>
                            {

                                Assert.Equal("mkay", r3.Foo);
                                Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                Assert.Equal((int?)8675309, r3.Bar);
                                Assert.Equal((double?)987654321.012345, r3.Buzz);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task ReadAllAsync_PreAllocated()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            await RunAsyncReaderVariants<_ReadAll>(
                opts,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var pre = new List<_ReadAll>();
                        pre.Add(new _ReadAll { Bar = 1, Buzz = 2.2, Fizz = new DateTime(3, 3, 3), Foo = "4" });

                        var read = await csv.ReadAllAsync(pre);
                        Assert.Collection(
                            read,
                            r1 =>
                            {
                                Assert.Equal("4", r1.Foo);
                                Assert.Equal(new DateTime(3, 3, 3), r1.Fizz);
                                Assert.Equal(1, r1.Bar);
                                Assert.Equal(2.2, r1.Buzz);
                            },
                            r2 =>
                            {

                                Assert.Equal("hello\nworld", r2.Foo);
                                Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r2.Fizz);
                                Assert.Equal((int?)null, r2.Bar);
                                Assert.Equal(123.45, r2.Buzz);
                            },
                            r3 =>
                            {

                                Assert.Equal("", r3.Foo);
                                Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r3.Fizz);
                                Assert.Equal((int?)null, r3.Bar);
                                Assert.Equal((double?)null, r3.Buzz);
                            },
                            r4 =>
                            {

                                Assert.Equal("mkay", r4.Foo);
                                Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r4.Fizz);
                                Assert.Equal((int?)8675309, r4.Bar);
                                Assert.Equal((double?)987654321.012345, r4.Buzz);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task EnumerateAllAsync()
        {
            var CSV =
$@"Foo,Fizz,Bar,Buzz
""hello{'\n'}world"",{new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local)},,123.45
,{new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)},,
mkay,{new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local)},8675309,987654321.012345";

            var opts = Options.Default;

            await RunAsyncReaderVariants<_ReadAll>(
                opts,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var enumerable = csv.EnumerateAllAsync();
                        var enumerator = enumerable.GetAsyncEnumerator();
                        try
                        {
                            Assert.True(await enumerator.MoveNextAsync());
                            {
                                var r1 = enumerator.Current;

                                Assert.Equal("hello\nworld", r1.Foo);
                                Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                Assert.Equal((int?)null, r1.Bar);
                                Assert.Equal(123.45, r1.Buzz);
                            }

                            Assert.True(await enumerator.MoveNextAsync());
                            {
                                var r2 = enumerator.Current;

                                Assert.Equal("", r2.Foo);
                                Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                Assert.Equal((int?)null, r2.Bar);
                                Assert.Equal((double?)null, r2.Buzz);
                            }

                            Assert.True(await enumerator.MoveNextAsync());
                            {
                                var r3 = enumerator.Current;

                                Assert.Equal("mkay", r3.Foo);
                                Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                Assert.Equal((int?)8675309, r3.Bar);
                                Assert.Equal((double?)987654321.012345, r3.Buzz);
                            }

                            Assert.False(await enumerator.MoveNextAsync());
                        }
                        finally
                        {
                            await enumerator.DisposeAsync();
                        }
                    }
                }
            );

            await RunAsyncReaderVariants<_ReadAll>(
                opts,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = new List<_ReadAll>();

                        await foreach (var row in csv.EnumerateAllAsync())
                        {
                            rows.Add(row);
                        }

                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                Assert.Equal("hello\nworld", r1.Foo);
                                Assert.Equal(new DateTime(1990, 1, 2, 3, 4, 5, DateTimeKind.Local), r1.Fizz);
                                Assert.Equal((int?)null, r1.Bar);
                                Assert.Equal(123.45, r1.Buzz);
                            },
                            r2 =>
                            {
                                Assert.Equal("", r2.Foo);
                                Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local), r2.Fizz);
                                Assert.Equal((int?)null, r2.Bar);
                                Assert.Equal((double?)null, r2.Buzz);
                            },
                            r3 =>
                            {
                                Assert.Equal("mkay", r3.Foo);
                                Assert.Equal(new DateTime(2001, 6, 6, 6, 6, 6, DateTimeKind.Local), r3.Fizz);
                                Assert.Equal((int?)8675309, r3.Bar);
                                Assert.Equal((double?)987654321.012345, r3.Buzz);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task OneColumnOneRowAsync()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // normal
            {
                await RunAsyncReaderVariants<_OneColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("hello"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.NotNull(t.Value);
                            Assert.Equal("hello", t.Value.Foo);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }

                    }
                );
            }

            // quoted
            {
                await RunAsyncReaderVariants<_OneColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"hello world\""))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.NotNull(t.Value);
                            Assert.Equal("hello world", t.Value.Foo);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // escaped
            {
                await RunAsyncReaderVariants<_OneColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"hello \"\" world\""))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.NotNull(t.Value);
                            Assert.Equal("hello \" world", t.Value.Foo);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task TwoColumnOneRowAsync()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // normal
            {
                await RunAsyncReaderVariants<_TwoColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("hello,world"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.Equal("hello", t.Value.One);
                            Assert.Equal("world", t.Value.Two);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // quoted
            {
                await RunAsyncReaderVariants<_TwoColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"hello,world\",\"fizz,buzz\""))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.Equal("hello,world", t.Value.One);
                            Assert.Equal("fizz,buzz", t.Value.Two);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // escaped
            {
                await RunAsyncReaderVariants<_TwoColumnOneRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"hello\"\"world\",\"fizz\"\"buzz\""))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();

                            Assert.True(t.HasValue);
                            Assert.Equal("hello\"world", t.Value.One);
                            Assert.Equal("fizz\"buzz", t.Value.Two);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task TwoColumnTwoRowAsync()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

            // normal
            {
                await RunAsyncReaderVariants<_TwoColumnTwoRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("hello,world\r\nfoo,bar"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);

                            Assert.Equal("hello", t.Value.Fizz);
                            Assert.Equal("world", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.Equal("foo", t.Value.Fizz);
                            Assert.Equal("bar", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // quoted
            {
                await RunAsyncReaderVariants<_TwoColumnTwoRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"hello,world\",whatever\r\n\"foo,bar\",whoever"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal("hello,world", t.Value.Fizz);
                            Assert.Equal("whatever", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal("foo,bar", t.Value.Fizz);
                            Assert.Equal("whoever", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // escaped
            {
                await RunAsyncReaderVariants<_TwoColumnTwoRow>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"hello\"\"world\",whatever\r\n\"foo\"\"bar\",whoever"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal("hello\"world", t.Value.Fizz);
                            Assert.Equal("whatever", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal("foo\"bar", t.Value.Fizz);
                            Assert.Equal("whoever", t.Value.Buzz);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task DetectLineEndingsAsync()
        {
            var opts = Options.Default.NewBuilder().WithRowEnding(RowEndings.Detect).WithReadHeader(ReadHeaders.Never).Build();

            // normal
            {
                // \r\n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            using (var str = getReader("a,bb,ccc\r\ndddd,eeeee,ffffff\r\n1,2,3\r\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("eeeee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \r
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            using (var str = getReader("a,bb,ccc\rdddd,eeeee,ffffff\r1,2,3\r"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("eeeee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            using (var str = getReader("a,bb,ccc\ndddd,eeeee,ffffff\n1,2,3\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("eeeee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }
            }

            // quoted
            {
                // \r\n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            using (var str = getReader("\"a\r\",bb,ccc\r\ndddd,\"ee\neee\",ffffff\r\n1,2,\"3\r\n\"\r\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \r
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            using (var str = getReader("\"a\r\",bb,ccc\rdddd,\"ee\neee\",ffffff\r1,2,\"3\r\n\"\r"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            using (var str = getReader("\"a\r\",bb,ccc\ndddd,\"ee\neee\",ffffff\n1,2,\"3\r\n\"\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("bb", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("2", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }
            }

            // escaped
            {
                // \r\n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\n\"\"\"dddd\",\"ee\neee\",ffffff\r\n1,\"\"\"2\"\"\",\"3\r\n\"\r\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("b\"b", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("\"dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("\"2\"", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }

                // \r
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                         opts,
                         async (config, getReader) =>
                         {
                             using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\"\"\"dddd\",\"ee\neee\",ffffff\r1,\"\"\"2\"\"\",\"3\r\n\"\r"))
                             await using (var reader = config.CreateAsyncReader(str))
                             {
                                 var t1 = await reader.TryReadAsync();
                                 Assert.Equal("a\r", t1.Value.Foo);
                                 Assert.Equal("b\"b", t1.Value.Bar);
                                 Assert.Equal("ccc", t1.Value.Fizz);

                                 var t2 = await reader.TryReadAsync();
                                 Assert.Equal("\"dddd", t2.Value.Foo);
                                 Assert.Equal("ee\neee", t2.Value.Bar);
                                 Assert.Equal("ffffff", t2.Value.Fizz);

                                 var t3 = await reader.TryReadAsync();
                                 Assert.Equal("1", t3.Value.Foo);
                                 Assert.Equal("\"2\"", t3.Value.Bar);
                                 Assert.Equal("3\r\n", t3.Value.Fizz);

                                 var t4 = await reader.TryReadAsync();
                                 Assert.False(t4.HasValue);
                             }
                         }
                    );
                }

                // \n
                {
                    await RunAsyncReaderVariants<_DetectLineEndings>(
                        opts,
                        async (config, getReader) =>
                        {
                            using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\n\"\"\"dddd\",\"ee\neee\",ffffff\n1,\"\"\"2\"\"\",\"3\r\n\"\n"))
                            await using (var reader = config.CreateAsyncReader(str))
                            {
                                var t1 = await reader.TryReadAsync();
                                Assert.Equal("a\r", t1.Value.Foo);
                                Assert.Equal("b\"b", t1.Value.Bar);
                                Assert.Equal("ccc", t1.Value.Fizz);

                                var t2 = await reader.TryReadAsync();
                                Assert.Equal("\"dddd", t2.Value.Foo);
                                Assert.Equal("ee\neee", t2.Value.Bar);
                                Assert.Equal("ffffff", t2.Value.Fizz);

                                var t3 = await reader.TryReadAsync();
                                Assert.Equal("1", t3.Value.Foo);
                                Assert.Equal("\"2\"", t3.Value.Bar);
                                Assert.Equal("3\r\n", t3.Value.Fizz);

                                var t4 = await reader.TryReadAsync();
                                Assert.False(t4.HasValue);
                            }
                        }
                    );
                }
            }
        }



        [Fact]
        public async Task DetectHeadersAsync()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Detect).WithRowEnding(RowEndings.Detect).Build();

            // no headers
            await RunAsyncReaderVariants<_DetectHeaders>(
                opts,
                async (config, del) =>
                {
                    using (var str = del("123,4.56"))
                    await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                    {
                        var t = await reader.TryReadAsync();
                        Assert.True(t.HasValue);
                        Assert.Equal(123, t.Value.Hello);
                        Assert.Equal(4.56, t.Value.World);

                        Assert.Equal(ReadHeaders.Never, reader.ReadHeaders.Value);

                        Assert.Collection(
                            reader.Columns,
                            c => Assert.Equal("Hello", c.Name),
                            c => Assert.Equal("World", c.Name)
                        );

                        t = await reader.TryReadAsync();
                        Assert.False(t.HasValue);
                    }
                }
            );

            // headers
            {
                // \r\n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        using (var str = del("Hello,World\r\n123,4.56\r\n789,0.12\r\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("Hello", c.Name),
                                c => Assert.Equal("World", c.Name)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        using (var str = del("Hello,World\n123,4.56\n789,0.12\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("Hello", c.Name),
                                c => Assert.Equal("World", c.Name)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        using (var str = del("Hello,World\r123,4.56\r789,0.12\r"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("Hello", c.Name),
                                c => Assert.Equal("World", c.Name)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // headers, different order
            {
                // \r\n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        using (var str = del("World,Hello\r\n4.56,123\r\n0.12,789\r\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Equal("Hello", c.Name)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        using (var str = del("World,Hello\n4.56,123\n0.12,789\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Equal("Hello", c.Name)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        using (var str = del("World,Hello\r4.56,123\r0.12,789\r"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(123, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Equal("Hello", c.Name)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(789, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }

            // headers, missing
            {
                // \r\n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        using (var str = del("World,Foo\r\n4.56,123\r\n0.12,789\r\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Same(Column.Ignored, c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        using (var str = del("World,Foo\n4.56,123\n0.12,789\n"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Same(Column.Ignored, c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncReaderVariants<_DetectHeaders>(
                    opts,
                    async (config, del) =>
                    {
                        using (var str = del("World,Foo\r4.56,123\r0.12,789\r"))
                        await using (var reader = (AsyncReader<_DetectHeaders>)config.CreateAsyncReader(str))
                        {
                            var t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(4.56, t.Value.World);

                            Assert.Equal(ReadHeaders.Always, reader.ReadHeaders.Value);

                            Assert.Collection(
                                reader.Columns,
                                c => Assert.Equal("World", c.Name),
                                c => Assert.Same(Column.Ignored, c)
                            );

                            t = await reader.TryReadAsync();
                            Assert.True(t.HasValue);
                            Assert.Equal(0, t.Value.Hello);
                            Assert.Equal(0.12, t.Value.World);

                            t = await reader.TryReadAsync();
                            Assert.False(t.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task IsRequiredNotInHeaderAsync()
        {
            var opts = Options.Default;
            var CSV = "A,C\r\nhello,world";

            await RunAsyncReaderVariants<_IsRequiredMissing>(
                opts,
                async (config, makeReader) =>
                {
                    await using (var csv = config.CreateAsyncReader(makeReader(CSV)))
                    {
                        await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                    }
                }
            );
        }

        [Fact]
        public async Task IsRequiredNotInRowAsync()
        {
            var opts = Options.Default;

            // beginning
            {
                var CSV = "B,C\r\nhello,world\r\n,";

                await RunAsyncReaderVariants<_IsRequiredMissing>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var csv = config.CreateAsyncReader(makeReader(CSV)))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                        }
                    }
                );
            }

            // middle
            {
                var CSV = "A,B,C\r\nhello,world,foo\r\n,,";

                await RunAsyncReaderVariants<_IsRequiredMissing>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var csv = config.CreateAsyncReader(makeReader(CSV)))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                        }
                    }
                );
            }

            // end
            {
                var CSV = "A,B\r\nhello,world\r\n,";

                await RunAsyncReaderVariants<_IsRequiredMissing>(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var csv = config.CreateAsyncReader(makeReader(CSV)))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task DifferentEscapesAsync()
        {
            var opts = Options.Default.NewBuilder().WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter('\\').Build();

            // simple
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("Foo,Bar\r\nhello,world"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("Foo,Bar\r\n\"hello\",\"world\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped with quotes
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("Foo,Bar\r\n\"he\\\"llo\",\"world\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("he\"llo", a.Foo); Assert.Equal("world", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escaped with slash
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("Foo,Bar\r\n\"hello\",\"w\\\\orld\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", a.Foo); Assert.Equal("w\\orld", a.Bar); }
                            );
                        }
                    }
                );
            }

            // escape char outside of quotes
            {
                await RunAsyncReaderVariants<_DifferentEscapes>(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("Foo,Bar\r\n\\,\\ooo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("\\", a.Foo); Assert.Equal("\\ooo", a.Bar); }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task TabSeparatorAsync()
        {
            const string TSV = @"Foo	Bar
""hello""""world""	123
";
            var opts = Options.Default.NewBuilder().WithEscapedValueStartAndEnd('"').WithValueSeparator('\t').Build();

            await RunAsyncReaderVariants<_TabSeparator>(
                opts,
                async (config, getReader) =>
                {
                    using (var reader = getReader(TSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello\"world", a.Foo); Assert.Equal(123, a.Bar); }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task CommentsAsync()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').Build();

            // comment first line
            {
                var CSV = "#this is a test comment!\r\nhello,world\r\nfoo,bar";
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            // comment after header
            {
                var CSV = "hello,world\r\n#this is a test comment\r\nfoo,bar";
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            // comment between rows
            {
                var CSV = "hello,world\r\nfoo,bar\r\n#comment!\r\nfizz,buzz";
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); },
                                b => { Assert.Equal("fizz", b.Hello); Assert.Equal("buzz", b.World); }
                            );
                        }
                    }
                );
            }

            // comment at end
            {
                var CSV = "hello,world\r\nfoo,bar\r\n#comment!";
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }
        }
    }
#pragma warning restore IDE1006
}