﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class ReaderTests
    {
        private sealed class _FailingParser
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void FailingParser()
        {
            var m = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);

            m.SetBuilder(InstanceProvider.ForDelegate((out _FailingParser val) => { val = new _FailingParser(); return true; }));

            var t = typeof(_FailingParser).GetTypeInfo();
            var s = Setter.ForMethod(t.GetProperty(nameof(_FailingParser.Foo)).SetMethod);
            var p = Parser.ForDelegate((ReadOnlySpan<char> data, in ReadContext ctx, out string result) => { result = ""; return false; });

            m.AddExplicitSetter(t, "Foo", s, p);

            var opt = Options.Default.NewBuilder().WithTypeDescriber(m).Build();

            RunSyncReaderVariants<_FailingParser>(
                opt,
                (config, getReader) =>
                {
                    using (var r = getReader("hello"))
                    using (var csv = config.CreateReader(r))
                    {
                        Assert.Throws<SerializationException>(() => csv.ReadAll());
                    }
                }
            );
        }

        class _NonGenericEnumerator
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void NonGenericEnumerator()
        {
            RunSyncReaderVariants<_NonGenericEnumerator>(
                Options.Default,
                (config, getReader) =>
                {
                    using(var reader = getReader("hello,world\r\nfizz,buzz"))
                    using(var csv = config.CreateReader(reader))
                    {
                        System.Collections.IEnumerable e = csv.EnumerateAll();

                        int ix = 0;
                        var i = e.GetEnumerator();
                        while (i.MoveNext())
                        {
                            object c = i.Current;
                            switch (ix)
                            {
                                case 0:
                                    {
                                        var a = (_NonGenericEnumerator)c;
                                        Assert.Equal("hello", a.Foo);
                                        Assert.Equal("world", a.Bar);
                                    }
                                    break;
                                case 1:
                                    {
                                        var a = (_NonGenericEnumerator)c;
                                        Assert.Equal("fizz", a.Foo);
                                        Assert.Equal("buzz", a.Bar);
                                    }
                                    break;
                                default:
                                    Assert.NotNull("Shouldn't be possible");
                                    break;
                            }

                            ix++;
                        }

                        Assert.Equal(2, ix);

                        Assert.Throws<NotSupportedException>(() => i.Reset());
                    }
                }
            );
        }

        class _DeserializableMemberHelpers
        {
#pragma warning disable CS0649
            public int Field;
#pragma warning restore CS0649
            public string Prop { get; set; }
        }

        [Fact]
        public void DeserializableMemberHelpers()
        {
            var t = typeof(_DeserializableMemberHelpers).GetTypeInfo();

            // fields
            {
                var f = t.GetField(nameof(_DeserializableMemberHelpers.Field));

                // 1
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null));

                    var d1 = DeserializableMember.ForField(f);
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Field", d1.Name);
                    Assert.Equal(Parser.GetDefault(typeof(int).GetTypeInfo()), d1.Parser);
                    Assert.Null(d1.Reset);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }

                // 2
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null, "Foo"));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, null));

                    var d1 = DeserializableMember.ForField(f, "Foo");
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Foo", d1.Name);
                    Assert.Equal(Parser.GetDefault(typeof(int).GetTypeInfo()), d1.Parser);
                    Assert.Null(d1.Reset);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }

                var parser = Parser.ForDelegate<int>((ReadOnlySpan<char> _, in ReadContext rc, out int v) => { v = 1; return true; });

                // 3
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null, "Bar", parser));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, null, parser));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, "Bar", null));

                    var d1 = DeserializableMember.ForField(f, "Bar", parser);
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Bar", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.Null(d1.Reset);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }

                // 4
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null, "Baf", parser, IsMemberRequired.Yes));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, null, parser, IsMemberRequired.Yes));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, "Baf", null, IsMemberRequired.Yes));
                    // there's a separate test for bogus IsMemberRequired

                    var d1 = DeserializableMember.ForField(f, "Baf", parser, IsMemberRequired.Yes);
                    Assert.True(d1.IsRequired);
                    Assert.Equal("Baf", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.Null(d1.Reset);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }

                var reset = Reset.ForDelegate(() => { });

                // 5
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(null, "Baz", parser, IsMemberRequired.Yes, reset));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, null, parser, IsMemberRequired.Yes, reset));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForField(f, "Baz", null, IsMemberRequired.Yes, reset));
                    // there's a separate test for bogus IsMemberRequired
                    // it's ok for reset = null

                    var d1 = DeserializableMember.ForField(f, "Baz", parser, IsMemberRequired.Yes, reset);
                    Assert.True(d1.IsRequired);
                    Assert.Equal("Baz", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.Equal(reset, d1.Reset);
                    Assert.Equal(Setter.ForField(f), d1.Setter);
                }
            }

            // properties
            {
                var p = t.GetProperty(nameof(_DeserializableMemberHelpers.Prop));

                // 1
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null));

                    var d1 = DeserializableMember.ForProperty(p);
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Prop", d1.Name);
                    Assert.Equal(Parser.GetDefault(typeof(string).GetTypeInfo()), d1.Parser);
                    Assert.Null(d1.Reset);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }

                // 2
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null, "Foo"));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, null));

                    var d1 = DeserializableMember.ForProperty(p, "Foo");
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Foo", d1.Name);
                    Assert.Equal(Parser.GetDefault(typeof(string).GetTypeInfo()), d1.Parser);
                    Assert.Null(d1.Reset);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }

                var parser = Parser.ForDelegate<string>((ReadOnlySpan<char> _, in ReadContext rc, out string v) => { v = "1"; return true; });

                // 3
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null, "Bar", parser));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, null, parser));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, "Bar", null));

                    var d1 = DeserializableMember.ForProperty(p, "Bar", parser);
                    Assert.False(d1.IsRequired);
                    Assert.Equal("Bar", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.Null(d1.Reset);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }

                // 4
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null, "Baf", parser, IsMemberRequired.Yes));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, null, parser, IsMemberRequired.Yes));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, "Baf", null, IsMemberRequired.Yes));
                    // there's a separate test for bogus IsMemberRequired

                    var d1 = DeserializableMember.ForProperty(p, "Baf", parser, IsMemberRequired.Yes);
                    Assert.True(d1.IsRequired);
                    Assert.Equal("Baf", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.Null(d1.Reset);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }

                var reset = Reset.ForDelegate(() => { });

                // 5
                {
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(null, "Baz", parser, IsMemberRequired.Yes, reset));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, null, parser, IsMemberRequired.Yes, reset));
                    Assert.Throws<ArgumentNullException>(() => DeserializableMember.ForProperty(p, "Baz", null, IsMemberRequired.Yes, reset));
                    // there's a separate test for bogus IsMemberRequired
                    // it's ok for reset = null

                    var d1 = DeserializableMember.ForProperty(p, "Baz", parser, IsMemberRequired.Yes, reset);
                    Assert.True(d1.IsRequired);
                    Assert.Equal("Baz", d1.Name);
                    Assert.Equal(parser, d1.Parser);
                    Assert.Equal(reset, d1.Reset);
                    Assert.Equal(Setter.ForMethod(p.SetMethod), d1.Setter);
                }
            }
        }

        class _DeserializableMemberEquality
        {
            public int Foo { get; set; }
            public int Bar { get; set; }
        }

        [Fact]
        public void DeserializableMemberEquality()
        {
            var t = typeof(_DeserializableMemberEquality).GetTypeInfo();
            var names = new[] { nameof(_DeserializableMemberEquality.Foo), nameof(_DeserializableMemberEquality.Bar) };
            var setters = new[] { Setter.ForMethod(t.GetProperty(names[0]).SetMethod), Setter.ForMethod(t.GetProperty(names[1]).SetMethod) };
            IEnumerable<Parser> parsers;
            {
                var a = Parser.GetDefault(typeof(int).GetTypeInfo());
                var b = Parser.ForDelegate<int>((ReadOnlySpan<char> s, in ReadContext rc, out int val) => { val = 123; return true; });
                parsers = new[] { a, b };
            }
            var isMemberRequireds = new[] { IsMemberRequired.Yes, IsMemberRequired.No };
            IEnumerable<Reset> resets;
            {
                var a = Reset.ForDelegate(() => { });
                var b = Reset.ForDelegate<_DeserializableMemberEquality>(_ => { });
                resets = new[] { a, b, null };
            }

            var members = new List<DeserializableMember>();

            foreach (var n in names)
            {
                foreach(var s in setters)
                {
                    foreach(var p in parsers)
                    {
                        foreach(var i in isMemberRequireds)
                        {
                            foreach(var r in resets)
                            {
                                members.Add(DeserializableMember.Create(t, n, s, p, i, r));
                            }
                        }
                    }
                }
            }

            var notSerializableMember = "";

            for (var i = 0; i < members.Count; i++)
            {
                var m1 = members[i];

                Assert.False(m1.Equals(notSerializableMember));

                for (var j = i; j < members.Count; j++)
                {

                    var m2 = members[j];

                    var eq = m1 == m2;
                    var neq = m1 != m2;
                    var hashEq = m1.GetHashCode() == m2.GetHashCode();
                    var objEq = m1.Equals((object)m2);

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                        Assert.True(objEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                        Assert.False(objEq);
                    }
                }
            }
        }

        class _DeserializeMemberErrors
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void DeserializableMemberErrors()
        {
            var type = typeof(_DeserializeMemberErrors).GetTypeInfo();
            var name = nameof(_DeserializeMemberErrors.Foo);
            var setter = Setter.ForMethod(type.GetProperty(name).SetMethod);
            var parser = Parser.GetDefault(typeof(string).GetTypeInfo());

            Assert.Throws<ArgumentNullException>(() => DeserializableMember.Create(null, name, setter, parser, IsMemberRequired.Yes, null));
            Assert.Throws<ArgumentNullException>(() => DeserializableMember.Create(type, null, setter, parser, IsMemberRequired.Yes, null));
            Assert.Throws<ArgumentNullException>(() => DeserializableMember.Create(type, name, null, parser, IsMemberRequired.Yes, null));
            Assert.Throws<ArgumentNullException>(() => DeserializableMember.Create(type, name, setter, null, IsMemberRequired.Yes, null));
            Assert.Throws<ArgumentException>(() => DeserializableMember.Create(type, name, setter, parser, 0, null));

            var badParser = Parser.GetDefault(typeof(int).GetTypeInfo());
            Assert.Throws<ArgumentException>(() => DeserializableMember.Create(type, name, setter, badParser, IsMemberRequired.Yes, null));

            var badReset = Reset.ForDelegate<string>((_) => { });
            Assert.Throws<ArgumentException>(() => DeserializableMember.Create(type, name, setter, parser, IsMemberRequired.Yes, badReset));
        }

        [Fact]
        public void ReadContexts()
        {
            // columns
            {
                var cc = Cesil.ReadContext.ConvertingColumn(1, ColumnIdentifier.Create(1), null);
                var cr = Cesil.ReadContext.ConvertingRow(1, null);
                var rc = Cesil.ReadContext.ReadingColumn(1, ColumnIdentifier.Create(1), null);

                Assert.True(cc.HasColumn);
                Assert.Equal(ColumnIdentifier.Create(1), cc.Column);

                Assert.False(cr.HasColumn);
                Assert.Throws<InvalidOperationException>(() => cr.Column);

                Assert.True(rc.HasColumn);
                Assert.Equal(ColumnIdentifier.Create(1), rc.Column);
            }

            // equality
            {
                var cc1 = Cesil.ReadContext.ConvertingColumn(1, ColumnIdentifier.Create(1), null);
                var cc2 = Cesil.ReadContext.ConvertingColumn(1, ColumnIdentifier.Create(1), "foo");
                var cc3 = Cesil.ReadContext.ConvertingColumn(1, ColumnIdentifier.Create(2), null);
                var cc4 = Cesil.ReadContext.ConvertingColumn(2, ColumnIdentifier.Create(1), null);

                var cr1 = Cesil.ReadContext.ConvertingRow(1, null);
                var cr2 = Cesil.ReadContext.ConvertingRow(1, "foo");
                var cr3 = Cesil.ReadContext.ConvertingRow(2, null);

                var rc1 = Cesil.ReadContext.ReadingColumn(1, ColumnIdentifier.Create(1), null);
                var rc2 = Cesil.ReadContext.ReadingColumn(1, ColumnIdentifier.Create(1), "foo");
                var rc3 = Cesil.ReadContext.ReadingColumn(1, ColumnIdentifier.Create(2), null);
                var rc4 = Cesil.ReadContext.ReadingColumn(2, ColumnIdentifier.Create(1), null);

                var contexts = new[] { cc1, cc2, cc3, cc4, cr1, cr2, cr3, rc1, rc2, rc3, rc4 };

                var notContext = "";

                for (var i = 0; i < contexts.Length; i++)
                {
                    var ctx1 = contexts[i];
                    Assert.False(ctx1.Equals(notContext));
                    Assert.NotNull(ctx1.ToString());

                    for (var j = i; j < contexts.Length; j++)
                    {
                        var ctx2 = contexts[j];

                        var objEq = ctx1.Equals((object)ctx2);
                        var eq = ctx1 == ctx2;
                        var neq = ctx1 != ctx2;
                        var hashEq = ctx1.GetHashCode() == ctx2.GetHashCode();

                        if (i == j)
                        {
                            Assert.True(objEq);
                            Assert.True(eq);
                            Assert.False(neq);
                            Assert.True(hashEq);
                        }
                        else
                        {
                            Assert.False(objEq);
                            Assert.False(eq);
                            Assert.True(neq);
                        }
                    }
                }
            }
        }

        class _ResultsErrors
        {
            public string Foo { get; set; }
        }

        [Fact]
        public async Task ResultErrorsAsync()
        {
            // without comments
            {
                await RunAsyncReaderVariants<_ResultsErrors>(
                    Options.Default,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("hello"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var resValue = await csv.TryReadAsync();
                            Assert.True(resValue.HasValue);
                            Assert.Equal("hello", resValue.Value.Foo);
                            var resValueStr = resValue.ToString();
                            Assert.NotNull(resValueStr);
                            Assert.NotEqual(-1, resValueStr.IndexOf(resValue.Value.ToString()));

                            var resNone = await csv.TryReadAsync();
                            Assert.False(resNone.HasValue);
                            Assert.NotNull(resNone.ToString());
                            Assert.Throws<InvalidOperationException>(() => resNone.Value);
                        }
                    }
                );
            }

            // with comments
            {
                var withComments = Options.Default.NewBuilder().WithCommentCharacter('#').Build();

                await RunAsyncReaderVariants<_ResultsErrors>(
                    withComments,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("hello\r\n#foo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var resValue = await csv.TryReadWithCommentAsync();
                            Assert.True(resValue.HasValue);
                            Assert.False(resValue.HasComment);
                            Assert.Equal(ReadWithCommentResultType.HasValue, resValue.ResultType);
                            Assert.Equal("hello", resValue.Value.Foo);
                            var resValueStr = resValue.ToString();
                            Assert.NotNull(resValueStr);
                            Assert.NotEqual(-1, resValueStr.IndexOf(resValue.Value.ToString()));
                            Assert.Throws<InvalidOperationException>(() => resValue.Comment);

                            var resComment = await csv.TryReadWithCommentAsync();
                            Assert.False(resComment.HasValue);
                            Assert.True(resComment.HasComment);
                            Assert.Equal(ReadWithCommentResultType.HasComment, resComment.ResultType);
                            Assert.Equal("foo", resComment.Comment);
                            var resCommentStr = resComment.ToString();
                            Assert.NotNull(resCommentStr);
                            Assert.NotEqual(-1, resCommentStr.IndexOf("foo"));
                            Assert.Throws<InvalidOperationException>(() => resComment.Value);

                            var resNone = await csv.TryReadWithCommentAsync();
                            Assert.False(resNone.HasValue);
                            Assert.False(resNone.HasComment);
                            Assert.Equal(ReadWithCommentResultType.NoValue, resNone.ResultType);
                            Assert.NotNull(resNone.ToString());
                            Assert.Throws<InvalidOperationException>(() => resNone.Comment);
                            Assert.Throws<InvalidOperationException>(() => resNone.Value);
                        }
                    }
                );
            }
        }

        class _RowCreationFailure
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void RowCreationFailure()
        {
            int failAfter = 0;
            int calls = 0;
            InstanceProviderDelegate<_RowCreationFailure> builder =
                (out _RowCreationFailure row) =>
                {
                    if (calls >= failAfter)
                    {
                        row = default;
                        return false;
                    }

                    calls++;

                    row = new _RowCreationFailure();
                    return true;
                };


            var typeDesc = new ManualTypeDescriber();
            typeDesc.AddDeserializableProperty(typeof(_RowCreationFailure).GetProperty(nameof(_RowCreationFailure.Foo)));
            typeDesc.SetBuilder((InstanceProvider)builder);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(typeDesc).Build();

            RunSyncReaderVariants<_RowCreationFailure>(
                opts,
                (config, makeReader) =>
                {
                    calls = 0;
                    failAfter = 3;

                    using (var reader = makeReader("Foo\r\n1\r\n2\r\n3\r\n4"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var r1));
                        Assert.Equal(1, r1.Foo);

                        Assert.True(csv.TryRead(out var r2));
                        Assert.Equal(2, r2.Foo);

                        Assert.True(csv.TryRead(out var r3));
                        Assert.Equal(3, r3.Foo);

                        Assert.Throws<InvalidOperationException>(() => csv.TryRead(out _));
                    }
                }
            );
        }

        private class _EnumeratorNoReset
        {
            public int A { get; set; }
            public int B { get; set; }
            public int C { get; set; }
        }


        [Fact]
        public void EnumeratorNoReset()
        {
            RunSyncReaderVariants<_EnumeratorNoReset>(
                Options.Default,
                (config, getReader) =>
                {
                    using (var reader = getReader("1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var e = csv.EnumerateAll();
                        using (var i = e.GetEnumerator())
                        {
                            Assert.True(i.MoveNext());
                            var r = i.Current;
                            Assert.NotNull(r);
                            Assert.Equal(1, r.A);
                            Assert.Equal(2, r.B);
                            Assert.Equal(3, r.C);

                            Assert.False(i.MoveNext());

                            Assert.Throws<NotSupportedException>(() => i.Reset());
                        }
                    }
                }
            );
        }

        private class _WithComments
        {
            public string A { get; set; }
            public int Nope { get; set; }
        }

        [Fact]
        public void WithComments()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                // with headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).Build();

                // with headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).Build();

                // with headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                RunSyncReaderVariants<_WithComments>(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncReaderVariants<_WithComments>(
                    opts,
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
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncReaderVariants<_WithComments>(
                    opts,
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

        private class _DelegateReset
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateStaticReset()
        {
            var resetCalled = 0;
            StaticResetDelegate resetDel =
                () =>
                {
                    resetCalled++;
                };

            var name = nameof(_DelegateReset.Foo);
            var parser = Parser.GetDefault(typeof(int).GetTypeInfo());
            var setter = Setter.ForMethod(typeof(_DelegateReset).GetProperty(name).SetMethod);
            var reset = Reset.ForDelegate(resetDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitSetter(typeof(_DelegateReset).GetTypeInfo(), name, setter, parser, IsMemberRequired.No, reset);
            InstanceProviderDelegate<_DelegateReset> del = (out _DelegateReset i) => { i = new _DelegateReset(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            RunSyncReaderVariants<_DelegateReset>(
                opts,
                (config, getReader) =>
                {
                    resetCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(23, r.Foo),
                            r => Assert.Equal(456, r.Foo),
                            r => Assert.Equal(7, r.Foo)
                        );
                    }

                    Assert.Equal(4, resetCalled);
                }
            );
        }

        [Fact]
        public void DelegateReset()
        {
            var resetCalled = 0;
            ResetDelegate<_DelegateReset> resetDel =
                (_DelegateReset row) =>
                {
                    resetCalled++;
                };

            var name = nameof(_DelegateReset.Foo);
            var parser = Parser.GetDefault(typeof(int).GetTypeInfo());
            var setter = Setter.ForMethod(typeof(_DelegateReset).GetProperty(name).SetMethod);
            var reset = Reset.ForDelegate(resetDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitSetter(typeof(_DelegateReset).GetTypeInfo(), name, setter, parser, IsMemberRequired.No, reset);
            InstanceProviderDelegate<_DelegateReset> del = (out _DelegateReset i) => { i = new _DelegateReset(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            RunSyncReaderVariants<_DelegateReset>(
                opts,
                (config, getReader) =>
                {
                    resetCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(23, r.Foo),
                            r => Assert.Equal(456, r.Foo),
                            r => Assert.Equal(7, r.Foo)
                        );
                    }

                    Assert.Equal(4, resetCalled);
                }
            );
        }

        private class _DelegateSetter
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateStaticSetter()
        {
            var setterCalled = 0;

            StaticSetterDelegate<int> parser =
                (int value) =>
                {
                    setterCalled++;
                };

            var describer = new ManualTypeDescriber();
            describer.AddExplicitSetter(typeof(_DelegateSetter).GetTypeInfo(), "Foo", Setter.ForDelegate(parser));
            InstanceProviderDelegate<_DelegateSetter> del = (out _DelegateSetter i) => { i = new _DelegateSetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            RunSyncReaderVariants<_DelegateSetter>(
                opts,
                (config, getReader) =>
                {
                    setterCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo)
                        );
                    }

                    Assert.Equal(4, setterCalled);
                }
            );
        }

        [Fact]
        public void DelegateSetter()
        {
            var setterCalled = 0;

            SetterDelegate<_DelegateSetter, int> parser =
                (_DelegateSetter row, int value) =>
                {
                    setterCalled++;

                    row.Foo = value * 2;
                };

            var describer = new ManualTypeDescriber();
            describer.AddExplicitSetter(typeof(_DelegateSetter).GetTypeInfo(), "Foo", Setter.ForDelegate(parser));
            InstanceProviderDelegate<_DelegateSetter> del = (out _DelegateSetter i) => { i = new _DelegateSetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            RunSyncReaderVariants<_DelegateSetter>(
                opts,
                (config, getReader) =>
                {
                    setterCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1 * 2, r.Foo),
                            r => Assert.Equal(23 * 2, r.Foo),
                            r => Assert.Equal(456 * 2, r.Foo),
                            r => Assert.Equal(7 * 2, r.Foo)
                        );
                    }

                    Assert.Equal(4, setterCalled);
                }
            );
        }

        private class _ConstructorParser_Outer
        {
            public _ConstructorParser Foo { get; set; }
            public _ConstructorParser_Outer() { }
        }

        private class _ConstructorParser
        {
            public static int Cons1Called = 0;
            public static int Cons2Called = 0;

            public string Value { get; }

            public _ConstructorParser(ReadOnlySpan<char> a)
            {
                Cons1Called++;
                Value = new string(a);
            }

            public _ConstructorParser(ReadOnlySpan<char> a, in ReadContext ctx)
            {
                Cons2Called++;
                Value = new string(a) + ctx.Column.Index;
            }
        }

        [Fact]
        public void ConstructorParser()
        {
            var cons1 = typeof(_ConstructorParser).GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
            var cons2 = typeof(_ConstructorParser).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });

            // single param
            {
                var describer = new ManualTypeDescriber();
                describer.AddDeserializableProperty(
                    typeof(_ConstructorParser_Outer).GetProperty(nameof(_ConstructorParser_Outer.Foo)),
                    nameof(_ConstructorParser_Outer.Foo),
                    Parser.ForConstructor(cons1)
                );

                InstanceProviderDelegate<_ConstructorParser_Outer> del = (out _ConstructorParser_Outer i) => { i = new _ConstructorParser_Outer(); return true; };
                describer.SetBuilder((InstanceProvider)del);

                var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

                RunSyncReaderVariants<_ConstructorParser_Outer>(
                    opts,
                    (config, getReader) =>
                    {
                        _ConstructorParser.Cons1Called = 0;

                        using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var row = csv.ReadAll();

                            Assert.Collection(
                                row,
                                r => Assert.Equal("1", r.Foo.Value),
                                r => Assert.Equal("23", r.Foo.Value),
                                r => Assert.Equal("456", r.Foo.Value),
                                r => Assert.Equal("7", r.Foo.Value)
                            );
                        }

                        Assert.Equal(4, _ConstructorParser.Cons1Called);
                    }
                );
            }

            // two params
            {
                var describer = new ManualTypeDescriber();
                describer.AddDeserializableProperty(
                    typeof(_ConstructorParser_Outer).GetProperty(nameof(_ConstructorParser_Outer.Foo)),
                    nameof(_ConstructorParser_Outer.Foo),
                    Parser.ForConstructor(cons2)
                );
                InstanceProviderDelegate<_ConstructorParser_Outer> del = (out _ConstructorParser_Outer i) => { i = new _ConstructorParser_Outer(); return true; };
                describer.SetBuilder((InstanceProvider)del);

                var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

                RunSyncReaderVariants<_ConstructorParser_Outer>(
                    opts,
                    (config, getReader) =>
                    {
                        _ConstructorParser.Cons2Called = 0;

                        using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var row = csv.ReadAll();

                            Assert.Collection(
                                row,
                                r => Assert.Equal("10", r.Foo.Value),
                                r => Assert.Equal("230", r.Foo.Value),
                                r => Assert.Equal("4560", r.Foo.Value),
                                r => Assert.Equal("70", r.Foo.Value)
                            );
                        }

                        Assert.Equal(4, _ConstructorParser.Cons2Called);
                    }
                );
            }
        }

        private class _DelegateParser
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateParser()
        {
            var parserCalled = 0;

            ParserDelegate<int> parser =
                (ReadOnlySpan<char> data, in ReadContext _, out int res) =>
                {
                    parserCalled++;

                    res = data.Length;
                    return true;
                };

            var describer = new ManualTypeDescriber();
            describer.AddDeserializableProperty(
                typeof(_DelegateParser).GetProperty(nameof(_DelegateParser.Foo)),
                nameof(_DelegateParser.Foo),
                Parser.ForDelegate(parser)
            );
            InstanceProviderDelegate<_DelegateParser> del = (out _DelegateParser i) => { i = new _DelegateParser(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            RunSyncReaderVariants<_DelegateParser>(
                opts,
                (config, getReader) =>
                {
                    parserCalled = 0;

                    using (var reader = getReader("1\r\n23\r\n456\r\n7\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(2, r.Foo),
                            r => Assert.Equal(3, r.Foo),
                            r => Assert.Equal(1, r.Foo)
                        );
                    }

                    Assert.Equal(4, parserCalled);
                }
            );
        }

        private class _StaticSetter
        {
            public static int Foo { get; set; }
        }

        [Fact]
        public void StaticSetter()
        {
            var describer = new ManualTypeDescriber();
            describer.AddDeserializableProperty(typeof(_StaticSetter).GetProperty(nameof(_StaticSetter.Foo), BindingFlags.Static | BindingFlags.Public));
            InstanceProviderDelegate<_StaticSetter> del = (out _StaticSetter i) => { i = new _StaticSetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            RunSyncReaderVariants<_StaticSetter>(
                opts,
                (config, getReader) =>
                {
                    _StaticSetter.Foo = 123;

                    using (var reader = getReader("456"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.ReadAll();

                        Assert.Collection(row, r => Assert.NotNull(r));
                    }

                    Assert.Equal(456, _StaticSetter.Foo);
                }
            );
        }

        private class _WithReset
        {
            public string A { get; set; }

            private int _B;
            public int B
            {
                get
                {
                    return _B;
                }
                set
                {
                    if (value > 5) return;

                    _B = value;
                }
            }

            public void ResetB()
            {
                _B = 2;
            }
        }

        private class _WithReset_Static
        {
            public static int Count;

            public string A { get; set; }

            public int B { get; set; }

            public static void ResetB()
            {
                Count++;
            }
        }

        private class _WithReset_StaticWithParam
        {
            public string A { get; set; }

            private int _B;
            public int B
            {
                get
                {
                    return _B;
                }
                set
                {
                    if (value > 5) return;

                    _B = value;
                }
            }

            public static void ResetB(_WithReset_StaticWithParam row)
            {
                row._B = 2;
            }
        }

        [Fact]
        public void WithReset()
        {
            // simple
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                RunSyncReaderVariants<_WithReset>(
                    Options.Default,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(2, a.B); }
                            );
                        }
                    }
                );
            }

            // static
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                RunSyncReaderVariants<_WithReset_Static>(
                    Options.Default,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            _WithReset_Static.Count = 0;

                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(6, a.B); }
                            );

                            Assert.Equal(2, _WithReset_Static.Count);
                        }
                    }
                );
            }

            // static with param
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                RunSyncReaderVariants<_WithReset_StaticWithParam>(
                    Options.Default,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader(CSV))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(2, a.B); }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public void TransitionMatrixConstants()
        {
            var maxStateVal = Enum.GetValues(typeof(ReaderStateMachine.State)).Cast<ReaderStateMachine.State>().Select(b => (byte)b).Max();

            // making these consts is a win, but we want to make sure we don't break them
            Assert.Equal(maxStateVal + 1, ReaderStateMachine.RuleCacheStateCount);

            var characterTypeMax = Enum.GetValues(typeof(ReaderStateMachine.CharacterType)).Cast<byte>().Max();

            Assert.Equal(characterTypeMax + 1, ReaderStateMachine.RuleCacheCharacterCount);
            Assert.Equal((maxStateVal + 1) * (characterTypeMax + 1), ReaderStateMachine.RuleCacheConfigSize);

            var rowEndingsMax = Enum.GetValues(typeof(RowEndings)).Cast<byte>().Max();

            Assert.Equal(rowEndingsMax + 1, ReaderStateMachine.RuleCacheRowEndingCount);
            Assert.Equal((rowEndingsMax + 1) * 4, ReaderStateMachine.RuleCacheConfigCount);
        }

        [Fact]
        public void StateMasks()
        {
            foreach (ReaderStateMachine.State state in Enum.GetValues(typeof(ReaderStateMachine.State)))
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
                        state == ReaderStateMachine.State.Invalid ||
                        state == ReaderStateMachine.State.DataEnded
                    );
                }
            }
        }

        private class _TabSeparator
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

        private class _DifferentEscapes
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

        private class _BadEscape
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

        private class _TryReadWithReuse
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

        private class _ReadAll
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

        private class _DetectLineEndings
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

        private class _DetectHeaders
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

        private class _IsRequiredMissing
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

        [Fact]
        public void WeirdComments()
        {
            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).Build();
                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\r\nhello,world\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\n#this is a test comment!\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).Build();
                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\rhello,world\rfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r#this is a test comment!\n\rfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();
                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\r\nhello,world\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                RunSyncReaderVariants<_Comment>(
                   opts,
                   (config, getReader) =>
                   {
                       var CSV = "#this is a test comment!\r\r\nhello,world\r\nfoo,bar";
                       using (var str = getReader(CSV))
                       using (var csv = config.CreateReader(str))
                       {
                           var rows = csv.ReadAll();
                           Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                       }
                   }
               );

                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\n\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                RunSyncReaderVariants<_Comment>(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\r\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }
        }

        private class _Comment
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

        private class _Context
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
        }

        private static List<string> _Context_ParseFoo_Records;
        public static bool _Context_ParseFoo(ReadOnlySpan<char> data, in ReadContext ctx, out string val)
        {
            _Context_ParseFoo_Records.Add($"{ctx.RowNumber},{ctx.Column.Name},{ctx.Column.Index},{new string(data)},{ctx.Context}");

            val = new string(data);
            return true;
        }

        private static List<string> _Context_ParseBar_Records;
        public static bool _Context_ParseBar(ReadOnlySpan<char> data, in ReadContext ctx, out int val)
        {
            _Context_ParseBar_Records.Add($"{ctx.RowNumber},{ctx.Column.Name},{ctx.Column.Index},{new string(data)},{ctx.Context}");

            if (!int.TryParse(data, out val))
            {
                val = default;
                return false;
            }

            return true;
        }

        [Fact]
        public void Context()
        {
            var parseFoo = (Parser)typeof(ReaderTests).GetMethod(nameof(_Context_ParseFoo));
            var parseBar = (Parser)typeof(ReaderTests).GetMethod(nameof(_Context_ParseBar));

            var describer = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
            describer.SetBuilder((InstanceProvider)typeof(_Context).GetConstructor(Type.EmptyTypes));
            describer.AddDeserializableProperty(typeof(_Context).GetProperty(nameof(_Context.Foo)), nameof(_Context.Foo), parseFoo);
            describer.AddDeserializableProperty(typeof(_Context).GetProperty(nameof(_Context.Bar)), nameof(_Context.Bar), parseBar);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            // no headers
            {
                RunSyncReaderVariants<_Context>(
                    opts,
                    (config, getReader) =>
                    {
                        _Context_ParseFoo_Records = new List<string>();
                        _Context_ParseBar_Records = new List<string>();

                        using (var reader = getReader("hello,123\r\nfoo,456\r\n,\r\nnope,7"))
                        using (var csv = config.CreateReader(reader, "context!"))
                        {
                            var r = csv.ReadAll();

                            Assert.Equal(4, r.Count);
                        }

                        Assert.Collection(
                            _Context_ParseFoo_Records,
                            c => Assert.Equal("0,Foo,0,hello,context!", c),
                            c => Assert.Equal("1,Foo,0,foo,context!", c),
                            c => Assert.Equal("2,Foo,0,,context!", c),
                            c => Assert.Equal("3,Foo,0,nope,context!", c)
                        );

                        Assert.Collection(
                            _Context_ParseBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c),
                            c => Assert.Equal("3,Bar,1,7,context!", c)
                        );
                    }
                );
            }

            // with headers
            {
                RunSyncReaderVariants<_Context>(
                    opts,
                    (config, getReader) =>
                    {
                        _Context_ParseFoo_Records = new List<string>();
                        _Context_ParseBar_Records = new List<string>();

                        using (var reader = getReader("Bar,Foo\r\n123,hello\r\n456,foo\r\n8,\r\n7,nope"))
                        using (var csv = config.CreateReader(reader, 999))
                        {
                            var r = csv.ReadAll();

                            Assert.Equal(4, r.Count);
                        }

                        Assert.Collection(
                            _Context_ParseFoo_Records,
                            c => Assert.Equal("0,Foo,1,hello,999", c),
                            c => Assert.Equal("1,Foo,1,foo,999", c),
                            c => Assert.Equal("3,Foo,1,nope,999", c)
                        );

                        Assert.Collection(
                            _Context_ParseBar_Records,
                            c => Assert.Equal("0,Bar,0,123,999", c),
                            c => Assert.Equal("1,Bar,0,456,999", c),
                            c => Assert.Equal("2,Bar,0,8,999", c),
                            c => Assert.Equal("3,Bar,0,7,999", c)
                        );
                    }
                );
            }
        }

        [Fact]
        public async Task FailingParserAsync()
        {
            var m = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);

            m.SetBuilder(InstanceProvider.ForDelegate((out _FailingParser val) => { val = new _FailingParser(); return true; }));

            var t = typeof(_FailingParser).GetTypeInfo();
            var s = Setter.ForMethod(t.GetProperty(nameof(_FailingParser.Foo)).SetMethod);
            var p = Parser.ForDelegate((ReadOnlySpan<char> data, in ReadContext ctx, out string result) => { result = ""; return false; });

            m.AddExplicitSetter(t, "Foo", s, p);

            var opt = Options.Default.NewBuilder().WithTypeDescriber(m).Build();

            await RunAsyncReaderVariants<_FailingParser>(
                opt,
                async (config, getReader) =>
                {
                    await using (var r = await getReader("hello"))
                    await using (var csv = config.CreateAsyncReader(r))
                    {
                        await Assert.ThrowsAsync<SerializationException>(async () => await csv.ReadAllAsync());
                    }
                }
            );
        }

