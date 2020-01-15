using System;
using System.Buffers;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class DynamicWriterTests
    {
        private static dynamic MakeDynamicRow(string csvStr)
        {
            var opts =
                Options.CreateBuilder(Options.Default)
                    .WithReadHeader(ReadHeader.Always)
                    .WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose)
                    .ToOptions();

            var config = Configuration.ForDynamic(opts);

            using (var reader = new StringReader(csvStr))
            using (var csv = config.CreateReader(reader))
            {
                Assert.True(csv.TryRead(out var ret));
                Assert.False(csv.TryRead(out _));

                return ret;
            }
        }

        private sealed class _ChainedFormatters_Context
        {
            public int F { get; set; }
        }

        private sealed class _ChainedFormatters_TypeDescriber : DefaultTypeDescriber
        {
            private readonly Formatter F;

            public _ChainedFormatters_TypeDescriber(Formatter f)
            {
                F = f;
            }

            public override IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext context, dynamic row)
            {
                string val = row.Foo;

                var ret = DynamicCellValue.Create("Foo", val, F);

                return new[] { ret };
            }
        }

        [Fact]
        public void ChainedFormatters()
        {
            var f1 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 1) return false;

                        var span = writer.GetSpan(4);
                        span[0] = '1';
                        span[1] = '2';
                        span[2] = '3';
                        span[3] = '4';

                        writer.Advance(4);

                        return true;
                    }
                );
            var f2 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 2) return false;

                        var span = writer.GetSpan(3);
                        span[0] = 'a';
                        span[1] = 'b';
                        span[2] = 'c';

                        writer.Advance(3);

                        return true;
                    }
                );
            var f3 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 3) return false;

                        var span = writer.GetSpan(2);
                        span[0] = '0';
                        span[1] = '0';

                        writer.Advance(2);

                        return true;
                    }
                );

            var f = f1.Else(f2).Else(f3);

            var td = new _ChainedFormatters_TypeDescriber(f);

            var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(td).ToOptions();



            var row = MakeDynamicRow("Foo\r\nabc");
            try
            {
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        var ctx = new _ChainedFormatters_Context();

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer, ctx))
                        {
                            ctx.F = 1;
                            csv.Write(row);
                            ctx.F = 2;
                            csv.Write(row);
                            ctx.F = 3;
                            csv.Write(row);
                            ctx.F = 1;
                            csv.Write(row);
                        }

                        var str = getStr();
                        Assert.Equal("Foo\r\n1234\r\nabc\r\n00\r\n1234", str);
                    }
                );
            }
            finally
            {
                row.Dispose();
            }
        }

        [Fact]
        public void NoEscapes()
        {
            // no escapes at all (TSV)
            {
                var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithValueSeparator('\t').WithEscapedValueStartAndEnd(null).WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new { Foo = "abc", Bar = "123" });
                            csv.Write(new { Foo = "\"", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo\tBar\r\nabc\t123\r\n\"\t,", str);
                    }
                );

                // explodes if there's an value separator in a value, since there are no escapes
                {
                    // \t
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new { Foo = "\t", Bar = "foo" }));

                                Assert.Equal("Tried to write a value contain '\t' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );

                    // \r\n
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new { Foo = "foo", Bar = "\r\n" }));

                                Assert.Equal("Tried to write a value contain '\r' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );

                    var optsWithComment = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();
                    // #
                    RunSyncDynamicWriterVariants(
                        optsWithComment,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new { Foo = "#", Bar = "fizz" }));

                                Assert.Equal("Tried to write a value contain '#' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );
                }
            }

            // escapes, but no escape for the escape start and end char
            {
                var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithValueSeparator('\t').WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new { Foo = "a\tbc", Bar = "#123" });
                            csv.Write(new { Foo = "\r", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t#123\r\n\"\r\"\t,", str);
                    }
                );

                var optsWithComments = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();

                // correct with comments
                RunSyncDynamicWriterVariants(
                    optsWithComments,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new { Foo = "a\tbc", Bar = "#123" });
                            csv.Write(new { Foo = "\r", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t\"#123\"\r\n\"\r\"\t,", str);
                    }
                );

                // explodes if there's an escape start character in a value, since it can't be escaped
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new { Foo = "a\tbc", Bar = "\"" }));

                            Assert.Equal("Tried to write a value contain '\"' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured", inv.Message);
                        }

                        getStr();
                    }
                );
            }
        }

        [Fact]
        public void NullComment()
        {
            RunSyncDynamicWriterVariants(
                Options.DynamicDefault,
                (config, getWriter, getStr) =>
                {
                    using (var w = getWriter())
                    using (var csv = config.CreateWriter(w))
                    {
                        Assert.Throws<ArgumentNullException>(() => csv.WriteComment(null));
                    }

                    var res = getStr();
                    Assert.NotNull(res);
                }
            );
        }

        private sealed class _FailingDynamicCellFormatter : DefaultTypeDescriber
        {
            private readonly int CellNum;
            private readonly int FailOn;

            public _FailingDynamicCellFormatter(int cellNum, int failOn)
            {
                CellNum = cellNum;
                FailOn = failOn;
            }

            public override IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in Cesil.WriteContext ctx, dynamic row)
            {
                var ret = new List<DynamicCellValue>();

                for (var i = 0; i < CellNum; i++)
                {
                    var f =
                        i == FailOn ?
                            Formatter.ForDelegate((string value, in WriteContext context, IBufferWriter<char> buffer) => false) :
                            Formatter.ForDelegate((string value, in WriteContext context, IBufferWriter<char> buffer) => true);

                    ret.Add(DynamicCellValue.Create("Bar" + i, "foo" + i, f));
                }

                return ret;
            }
        }

        [Fact]
        public void FailingDynamicCellFormatter()
        {
            const int MAX_CELLS = 20;

            for (var i = 0; i < MAX_CELLS; i++)
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(new _FailingDynamicCellFormatter(MAX_CELLS, i)).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var w = getWriter())
                        using (var csv = config.CreateWriter(w))
                        {
                            Assert.Throws<SerializationException>(() => csv.Write(new object()));
                        }

                        getStr();
                    }
                );
            }
        }

        [Fact]
        public void LotsOfComments()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

            RunSyncDynamicWriterVariants(
                opts,
                (config, getWriter, getStr) =>
                {
                    var cs = string.Join("\r\n", System.Linq.Enumerable.Repeat("foo", 1_000));

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.WriteComment(cs);
                    }

                    var str = getStr();
                    var expected = string.Join("\r\n", System.Linq.Enumerable.Repeat("#foo", 1_000));
                    Assert.Equal(expected, str);
                }
            );
        }

        [Fact]
        public void WriteComment()
        {
            var dynOpts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("#hello", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }
            }

            // trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }
            }
        }

        [Fact]
        public void NeedEscapeColumnNames()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.Default)
                        .WithReadHeader(ReadHeader.Always)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo = MakeDynamicRow("\"He,llo\",\"\"\"\"\r\n123,456");

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo);
                        }

                        foo.Dispose();

                        var res = getStr();

                        Assert.Equal("\"He,llo\",\"\"\"\"\r\n123,456", res);
                    }
                );
            }
        }

        [Fact]
        public void Simple()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                        .ToOptions();

                // DynamicRow
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // ExpandoObject
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // underlying is statically typed
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // all of 'em mixed together
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                            csv.Write(foo3);
                            csv.Write(foo4);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n111,789\r\n333,222\r\n789,456", res);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturn)
                        .ToOptions();

                // DynamicRow
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // ExpandoObject
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // underlying is statically typed
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // all of 'em mixed together
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                            csv.Write(foo3);
                            csv.Write(foo4);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r111,789\r333,222\r789,456", res);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.LineFeed)
                        .ToOptions();

                // DynamicRow
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // ExpandoObject
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // underlying is statically typed
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // all of 'em mixed together
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                            csv.Write(foo3);
                            csv.Write(foo4);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n111,789\n333,222\n789,456", res);
                    }
                );
            }
        }

        [Fact]
        public void CommentEscape()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\r\n", txt);
                    }
                );

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithRowEnding(RowEnding.CarriageReturn)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\r", txt);
                    }
                );

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithRowEnding(RowEnding.LineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\n", txt);
                    }
                );

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\n", txt);
                    }
                );
            }
        }

        [Fact]
        public void EscapeHeaders()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            writer.Write(expando1);
                            writer.Write(expando2);
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\nfizz,buzz,yes\r\nping,pong,no\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturn)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            writer.Write(expando1);
                            writer.Write(expando2);
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\rfizz,buzz,yes\rping,pong,no\r", txt);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.LineFeed)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            writer.Write(expando1);
                            writer.Write(expando2);
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\nfizz,buzz,yes\nping,pong,no\n", txt);
                    }
                );
            }
        }

        [Fact]
        public void NeedEscape()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            writer.Write(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\r\n\"foo\"\"bar\",789,\r\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            writer.Write(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\r\"foo\"\"bar\",789,\r\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.LineFeed).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            writer.Write(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\n\"foo\"\"bar\",789,\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // large
            {
                var opts = Options.DynamicDefault;
                var val = string.Join("", System.Linq.Enumerable.Repeat("abc\r\n", 450));

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new { Foo = val, Bar = 001, Nope = 009 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Nope\r\n\"abc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\n\",1,9", txt);
                    }
                );
            }
        }

        [Fact]
        public void WriteAll()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\rhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\rhello,456,,1980-02-02 01:01:01Z\r,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\nhello,456,,1980-02-02 01:01:01Z\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }

        // async tests

        [Fact]
        public async Task ChainedFormattersAsync()
        {
            var f1 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 1) return false;

                        var span = writer.GetSpan(4);
                        span[0] = '1';
                        span[1] = '2';
                        span[2] = '3';
                        span[3] = '4';

                        writer.Advance(4);

                        return true;
                    }
                );
            var f2 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 2) return false;

                        var span = writer.GetSpan(3);
                        span[0] = 'a';
                        span[1] = 'b';
                        span[2] = 'c';

                        writer.Advance(3);

                        return true;
                    }
                );
            var f3 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 3) return false;

                        var span = writer.GetSpan(2);
                        span[0] = '0';
                        span[1] = '0';

                        writer.Advance(2);

                        return true;
                    }
                );

            var f = f1.Else(f2).Else(f3);

            var td = new _ChainedFormatters_TypeDescriber(f);

            var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(td).ToOptions();

            var row = MakeDynamicRow("Foo\r\nabc");
            try
            {
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        var ctx = new _ChainedFormatters_Context();

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer, ctx))
                        {
                            ctx.F = 1;
                            await csv.WriteAsync(row);
                            ctx.F = 2;
                            await csv.WriteAsync(row);
                            ctx.F = 3;
                            await csv.WriteAsync(row);
                            ctx.F = 1;
                            await csv.WriteAsync(row);
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\r\n1234\r\nabc\r\n00\r\n1234", str);
                    }
                );
            }
            finally
            {
                row.Dispose();
            }
        }

        [Fact]
        public async Task NoEscapesAsync()
        {
            // no escapes at all (TSV)
            {
                var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithValueSeparator('\t').WithEscapedValueStartAndEnd(null).WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new { Foo = "abc", Bar = "123" });
                            await csv.WriteAsync(new { Foo = "\"", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\tBar\r\nabc\t123\r\n\"\t,", str);
                    }
                );

                // explodes if there's an value separator in a value, since there are no escapes
                {
                    // \t
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new { Foo = "\t", Bar = "foo" }));

                                Assert.Equal("Tried to write a value contain '\t' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );

                    // \r\n
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new { Foo = "foo", Bar = "\r\n" }));

                                Assert.Equal("Tried to write a value contain '\r' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );

                    var optsWithComment = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();
                    // #
                    await RunAsyncDynamicWriterVariants(
                        optsWithComment,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new { Foo = "#", Bar = "fizz" }));

                                Assert.Equal("Tried to write a value contain '#' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );
                }
            }

            // escapes, but no escape for the escape start and end char
            {
                var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithValueSeparator('\t').WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new { Foo = "a\tbc", Bar = "#123" });
                            await csv.WriteAsync(new { Foo = "\r", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t#123\r\n\"\r\"\t,", str);
                    }
                );

                var optsWithComments = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();

                // correct with comments
                await RunAsyncDynamicWriterVariants(
                    optsWithComments,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new { Foo = "a\tbc", Bar = "#123" });
                            await csv.WriteAsync(new { Foo = "\r", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t\"#123\"\r\n\"\r\"\t,", str);
                    }
                );

                // explodes if there's an escape start character in a value, since it can't be escaped
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new { Foo = "a\tbc", Bar = "\"" }));

                            Assert.Equal("Tried to write a value contain '\"' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured", inv.Message);
                        }

                        await getStr();
                    }
                );
            }
        }

        [Fact]
        public async Task NullCommentAsync()
        {
            await RunAsyncDynamicWriterVariants(
                Options.DynamicDefault,
                async (config, getWriter, getStr) =>
                {
                    await using (var w = getWriter())
                    await using (var csv = config.CreateAsyncWriter(w))
                    {
                        await Assert.ThrowsAsync<ArgumentNullException>(async () => await csv.WriteCommentAsync(null));
                    }

                    var res = await getStr();
                    Assert.NotNull(res);
                }
            );
        }

        [Fact]
        public async Task FailingDynamicCellFormatterAsync()
        {
            const int MAX_CELLS = 20;
            for (var i = 0; i < MAX_CELLS; i++)
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(new _FailingDynamicCellFormatter(MAX_CELLS, i)).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var w = getWriter())
                        await using (var csv = config.CreateAsyncWriter(w))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.WriteAsync(new object()));
                        }

                        await getStr();
                    }
                );
            }
        }

        [Fact]
        public async Task LotsOfCommentsAsync()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

            await RunAsyncDynamicWriterVariants(
                opts,
                async (config, getWriter, getStr) =>
                {
                    var cs = string.Join("\r\n", System.Linq.Enumerable.Repeat("foo", 1_000));

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteCommentAsync(cs);
                    }

                    var str = await getStr();
                    var expected = string.Join("\r\n", System.Linq.Enumerable.Repeat("#foo", 1_000));
                    Assert.Equal(expected, str);
                }
            );
        }

        [Fact]
        public async Task SimpleAsync()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                        .ToOptions();

                // DynamicRow
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // ExpandoObject
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // underlying is statically typed
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // all of 'em mixed together
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                            await csv.WriteAsync(foo3);
                            await csv.WriteAsync(foo4);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n111,789\r\n333,222\r\n789,456", res);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturn)
                        .ToOptions();

                // DynamicRow
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // ExpandoObject
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // underlying is statically typed
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // all of 'em mixed together
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                            await csv.WriteAsync(foo3);
                            await csv.WriteAsync(foo4);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r111,789\r333,222\r789,456", res);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.LineFeed)
                        .ToOptions();

                // DynamicRow
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // ExpandoObject
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // underlying is statically typed
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // all of 'em mixed together
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                            await csv.WriteAsync(foo3);
                            await csv.WriteAsync(foo4);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n111,789\n333,222\n789,456", res);
                    }
                );
            }
        }

        [Fact]
        public async Task WriteCommentAsync()
        {
            var dynOpts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }
            }

            // trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task NeedEscapeColumnNamesAsync()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithReadHeader(ReadHeader.Always)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo = MakeDynamicRow("\"He,llo\",\"\"\"\"\r\n123,456");

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo);
                        }

                        foo.Dispose();

                        var res = await getStr();

                        Assert.Equal("\"He,llo\",\"\"\"\"\r\n123,456", res);
                    }
                );
            }
        }

        [Fact]
        public async Task CommentEscapeAsync()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                        }

                        var txt = await getString();
                        Assert.Equal("\"#hello\",foo\r\n", txt);
                    }
                );

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                         }

                         var txt = await getString();
                         Assert.Equal("hello,\"fo#o\"\r\n", txt);
                     }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithRowEnding(RowEnding.CarriageReturn)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                         }

                         var txt = await getString();
                         Assert.Equal("\"#hello\",foo\r", txt);
                     }
                );

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                         }

                         var txt = await getString();
                         Assert.Equal("hello,\"fo#o\"\r", txt);
                     }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithRowEnding(RowEnding.LineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                         }

                         var txt = await getString();
                         Assert.Equal("\"#hello\",foo\n", txt);
                     }
                );

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                         }

                         var txt = await getString();
                         Assert.Equal("hello,\"fo#o\"\n", txt);
                     }
                );
            }
        }

        [Fact]
        public async Task EscapeHeadersAsync()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            await writer.WriteAsync(expando1);
                            await writer.WriteAsync(expando2);
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\nfizz,buzz,yes\r\nping,pong,no\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.CarriageReturn)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            await writer.WriteAsync(expando1);
                            await writer.WriteAsync(expando2);
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\rfizz,buzz,yes\rping,pong,no\r", txt);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithRowEnding(RowEnding.LineFeed)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            await writer.WriteAsync(expando1);
                            await writer.WriteAsync(expando2);
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\nfizz,buzz,yes\nping,pong,no\n", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task NeedEscapeAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            await writer.WriteAsync(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello,world\",123,456\r\n\"foo\"\"bar\",789,\r\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            await writer.WriteAsync(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello,world\",123,456\r\"foo\"\"bar\",789,\r\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.LineFeed).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            await writer.WriteAsync(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello,world\",123,456\n\"foo\"\"bar\",789,\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // large
            {
                var opts = Options.DynamicDefault;
                var val = string.Join("", System.Linq.Enumerable.Repeat("abc\r\n", 450));

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new { Foo = val, Bar = 001, Nope = 009 });
                        }

                        var txt = await getString();
                        Assert.Equal("Foo,Bar,Nope\r\n\"abc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\n\",1,9", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task WriteAllAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = await getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = await getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\rhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\rhello,456,,1980-02-02 01:01:01Z\r,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = await getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\nhello,456,,1980-02-02 01:01:01Z\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }
    }
}
