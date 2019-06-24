using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class DynamicWriterTests
    {
        private static dynamic MakeDynamicRow(string csvStr)
        {
            var opts =
                Options.Default
                    .NewBuilder()
                    .WithReadHeader(ReadHeaders.Always)
                    .WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose)
                    .Build();

            var config = Configuration.ForDynamic(opts);

            using (var reader = new StringReader(csvStr))
            using (var csv = config.CreateReader(reader))
            {
                Assert.True(csv.TryRead(out var ret));
                Assert.False(csv.TryRead(out _));

                return ret;
            }
        }

        [Fact]
        public void WriteComment()
        {
            // todo: async

            var dynOpts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no trailing new line
            {
                // first line, no headers
                {
                    var opts = dynOpts.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

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
                    var opts = dynOpts.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

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
                    var opts = dynOpts.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

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
                    var opts = dynOpts.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

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
                    var opts = dynOpts.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

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
                    var opts = dynOpts.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

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
                    var opts = dynOpts.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

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
                    var opts = dynOpts.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

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
            // todo: async

            // \r\n
            {
                var opts =
                    Options.Default
                        .NewBuilder()
                        .WithReadHeader(ReadHeaders.Always)
                        .WithWriteHeader(WriteHeaders.Always)
                        .WithRowEnding(RowEndings.CarriageReturnLineFeed)
                        .Build();

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
            // todo: async

            // \r\n
            {
                var opts =
                    Options.DynamicDefault
                        .NewBuilder()
                        .WithWriteHeader(WriteHeaders.Always)
                        .WithRowEnding(RowEndings.CarriageReturnLineFeed)
                        .Build();

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
                    Options.DynamicDefault
                        .NewBuilder()
                        .WithWriteHeader(WriteHeaders.Always)
                        .WithRowEnding(RowEndings.CarriageReturn)
                        .Build();

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
                    Options.DynamicDefault
                        .NewBuilder()
                        .WithWriteHeader(WriteHeaders.Always)
                        .WithRowEnding(RowEndings.LineFeed)
                        .Build();

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
            // todo: async

            // \r\n
            {
                var opts = 
                    Options.DynamicDefault.NewBuilder()
                        .WithWriteHeader(WriteHeaders.Never)
                        .WithRowEnding(RowEndings.CarriageReturnLineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingNewLine(WriteTrailingNewLines.Always)
                        .Build();

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
                    Options.DynamicDefault.NewBuilder()
                        .WithWriteHeader(WriteHeaders.Never)
                        .WithRowEnding(RowEndings.CarriageReturn)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingNewLine(WriteTrailingNewLines.Always)
                        .Build();

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
                    Options.DynamicDefault.NewBuilder()
                        .WithWriteHeader(WriteHeaders.Never)
                        .WithRowEnding(RowEndings.LineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingNewLine(WriteTrailingNewLines.Always)
                        .Build();

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
            // todo: async

            // \r\n
            {
                var opts = 
                    Options.DynamicDefault
                        .NewBuilder()
                        .WithWriteHeader(WriteHeaders.Always)
                        .WithRowEnding(RowEndings.CarriageReturnLineFeed)
                        .WithWriteTrailingNewLine(WriteTrailingNewLines.Always)
                        .Build();

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
                    Options.DynamicDefault
                        .NewBuilder()
                        .WithWriteHeader(WriteHeaders.Always)
                        .WithRowEnding(RowEndings.CarriageReturn)
                        .WithWriteTrailingNewLine(WriteTrailingNewLines.Always)
                        .Build();

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
                    Options.DynamicDefault
                        .NewBuilder()
                        .WithWriteHeader(WriteHeaders.Always)
                        .WithRowEnding(RowEndings.LineFeed)
                        .WithWriteTrailingNewLine(WriteTrailingNewLines.Always)
                        .Build();

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
            // todo: async

            // \r\n
            {
                var opts = Options.DynamicDefault.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

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
                var opts = Options.DynamicDefault.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturn).Build();

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
                var opts = Options.DynamicDefault.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.LineFeed).Build();

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
            // todo: async

            // \r\n
            {
                var opts = Options.DynamicDefault.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

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
                var opts = Options.DynamicDefault.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).Build();

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
                var opts = Options.DynamicDefault.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.LineFeed).Build();

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
    }
}