#if DEBUG
        private sealed class _AsyncEnumerableAsync
        {
            public string Foo { get; set; }
        }

        [Fact]
        public async Task AsyncEnumerableAsync()
        {
            await RunAsyncReaderVariants<_AsyncEnumerableAsync>(
                Options.Default,
                async (config, getReader) =>
                {
                    var testConfig = config as AsyncCountingAndForcingConfig<_AsyncEnumerableAsync>;

                    await using (var reader = await getReader("foo\r\n123\r\nnope"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var e = csv.EnumerateAllAsync();
                        testConfig?.Set(e);

                        var ix = 0;
                        await foreach(var row in e)
                        {
                            switch (ix)
                            {
                                case 0:
                                    Assert.Equal("foo", row.Foo);
                                    break;
                                case 1:
                                    Assert.Equal("123", row.Foo);
                                    break;
                                case 2:
                                    Assert.Equal("nope", row.Foo);
                                    break;
                                default:
                                    Assert.NotNull("Shouldn't be possible");
                                    break;
                            }
                            ix++;
                        }

                        Assert.Equal(3, ix);
                    }
                }
            );

            await RunAsyncReaderVariants<_AsyncEnumerableAsync>(
                Options.Default,
                async (config, getReader) =>
                {
                    var testConfig = config as AsyncCountingAndForcingConfig<_AsyncEnumerableAsync>;

                    await using (var reader = await getReader("foo\r\n123\r\nnope"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var e = csv.EnumerateAllAsync();
                        var i = e.GetAsyncEnumerator();

                        testConfig?.Set(i);

                        var ix = 0;
                        while(await i.MoveNextAsync())
                        {
                            var row = i.Current;
                            switch (ix)
                            {
                                case 0:
                                    Assert.Equal("foo", row.Foo);
                                    break;
                                case 1:
                                    Assert.Equal("123", row.Foo);
                                    break;
                                case 2:
                                    Assert.Equal("nope", row.Foo);
                                    break;
                                default:
                                    Assert.NotNull("Shouldn't be possible");
                                    break;
                            }
                            ix++;
                        }

                        Assert.Equal(3, ix);
                    }
                }
            );
        }
#endif

        [Fact]
        public async Task RowCreationFailureAsync()
        {
            int failAfter = 0;
            int calls = 0;
            InstanceProviderDelegate<_RowCreationFailure> builder =
                (out _RowCreationFailure row) =>
                {
                    if (calls >= failAfter)
                    {
                        row = default;
                        return false;
                    }

                    calls++;

                    row = new _RowCreationFailure();
                    return true;
                };


            var typeDesc = new ManualTypeDescriber();
            typeDesc.AddDeserializableProperty(typeof(_RowCreationFailure).GetProperty(nameof(_RowCreationFailure.Foo)));
            typeDesc.SetBuilder((InstanceProvider)builder);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(typeDesc).Build();

            await RunAsyncReaderVariants<_RowCreationFailure>(
                opts,
                async (config, makeReader) =>
                {
                    calls = 0;
                    failAfter = 3;

                    await using (var reader = await makeReader("Foo\r\n1\r\n2\r\n3\r\n4"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var res1 = await csv.TryReadAsync();
                        Assert.True(res1.HasValue);
                        Assert.Equal(1, res1.Value.Foo);

                        var res2 = await csv.TryReadAsync();
                        Assert.True(res2.HasValue);
                        Assert.Equal(2, res2.Value.Foo);

                        var res3 = await csv.TryReadAsync();
                        Assert.True(res3.HasValue);
                        Assert.Equal(3, res3.Value.Foo);

                        await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                    }
                }
            );
        }


        [Fact]
        public async Task WithCommentsAsync()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                // with headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope\r\n#comment\rwhatever\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\r\nA,Nope\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\r\n#again!###foo###"))
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
                var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).Build();

                // with headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope\r#comment\nwhatever\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\nwhatever\rA,Nope\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\nwhatever\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\nwhatever\r#again!###foo###"))
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
                var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).Build();

                // with headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope\n#comment\rwhatever\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\nA,Nope\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, no comment
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", row.A);
                            Assert.Equal(123, row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncReaderVariants<_WithComments>(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\n#again!###foo###"))
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
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.LineFeed).Build();
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\r\nhello,world\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\n#this is a test comment!\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturn).Build();
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\rhello,world\rfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r#this is a test comment!\n\rfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }

            {
                var opts = Options.Default.NewBuilder().WithReadHeader(ReadHeaders.Always).WithCommentCharacter('#').WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();
                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\r\nhello,world\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                await RunAsyncReaderVariants<_Comment>(
                   opts,
                   async (config, getReader) =>
                   {
                       var CSV = "#this is a test comment!\r\r\nhello,world\r\nfoo,bar";
                       await using (var str = await getReader(CSV))
                       await using (var csv = config.CreateAsyncReader(str))
                       {
                           var rows = await csv.ReadAllAsync();
                           Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                       }
                   }
               );

                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\n\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );

                await RunAsyncReaderVariants<_Comment>(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\r\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task DelegateStaticResetAsync()
        {
            var resetCalled = 0;
            StaticResetDelegate resetDel =
                () =>
                {
                    resetCalled++;
                };

            var name = nameof(_DelegateReset.Foo);
            var parser = Parser.GetDefault(typeof(int).GetTypeInfo());
            var setter = Setter.ForMethod(typeof(_DelegateReset).GetProperty(name).SetMethod);
            var reset = Reset.ForDelegate(resetDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitSetter(typeof(_DelegateReset).GetTypeInfo(), name, setter, parser, IsMemberRequired.No, reset);
            InstanceProviderDelegate<_DelegateReset> del = (out _DelegateReset i) => { i = new _DelegateReset(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            await RunAsyncReaderVariants<_DelegateReset>(
                opts,
                async (config, getReader) =>
                {
                    resetCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(23, r.Foo),
                            r => Assert.Equal(456, r.Foo),
                            r => Assert.Equal(7, r.Foo)
                        );
                    }

                    Assert.Equal(4, resetCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateResetAsync()
        {
            var resetCalled = 0;
            ResetDelegate<_DelegateReset> resetDel =
                (_DelegateReset row) =>
                {
                    resetCalled++;
                };

            var name = nameof(_DelegateReset.Foo);
            var parser = Parser.GetDefault(typeof(int).GetTypeInfo());
            var setter = Setter.ForMethod(typeof(_DelegateReset).GetProperty(name).SetMethod);
            var reset = Reset.ForDelegate(resetDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitSetter(typeof(_DelegateReset).GetTypeInfo(), name, setter, parser, IsMemberRequired.No, reset);
            InstanceProviderDelegate<_DelegateReset> del = (out _DelegateReset i) => { i = new _DelegateReset(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            await RunAsyncReaderVariants<_DelegateReset>(
                opts,
                async (config, getReader) =>
                {
                    resetCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(23, r.Foo),
                            r => Assert.Equal(456, r.Foo),
                            r => Assert.Equal(7, r.Foo)
                        );
                    }

                    Assert.Equal(4, resetCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateStaticSetterAsync()
        {
            var setterCalled = 0;

            StaticSetterDelegate<int> parser =
                (int value) =>
                {
                    setterCalled++;
                };

            var describer = new ManualTypeDescriber();
            describer.AddExplicitSetter(typeof(_DelegateSetter).GetTypeInfo(), "Foo", Setter.ForDelegate(parser));
            InstanceProviderDelegate<_DelegateSetter> del = (out _DelegateSetter i) => { i = new _DelegateSetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            await RunAsyncReaderVariants<_DelegateSetter>(
                opts,
                async (config, getReader) =>
                {
                    setterCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo),
                            r => Assert.Equal(0, r.Foo)
                        );
                    }

                    Assert.Equal(4, setterCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateSetterAsync()
        {
            var setterCalled = 0;

            SetterDelegate<_DelegateSetter, int> parser =
                (_DelegateSetter row, int value) =>
                {
                    setterCalled++;

                    row.Foo = value * 2;
                };

            var describer = new ManualTypeDescriber();
            describer.AddExplicitSetter(typeof(_DelegateSetter).GetTypeInfo(), "Foo", Setter.ForDelegate(parser));
            InstanceProviderDelegate<_DelegateSetter> del = (out _DelegateSetter i) => { i = new _DelegateSetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            await RunAsyncReaderVariants<_DelegateSetter>(
                opts,
                async (config, getReader) =>
                {
                    setterCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1 * 2, r.Foo),
                            r => Assert.Equal(23 * 2, r.Foo),
                            r => Assert.Equal(456 * 2, r.Foo),
                            r => Assert.Equal(7 * 2, r.Foo)
                        );
                    }

                    Assert.Equal(4, setterCalled);
                }
            );
        }

        [Fact]
        public async Task ConstructorParserAsync()
        {
            var cons1 = typeof(_ConstructorParser).GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
            var cons2 = typeof(_ConstructorParser).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });

            // single param
            {
                var describer = new ManualTypeDescriber();
                describer.AddDeserializableProperty(
                    typeof(_ConstructorParser_Outer).GetProperty(nameof(_ConstructorParser_Outer.Foo)),
                    nameof(_ConstructorParser_Outer.Foo),
                    Parser.ForConstructor(cons1)
                );
                InstanceProviderDelegate<_ConstructorParser_Outer> del = (out _ConstructorParser_Outer i) => { i = new _ConstructorParser_Outer(); return true; };
                describer.SetBuilder((InstanceProvider)del);

                var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

                await RunAsyncReaderVariants<_ConstructorParser_Outer>(
                    opts,
                    async (config, getReader) =>
                    {
                        _ConstructorParser.Cons1Called = 0;

                        await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var row = await csv.ReadAllAsync();

                            Assert.Collection(
                                row,
                                r => Assert.Equal("1", r.Foo.Value),
                                r => Assert.Equal("23", r.Foo.Value),
                                r => Assert.Equal("456", r.Foo.Value),
                                r => Assert.Equal("7", r.Foo.Value)
                            );
                        }

                        Assert.Equal(4, _ConstructorParser.Cons1Called);
                    }
                );
            }

            // two params
            {
                var describer = new ManualTypeDescriber();
                describer.AddDeserializableProperty(
                    typeof(_ConstructorParser_Outer).GetProperty(nameof(_ConstructorParser_Outer.Foo)),
                    nameof(_ConstructorParser_Outer.Foo),
                    Parser.ForConstructor(cons2)
                );
                InstanceProviderDelegate<_ConstructorParser_Outer> del = (out _ConstructorParser_Outer i) => { i = new _ConstructorParser_Outer(); return true; };
                describer.SetBuilder((InstanceProvider)del);

                var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

                await RunAsyncReaderVariants<_ConstructorParser_Outer>(
                    opts,
                    async (config, getReader) =>
                    {
                        _ConstructorParser.Cons2Called = 0;

                        await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var row = await csv.ReadAllAsync();

                            Assert.Collection(
                                row,
                                r => Assert.Equal("10", r.Foo.Value),
                                r => Assert.Equal("230", r.Foo.Value),
                                r => Assert.Equal("4560", r.Foo.Value),
                                r => Assert.Equal("70", r.Foo.Value)
                            );
                        }

                        Assert.Equal(4, _ConstructorParser.Cons2Called);
                    }
                );
            }
        }

        [Fact]
        public async Task DelegateParserAsync()
        {
            var parserCalled = 0;

            ParserDelegate<int> parser =
                (ReadOnlySpan<char> data, in ReadContext _, out int res) =>
                {
                    parserCalled++;

                    res = data.Length;
                    return true;
                };

            var describer = new ManualTypeDescriber();
            describer.AddDeserializableProperty(
                typeof(_DelegateParser).GetProperty(nameof(_DelegateParser.Foo)),
                nameof(_DelegateParser.Foo),
                Parser.ForDelegate(parser)
            );
            InstanceProviderDelegate<_DelegateParser> del = (out _DelegateParser i) => { i = new _DelegateParser(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            await RunAsyncReaderVariants<_DelegateParser>(
                opts,
                async (config, getReader) =>
                {
                    parserCalled = 0;

                    await using (var reader = await getReader("1\r\n23\r\n456\r\n7\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(
                            row,
                            r => Assert.Equal(1, r.Foo),
                            r => Assert.Equal(2, r.Foo),
                            r => Assert.Equal(3, r.Foo),
                            r => Assert.Equal(1, r.Foo)
                        );
                    }

                    Assert.Equal(4, parserCalled);
                }
            );
        }

        [Fact]
        public async Task WithResetAsync()
        {
            // simple
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                await RunAsyncReaderVariants<_WithReset>(
                    Options.Default,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(2, a.B); }
                            );
                        }
                    }
                );
            }

            // static
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                await RunAsyncReaderVariants<_WithReset_Static>(
                    Options.Default,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            _WithReset_Static.Count = 0;

                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(6, a.B); }
                            );

                            Assert.Equal(2, _WithReset_Static.Count);
                        }
                    }
                );
            }

            // static with param
            {
                const string CSV = "A,B\r\nfoo,1\r\nbar,6\r\n";

                await RunAsyncReaderVariants<_WithReset_StaticWithParam>(
                    Options.Default,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("foo", a.A); Assert.Equal(1, a.B); },
                                a => { Assert.Equal("bar", a.A); Assert.Equal(2, a.B); }
                            );
                        }
                    }
                );
            }
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
                    await using (var reader = await makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        try
                        {
                            await csv.ReadAllAsync();
                        }
                        catch (Exception e)
                        {
                            switch (e)
                            {
                                case AggregateException ae:
                                    Assert.Collection(
                                        ae.InnerExceptions,
                                        (e) => Assert.True(e is InvalidOperationException)
                                    );
                                    break;
                                case InvalidOperationException ioe:
                                    break;
                                default:
                                    // intentionally fail
                                    Assert.Null(e);
                                    break;
                            }
                        }
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

                    await using (var reader = await getReader(CSV))
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
                    await using (var reader = await makeReader(CSV))
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
                    await using (var reader = await makeReader(CSV))
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
                    await using (var reader = await makeReader(CSV))
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
                    await using (var reader = await makeReader(CSV))
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
                        await using (var str = await getReader("hello"))
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
                        await using (var str = await getReader("\"hello world\""))
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
                        await using (var str = await getReader("\"hello \"\" world\""))
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
                        await using (var str = await getReader("hello,world"))
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
                        await using (var str = await getReader("\"hello,world\",\"fizz,buzz\""))
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
                        await using (var str = await getReader("\"hello\"\"world\",\"fizz\"\"buzz\""))
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
                        await using (var str = await getReader("hello,world\r\nfoo,bar"))
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
                        await using (var str = await getReader("\"hello,world\",whatever\r\n\"foo,bar\",whoever"))
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
                        await using (var str = await getReader("\"hello\"\"world\",whatever\r\n\"foo\"\"bar\",whoever"))
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
                            await using (var str = await getReader("a,bb,ccc\r\ndddd,eeeee,ffffff\r\n1,2,3\r\n"))
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
                            await using (var str = await getReader("a,bb,ccc\rdddd,eeeee,ffffff\r1,2,3\r"))
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
                            await using (var str = await getReader("a,bb,ccc\ndddd,eeeee,ffffff\n1,2,3\n"))
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
                            await using (var str = await getReader("\"a\r\",bb,ccc\r\ndddd,\"ee\neee\",ffffff\r\n1,2,\"3\r\n\"\r\n"))
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
                            await using (var str = await getReader("\"a\r\",bb,ccc\rdddd,\"ee\neee\",ffffff\r1,2,\"3\r\n\"\r"))
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
                            await using (var str = await getReader("\"a\r\",bb,ccc\ndddd,\"ee\neee\",ffffff\n1,2,\"3\r\n\"\n"))
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
                            await using (var str = await getReader("\"a\r\",\"b\"\"b\",ccc\r\n\"\"\"dddd\",\"ee\neee\",ffffff\r\n1,\"\"\"2\"\"\",\"3\r\n\"\r\n"))
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
                             await using (var str = await getReader("\"a\r\",\"b\"\"b\",ccc\r\"\"\"dddd\",\"ee\neee\",ffffff\r1,\"\"\"2\"\"\",\"3\r\n\"\r"))
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
                            await using (var str = await getReader("\"a\r\",\"b\"\"b\",ccc\n\"\"\"dddd\",\"ee\neee\",ffffff\n1,\"\"\"2\"\"\",\"3\r\n\"\n"))
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
                    await using (var str = await del("123,4.56"))
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
                        await using (var str = await del("Hello,World\r\n123,4.56\r\n789,0.12\r\n"))
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
                        await using (var str = await del("Hello,World\n123,4.56\n789,0.12\n"))
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
                        await using (var str = await del("Hello,World\r123,4.56\r789,0.12\r"))
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
                        await using (var str = await del("World,Hello\r\n4.56,123\r\n0.12,789\r\n"))
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
                        await using (var str = await del("World,Hello\n4.56,123\n0.12,789\n"))
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
                        await using (var str = await del("World,Hello\r4.56,123\r0.12,789\r"))
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
                        await using (var str = await del("World,Foo\r\n4.56,123\r\n0.12,789\r\n"))
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
                        await using (var str = await del("World,Foo\n4.56,123\n0.12,789\n"))
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
                        await using (var str = await del("World,Foo\r4.56,123\r0.12,789\r"))
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
                    await using (var reader = await makeReader(CSV))
                    await using (var csv = config.CreateAsyncReader(reader))
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
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
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
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
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
                        await using (var reader = await makeReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
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
                        await using (var reader = await makeReader("Foo,Bar\r\nhello,world"))
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
                        await using (var reader = await makeReader("Foo,Bar\r\n\"hello\",\"world\""))
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
                        await using (var reader = await makeReader("Foo,Bar\r\n\"he\\\"llo\",\"world\""))
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
                        await using (var reader = await makeReader("Foo,Bar\r\n\"hello\",\"w\\\\orld\""))
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
                        await using (var reader = await makeReader("Foo,Bar\r\n\\,\\ooo"))
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
                    await using (var reader = await getReader(TSV))
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
                        await using (var reader = await getReader(CSV))
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
                        await using (var reader = await getReader(CSV))
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
                        await using (var reader = await getReader(CSV))
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
                        await using (var reader = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", a.Hello); Assert.Equal("bar", a.World); });
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task ContextAsync()
        {
            var parseFoo = (Parser)typeof(ReaderTests).GetMethod(nameof(_Context_ParseFoo));
            var parseBar = (Parser)typeof(ReaderTests).GetMethod(nameof(_Context_ParseBar));

            var describer = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
            describer.SetBuilder((InstanceProvider)typeof(_Context).GetConstructor(Type.EmptyTypes));
            describer.AddDeserializableProperty(typeof(_Context).GetProperty(nameof(_Context.Foo)), nameof(_Context.Foo), parseFoo);
            describer.AddDeserializableProperty(typeof(_Context).GetProperty(nameof(_Context.Bar)), nameof(_Context.Bar), parseBar);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            // no headers
            {
                await RunAsyncReaderVariants<_Context>(
                    opts,
                    async (config, getReader) =>
                    {
                        _Context_ParseFoo_Records = new List<string>();
                        _Context_ParseBar_Records = new List<string>();

                        await using (var reader = await getReader("hello,123\r\nfoo,456\r\n,\r\nnope,7"))
                        await using (var csv = config.CreateAsyncReader(reader, -22))
                        {
                            var r = await csv.ReadAllAsync();

                            Assert.Equal(4, r.Count);
                        }

                        Assert.Collection(
                            _Context_ParseFoo_Records,
                            c => Assert.Equal("0,Foo,0,hello,-22", c),
                            c => Assert.Equal("1,Foo,0,foo,-22", c),
                            c => Assert.Equal("2,Foo,0,,-22", c),
                            c => Assert.Equal("3,Foo,0,nope,-22", c)
                        );

                        Assert.Collection(
                            _Context_ParseBar_Records,
                            c => Assert.Equal("0,Bar,1,123,-22", c),
                            c => Assert.Equal("1,Bar,1,456,-22", c),
                            c => Assert.Equal("3,Bar,1,7,-22", c)
                        );
                    }
                );
            }

            // with headers
            {
                await RunAsyncReaderVariants<_Context>(
                    opts,
                    async (config, getReader) =>
                    {
                        _Context_ParseFoo_Records = new List<string>();
                        _Context_ParseBar_Records = new List<string>();

                        await using (var reader = await getReader("Bar,Foo\r\n123,hello\r\n456,foo\r\n8,\r\n7,nope"))
                        await using (var csv = config.CreateAsyncReader(reader, "world"))
                        {
                            var r = await csv.ReadAllAsync();

                            Assert.Equal(4, r.Count);
                        }

                        Assert.Collection(
                            _Context_ParseFoo_Records,
                            c => Assert.Equal("0,Foo,1,hello,world", c),
                            c => Assert.Equal("1,Foo,1,foo,world", c),
                            c => Assert.Equal("3,Foo,1,nope,world", c)
                        );

                        Assert.Collection(
                            _Context_ParseBar_Records,
                            c => Assert.Equal("0,Bar,0,123,world", c),
                            c => Assert.Equal("1,Bar,0,456,world", c),
                            c => Assert.Equal("2,Bar,0,8,world", c),
                            c => Assert.Equal("3,Bar,0,7,world", c)
                        );
                    }
                );
            }
        }

        [Fact]
        public async Task StaticSetterAsync()
        {
            var describer = new ManualTypeDescriber();
            describer.AddDeserializableProperty(typeof(_StaticSetter).GetProperty(nameof(_StaticSetter.Foo), BindingFlags.Static | BindingFlags.Public));
            InstanceProviderDelegate<_StaticSetter> del = (out _StaticSetter i) => { i = new _StaticSetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

            await RunAsyncReaderVariants<_StaticSetter>(
                opts,
                async (config, getReader) =>
                {
                    _StaticSetter.Foo = 123;

                    await using (var reader = await getReader("456"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var row = await csv.ReadAllAsync();

                        Assert.Collection(row, r => Assert.NotNull(r));
                    }

                    Assert.Equal(456, _StaticSetter.Foo);
                }
            );
        }
    }
#pragma warning restore IDE1006
}