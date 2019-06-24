using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class DynamicReaderTests
    {
        [Fact]
        public void WithComments()
        {
            // \r\n
            {
                var opts1 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).WithReadHeader(ReadHeaders.Always).Build();
                var opts2 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).WithReadHeader(ReadHeaders.Never).Build();

                // with headers
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\r\n#comment\rwhatever\r\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\nA,Nope\r\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\n#again!###foo###"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \r
            {
                var opts1 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).WithReadHeader(ReadHeaders.Always).Build();
                var opts2 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).WithReadHeader(ReadHeaders.Never).Build();

                // with headers
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\r#comment\nwhatever\rhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\rA,Nope\rhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\rhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\r#again!###foo###"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \n
            {
                var opts1 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).WithReadHeader(ReadHeaders.Always).Build();
                var opts2 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).WithReadHeader(ReadHeaders.Never).Build();

                // with headers
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\n#comment\rwhatever\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\nA,Nope\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\n#again!###foo###"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }
        }

        [Fact]
        public void WeirdComments()
        {
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).WithReadHeader(ReadHeaders.Always).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\r\nhello,world\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\n#this is a test comment!\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }

            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).WithReadHeader(ReadHeaders.Always).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\rhello,world\rfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r#this is a test comment!\n\rfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }

            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).WithReadHeader(ReadHeaders.Always).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\r\nhello,world\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                RunSyncDynamicReaderVariants(
                   opts,
                   (config, getReader) =>
                   {
                       var CSV = "#this is a test comment!\r\r\nhello,world\r\nfoo,bar";
                       using (var str = getReader(CSV))
                       using (var csv = config.CreateReader(str))
                       {
                           var rows = csv.ReadAll();
                           Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                       }
                   }
               );

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\n\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\r\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }
        }

        [Fact]
        public void Comments()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithReadHeader(ReadHeaders.Always).Build();

            // comment first line
            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    var CSV = "#this is a test comment!\r\nhello,world\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );

            // comment after header
            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\n#this is a test comment\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );

            // comment between rows
            RunSyncDynamicReaderVariants(
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
                            a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); },
                            b => { Assert.Equal("fizz", (string)b.hello); Assert.Equal("buzz", (string)b.world); }
                        );
                    }
                }
            );

            // comment at end
            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );
        }

        [Fact]
        public void RangeUseableAfterDisposeOrReuse()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var row));
                        Assert.False(csv.TryRead(out _));

                        Assert.Equal("1", (string)row[0]);
                        Assert.Equal("2", (string)row[1]);
                        Assert.Equal("3", (string)row[2]);

                        var subset = row[1..3];
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        row.Dispose();

                        Assert.Throws<ObjectDisposedException>(() => row[0]);

                        // subset should still be good here
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        subset.Dispose();

                        Assert.Throws<ObjectDisposedException>(() => subset[0]);
                    }
                }
            );

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3\r\n4,5,6"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var row));

                        Assert.Equal("1", (string)row[0]);
                        Assert.Equal("2", (string)row[1]);
                        Assert.Equal("3", (string)row[2]);

                        var subset = row[1..3];
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        Assert.True(csv.TryReadWithReuse(ref row));
                        Assert.False(csv.TryRead(out _));

                        // row has now changed
                        Assert.Equal("4", (string)row[0]);
                        Assert.Equal("5", (string)row[1]);
                        Assert.Equal("6", (string)row[2]);

                        // subset should be unmodified
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        row.Dispose();

                        Assert.Throws<ObjectDisposedException>(() => row[0]);

                        // subset should still be good
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        subset.Dispose();

                        Assert.Throws<ObjectDisposedException>(() => subset[0]);
                    }
                }
            );
        }

        [Fact]
        public void Range()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            var equivalent = new[] { "1", "2", "3" };

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var row));
                        Assert.False(csv.TryRead(out _));

                        var all = 0..3;
                        var allEnd = ^3..^0;
                        var allImp = ..;

                        var skip1Front = 1..3;
                        var skip1FrontImp = 1..;
                        var skip1Back = 0..2;
                        var skip1BackImp = ..2;
                        var skip1FrontEnd = ^2..^0;
                        var skip1FrontEndImp = ^2..;
                        var skip1BackEnd = ^3..^1;
                        var skip1BackEndImp = ..^1;

                        var skip2Front = 2..3;
                        var skip2FrontImp = 2..;
                        var skip2Back = 0..1;
                        var skip2BackImp = ..1;
                        var skip2FrontEnd = ^1..^0;
                        var skip2FrontEndImp = ^1..;
                        var skip2BackEnd = ^3..^2;
                        var skip2BackEndImp = ..^2;

                        var emptyZero = 0..0;
                        var emptyZeroEnd = ^0..^0;
                        var emptyOne = 1..1;
                        var emptyOneEnd = ^1..^1;
                        var emptyTwo = 2..2;
                        var emptyTwoEnd = ^2..^2;

                        Action<Range> check =
                            range =>
                            {
                                var dynRes = row[range];
                                var shouldMatchRes = equivalent[range];

                                for (var i = 0; i < shouldMatchRes.Length; i++)
                                {
                                    Assert.Equal(shouldMatchRes[i], (string)dynRes[i]);
                                }

                                var ix = 0;
                                foreach (string val in dynRes)
                                {
                                    Assert.Equal(shouldMatchRes[ix], val);
                                    ix++;
                                }
                            };

                        check(all);
                        check(allEnd);
                        check(allImp);

                        check(skip1Front);
                        check(skip1FrontImp);
                        check(skip1Back);
                        check(skip1BackImp);
                        check(skip1FrontEnd);
                        check(skip1FrontEndImp);
                        check(skip1BackEnd);
                        check(skip1BackEndImp);

                        check(skip2Front);
                        check(skip2FrontImp);
                        check(skip2Back);
                        check(skip2BackImp);
                        check(skip2FrontEnd);
                        check(skip2FrontEndImp);
                        check(skip2BackEnd);
                        check(skip2BackEndImp);

                        check(emptyZero);
                        check(emptyZeroEnd);
                        check(emptyOne);
                        check(emptyOneEnd);
                        check(emptyTwo);
                        check(emptyTwoEnd);
                    }
                }
            );
        }

        [Fact]
        public void Index()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var row));
                        Assert.False(csv.TryRead(out _));

                        var zeroFromStart = new Index(0, false);
                        var oneFromStart = new Index(1, false);
                        var twoFromStart = new Index(2, false);

                        var oneFromEnd = new Index(1, true);
                        var twoFromEnd = new Index(2, true);
                        var threeFromEnd = new Index(3, true);

                        int a1 = row[zeroFromStart];
                        int b1 = row[oneFromStart];
                        int c1 = row[twoFromStart];

                        int a2 = row[threeFromEnd];
                        int b2 = row[twoFromEnd];
                        int c2 = row[oneFromEnd];

                        int a3 = row[(Index)0];
                        int b3 = row[(Index)1];
                        int c3 = row[(Index)2];

                        int a4 = row[^3];
                        int b4 = row[^2];
                        int c4 = row[^1];

                        Assert.Equal(1, a1);
                        Assert.Equal(1, a2);
                        Assert.Equal(1, a3);
                        Assert.Equal(1, a4);

                        Assert.Equal(2, b1);
                        Assert.Equal(2, b2);
                        Assert.Equal(2, b3);
                        Assert.Equal(2, b4);

                        Assert.Equal(3, c1);
                        Assert.Equal(3, c2);
                        Assert.Equal(3, c3);
                        Assert.Equal(3, c4);
                    }
                }
            );
        }

        [Fact]
        public void DynamicCellErrors()
        {
            // missing conversion
            {
                var row = MakeRow();
                var cell = row[0];

                dynamic o = null;

                Action cast = () => GC.KeepAlive((DynamicReaderTests)o);

                o = cell;
                Assert.Throws<InvalidOperationException>(cast);

                o = this;
                cast();

                row.Dispose();
            }

            // bad conversion
            {
                ParserDelegate<Guid> parser =
                    (ReadOnlySpan<char> data, in ReadContext ctx, out Guid val) =>
                    {
                        val = Guid.NewGuid();
                        return true;
                    };
                var conv = new _DynamicRowOrCellErrors((Parser)parser);

                var row = MakeRow(conv);
                var cell = row[0];

                Assert.Throws<InvalidOperationException>(() => _Cast<(int, int, int)>(cell));

                _Cast<Guid>(cell);

                row.Dispose();
            }

            // create a test row
            dynamic MakeRow(ITypeDescriber c = null)
            {
                var opts =
                    Options.Default
                        .NewBuilder()
                        .WithReadHeader(ReadHeaders.Never)
                        .WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose)
                        .WithTypeDescriber(c ?? TypeDescribers.Default)
                        .Build();
                var config = Configuration.ForDynamic(opts);

                using (var str = new System.IO.StringReader("1,2,3"))
                using (var csv = config.CreateReader(str))
                {
                    var rows = csv.ReadAll();

                    return rows.Single();
                }
            }
        }

        [Fact]
        public void ChangingRowIndexTypes()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
            var config = Configuration.ForDynamic(opts);

            using (var str = new System.IO.StringReader("a,b,c\r\n1,2,3"))
            using (var csv = config.CreateReader(str))
            {
                var row = csv.ReadAll().Single();

                var ix = 1;
                var key = "c";

                dynamic lookup = null;

                Func<dynamic> get = () => row[lookup];

                lookup = ix;
                int b = get();

                lookup = key;
                int c = get();

                Assert.Equal(2, b);
                Assert.Equal(3, c);
            }
        }

        private class _DynamicRowOrCellErrors : ITypeDescriber
        {
            private readonly Parser P;
            private readonly DynamicRowConverter D;

            public _DynamicRowOrCellErrors(Parser p)
            {
                P = p;
                D = null;
            }

            public _DynamicRowOrCellErrors(DynamicRowConverter d)
            {
                P = null;
                D = d;
            }

            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            => P ?? TypeDescribers.Default.GetDynamicCellParserFor(in ctx, targetType);

            public IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext ctx, object row)
            => TypeDescribers.Default.GetCellsForDynamicRow(in ctx, row);

            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => D ?? TypeDescribers.Default.GetDynamicRowConverter(in ctx, columns, targetType);

            public InstanceBuilder GetInstanceBuilder(TypeInfo forType)
            => TypeDescribers.Default.GetInstanceBuilder(forType);

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToSerialize(forType);

            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToDeserialize(forType);
        }

        [Fact]
        public void DynamicRowErrors()
        {
            // bad indexing
            {
                var row = MakeRow();
                var correct = new int[2, 2];

                dynamic o = null;

                Action twoIndexes = () => GC.KeepAlive(o[0, 1]);

                o = row;
                Assert.Throws<InvalidOperationException>(twoIndexes);
                o = correct;
                twoIndexes();

                row.Dispose();
            }

            // bad index type
            {
                var row = MakeRow();
                var correct = new Dictionary<Guid, Guid>();
                var key = Guid.NewGuid();
                correct[key] = Guid.NewGuid();

                dynamic o = null;

                Action guidIndex = () => GC.KeepAlive(o[key]);

                o = row;
                Assert.Throws<InvalidOperationException>(guidIndex);
                o = correct;
                guidIndex();

                row.Dispose();
            }

            // missing row conversion
            {
                var row = MakeRow();
                var ok = Guid.NewGuid();
                dynamic o = null;

                Action cast = () => GC.KeepAlive((Guid)o);

                o = row;
                Assert.Throws<InvalidOperationException>(cast);
                o = ok;
                cast();

                row.Dispose();
            }

            // bad row conversion
            {
                DynamicRowConverterDelegate<ValueTuple<int, int, int>> del =
                    (dynamic row, in ReadContext ctx, out ValueTuple<int, int, int> res) =>
                    {
                        int a = row[0];
                        int b = row[1];
                        int c = row[2];

                        res = (a, b, c);

                        return true;
                    };
                var conv = new _DynamicRowOrCellErrors((DynamicRowConverter)del);

                var row = MakeRow(conv);

                Assert.Throws<InvalidOperationException>(() => _Cast<Guid>(row));

                _Cast<(int A, int B, int C)>(row);

                row.Dispose();
            }

            // create a test row
            dynamic MakeRow(ITypeDescriber c = null)
            {
                var opts =
                    Options.Default
                        .NewBuilder()
                        .WithReadHeader(ReadHeaders.Never)
                        .WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose)
                        .WithTypeDescriber(c ?? TypeDescribers.Default)
                        .Build();
                var config = Configuration.ForDynamic(opts);

                using (var str = new System.IO.StringReader("1,2,3"))
                using (var csv = config.CreateReader(str))
                {
                    var rows = csv.ReadAll();

                    return rows.Single();
                }
            }
        }

        private static T _Cast<T>(dynamic row)
        => (T)row;

        private class _CustomDynamicCellConverter : ITypeDescriber
        {
            private readonly Dictionary<TypeInfo, Parser> Lookup;

            public _CustomDynamicCellConverter()
            {
                Lookup = new Dictionary<TypeInfo, Parser>();
            }

            public void Add(TypeInfo targetType, Parser converter)
            => Lookup.Add(targetType, converter);

            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            => Lookup[targetType];

            public IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext ctx, object row)
            => TypeDescribers.Default.GetCellsForDynamicRow(in ctx, row);

            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => TypeDescribers.Default.GetDynamicRowConverter(in ctx, columns, targetType);

            public InstanceBuilder GetInstanceBuilder(TypeInfo forType)
            => TypeDescribers.Default.GetInstanceBuilder(forType);

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToSerialize(forType);

            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToDeserialize(forType);
        }

        [Fact]
        public void CustomDynamicCellConverter()
        {
            // method
            {
                var converter = new _CustomDynamicCellConverter();
                var mtd = typeof(DynamicReaderTests).GetMethod(nameof(_CustomDynamicCellConverter_Int), BindingFlags.Public | BindingFlags.Static);
                var cellConverter = Parser.ForMethod(mtd);

                converter.Add(typeof(int).GetTypeInfo(), cellConverter);

                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(converter).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Int_Calls = 0;

                        using (var str = getReader("a,bb,ccc"))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.True(csv.TryRead(out var t1));

                            var res1 = (int)(t1[0]);
                            Assert.Equal(3, res1);
                            Assert.Equal(1, _CustomDynamicCellConverter_Int_Calls);

                            var res2 = (int)(t1[1]);
                            Assert.Equal(4, res2);
                            Assert.Equal(2, _CustomDynamicCellConverter_Int_Calls);

                            var res3 = (int)(t1[2]);
                            Assert.Equal(5, res3);
                            Assert.Equal(3, _CustomDynamicCellConverter_Int_Calls);

                            Assert.False(csv.TryRead(out _));
                        }
                    }
                );
            }

            // delegate
            {
                var converter = new _CustomDynamicCellConverter();
                var called = 0;
                ParserDelegate<int> del =
                    (ReadOnlySpan<char> _, in ReadContext ctx, out int val) =>
                    {
                        called++;

                        val = ctx.Column.Index + 4;

                        return true;
                    };
                var cellConverter = Parser.ForDelegate(del);

                converter.Add(typeof(int).GetTypeInfo(), cellConverter);

                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(converter).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        called = 0;

                        using (var str = getReader("a,bb,ccc"))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.True(csv.TryRead(out var t1));

                            var res1 = (int)(t1[0]);
                            Assert.Equal(4, res1);
                            Assert.Equal(1, called);

                            var res2 = (int)(t1[1]);
                            Assert.Equal(5, res2);
                            Assert.Equal(2, called);

                            var res3 = (int)(t1[2]);
                            Assert.Equal(6, res3);
                            Assert.Equal(3, called);

                            Assert.False(csv.TryRead(out _));
                        }
                    }
                );
            }

            // 1 param constructor
            {
                var converter = new _CustomDynamicCellConverter();
                var cons = typeof(_CustomDynamicCellConverter_Cons).GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
                var cellConverter = Parser.ForConstructor(cons);

                converter.Add(typeof(_CustomDynamicCellConverter_Cons).GetTypeInfo(), cellConverter);

                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(converter).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Cons1_Called = 0;

                        using (var str = getReader("a,bb,ccc"))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.True(csv.TryRead(out var t1));

                            var res1 = (_CustomDynamicCellConverter_Cons)(t1[0]);
                            Assert.Equal("a", res1.Val);
                            Assert.Equal(1, _CustomDynamicCellConverter_Cons1_Called);

                            var res2 = (_CustomDynamicCellConverter_Cons)(t1[1]);
                            Assert.Equal("bb", res2.Val);
                            Assert.Equal(2, _CustomDynamicCellConverter_Cons1_Called);

                            var res3 = (_CustomDynamicCellConverter_Cons)(t1[2]);
                            Assert.Equal("ccc", res3.Val);
                            Assert.Equal(3, _CustomDynamicCellConverter_Cons1_Called);

                            Assert.False(csv.TryRead(out _));
                        }
                    }
                );
            }

            // 2 params constructor
            {
                var converter = new _CustomDynamicCellConverter();
                var cons = typeof(_CustomDynamicCellConverter_Cons).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });
                var cellConverter = Parser.ForConstructor(cons);

                converter.Add(typeof(_CustomDynamicCellConverter_Cons).GetTypeInfo(), cellConverter);

                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(converter).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Cons2_Called = 0;

                        using (var str = getReader("a,bb,ccc"))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.True(csv.TryRead(out var t1));

                            var res1 = (_CustomDynamicCellConverter_Cons)(t1[0]);
                            Assert.Equal("a0", res1.Val);
                            Assert.Equal(1, _CustomDynamicCellConverter_Cons2_Called);

                            var res2 = (_CustomDynamicCellConverter_Cons)(t1[1]);
                            Assert.Equal("bb1", res2.Val);
                            Assert.Equal(2, _CustomDynamicCellConverter_Cons2_Called);

                            var res3 = (_CustomDynamicCellConverter_Cons)(t1[2]);
                            Assert.Equal("ccc2", res3.Val);
                            Assert.Equal(3, _CustomDynamicCellConverter_Cons2_Called);

                            Assert.False(csv.TryRead(out _));
                        }
                    }
                );
            }
        }

        private static int _CustomDynamicCellConverter_Cons1_Called = 0;
        private static int _CustomDynamicCellConverter_Cons2_Called = 0;

        private class _CustomDynamicCellConverter_Cons
        {
            public readonly string Val;

            public _CustomDynamicCellConverter_Cons(ReadOnlySpan<char> c)
            {
                _CustomDynamicCellConverter_Cons1_Called++;

                Val = new string(c);
            }

            public _CustomDynamicCellConverter_Cons(ReadOnlySpan<char> c, in ReadContext ctx)
            {
                _CustomDynamicCellConverter_Cons2_Called++;

                Val = new string(c) + ctx.Column.Index;
            }
        }

        private static int _CustomDynamicCellConverter_Int_Calls = 0;
        public static bool _CustomDynamicCellConverter_Int(ReadOnlySpan<char> _, in ReadContext ctx, out int val)
        {
            _CustomDynamicCellConverter_Int_Calls++;

            val = ctx.Column.Index + 3;
            return true;
        }

        [Fact]
        public void DetectLineEndings()
        {
            var opts = Options.Default.NewBuilder().WithRowEnding(RowEndings.Detect).WithReadHeader(ReadHeaders.Never).Build();

            // normal
            {
                // \r\n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\r\ndddd,eeeee,ffffff\r\n1,2,3\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\rdddd,eeeee,ffffff\r1,2,3\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\ndddd,eeeee,ffffff\n1,2,3\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // quoted
            {
                // \r\n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\r\ndddd,\"ee\neee\",ffffff\r\n1,2,\"3\r\n\"\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\rdddd,\"ee\neee\",ffffff\r1,2,\"3\r\n\"\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\ndddd,\"ee\neee\",ffffff\n1,2,\"3\r\n\"\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // escaped
            {
                // \r\n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\n\"\"\"dddd\",\"ee\neee\",ffffff\r\n1,\"\"\"2\"\"\",\"3\r\n\"\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\"\"\"dddd\",\"ee\neee\",ffffff\r1,\"\"\"2\"\"\",\"3\r\n\"\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\n\"\"\"dddd\",\"ee\neee\",ffffff\n1,\"\"\"2\"\"\",\"3\r\n\"\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }
        }

        [Fact]
        public void Multi()
        {
            var optsHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            RunSyncDynamicReaderVariants(
                optsHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1.A;
                                string a2 = row1["A"];
                                string a3 = row1[0];

                                Assert.Equal("foo", a1);
                                Assert.Equal("foo", a2);
                                Assert.Equal("foo", a3);

                                string b1 = row1.B;
                                string b2 = row1["B"];
                                string b3 = row1[1];

                                Assert.Equal("bar", b1);
                                Assert.Equal("bar", b2);
                                Assert.Equal("bar", b3);
                            },
                            row2 =>
                            {
                                int a1 = row2.A;
                                int a2 = row2["A"];
                                int a3 = row2[0];

                                Assert.Equal(1, a1);
                                Assert.Equal(1, a2);
                                Assert.Equal(1, a3);

                                double b1 = row2.B;
                                double b2 = row2["B"];
                                double b3 = row2[1];

                                Assert.Equal(3.3, b1);
                                Assert.Equal(3.3, b2);
                                Assert.Equal(3.3, b3);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3.A;
                                DateTime a2 = row3["A"];
                                DateTime a3 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a2);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a3);

                                char b1 = row3.B;
                                char b2 = row3["B"];
                                char b3 = row3[1];

                                Assert.Equal('d', b1);
                                Assert.Equal('d', b2);
                                Assert.Equal('d', b3);
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            RunSyncDynamicReaderVariants(
                optsNoHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1[0];

                                Assert.Equal("foo", a1);

                                string b1 = row1[1];

                                Assert.Equal("bar", b1);
                            },
                            row2 =>
                            {
                                int a1 = row2[0];

                                Assert.Equal(1, a1);

                                double b1 = row2[1];

                                Assert.Equal(3.3, b1);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);

                                char b1 = row3[1];

                                Assert.Equal('d', b1);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void Simple()
        {
            var optsHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            RunSyncDynamicReaderVariants(
                optsHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];
                                string aName = row["A"];
                                string aMem = row.A;

                                Assert.Equal("foo", aIx);
                                Assert.Equal("foo", aName);
                                Assert.Equal("foo", aMem);

                                string bIx = row[1];
                                string bName = row["B"];
                                string bMem = row.B;

                                Assert.Equal("bar", bIx);
                                Assert.Equal("bar", bName);
                                Assert.Equal("bar", bMem);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            RunSyncDynamicReaderVariants(
                optsNoHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];

                                Assert.Equal("foo", aIx);

                                string bIx = row[1];

                                Assert.Equal("bar", bIx);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }
                            }
                        );
                    }
                }
            );
        }

        private class _Conversions
        {
            public int I { get; set; }

            public _Conversions(ReadOnlySpan<char> foo)
            {
                I = int.Parse(foo);
            }
        }

        [Fact]
        public void Conversions()
        {
            var optsHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            RunSyncDynamicReaderVariants(
                optsHeaders,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row.B;
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );

            var optsNoHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // with no headers
            RunSyncDynamicReaderVariants(
                optsNoHeaders,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row[1];
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );
        }

        private enum _Tuple
        {
            Red,
            Green
        }

        [Fact]
        public void Tuple()
        {
            var optWithHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // headers
            {
                // one
                RunSyncDynamicReaderVariants(
                    optWithHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    optWithHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        optWithHeaders,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            var optNoHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            {
                // one
                RunSyncDynamicReaderVariants(
                    optNoHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    optNoHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        optNoHeaders,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public void ValueTuple()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                // one
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                // one
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        private class _POCO_Constructor
        {
            public int Prop1 { get; }
            public string Prop2 { get; }
            public DateTime Prop3 { get; }

            internal _POCO_Constructor(int p1, string p2, DateTime p3)
            {
                Prop1 = p1;
                Prop2 = p2;
                Prop3 = p3;
            }
        }

        [Fact]
        public void POCO_Constructor()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }
        }

        private class _POCO_Properties
        {
            public int A { get; set; }
            public string B { get; set; }
            internal DateTime C { get; set; }

            public _POCO_Properties() { }
        }

        [Fact]
        public void POCO_Properties()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                    using (var csv = config.CreateReader(str))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var lo = (_POCO_Properties)row;

                                Assert.Equal(1, lo.A);
                                Assert.Equal("foo", lo.B);
                                Assert.Equal(new DateTime(2019, 01, 03), lo.C);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void DynamicRowDisposalOptions()
        {
            // dispose with reader
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        using (var csv = config.CreateReader(str))
                        {
                            read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // explodes now that reader is disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }

            // explicit disposal
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        using (var csv = config.CreateReader(str))
                        {
                            read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // still good after reader
                        Assert.Collection(
                            read,
                            row =>
                            {
                                int a = row[0];
                                int b = row[1];
                                int c = row[2];

                                Assert.Equal(1, a);
                                Assert.Equal(2, b);
                                Assert.Equal(3, c);
                            }
                        );

                        foreach (var r in read)
                        {
                            r.Dispose();
                        }

                        // explodes now that row are disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }
        }

        [Fact]
        public void ReusingRows()
        {
            // both auto
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts,
                            (config2, makeReader2) =>
                            {
                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    dynamic row = null;
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);

                                    Assert.False(csv1.TryReadWithReuse(ref row));
                                    Assert.False(csv2.TryReadWithReuse(ref row));
                                }
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }

            // auto then explicitly
            {
                var opts1 = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                var opts2 = opts1.NewBuilder().WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts2,
                            (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }

            // explicitly then auto
            {
                var opts1 = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                var opts2 = opts1.NewBuilder().WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose).Build();
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts2,
                            (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);


                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);
                                }

                                Assert.Equal(4, (int)row[0]);
                                Assert.Equal(5, (int)row[1]);
                                Assert.Equal(6, (int)row[2]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }

            // both explicitly
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts,
                            (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 3
                );
            }
        }

        private class _DelegateRowConversions<T> : ITypeDescriber
        {
            private readonly DynamicRowConverterDelegate<T> D;

            public _DelegateRowConversions(DynamicRowConverterDelegate<T> d)
            {
                D = d;
            }

            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            => TypeDescribers.Default.GetDynamicCellParserFor(in ctx, targetType);

            public IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext ctx, object row)
            => TypeDescribers.Default.GetCellsForDynamicRow(in ctx, row);

            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => (DynamicRowConverter)D;

            public InstanceBuilder GetInstanceBuilder(TypeInfo forType)
            => TypeDescribers.Default.GetInstanceBuilder(forType);

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToSerialize(forType);

            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToDeserialize(forType);
        }

        private class __DelegateRowConversions_Row
        {
            public string Yup;
        }

        [Fact]
        public void DelegateRowConversions()
        {
            DynamicRowConverterDelegate<__DelegateRowConversions_Row> x =
                (dynamic row, in ReadContext ctx, out __DelegateRowConversions_Row res) =>
                {
                    var a = (string)row[0];
                    var b = (string)row[1];
                    var c = (string)row[2];

                    var x = a + b + b + c + c + c;

                    res = new __DelegateRowConversions_Row { Yup = x };

                    return true;
                };

            var convert = new _DelegateRowConversions<__DelegateRowConversions_Row>(x);

            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithTypeDescriber(convert).Build();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (__DelegateRowConversions_Row)row;

                                    Assert.Equal("1foofoo2019-01-032019-01-032019-01-03", lo.Yup);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(convert).Build();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (__DelegateRowConversions_Row)row;

                                    Assert.Equal("1foofoo2019-01-032019-01-032019-01-03", lo.Yup);
                                }
                            );
                        }
                    }
                );
            }
        }

        // async tests

        [Fact]
        public async Task WithCommentsAsync()
        {
            // \r\n
            {
                var opts1 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).WithReadHeader(ReadHeaders.Always).Build();
                var opts2 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).WithReadHeader(ReadHeaders.Never).Build();

                // with headers
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\r\n#comment\rwhatever\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\nA,Nope\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\n#again!###foo###"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \r
            {
                var opts1 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).WithReadHeader(ReadHeaders.Always).Build();
                var opts2 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).WithReadHeader(ReadHeaders.Never).Build();

                // with headers
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\r#comment\nwhatever\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\rA,Nope\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\r#again!###foo###"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \n
            {
                var opts1 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).WithReadHeader(ReadHeaders.Always).Build();
                var opts2 = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).WithReadHeader(ReadHeaders.Never).Build();

                // with headers
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\n#comment\rwhatever\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\nA,Nope\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\n#again!###foo###"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task WeirdCommentsAsync()
        {
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).WithReadHeader(ReadHeaders.Always).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\r\nhello,world\nfoo,bar";
                        using (var str = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\n#this is a test comment!\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }

            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).WithReadHeader(ReadHeaders.Always).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\rhello,world\rfoo,bar";
                        using (var str = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r#this is a test comment!\n\rfoo,bar";
                        using (var str = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }

            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).WithReadHeader(ReadHeaders.Always).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\r\nhello,world\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                await RunAsyncDynamicReaderVariants(
                   opts,
                   async (config, getReader) =>
                   {
                       var CSV = "#this is a test comment!\r\r\nhello,world\r\nfoo,bar";
                       using (var str = getReader(CSV))
                       await using (var csv = config.CreateAsyncReader(str))
                       {
                           var rows = await csv.ReadAllAsync();
                           Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                       }
                   }
               );

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\n\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\r\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task CommentsAsync()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithReadHeader(ReadHeaders.Always).Build();

            // comment first line
            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    var CSV = "#this is a test comment!\r\nhello,world\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );

            // comment after header
            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    var CSV = "hello,world\r\n#this is a test comment\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );

            // comment between rows
            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!\r\nfizz,buzz";
                    using (var str = getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); },
                            b => { Assert.Equal("fizz", (string)b.hello); Assert.Equal("buzz", (string)b.world); }
                        );
                    }
                }
            );

            // comment at end
            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!";
                    using (var str = getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );
        }

        [Fact]
        public async Task RangeAsync()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            var equivalent = new[] { "1", "2", "3" };

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var r1 = await csv.TryReadAsync();
                        Assert.True(r1.HasValue);
                        var row = r1.Value;
                        var r2 = await csv.TryReadAsync();
                        Assert.False(r2.HasValue);

                        var all = 0..3;
                        var allEnd = ^3..^0;
                        var allImp = ..;

                        var skip1Front = 1..3;
                        var skip1FrontImp = 1..;
                        var skip1Back = 0..2;
                        var skip1BackImp = ..2;
                        var skip1FrontEnd = ^2..^0;
                        var skip1FrontEndImp = ^2..;
                        var skip1BackEnd = ^3..^1;
                        var skip1BackEndImp = ..^1;

                        var skip2Front = 2..3;
                        var skip2FrontImp = 2..;
                        var skip2Back = 0..1;
                        var skip2BackImp = ..1;
                        var skip2FrontEnd = ^1..^0;
                        var skip2FrontEndImp = ^1..;
                        var skip2BackEnd = ^3..^2;
                        var skip2BackEndImp = ..^2;

                        var emptyZero = 0..0;
                        var emptyZeroEnd = ^0..^0;
                        var emptyOne = 1..1;
                        var emptyOneEnd = ^1..^1;
                        var emptyTwo = 2..2;
                        var emptyTwoEnd = ^2..^2;

                        Action<Range> check =
                            range =>
                            {
                                var dynRes = row[range];
                                var shouldMatchRes = equivalent[range];

                                for (var i = 0; i < shouldMatchRes.Length; i++)
                                {
                                    Assert.Equal(shouldMatchRes[i], (string)dynRes[i]);
                                }

                                var ix = 0;
                                foreach (string val in dynRes)
                                {
                                    Assert.Equal(shouldMatchRes[ix], val);
                                    ix++;
                                }
                            };

                        check(all);
                        check(allEnd);
                        check(allImp);

                        check(skip1Front);
                        check(skip1FrontImp);
                        check(skip1Back);
                        check(skip1BackImp);
                        check(skip1FrontEnd);
                        check(skip1FrontEndImp);
                        check(skip1BackEnd);
                        check(skip1BackEndImp);

                        check(skip2Front);
                        check(skip2FrontImp);
                        check(skip2Back);
                        check(skip2BackImp);
                        check(skip2FrontEnd);
                        check(skip2FrontEndImp);
                        check(skip2BackEnd);
                        check(skip2BackEndImp);

                        check(emptyZero);
                        check(emptyZeroEnd);
                        check(emptyOne);
                        check(emptyOneEnd);
                        check(emptyTwo);
                        check(emptyTwoEnd);
                    }
                }
            );
        }

        [Fact]
        public async Task IndexAsync()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var res1 = await csv.TryReadAsync();
                        Assert.True(res1.HasValue);
                        var row = res1.Value;

                        var res2 = await csv.TryReadAsync();
                        Assert.False(res2.HasValue);

                        var zeroFromStart = new Index(0, false);
                        var oneFromStart = new Index(1, false);
                        var twoFromStart = new Index(2, false);

                        var oneFromEnd = new Index(1, true);
                        var twoFromEnd = new Index(2, true);
                        var threeFromEnd = new Index(3, true);

                        int a1 = row[zeroFromStart];
                        int b1 = row[oneFromStart];
                        int c1 = row[twoFromStart];

                        int a2 = row[threeFromEnd];
                        int b2 = row[twoFromEnd];
                        int c2 = row[oneFromEnd];

                        int a3 = row[(Index)0];
                        int b3 = row[(Index)1];
                        int c3 = row[(Index)2];

                        int a4 = row[^3];
                        int b4 = row[^2];
                        int c4 = row[^1];

                        Assert.Equal(1, a1);
                        Assert.Equal(1, a2);
                        Assert.Equal(1, a3);
                        Assert.Equal(1, a4);

                        Assert.Equal(2, b1);
                        Assert.Equal(2, b2);
                        Assert.Equal(2, b3);
                        Assert.Equal(2, b4);

                        Assert.Equal(3, c1);
                        Assert.Equal(3, c2);
                        Assert.Equal(3, c3);
                        Assert.Equal(3, c4);
                    }
                }
            );
        }

        [Fact]
        public async Task CustomDynamicCellConverterAsync()
        {
            // method
            {
                var converter = new _CustomDynamicCellConverter();
                var mtd = typeof(DynamicReaderTests).GetMethod(nameof(_CustomDynamicCellConverter_Int), BindingFlags.Public | BindingFlags.Static);
                var cellConverter = Parser.ForMethod(mtd);

                converter.Add(typeof(int).GetTypeInfo(), cellConverter);

                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(converter).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Int_Calls = 0;

                        using (var str = getReader("a,bb,ccc"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var x = await csv.TryReadAsync();
                            Assert.True(x.HasValue);
                            var t1 = x.Value;

                            var res1 = (int)(t1[0]);
                            Assert.Equal(3, res1);
                            Assert.Equal(1, _CustomDynamicCellConverter_Int_Calls);

                            var res2 = (int)(t1[1]);
                            Assert.Equal(4, res2);
                            Assert.Equal(2, _CustomDynamicCellConverter_Int_Calls);

                            var res3 = (int)(t1[2]);
                            Assert.Equal(5, res3);
                            Assert.Equal(3, _CustomDynamicCellConverter_Int_Calls);

                            x = await csv.TryReadAsync();
                            Assert.False(x.HasValue);
                        }
                    }
                );
            }

            // delegate
            {
                var converter = new _CustomDynamicCellConverter();
                var called = 0;
                ParserDelegate<int> del =
                    (ReadOnlySpan<char> _, in ReadContext ctx, out int val) =>
                    {
                        called++;

                        val = ctx.Column.Index + 4;

                        return true;
                    };
                var cellConverter = Parser.ForDelegate(del);

                converter.Add(typeof(int).GetTypeInfo(), cellConverter);

                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(converter).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        called = 0;

                        using (var str = getReader("a,bb,ccc"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var x = await csv.TryReadAsync();
                            Assert.True(x.HasValue);
                            var t1 = x.Value;

                            var res1 = (int)(t1[0]);
                            Assert.Equal(4, res1);
                            Assert.Equal(1, called);

                            var res2 = (int)(t1[1]);
                            Assert.Equal(5, res2);
                            Assert.Equal(2, called);

                            var res3 = (int)(t1[2]);
                            Assert.Equal(6, res3);
                            Assert.Equal(3, called);

                            x = await csv.TryReadAsync();
                            Assert.False(x.HasValue);
                        }
                    }
                );
            }

            // 1 param constructor
            {
                var converter = new _CustomDynamicCellConverter();
                var cons = typeof(_CustomDynamicCellConverter_Cons).GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
                var cellConverter = Parser.ForConstructor(cons);

                converter.Add(typeof(_CustomDynamicCellConverter_Cons).GetTypeInfo(), cellConverter);

                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(converter).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Cons1_Called = 0;

                        using (var str = getReader("a,bb,ccc"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var x = await csv.TryReadAsync();
                            Assert.True(x.HasValue);
                            var t1 = x.Value;

                            var res1 = (_CustomDynamicCellConverter_Cons)(t1[0]);
                            Assert.Equal("a", res1.Val);
                            Assert.Equal(1, _CustomDynamicCellConverter_Cons1_Called);

                            var res2 = (_CustomDynamicCellConverter_Cons)(t1[1]);
                            Assert.Equal("bb", res2.Val);
                            Assert.Equal(2, _CustomDynamicCellConverter_Cons1_Called);

                            var res3 = (_CustomDynamicCellConverter_Cons)(t1[2]);
                            Assert.Equal("ccc", res3.Val);
                            Assert.Equal(3, _CustomDynamicCellConverter_Cons1_Called);

                            x = await csv.TryReadAsync();
                            Assert.False(x.HasValue);
                        }
                    }
                );
            }

            // 2 params constructor
            {
                var converter = new _CustomDynamicCellConverter();
                var cons = typeof(_CustomDynamicCellConverter_Cons).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });
                var cellConverter = Parser.ForConstructor(cons);

                converter.Add(typeof(_CustomDynamicCellConverter_Cons).GetTypeInfo(), cellConverter);

                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(converter).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Cons2_Called = 0;

                        using (var str = getReader("a,bb,ccc"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var r = await csv.TryReadAsync();
                            Assert.True(r.HasValue);
                            var t1 = r.Value;

                            var res1 = (_CustomDynamicCellConverter_Cons)(t1[0]);
                            Assert.Equal("a0", res1.Val);
                            Assert.Equal(1, _CustomDynamicCellConverter_Cons2_Called);

                            var res2 = (_CustomDynamicCellConverter_Cons)(t1[1]);
                            Assert.Equal("bb1", res2.Val);
                            Assert.Equal(2, _CustomDynamicCellConverter_Cons2_Called);

                            var res3 = (_CustomDynamicCellConverter_Cons)(t1[2]);
                            Assert.Equal("ccc2", res3.Val);
                            Assert.Equal(3, _CustomDynamicCellConverter_Cons2_Called);

                            r = await csv.TryReadAsync();
                            Assert.False(r.HasValue);
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
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\r\ndddd,eeeee,ffffff\r\n1,2,3\r\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\rdddd,eeeee,ffffff\r1,2,3\r"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\ndddd,eeeee,ffffff\n1,2,3\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );
            }

            // quoted
            {
                // \r\n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\r\ndddd,\"ee\neee\",ffffff\r\n1,2,\"3\r\n\"\r\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\rdddd,\"ee\neee\",ffffff\r1,2,\"3\r\n\"\r"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\ndddd,\"ee\neee\",ffffff\n1,2,\"3\r\n\"\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );
            }

            // escaped
            {
                // \r\n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\n\"\"\"dddd\",\"ee\neee\",ffffff\r\n1,\"\"\"2\"\"\",\"3\r\n\"\r\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\"\"\"dddd\",\"ee\neee\",ffffff\r1,\"\"\"2\"\"\",\"3\r\n\"\r"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\n\"\"\"dddd\",\"ee\neee\",ffffff\n1,\"\"\"2\"\"\",\"3\r\n\"\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task MultiAsync()
        {
            var optsHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            await RunAsyncDynamicReaderVariants(
                optsHeader,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1.A;
                                string a2 = row1["A"];
                                string a3 = row1[0];

                                Assert.Equal("foo", a1);
                                Assert.Equal("foo", a2);
                                Assert.Equal("foo", a3);

                                string b1 = row1.B;
                                string b2 = row1["B"];
                                string b3 = row1[1];

                                Assert.Equal("bar", b1);
                                Assert.Equal("bar", b2);
                                Assert.Equal("bar", b3);
                            },
                            row2 =>
                            {
                                int a1 = row2.A;
                                int a2 = row2["A"];
                                int a3 = row2[0];

                                Assert.Equal(1, a1);
                                Assert.Equal(1, a2);
                                Assert.Equal(1, a3);

                                double b1 = row2.B;
                                double b2 = row2["B"];
                                double b3 = row2[1];

                                Assert.Equal(3.3, b1);
                                Assert.Equal(3.3, b2);
                                Assert.Equal(3.3, b3);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3.A;
                                DateTime a2 = row3["A"];
                                DateTime a3 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a2);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a3);

                                char b1 = row3.B;
                                char b2 = row3["B"];
                                char b3 = row3[1];

                                Assert.Equal('d', b1);
                                Assert.Equal('d', b2);
                                Assert.Equal('d', b3);
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            await RunAsyncDynamicReaderVariants(
                optsNoHeader,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1[0];

                                Assert.Equal("foo", a1);

                                string b1 = row1[1];

                                Assert.Equal("bar", b1);
                            },
                            row2 =>
                            {
                                int a1 = row2[0];

                                Assert.Equal(1, a1);

                                double b1 = row2[1];

                                Assert.Equal(3.3, b1);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);

                                char b1 = row3[1];

                                Assert.Equal('d', b1);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task SimpleAsync()
        {
            var optsHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            await RunAsyncDynamicReaderVariants(
                optsHeader,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];
                                string aName = row["A"];
                                string aMem = row.A;

                                Assert.Equal("foo", aIx);
                                Assert.Equal("foo", aName);
                                Assert.Equal("foo", aMem);

                                string bIx = row[1];
                                string bName = row["B"];
                                string bMem = row.B;

                                Assert.Equal("bar", bIx);
                                Assert.Equal("bar", bName);
                                Assert.Equal("bar", bMem);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            await RunAsyncDynamicReaderVariants(
                optsNoHeader,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];

                                Assert.Equal("foo", aIx);

                                string bIx = row[1];

                                Assert.Equal("bar", bIx);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task ConversionsAsync()
        {
            var optsHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // with headers
            await RunAsyncDynamicReaderVariants(
                optsHeaders,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row.B;
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );

            var optsNoHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // with no headers
            await RunAsyncDynamicReaderVariants(
                optsNoHeaders,
                async (config, makeReader) =>
                {
                    using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row[1];
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task TupleAsync()
        {
            var optWithHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            // headers
            {
                // one
                await RunAsyncDynamicReaderVariants(
                    optWithHeaders,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    optWithHeaders,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        optWithHeaders,
                        async (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            var optNoHeaders = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

            // no headers
            {
                // one
                await RunAsyncDynamicReaderVariants(
                    optNoHeaders,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    optNoHeaders,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        optNoHeaders,
                        async (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task ValueTupleAsync()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                // one
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                // one
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task POCO_ConstructorAsync()
        {
            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task POCO_PropertiesAsync()
        {
            var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).Build();

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, makeReader) =>
                {
                    using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var lo = (_POCO_Properties)row;

                                Assert.Equal(1, lo.A);
                                Assert.Equal("foo", lo.B);
                                Assert.Equal(new DateTime(2019, 01, 03), lo.C);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task DynamicRowDisposalOptionsAsync()
        {
            // dispose with reader
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // explodes now that reader is disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }

            // explicit disposal
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // still good after reader
                        Assert.Collection(
                            read,
                            row =>
                            {
                                int a = row[0];
                                int b = row[1];
                                int c = row[2];

                                Assert.Equal(1, a);
                                Assert.Equal(2, b);
                                Assert.Equal(3, c);
                            }
                        );

                        foreach (var r in read)
                        {
                            r.Dispose();
                        }

                        // explodes now that row are disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }
        }

        [Fact]
        public async Task ReusingRowsAsync()
        {
            // both auto
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts,
                            async (config2, makeReader2) =>
                            {
                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    dynamic row = null;
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.False(res.HasValue);
                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.False(res.HasValue);
                                }
                            }
                        );
                    },
                    expectedRuns: 4
                );
            }

            // auto then explicitly
            {
                var opts1 = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).Build();
                var opts2 = opts1.NewBuilder().WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts2,
                            async (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 4
                );
            }

            // explicitly then auto
            {
                var opts1 = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                var opts2 = opts1.NewBuilder().WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose).Build();
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts2,
                            async (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);


                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);
                                }

                                Assert.Equal(4, (int)row[0]);
                                Assert.Equal(5, (int)row[1]);
                                Assert.Equal(6, (int)row[2]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 4
                );
            }

            // both explicitly
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).Build();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts,
                            async (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 4
                );
            }
        }

        [Fact]
        public async Task DelegateRowConversionsAsync()
        {
            DynamicRowConverterDelegate<__DelegateRowConversions_Row> x =
                (dynamic row, in ReadContext ctx, out __DelegateRowConversions_Row res) =>
                {
                    var a = (string)row[0];
                    var b = (string)row[1];
                    var c = (string)row[2];

                    var x = a + b + b + c + c + c;

                    res = new __DelegateRowConversions_Row { Yup = x };

                    return true;
                };

            var convert = new _DelegateRowConversions<__DelegateRowConversions_Row>(x);

            // headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithTypeDescriber(convert).Build();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (__DelegateRowConversions_Row)row;

                                    Assert.Equal("1foofoo2019-01-032019-01-032019-01-03", lo.Yup);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Never).WithTypeDescriber(convert).Build();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (__DelegateRowConversions_Row)row;

                                    Assert.Equal("1foofoo2019-01-032019-01-032019-01-03", lo.Yup);
                                }
                            );
                        }
                    }
                );
            }
        }
    }
}