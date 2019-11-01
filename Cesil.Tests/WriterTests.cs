using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class WriterTests
    {
        private sealed class _Errors
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void Errors()
        {
            RunSyncWriterVariants<_Errors>(
                Options.Default,
                (config, makeWriter, getStr) =>
                {
                    using(var w = makeWriter())
                    using(var csv = config.CreateWriter(w))
                    {
                        Assert.Throws<ArgumentNullException>(() => csv.WriteAll(null));

                        var exc = Assert.Throws<InvalidOperationException>(() => csv.WriteComment("foo"));
                        Assert.Equal($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line", exc.Message);
                    }

                    getStr();
                }
            );
        }

        private sealed class _BufferWriterByte
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void BufferWriterByte()
        {
            var pipe = new Pipe();

            var config = Configuration.For<_BufferWriterByte>();
            using (var writer = config.CreateWriter(pipe.Writer, Encoding.UTF7))
            {
                writer.Write(new _BufferWriterByte { Foo = "hello", Bar = "world" });
            }

            pipe.Writer.Complete();

            var bytes = new List<byte>();

            while (pipe.Reader.TryRead(out var res))
            {
                foreach (var b in res.Buffer)
                {
                    bytes.AddRange(b.ToArray());
                }

                if (res.IsCompleted)
                {
                    break;
                }
            }

            var str = Encoding.UTF7.GetString(bytes.ToArray());

            Assert.Equal("Foo,Bar\r\nhello,world", str);
        }

        private sealed class _BufferWriterChar
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        private sealed class _BufferWriterChar_Writer : IBufferWriter<char>
        {
            public List<char> Data;

            private Memory<char> Current;

            public _BufferWriterChar_Writer()
            {
                Data = new List<char>();
            }

            public void Advance(int count)
            {
                Data.AddRange(Current.Slice(0, count).ToArray());
                Current = Memory<char>.Empty;
            }

            public Memory<char> GetMemory(int sizeHint = 0)
            {
                if (sizeHint <= 0) sizeHint = 8;
                var arr = new char[sizeHint];

                Current = arr.AsMemory();

                return Current;
            }

            public Span<char> GetSpan(int sizeHint = 0)
            => GetMemory(sizeHint).Span;
        }

        [Fact]
        public void BufferWriterChar()
        {
            var charWriter = new _BufferWriterChar_Writer();

            var config = Configuration.For<_BufferWriterByte>();
            using (var writer = config.CreateWriter(charWriter))
            {
                writer.Write(new _BufferWriterByte { Foo = "hello", Bar = "world" });
            }

            var str = new string(charWriter.Data.ToArray());

            Assert.Equal("Foo,Bar\r\nhello,world", str);
        }

        private sealed class _FailingGetter
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void FailingGetter()
        {
            var m = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
            var t = typeof(_FailingGetter).GetTypeInfo();
            var g = Getter.ForMethod(t.GetProperty(nameof(_FailingGetter.Foo)).GetMethod);
            var f = Formatter.ForDelegate((int value, in WriteContext context, IBufferWriter<char> buffer) => false);

            m.SetBuilder(InstanceProvider.ForDelegate((out _FailingGetter val) => { val = new _FailingGetter(); return true; }));
            m.AddExplicitGetter(t, "bar", g, f);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(m).Build();

            RunSyncWriterVariants<_FailingGetter>(
                opts,
                (config, getWriter, getStr) =>
                {
                    using (var w = getWriter())
                    using (var csv = config.CreateWriter(w))
                    {
                        Assert.Throws<SerializationException>(() => csv.Write(new _FailingGetter()));
                    }

                    var res = getStr();
                    Assert.Equal("bar\r\n", res);
                }
            );
        }

        class _SerializableMemberDefaults
        {
            public int Prop { get; set; }
#pragma warning disable CS0649
            public string Field;
#pragma warning restore CS0649
        }

        [Fact]
        public void SerializableMemberHelpers()
        {
            // fields
            {
                var f = typeof(_SerializableMemberDefaults).GetField(nameof(_SerializableMemberDefaults.Field));

                // 1
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null));

                    var s1 = SerializableMember.ForField(f);
                    Assert.True(s1.EmitDefaultValue);
                    Assert.Equal(Formatter.GetDefault(typeof(string).GetTypeInfo()), s1.Formatter);
                    Assert.Equal(Getter.ForField(f), s1.Getter);
                    Assert.Equal("Field", s1.Name);
                    Assert.False(s1.ShouldSerialize.HasValue);
                }

                // 2
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null, "Nope"));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, null));

                    var s2 = SerializableMember.ForField(f, "Nope");
                    Assert.True(s2.EmitDefaultValue);
                    Assert.Equal(Formatter.GetDefault(typeof(string).GetTypeInfo()), s2.Formatter);
                    Assert.Equal(Getter.ForField(f), s2.Getter);
                    Assert.Equal("Nope", s2.Name);
                    Assert.False(s2.ShouldSerialize.HasValue);
                }

                var formatter =
                    Formatter.ForDelegate(
                        (string val, in WriteContext ctx, IBufferWriter<char> buffer) =>
                        {
                            return true;
                        }
                    );
                
                // 3
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null, "Yep", formatter));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, null, formatter));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, "Yep", null));

                    var s3 = SerializableMember.ForField(f, "Yep", formatter);
                    Assert.True(s3.EmitDefaultValue);
                    Assert.Equal(formatter, s3.Formatter);
                    Assert.Equal(Getter.ForField(f), s3.Getter);
                    Assert.Equal("Yep", s3.Name);
                    Assert.False(s3.ShouldSerialize.HasValue);
                }

                var shouldSerialize =
                    Cesil.ShouldSerialize.ForDelegate(
                        () => true
                    );

                // 4
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null, "Yep", formatter, shouldSerialize));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, null, formatter, shouldSerialize));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, "Yep", null, shouldSerialize));
                    // it's ok if shouldSerialize == null

                    var s4 = SerializableMember.ForField(f, "Fizz", formatter, shouldSerialize);
                    Assert.True(s4.EmitDefaultValue);
                    Assert.Equal(formatter, s4.Formatter);
                    Assert.Equal(Getter.ForField(f), s4.Getter);
                    Assert.Equal("Fizz", s4.Name);
                    Assert.Equal(shouldSerialize, s4.ShouldSerialize.Value);
                }

                // 5
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null, "Yep", formatter, shouldSerialize, WillEmitDefaultValue.Yes));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, null, formatter, shouldSerialize, WillEmitDefaultValue.Yes));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, "Yep", null, shouldSerialize, WillEmitDefaultValue.Yes));
                    // it's ok if shouldSerialize == null
                    // bad values for WillEmitDefaultValue are tested elsewhere

                    var s5 = SerializableMember.ForField(f, "Buzz", formatter, shouldSerialize, WillEmitDefaultValue.No);
                    Assert.False(s5.EmitDefaultValue);
                    Assert.Equal(formatter, s5.Formatter);
                    Assert.Equal(Getter.ForField(f), s5.Getter);
                    Assert.Equal("Buzz", s5.Name);
                    Assert.Equal(shouldSerialize, s5.ShouldSerialize.Value);
                }
            }

            // property
            {
                var p = typeof(_SerializableMemberDefaults).GetProperty(nameof(_SerializableMemberDefaults.Prop));

                // 1
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null));

                    var s1 = SerializableMember.ForProperty(p);
                    Assert.True(s1.EmitDefaultValue);
                    Assert.Equal(Formatter.GetDefault(typeof(int).GetTypeInfo()), s1.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s1.Getter);
                    Assert.Equal("Prop", s1.Name);
                    Assert.False(s1.ShouldSerialize.HasValue);
                }

                // 2
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null, "Hello"));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, null));

                    var s2 = SerializableMember.ForProperty(p, "Hello");
                    Assert.True(s2.EmitDefaultValue);
                    Assert.Equal(Formatter.GetDefault(typeof(int).GetTypeInfo()), s2.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s2.Getter);
                    Assert.Equal("Hello", s2.Name);
                    Assert.False(s2.ShouldSerialize.HasValue);
                }

                var formatter =
                    Formatter.ForDelegate(
                        (int val, in WriteContext ctx, IBufferWriter<char> buffer) =>
                        {
                            return true;
                        }
                    );

                // 3
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null, "World", formatter));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, null, formatter));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, "World", null));

                    var s3 = SerializableMember.ForProperty(p, "World", formatter);
                    Assert.True(s3.EmitDefaultValue);
                    Assert.Equal(formatter, s3.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s3.Getter);
                    Assert.Equal("World", s3.Name);
                    Assert.False(s3.ShouldSerialize.HasValue);
                }

                var shouldSerialize =
                    Cesil.ShouldSerialize.ForDelegate(
                        () => true
                    );

                // 4
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null, "Blogo", formatter, shouldSerialize));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, null, formatter, shouldSerialize));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, "Blogo", null, shouldSerialize));
                    // it's ok if shouldSerialize == null

                    var s4 = SerializableMember.ForProperty(p, "Blogo", formatter, shouldSerialize);
                    Assert.True(s4.EmitDefaultValue);
                    Assert.Equal(formatter, s4.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s4.Getter);
                    Assert.Equal("Blogo", s4.Name);
                    Assert.Equal(shouldSerialize, s4.ShouldSerialize.Value);
                }

                // 5
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null, "Blogo", formatter, shouldSerialize, WillEmitDefaultValue.Yes));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, null, formatter, shouldSerialize, WillEmitDefaultValue.Yes));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, "Blogo", null, shouldSerialize, WillEmitDefaultValue.Yes));
                    // it's ok if shouldSerialize == null
                    // bad values for WillEmitDefaultValue are tested elsewhere

                    var s5 = SerializableMember.ForProperty(p, "Sphere", formatter, shouldSerialize, WillEmitDefaultValue.No);
                    Assert.False(s5.EmitDefaultValue);
                    Assert.Equal(formatter, s5.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s5.Getter);
                    Assert.Equal("Sphere", s5.Name);
                    Assert.Equal(shouldSerialize, s5.ShouldSerialize.Value);
                }
            }
        }

        [Fact]
        public void SerializableMemberEquality()
        {
            var t = typeof(WriterTests).GetTypeInfo();

            var emitDefaults = new[] { WillEmitDefaultValue.Yes, WillEmitDefaultValue.No };
            IEnumerable<Formatter> formatters;
            {
                var a = (Formatter)(FormatterDelegate<int>)_Formatter;
                var b = Formatter.GetDefault(typeof(int).GetTypeInfo());

                formatters = new[] { a, b };
            }
            IEnumerable<Getter> getters;
            {
                var a = (Getter)(StaticGetterDelegate<int>)(() => 1);
                var b = (Getter)(StaticGetterDelegate<int>)(() => 2);

                getters = new[] { a, b };
            }
            var names = new[] { "foo", "bar" };
            IEnumerable<Cesil.ShouldSerialize> shouldSerializes;
            {
                var a = (Cesil.ShouldSerialize)(StaticShouldSerializeDelegate)(() => true);
                var b = (Cesil.ShouldSerialize)(StaticShouldSerializeDelegate)(() => false);
                shouldSerializes = new[] { a, b, null };
            }

            var members = new List<SerializableMember>();
            foreach (var e in emitDefaults)
            {
                foreach (var f in formatters)
                {
                    foreach (var g in getters)
                    {
                        foreach (var n in names)
                        {
                            foreach (var s in shouldSerializes)
                            {
                                members.Add(SerializableMember.Create(t, n, g, f, s, e));
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

                    if(i == j)
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

            static bool _Formatter(int v, in WriteContext wc, IBufferWriter<char> b)
            {
                return true;
            }
        }

        class _SerializableMemberErrors
        {
#pragma warning disable CS0649
            public int A;
#pragma warning restore CS0649
        }

        class _SerializeMemberErrors_Unreleated
        {
            public bool ShouldSerializeA() { return true; }
        }

        [Fact]
        public void SerializableMemberErrors()
        {
            var type = typeof(_SerializableMemberErrors).GetTypeInfo();
            Assert.NotNull(type);
            var getter = Getter.ForField(typeof(_SerializableMemberErrors).GetField(nameof(_SerializableMemberErrors.A)));
            Assert.NotNull(getter);
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            Assert.NotNull(formatter);

            Assert.Throws<ArgumentNullException>(() => SerializableMember.Create(null, "foo", getter, formatter, null, WillEmitDefaultValue.Yes));
            Assert.Throws<ArgumentNullException>(() => SerializableMember.Create(type, null, getter, formatter, null, WillEmitDefaultValue.Yes));
            Assert.Throws<ArgumentNullException>(() => SerializableMember.Create(type, "foo", null, formatter, null, WillEmitDefaultValue.Yes));
            Assert.Throws<ArgumentNullException>(() => SerializableMember.Create(type, "foo", getter, null, null, WillEmitDefaultValue.Yes));
            Assert.Throws<InvalidOperationException>(() => SerializableMember.Create(type, "foo", getter, formatter, null, 0));

            var shouldSerialize = (ShouldSerialize)typeof(_SerializeMemberErrors_Unreleated).GetMethod(nameof(_SerializeMemberErrors_Unreleated.ShouldSerializeA));
            Assert.NotNull(shouldSerialize);

            Assert.Throws<ArgumentException>(() => SerializableMember.Create(type, "foo", getter, formatter, shouldSerialize, WillEmitDefaultValue.Yes));
        }

        class _LotsOfComments
        {
            public string Hello { get; set; }
        }

        [Fact]
        public void LotsOfComments()
        {
            var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

            RunSyncWriterVariants<_LotsOfComments>(
                opts,
                (config, getWriter, getStr) =>
                {
                    var cs = string.Join("\r\n", Enumerable.Repeat("foo", 1_000));

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.WriteComment(cs);
                    }

                    var str = getStr();
                    var expected = nameof(_LotsOfComments.Hello) + "\r\n" + string.Join("\r\n", Enumerable.Repeat("#foo", 1_000));
                    Assert.Equal(expected, str);
                }
            );
        }

        class _NullCommentError
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void NullCommentError()
        {
            RunSyncWriterVariants<_NullCommentError>(
                Options.Default,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        Assert.Throws<ArgumentNullException>(() => csv.WriteComment(null));
                    }

                    var _ = getStr();
                    Assert.NotNull(_);
                }
            );
        }

        [Fact]
        public void WriteContexts()
        {
            var dc1 = Cesil.WriteContext.DiscoveringCells(1, null);
            var dc2 = Cesil.WriteContext.DiscoveringCells(1, "foo");

            Assert.Equal(WriteContextMode.DiscoveringCells, dc1.Mode);
            Assert.False(dc1.HasColumn);
            Assert.True(dc1.HasRowNumber);
            Assert.Equal(1, dc1.RowNumber);
            Assert.Throws<InvalidOperationException>(() => dc1.Column);

            var dcol1 = Cesil.WriteContext.DiscoveringColumns(null);
            var dcol2 = Cesil.WriteContext.DiscoveringColumns("foo");
            Assert.Equal(WriteContextMode.DiscoveringColumns, dcol1.Mode);
            Assert.False(dcol1.HasRowNumber);
            Assert.False(dcol1.HasColumn);
            Assert.Throws<InvalidOperationException>(() => dcol1.RowNumber);
            Assert.Throws<InvalidOperationException>(() => dcol1.Column);

            var wc1 = Cesil.WriteContext.WritingColumn(1, ColumnIdentifier.Create(1), null);
            var wc2 = Cesil.WriteContext.WritingColumn(1, ColumnIdentifier.Create(1), "foo");
            var wc3 = Cesil.WriteContext.WritingColumn(1, ColumnIdentifier.Create(2), null);
            var wc4 = Cesil.WriteContext.WritingColumn(2, ColumnIdentifier.Create(1), null);
            Assert.Equal(WriteContextMode.WritingColumn, wc1.Mode);
            Assert.True(wc1.HasColumn);
            Assert.True(wc1.HasRowNumber);

            var contexts = new[] { dc1, dc2, dcol1, dcol2, wc1, wc2, wc3, wc4 };

            var notContext = "";

            for (var i = 0; i < contexts.Length; i++)
            {
                var ctx1 = contexts[i];
                Assert.False(ctx1.Equals(notContext));
                Assert.NotNull(ctx1.ToString());

                for (var j = i; j < contexts.Length; j++)
                {
                    var ctx2 = contexts[j];

                    var eq = ctx1 == ctx2;
                    var neq = ctx1 != ctx2;
                    var hashEq = ctx1.GetHashCode() == ctx2.GetHashCode();
                    var objEq = ctx1.Equals((object)ctx2);

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.True(objEq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.False(objEq);
                        Assert.True(neq);
                    }
                }
            }
        }

        private class _WriteComment
        {
            public int Foo { get; set; }
            public int Bar { get; set; }
        }

        [Fact]
        public void WriteComment()
        {
            // no trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
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

                // first line, headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }

                // before row, headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }
            }

            // trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
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

                // first line, headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }

                // before row, headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }
            }
        }

        private class _DelegateShouldSerialize
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateStaticShouldSerialize()
        {
            var shouldSerializeCalled = 0;
            StaticShouldSerializeDelegate shouldSerializeDel =
                () =>
                {
                    shouldSerializeCalled++;

                    return false;
                };

            var name = nameof(_DelegateShouldSerialize.Foo);
            var getter = (Getter)typeof(_DelegateShouldSerialize).GetProperty(nameof(_DelegateShouldSerialize.Foo)).GetMethod;
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            var shouldSerialize = Cesil.ShouldSerialize.ForDelegate(shouldSerializeDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateShouldSerialize).GetTypeInfo(), name, getter, formatter, shouldSerialize);
            InstanceProviderDelegate<_DelegateShouldSerialize> del = (out _DelegateShouldSerialize i) => { i = new _DelegateShouldSerialize(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            RunSyncWriterVariants<_DelegateShouldSerialize>(
                opts,
                (config, getWriter, getStr) =>
                {
                    shouldSerializeCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateShouldSerialize { Foo = 123 });
                        csv.Write(new _DelegateShouldSerialize { Foo = 0 });
                        csv.Write(new _DelegateShouldSerialize { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n\r\n\r\n", res);

                    Assert.Equal(3, shouldSerializeCalled);
                }
            );
        }

        [Fact]
        public void DelegateShouldSerialize()
        {
            var shouldSerializeCalled = 0;
            ShouldSerializeDelegate<_DelegateShouldSerialize> shouldSerializeDel =
                row =>
                {
                    shouldSerializeCalled++;

                    return row.Foo % 2 != 0;
                };

            var name = nameof(_DelegateShouldSerialize.Foo);
            var getter = (Getter)typeof(_DelegateShouldSerialize).GetProperty(nameof(_DelegateShouldSerialize.Foo)).GetMethod;
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            var shouldSerialize = Cesil.ShouldSerialize.ForDelegate(shouldSerializeDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateShouldSerialize).GetTypeInfo(), name, getter, formatter, shouldSerialize);
            InstanceProviderDelegate<_DelegateShouldSerialize> del = (out _DelegateShouldSerialize i) => { i = new _DelegateShouldSerialize(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            RunSyncWriterVariants<_DelegateShouldSerialize>(
                opts,
                (config, getWriter, getStr) =>
                {
                    shouldSerializeCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateShouldSerialize { Foo = 123 });
                        csv.Write(new _DelegateShouldSerialize { Foo = 0 });
                        csv.Write(new _DelegateShouldSerialize { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n123\r\n\r\n", res);

                    Assert.Equal(3, shouldSerializeCalled);
                }
            );
        }

        private class _DelegateFormatter
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateFormatter()
        {
            var formatterCalled = 0;
            FormatterDelegate<int> formatDel =
                (int val, in WriteContext _, IBufferWriter<char> buffer) =>
                {
                    formatterCalled++;

                    var s = val.ToString();

                    buffer.Write(s);
                    buffer.Write(s);

                    return true;
                };

            var name = nameof(_DelegateFormatter.Foo);
            var getter = (Getter)typeof(_DelegateFormatter).GetProperty(nameof(_DelegateFormatter.Foo)).GetMethod;
            var formatter = Formatter.ForDelegate(formatDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateFormatter).GetTypeInfo(), name, getter, formatter);
            InstanceProviderDelegate<_DelegateFormatter> del = (out _DelegateFormatter i) => { i = new _DelegateFormatter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            RunSyncWriterVariants<_DelegateFormatter>(
                opts,
                (config, getWriter, getStr) =>
                {
                    formatterCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateFormatter { Foo = 123 });
                        csv.Write(new _DelegateFormatter { Foo = 0 });
                        csv.Write(new _DelegateFormatter { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n123123\r\n00\r\n456456", res);

                    Assert.Equal(3, formatterCalled);
                }
            );
        }

        private class _DelegateGetter
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateStaticGetter()
        {
            var getterCalled = 0;
            StaticGetterDelegate<int> getDel =
                () =>
                {
                    getterCalled++;

                    return getterCalled;
                };

            var name = nameof(_DelegateGetter.Foo);
            var getter = Getter.ForDelegate(getDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateGetter).GetTypeInfo(), name, getter);
            InstanceProviderDelegate<_DelegateGetter> del = (out _DelegateGetter i) => { i = new _DelegateGetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            RunSyncWriterVariants<_DelegateGetter>(
                opts,
                (config, getWriter, getStr) =>
                {
                    getterCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateGetter { Foo = 123 });
                        csv.Write(new _DelegateGetter { Foo = 0 });
                        csv.Write(new _DelegateGetter { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n1\r\n2\r\n3", res);

                    Assert.Equal(3, getterCalled);
                }
            );
        }

        [Fact]
        public void DelegateGetter()
        {
            var getterCalled = 0;
            GetterDelegate<_DelegateGetter, int> getDel =
                (_DelegateGetter row) =>
                {
                    getterCalled++;

                    return row.Foo * 2;
                };

            var name = nameof(_DelegateGetter.Foo);
            var getter = Getter.ForDelegate(getDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateGetter).GetTypeInfo(), name, getter);
            InstanceProviderDelegate<_DelegateGetter> del = (out _DelegateGetter i) => { i = new _DelegateGetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            RunSyncWriterVariants<_DelegateGetter>(
                opts,
                (config, getWriter, getStr) =>
                {
                    getterCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateGetter { Foo = 123 });
                        csv.Write(new _DelegateGetter { Foo = 0 });
                        csv.Write(new _DelegateGetter { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n246\r\n0\r\n912", res);

                    Assert.Equal(3, getterCalled);
                }
            );
        }

        private struct _UserDefinedEmitDefaultValue_ValueType
        {
            public int Value { get; set; }
        }

        private struct _UserDefinedEmitDefaultValue_ValueType_Equatable : IEquatable<_UserDefinedEmitDefaultValue_ValueType_Equatable>
        {
            public static int EqualsCallCount = 0;

            public int Value { get; set; }

            public bool Equals(_UserDefinedEmitDefaultValue_ValueType_Equatable other)
            {
                EqualsCallCount++;

                return Value == other.Value;
            }
        }

        private struct _UserDefinedEmitDefaultValue_ValueType_Operator
        {
            public static int OperatorCallCount = 0;

            public int Value { get; set; }

            public static bool operator ==(_UserDefinedEmitDefaultValue_ValueType_Operator a, _UserDefinedEmitDefaultValue_ValueType_Operator b)
            {
                OperatorCallCount++;

                return a.Value == b.Value;
            }

            public static bool operator !=(_UserDefinedEmitDefaultValue_ValueType_Operator a, _UserDefinedEmitDefaultValue_ValueType_Operator b)
            => !(a == b);

            public override bool Equals(object obj)
            {
                if (obj is _UserDefinedEmitDefaultValue_ValueType_Operator o)
                {
                    return this == o;
                }

                return false;
            }

            public override int GetHashCode()
            => Value;
        }

        private class _UserDefinedEmitDefaultValue1
        {
            public string Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public _UserDefinedEmitDefaultValue_ValueType Bar { get; set; }
        }

        private class _UserDefinedEmitDefaultValue2
        {
            public string Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public _UserDefinedEmitDefaultValue_ValueType_Equatable Bar { get; set; }
        }

        private class _UserDefinedEmitDefaultValue3
        {
            public string Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public _UserDefinedEmitDefaultValue_ValueType_Operator Bar { get; set; }
        }

        private class _UserDefinedEmitDefaultValue_TypeDescripter : DefaultTypeDescriber
        {
            public static bool Format_UserDefinedEmitDefaultValue_ValueType(_UserDefinedEmitDefaultValue_ValueType t, in WriteContext _, IBufferWriter<char> writer)
            {
                var asStr = t.Value.ToString();
                writer.Write(asStr.AsSpan());

                return true;
            }

            public static bool Format_UserDefinedEmitDefaultValue_ValueType_Equatable(_UserDefinedEmitDefaultValue_ValueType_Equatable t, in WriteContext _, IBufferWriter<char> writer)
            {
                var asStr = t.Value.ToString();
                writer.Write(asStr.AsSpan());

                return true;
            }

            public static bool Format_UserDefinedEmitDefaultValue_ValueType_Operator(_UserDefinedEmitDefaultValue_ValueType_Operator t, in WriteContext _, IBufferWriter<char> writer)
            {
                var asStr = t.Value.ToString();
                writer.Write(asStr.AsSpan());

                return true;
            }

            protected override bool ShouldDeserialize(TypeInfo forType, PropertyInfo property)
            {
                if (forType == typeof(_UserDefinedEmitDefaultValue1).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue1.Bar))
                {
                    return false;
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue2).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue2.Bar))
                {
                    return false;
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue3).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue3.Bar))
                {
                    return false;
                }

                return base.ShouldDeserialize(forType, property);
            }

            protected override Formatter GetFormatter(TypeInfo forType, PropertyInfo property)
            {
                if (forType == typeof(_UserDefinedEmitDefaultValue1).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue1.Bar))
                {
                    return (Formatter)typeof(_UserDefinedEmitDefaultValue_TypeDescripter).GetMethod(nameof(Format_UserDefinedEmitDefaultValue_ValueType), BindingFlags.Public | BindingFlags.Static);
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue2).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue2.Bar))
                {
                    return (Formatter)typeof(_UserDefinedEmitDefaultValue_TypeDescripter).GetMethod(nameof(Format_UserDefinedEmitDefaultValue_ValueType_Equatable), BindingFlags.Public | BindingFlags.Static);
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue3).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue3.Bar))
                {
                    return (Formatter)typeof(_UserDefinedEmitDefaultValue_TypeDescripter).GetMethod(nameof(Format_UserDefinedEmitDefaultValue_ValueType_Operator), BindingFlags.Public | BindingFlags.Static);
                }

                return base.GetFormatter(forType, property);
            }
        }

        [Fact]
        public void UserDefinedEmitDefaultValue()
        {
            var opts = Options.Default.NewBuilder().WithTypeDescriber(new _UserDefinedEmitDefaultValue_TypeDescripter()).Build();

            // not equatable
            RunSyncWriterVariants<_UserDefinedEmitDefaultValue1>(
                opts,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _UserDefinedEmitDefaultValue1 { Foo = "hello", Bar = default });
                        csv.Write(new _UserDefinedEmitDefaultValue1 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType { Value = 2 } });
                    }

                    var res = getStr();
                    Assert.Equal("Bar,Foo\r\n,hello\r\n2,world", res);
                }
            );

            // equatable
            RunSyncWriterVariants<_UserDefinedEmitDefaultValue2>(
                opts,
                (config, getWriter, getStr) =>
                {
                    _UserDefinedEmitDefaultValue_ValueType_Equatable.EqualsCallCount = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _UserDefinedEmitDefaultValue2 { Foo = "hello", Bar = default });
                        csv.Write(new _UserDefinedEmitDefaultValue2 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Equatable { Value = 2 } });
                    }

                    var res = getStr();
                    Assert.Equal("Bar,Foo\r\n,hello\r\n2,world", res);
                    Assert.Equal(2, _UserDefinedEmitDefaultValue_ValueType_Equatable.EqualsCallCount);
                }
            );

            // operator
            RunSyncWriterVariants<_UserDefinedEmitDefaultValue3>(
                opts,
                (config, getWriter, getStr) =>
                {
                    _UserDefinedEmitDefaultValue_ValueType_Operator.OperatorCallCount = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _UserDefinedEmitDefaultValue3 { Foo = "hello", Bar = default });
                        csv.Write(new _UserDefinedEmitDefaultValue3 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Operator { Value = 2 } });
                    }

                    var res = getStr();
                    Assert.Equal("Bar,Foo\r\n,hello\r\n2,world", res);
                    Assert.Equal(2, _UserDefinedEmitDefaultValue_ValueType_Operator.OperatorCallCount);
                }
            );
        }

        private class _Context
        {
            [DataMember(Order = 1)]
            public string Foo { get; set; }
            [DataMember(Order = 2)]
            public int Bar { get; set; }
        }

        private static List<string> _Context_FormatFoo_Records;
        public static bool _Context_FormatFoo(string data, in WriteContext ctx, IBufferWriter<char> writer)
        {
            _Context_FormatFoo_Records.Add($"{ctx.RowNumber},{ctx.Column.Name},{ctx.Column.Index},{data},{ctx.Context}");

            var span = data.AsSpan();

            while (!span.IsEmpty)
            {
                var writeTo = writer.GetSpan(span.Length);
                var len = Math.Min(span.Length, writeTo.Length);

                span.Slice(0, len).CopyTo(writeTo);
                writer.Advance(len);

                span = span.Slice(len);
            }

            return true;
        }

        private static List<string> _Context_FormatBar_Records;
        public static bool _Context_FormatBar(int data, in WriteContext ctx, IBufferWriter<char> writer)
        {
            _Context_FormatBar_Records.Add($"{ctx.RowNumber},{ctx.Column.Name},{ctx.Column.Index},{data},{ctx.Context}");

            var asStr = data.ToString();
            writer.Write(asStr.AsSpan());

            return true;
        }

        [Fact]
        public void Context()
        {
            var formatFoo = (Formatter)typeof(WriterTests).GetMethod(nameof(_Context_FormatFoo));
            var formatBar = (Formatter)typeof(WriterTests).GetMethod(nameof(_Context_FormatBar));

            var describer = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
            describer.SetBuilder((InstanceProvider)typeof(_Context).GetConstructor(Type.EmptyTypes));
            describer.AddSerializableProperty(typeof(_Context).GetProperty(nameof(_Context.Foo)), nameof(_Context.Foo), formatFoo);
            describer.AddSerializableProperty(typeof(_Context).GetProperty(nameof(_Context.Bar)), nameof(_Context.Bar), formatBar);

            var optsBase = Options.Default.NewBuilder().WithTypeDescriber(describer);

            // no headers
            {
                var opts = optsBase.WithWriteHeader(WriteHeaders.Never).Build();

                RunSyncWriterVariants<_Context>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        _Context_FormatFoo_Records = new List<string>();
                        _Context_FormatBar_Records = new List<string>();

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer, "context!"))
                        {
                            csv.Write(new _Context { Bar = 123, Foo = "whatever" });
                            csv.Write(new _Context { Bar = 456, Foo = "indeed" });
                        }

                        var res = getStr();
                        Assert.Equal("whatever,123\r\nindeed,456", res);

                        Assert.Collection(
                            _Context_FormatFoo_Records,
                            c => Assert.Equal("0,Foo,0,whatever,context!", c),
                            c => Assert.Equal("1,Foo,0,indeed,context!", c)
                        );

                        Assert.Collection(
                            _Context_FormatBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c)
                        );
                    }
                );
            }

            // with headers
            {
                var opts = optsBase.WithWriteHeader(WriteHeaders.Always).Build();

                RunSyncWriterVariants<_Context>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        _Context_FormatFoo_Records = new List<string>();
                        _Context_FormatBar_Records = new List<string>();

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer, "context!"))
                        {
                            csv.Write(new _Context { Bar = 123, Foo = "whatever" });
                            csv.Write(new _Context { Bar = 456, Foo = "indeed" });
                        }

                        var res = getStr();
                        Assert.Equal("Foo,Bar\r\nwhatever,123\r\nindeed,456", res);

                        Assert.Collection(
                            _Context_FormatFoo_Records,
                            c => Assert.Equal("0,Foo,0,whatever,context!", c),
                            c => Assert.Equal("1,Foo,0,indeed,context!", c)
                        );

                        Assert.Collection(
                            _Context_FormatBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c)
                        );
                    }
                );
            }
        }

        private class _CommentEscape
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        [Fact]
        public void CommentEscape()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturnLineFeed).WithCommentCharacter('#').WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\r\n", txt);
                    }
                );

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturn).WithCommentCharacter('#').WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\r", txt);
                    }
                );

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.LineFeed).WithCommentCharacter('#').WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\n", txt);
                    }
                );

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\n", txt);
                    }
                );
            }
        }

        private class _Simple
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
            public ulong? Nope { get; set; }
        }

        [Fact]
        public void Simple()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = getString();
                        Assert.Equal("hello,123,456\r\n,789,", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.LineFeed).Build();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = getString();
                        Assert.Equal("hello,123,456\n,789,", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturn).Build();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = getString();
                        Assert.Equal("hello,123,456\r,789,", txt);
                    }
                );
            }
        }

        [Fact]
        public void NeedEscape()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            writer.Write(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\r\n\"foo\"\"bar\",789,\r\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturn).Build();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            writer.Write(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\r\"foo\"\"bar\",789,\r\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.LineFeed).Build();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            writer.Write(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\n\"foo\"\"bar\",789,\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // large
            {
                var opts = Options.Default;
                var val = string.Join("", Enumerable.Repeat("abc\r\n", 450));

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = val, Bar = 001, Nope = 009 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Nope\r\n\"abc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\n\",1,9", txt);
                    }
                );
            }
        }

        private class _WriteAll
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
            public Guid? Fizz { get; set; }
            public DateTimeOffset Buzz { get; set; }
        }

        [Fact]
        public void WriteAll()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                RunSyncWriterVariants<_WriteAll>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
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
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).Build();

                RunSyncWriterVariants<_WriteAll>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
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
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.LineFeed).Build();

                RunSyncWriterVariants<_WriteAll>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                                }
                            );
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\nhello,456,,1980-02-02 01:01:01Z\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }

        private class _Headers
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
        }

        [Fact]
        public void Headers()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Headers { Foo = "hello", Bar = 123 });
                            writer.Write(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar\r\nhello,123\r\nfoo,789", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).Build();

                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Headers { Foo = "hello", Bar = 123 });
                            writer.Write(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar\rhello,123\rfoo,789", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.LineFeed).Build();

                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Headers { Foo = "hello", Bar = 123 });
                            writer.Write(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar\nhello,123\nfoo,789", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }
        }

        public class _EscapeHeaders
        {
            [DataMember(Name = "hello\r\nworld")]
            public string A { get; set; }

            [DataMember(Name = "foo,bar")]
            public string B { get; set; }

            [DataMember(Name = "yup")]
            public string C { get; set; }
        }

        [Fact]
        public void EscapeHeaders()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            writer.Write(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\nfizz,buzz,yes\r\nping,pong,no\r\n", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            writer.Write(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\rfizz,buzz,yes\rping,pong,no\r", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.LineFeed).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            writer.Write(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\nfizz,buzz,yes\nping,pong,no\n", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\n", txt);
                    }
                );
            }
        }

        private class _EscapeLargeHeaders
        {
            [DataMember(Name = "A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh")]
            public string A { get; set; }
            [DataMember(Name = "Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop")]
            public string B { get; set; }
            [DataMember(Name = "Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx")]
            public string C { get; set; }
            [DataMember(Name = "0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567")]
            public string D { get; set; }
            [DataMember(Name = ",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,")]
            public string E { get; set; }
            [DataMember(Name = "hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world")]
            public string F { get; set; }
            [DataMember(Name = "foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar")]
            public string G { get; set; }
            [DataMember(Name = "fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz")]
            public string H { get; set; }
        }

        [Fact]
        public void EscapeLargeHeaders()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r\na,b,c,d,e,f,g,h\r\n", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\ra,b,c,d,e,f,g,h\r", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.LineFeed).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\na,b,c,d,e,f,g,h\n", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\n", txt);
                    }
                );
            }
        }

        private class _MultiSegmentValue_TypeDescriber : DefaultTypeDescriber
        {
            protected override Formatter GetFormatter(TypeInfo forType, PropertyInfo property)
            {
                var ret = typeof(_MultiSegmentValue_TypeDescriber).GetMethod(nameof(TryFormatStringCrazy));

                return (Formatter)ret;
            }

            public static bool TryFormatStringCrazy(string val, in WriteContext ctx, IBufferWriter<char> buffer)
            {
                for (var i = 0; i < val.Length; i++)
                {
                    var charSpan = buffer.GetSpan(1);
                    charSpan[0] = val[i];
                    buffer.Advance(1);
                }

                return true;
            }
        }

        private class _MultiSegmentValue
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void MultiSegmentValue()
        {
            var opts = Options.Default.NewBuilder().WithTypeDescriber(new _MultiSegmentValue_TypeDescriber()).Build();

            // no encoding
            RunSyncWriterVariants<_MultiSegmentValue>(
                opts,
                (config, getWriter, getString) =>
                {
                    using (var writer = config.CreateWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat('c', 10_000)) };
                        writer.Write(row);
                    }

                    var txt = getString();
                    Assert.Equal("Foo\r\n" + string.Join("", Enumerable.Repeat('c', 10_000)), txt);
                }
            );

            // quoted
            RunSyncWriterVariants<_MultiSegmentValue>(
                opts,
                (config, getWriter, getString) =>
                {
                    using (var writer = config.CreateWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat("d,", 10_000)) };
                        writer.Write(row);
                    }

                    var txt = getString();
                    Assert.Equal("Foo\r\n\"" + string.Join("", Enumerable.Repeat("d,", 10_000)) + "\"", txt);
                }
            );

            // escaped
            RunSyncWriterVariants<_MultiSegmentValue>(
                opts,
                (config, getWriter, getString) =>
                {
                    using (var writer = config.CreateWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat("foo\"bar", 10_000)) };
                        writer.Write(row);
                    }

                    var txt = getString();
                    Assert.Equal("Foo\r\n\"" + string.Join("", Enumerable.Repeat("foo\"\"bar", 10_000)) + "\"", txt);
                }
            );
        }

        private class _ShouldSerialize
        {
            public static bool OnOff;

            public int Foo { get; set; }
            public string Bar { get; set; }

            public bool ShouldSerializeFoo()
            => Foo % 2 == 0;

            public static bool ShouldSerializeBar()
            => OnOff;

            public static void Reset()
            {
                OnOff = default;
            }
        }

        [Fact]
        public void ShouldSerialize()
        {
            _ShouldSerialize.Reset();

            var opts = Options.Default;

            RunSyncWriterVariants<_ShouldSerialize>(
                opts,
                (config, getWriter, getString) =>
                {
                    _ShouldSerialize.Reset();

                    using (var csv = config.CreateWriter(getWriter()))
                    {
                        csv.Write(new _ShouldSerialize { Foo = 1, Bar = "hello" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        csv.Write(new _ShouldSerialize { Foo = 3, Bar = "world" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        csv.Write(new _ShouldSerialize { Foo = 4, Bar = "fizz" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        csv.Write(new _ShouldSerialize { Foo = 9, Bar = "buzz" });
                        _ShouldSerialize.OnOff = true;
                        csv.Write(new _ShouldSerialize { Foo = 10, Bar = "bonzai" });
                    }

                    var txt = getString();
                    Assert.Equal("Foo,Bar\r\n,\r\n,world\r\n4,\r\n,buzz\r\n10,bonzai", txt);
                }
            );
        }

        private class _StaticGetters
        {
            private int Foo;

            public _StaticGetters() { }

            public _StaticGetters(int f) : this()
            {
                Foo = f;
            }

            public static int GetBar() => 2;

            public static int GetFizz(_StaticGetters sg) => sg.Foo + GetBar();
        }

        [Fact]
        public void StaticGetters()
        {
            var m = new ManualTypeDescriber();
            m.SetBuilder((InstanceProvider)typeof(_StaticGetters).GetConstructor(Type.EmptyTypes));
            m.AddExplicitGetter(typeof(_StaticGetters).GetTypeInfo(), "Bar", (Getter)typeof(_StaticGetters).GetMethod("GetBar", BindingFlags.Static | BindingFlags.Public));
            m.AddExplicitGetter(typeof(_StaticGetters).GetTypeInfo(), "Fizz", (Getter)typeof(_StaticGetters).GetMethod("GetFizz", BindingFlags.Static | BindingFlags.Public));

            var opts = Options.Default.NewBuilder().WithTypeDescriber(m).Build();

            RunSyncWriterVariants<_StaticGetters>(
                opts,
                (config, getWriter, getStr) =>
                {
                    using (var csv = config.CreateWriter(getWriter()))
                    {
                        csv.Write(new _StaticGetters(1));
                        csv.Write(new _StaticGetters(2));
                        csv.Write(new _StaticGetters(3));
                    }

                    var str = getStr();
                    Assert.Equal("Bar,Fizz\r\n2,3\r\n2,4\r\n2,5", str);
                }
            );
        }

        private class _EmitDefaultValue
        {
            public enum E
            {
                None = 0,
                Fizz,
                Buzz
            }

            [DataMember(EmitDefaultValue = false)]
            public int Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public E Bar { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public E? Hello { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public DateTime World { get; set; }
        }

        [Fact]
        public void EmitDefaultValue()
        {
            var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).Build();

            RunSyncWriterVariants<_EmitDefaultValue>(
                opts,
                (config, getWriter, getString) =>
                {
                    using (var writer = config.CreateWriter(getWriter()))
                    {
                        var rows =
                            new[]
                            {
                            new _EmitDefaultValue { Foo = 1, Bar = _EmitDefaultValue.E.None, Hello = _EmitDefaultValue.E.None, World = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)},
                            new _EmitDefaultValue { Foo = 0, Bar = _EmitDefaultValue.E.Fizz, Hello = null, World = default},
                            };

                        writer.WriteAll(rows);
                    }

                    var txt = getString();
                    Assert.Equal("1,,None,1970-01-01 00:00:00Z\r\n,Fizz,,", txt);
                }
            );
        }

        [Fact]
        public async Task ErrorsAsync()
        {
            await RunAsyncWriterVariants<_Errors>(
                Options.Default,
                async (config, makeWriter, getStr) =>
                {
                    await using (var w = makeWriter())
                    await using (var csv = config.CreateAsyncWriter(w))
                    {
                        await Assert.ThrowsAsync<ArgumentNullException>(async () => await csv.WriteAllAsync(default(IEnumerable<_Errors>)));
                        await Assert.ThrowsAsync<ArgumentNullException>(async () => await csv.WriteAllAsync(default(IAsyncEnumerable<_Errors>)));

                        var exc = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteCommentAsync("foo"));
                        Assert.Equal($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line", exc.Message);
                    }

                    await getStr();
                }
            );
        }

        private sealed class _PipeWriterAsync
        {
            public string Fizz { get; set; }
            public int Buzz { get; set; }
        }

        [Fact]
        public async Task PipeWriterAsync()
        {
            var pipe = new Pipe();

            var config = Configuration.For<_PipeWriterAsync>();
            await using(var csv = config.CreateAsyncWriter(pipe.Writer, Encoding.UTF7))
            {
                await csv.WriteAsync(new _PipeWriterAsync { Fizz = "hello", Buzz = 12345 });
            }

            pipe.Writer.Complete();

            var bytes = new List<byte>();
            while (true)
            {
                var res = await pipe.Reader.ReadAsync();
                foreach(var seg in res.Buffer)
                {
                    bytes.AddRange(seg.ToArray());
                }

                if(res.IsCompleted || res.IsCanceled)
                {
                    break;
                }
            }

            var str = Encoding.UTF7.GetString(bytes.ToArray());

            Assert.Equal("Fizz,Buzz\r\nhello,12345", str);
        }

        [Fact]
        public async Task FailingGetterAsync()
        {
            var m = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
            var t = typeof(_FailingGetter).GetTypeInfo();
            var g = Getter.ForMethod(t.GetProperty(nameof(_FailingGetter.Foo)).GetMethod);
            var f = Formatter.ForDelegate((int value, in WriteContext context, IBufferWriter<char> buffer) => false);

            m.SetBuilder(InstanceProvider.ForDelegate((out _FailingGetter val) => { val = new _FailingGetter(); return true; }));
            m.AddExplicitGetter(t, "bar", g, f);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(m).Build();

            await RunAsyncWriterVariants<_FailingGetter>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var w = getWriter())
                    await using (var csv = config.CreateAsyncWriter(w))
                    {
                        await Assert.ThrowsAsync<SerializationException>(async () => await csv.WriteAsync(new _FailingGetter()));
                    }

                    var res = await getStr();
                    Assert.Equal("bar\r\n", res);
                }
            );
        }

        [Fact]
        public async Task LotsOfCommentsAsync()
        {
            var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

            await RunAsyncWriterVariants<_LotsOfComments>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    var cs = string.Join("\r\n", Enumerable.Repeat("foo", 1_000));

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteCommentAsync(cs);
                    }

                    var str = await getStr();
                    var expected = nameof(_LotsOfComments.Hello) + "\r\n" + string.Join("\r\n", Enumerable.Repeat("#foo", 1_000));
                    Assert.Equal(expected, str);
                }
            );
        }

        [Fact]
        public async Task NullCommentErrorAsync()
        {
            await RunAsyncWriterVariants<_NullCommentError>(
                Options.Default,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await Assert.ThrowsAsync<ArgumentNullException>(async () => await csv.WriteCommentAsync(null));
                    }

                    var _ = await getStr();
                    Assert.NotNull(_);
                }
            );
        }

        [Fact]
        public async Task WriteCommentAsync()
        {
            // no trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
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

                // first line, headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }

                // before row, headers
                {
                    var opts = Options.Default.NewBuilder().WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }
            }

            // trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
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

                // first line, headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Never).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }

                // before row, headers
                {
                    var opts = Options.Default.NewBuilder().WithWriteTrailingNewLine(WriteTrailingNewLines.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeaders.Always).Build();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task DelegateStaticShouldSerializeAsync()
        {
            var shouldSerializeCalled = 0;
            StaticShouldSerializeDelegate shouldSerializeDel =
                () =>
                {
                    shouldSerializeCalled++;

                    return false;
                };

            var name = nameof(_DelegateShouldSerialize.Foo);
            var getter = (Getter)typeof(_DelegateShouldSerialize).GetProperty(nameof(_DelegateShouldSerialize.Foo)).GetMethod;
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            var shouldSerialize = Cesil.ShouldSerialize.ForDelegate(shouldSerializeDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateShouldSerialize).GetTypeInfo(), name, getter, formatter, shouldSerialize);
            InstanceProviderDelegate<_DelegateShouldSerialize> del = (out _DelegateShouldSerialize i) => { i = new _DelegateShouldSerialize(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            await RunAsyncWriterVariants<_DelegateShouldSerialize>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    shouldSerializeCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 123 });
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 0 });
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n\r\n\r\n", res);

                    Assert.Equal(3, shouldSerializeCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateShouldSerializeAsync()
        {
            var shouldSerializeCalled = 0;
            ShouldSerializeDelegate<_DelegateShouldSerialize> shouldSerializeDel =
                row =>
                {
                    shouldSerializeCalled++;

                    return row.Foo % 2 != 0;
                };

            var name = nameof(_DelegateShouldSerialize.Foo);
            var getter = (Getter)typeof(_DelegateShouldSerialize).GetProperty(nameof(_DelegateShouldSerialize.Foo)).GetMethod;
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            var shouldSerialize = Cesil.ShouldSerialize.ForDelegate(shouldSerializeDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateShouldSerialize).GetTypeInfo(), name, getter, formatter, shouldSerialize);
            InstanceProviderDelegate<_DelegateShouldSerialize> del = (out _DelegateShouldSerialize i) => { i = new _DelegateShouldSerialize(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            await RunAsyncWriterVariants<_DelegateShouldSerialize>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    shouldSerializeCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 123 });
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 0 });
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n123\r\n\r\n", res);

                    Assert.Equal(3, shouldSerializeCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateFormatterAsync()
        {
            var formatterCalled = 0;
            FormatterDelegate<int> formatDel =
                (int val, in WriteContext _, IBufferWriter<char> buffer) =>
                {
                    formatterCalled++;

                    var s = val.ToString();

                    var span = s.AsSpan();
                    while (!span.IsEmpty)
                    {
                        var writeTo = buffer.GetSpan(span.Length);
                        var len = Math.Min(span.Length, writeTo.Length);

                        var toWrite = span.Slice(0, len);
                        toWrite.CopyTo(writeTo);
                        buffer.Advance(len);

                        span = span.Slice(len);
                    }

                    span = s.AsSpan();
                    while (!span.IsEmpty)
                    {
                        var writeTo = buffer.GetSpan(span.Length);
                        var len = Math.Min(span.Length, writeTo.Length);

                        var toWrite = span.Slice(0, len);
                        toWrite.CopyTo(writeTo);
                        buffer.Advance(len);

                        span = span.Slice(len);
                    }

                    return true;
                };

            var name = nameof(_DelegateFormatter.Foo);
            var getter = (Getter)typeof(_DelegateFormatter).GetProperty(nameof(_DelegateFormatter.Foo)).GetMethod;
            var formatter = Formatter.ForDelegate(formatDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateFormatter).GetTypeInfo(), name, getter, formatter);
            InstanceProviderDelegate<_DelegateFormatter> del = (out _DelegateFormatter i) => { i = new _DelegateFormatter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            await RunAsyncWriterVariants<_DelegateFormatter>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    formatterCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateFormatter { Foo = 123 });
                        await csv.WriteAsync(new _DelegateFormatter { Foo = 0 });
                        await csv.WriteAsync(new _DelegateFormatter { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n123123\r\n00\r\n456456", res);

                    Assert.Equal(3, formatterCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateStaticGetterAsync()
        {
            var getterCalled = 0;
            StaticGetterDelegate<int> getDel =
                () =>
                {
                    getterCalled++;

                    return getterCalled;
                };

            var name = nameof(_DelegateGetter.Foo);
            var getter = Getter.ForDelegate(getDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateGetter).GetTypeInfo(), name, getter);
            InstanceProviderDelegate<_DelegateGetter> del = (out _DelegateGetter i) => { i = new _DelegateGetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            await RunAsyncWriterVariants<_DelegateGetter>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    getterCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateGetter { Foo = 123 });
                        await csv.WriteAsync(new _DelegateGetter { Foo = 0 });
                        await csv.WriteAsync(new _DelegateGetter { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n1\r\n2\r\n3", res);

                    Assert.Equal(3, getterCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateGetterAsync()
        {
            var getterCalled = 0;
            GetterDelegate<_DelegateGetter, int> getDel =
                (_DelegateGetter row) =>
                {
                    getterCalled++;

                    return row.Foo * 2;
                };

            var name = nameof(_DelegateGetter.Foo);
            var getter = Getter.ForDelegate(getDel);

            var describer = new ManualTypeDescriber();
            describer.AddExplicitGetter(typeof(_DelegateGetter).GetTypeInfo(), name, getter);
            InstanceProviderDelegate<_DelegateGetter> del = (out _DelegateGetter i) => { i = new _DelegateGetter(); return true; };
            describer.SetBuilder((InstanceProvider)del);

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).WithWriteHeader(WriteHeaders.Always).Build();

            await RunAsyncWriterVariants<_DelegateGetter>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    getterCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateGetter { Foo = 123 });
                        await csv.WriteAsync(new _DelegateGetter { Foo = 0 });
                        await csv.WriteAsync(new _DelegateGetter { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n246\r\n0\r\n912", res);

                    Assert.Equal(3, getterCalled);
                }
            );
        }

        [Fact]
        public async Task UserDefinedEmitDefaultValueAsync()
        {
            var opts = Options.Default.NewBuilder().WithTypeDescriber(new _UserDefinedEmitDefaultValue_TypeDescripter()).Build();

            // not equatable
            await RunAsyncWriterVariants<_UserDefinedEmitDefaultValue1>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue1 { Foo = "hello", Bar = default });
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue1 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType { Value = 2 } });
                    }

                    var res = await getStr();
                    Assert.Equal("Bar,Foo\r\n,hello\r\n2,world", res);
                }
            );

            // equatable
            await RunAsyncWriterVariants<_UserDefinedEmitDefaultValue2>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    _UserDefinedEmitDefaultValue_ValueType_Equatable.EqualsCallCount = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue2 { Foo = "hello", Bar = default });
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue2 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Equatable { Value = 2 } });
                    }

                    var res = await getStr();
                    Assert.Equal("Bar,Foo\r\n,hello\r\n2,world", res);
                    Assert.Equal(2, _UserDefinedEmitDefaultValue_ValueType_Equatable.EqualsCallCount);
                }
            );

            // operator
            await RunAsyncWriterVariants<_UserDefinedEmitDefaultValue3>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    _UserDefinedEmitDefaultValue_ValueType_Operator.OperatorCallCount = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue3 { Foo = "hello", Bar = default });
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue3 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Operator { Value = 2 } });
                    }

                    var res = await getStr();
                    Assert.Equal("Bar,Foo\r\n,hello\r\n2,world", res);
                    Assert.Equal(2, _UserDefinedEmitDefaultValue_ValueType_Operator.OperatorCallCount);
                }
            );
        }

        [Fact]
        public async Task ContextAsync()
        {
            var formatFoo = (Formatter)typeof(WriterTests).GetMethod(nameof(_Context_FormatFoo));
            var formatBar = (Formatter)typeof(WriterTests).GetMethod(nameof(_Context_FormatBar));

            var describer = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
            describer.SetBuilder((InstanceProvider)typeof(_Context).GetConstructor(Type.EmptyTypes));
            describer.AddSerializableProperty(typeof(_Context).GetProperty(nameof(_Context.Foo)), nameof(_Context.Foo), formatFoo);
            describer.AddSerializableProperty(typeof(_Context).GetProperty(nameof(_Context.Bar)), nameof(_Context.Bar), formatBar);

            var optsBase = Options.Default.NewBuilder().WithTypeDescriber(describer);

            // no headers
            {
                var opts = optsBase.WithWriteHeader(WriteHeaders.Never).Build();

                await RunAsyncWriterVariants<_Context>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        _Context_FormatFoo_Records = new List<string>();
                        _Context_FormatBar_Records = new List<string>();

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer, "context!"))
                        {
                            await csv.WriteAsync(new _Context { Bar = 123, Foo = "whatever" });
                            await csv.WriteAsync(new _Context { Bar = 456, Foo = "indeed" });
                        }

                        var res = await getStr();
                        Assert.Equal("whatever,123\r\nindeed,456", res);

                        Assert.Collection(
                            _Context_FormatFoo_Records,
                            c => Assert.Equal("0,Foo,0,whatever,context!", c),
                            c => Assert.Equal("1,Foo,0,indeed,context!", c)
                        );

                        Assert.Collection(
                            _Context_FormatBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c)
                        );
                    }
                );
            }

            // with headers
            {
                var opts = optsBase.WithWriteHeader(WriteHeaders.Always).Build();

                await RunAsyncWriterVariants<_Context>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        _Context_FormatFoo_Records = new List<string>();
                        _Context_FormatBar_Records = new List<string>();

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer, "context!"))
                        {
                            await csv.WriteAsync(new _Context { Bar = 123, Foo = "whatever" });
                            await csv.WriteAsync(new _Context { Bar = 456, Foo = "indeed" });
                        }

                        var res = await getStr();
                        Assert.Equal("Foo,Bar\r\nwhatever,123\r\nindeed,456", res);

                        Assert.Collection(
                            _Context_FormatFoo_Records,
                            c => Assert.Equal("0,Foo,0,whatever,context!", c),
                            c => Assert.Equal("1,Foo,0,indeed,context!", c)
                        );

                        Assert.Collection(
                            _Context_FormatBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c)
                        );
                    }
                );
            }
        }

        [Fact]
        public async Task CommentEscapeAsync()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturnLineFeed).WithCommentCharacter('#').WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = await getString();
                        Assert.Equal("\"#hello\",foo\r\n", txt);
                    }
                );

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                     async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = await getString();
                        Assert.Equal("hello,\"fo#o\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturn).WithCommentCharacter('#').WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = await getString();
                        Assert.Equal("\"#hello\",foo\r", txt);
                    }
                );

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = await getString();
                        Assert.Equal("hello,\"fo#o\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.LineFeed).WithCommentCharacter('#').WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                     async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = await getString();
                        Assert.Equal("\"#hello\",foo\n", txt);
                    }
                );

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "hello", B = "fo#o" });
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
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            await writer.WriteAsync(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\nfizz,buzz,yes\r\nping,pong,no\r\n", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            await writer.WriteAsync(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\rfizz,buzz,yes\rping,pong,no\r", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.LineFeed).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            await writer.WriteAsync(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\nfizz,buzz,yes\nping,pong,no\n", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\n", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task HeadersAsync()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Headers { Foo = "hello", Bar = 123 });
                            await writer.WriteAsync(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar\r\nhello,123\r\nfoo,789", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).Build();

                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Headers { Foo = "hello", Bar = 123 });
                            await writer.WriteAsync(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar\rhello,123\rfoo,789", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.LineFeed).Build();

                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Headers { Foo = "hello", Bar = 123 });
                            await writer.WriteAsync(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar\nhello,123\nfoo,789", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task MultiSegmentValueAsync()
        {
            var opts = Options.Default.NewBuilder().WithTypeDescriber(new _MultiSegmentValue_TypeDescriber()).Build();

            // no encoding
            await RunAsyncWriterVariants<_MultiSegmentValue>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = config.CreateAsyncWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat('c', 5_000)) };
                        await writer.WriteAsync(row);
                    }

                    var txt = await getStr();
                    Assert.Equal("Foo\r\n" + string.Join("", Enumerable.Repeat('c', 5_000)), txt);
                }
            );

            // quoted
            await RunAsyncWriterVariants<_MultiSegmentValue>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = config.CreateAsyncWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat("d,", 5_000)) };
                        await writer.WriteAsync(row);
                    }

                    var txt = await getStr();
                    Assert.Equal("Foo\r\n\"" + string.Join("", Enumerable.Repeat("d,", 5_000)) + "\"", txt);
                }
            );

            // escaped
            await RunAsyncWriterVariants<_MultiSegmentValue>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = config.CreateAsyncWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat("foo\"bar", 1_000)) };
                        await writer.WriteAsync(row);
                    }

                    var txt = await getStr();
                    Assert.Equal("Foo\r\n\"" + string.Join("", Enumerable.Repeat("foo\"\"bar", 1_000)) + "\"", txt);
                }
            );
        }

        [Fact]
        public async Task NeedEscapeAsync()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            await writer.WriteAsync(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello,world\",123,456\r\n\"foo\"\"bar\",789,\r\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturn).Build();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            await writer.WriteAsync(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello,world\",123,456\r\"foo\"\"bar\",789,\r\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.LineFeed).Build();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            await writer.WriteAsync(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello,world\",123,456\n\"foo\"\"bar\",789,\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // large
            {
                var opts = Options.Default;
                var val = string.Join("", Enumerable.Repeat("abc\r\n", 450));

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = val, Bar = 001, Nope = 009 });
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Nope\r\n\"abc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\n\",1,9", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task ShouldSerializeAsync()
        {
            var opts = Options.Default;

            await RunAsyncWriterVariants<_ShouldSerialize>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    _ShouldSerialize.Reset();

                    await using (var csv = config.CreateAsyncWriter(getWriter()))
                    {
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 1, Bar = "hello" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 3, Bar = "world" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 4, Bar = "fizz" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 9, Bar = "buzz" });
                        _ShouldSerialize.OnOff = true;
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 10, Bar = "bonzai" });
                    }

                    var txt = await getStr();
                    Assert.Equal("Foo,Bar\r\n,\r\n,world\r\n4,\r\n,buzz\r\n10,bonzai", txt);
                }
            );
        }

        [Fact]
        public async Task StaticGettersAsync()
        {
            var m = new ManualTypeDescriber();
            m.SetBuilder((InstanceProvider)typeof(_StaticGetters).GetConstructor(Type.EmptyTypes));
            m.AddExplicitGetter(typeof(_StaticGetters).GetTypeInfo(), "Bar", (Getter)typeof(_StaticGetters).GetMethod("GetBar", BindingFlags.Static | BindingFlags.Public));
            m.AddExplicitGetter(typeof(_StaticGetters).GetTypeInfo(), "Fizz", (Getter)typeof(_StaticGetters).GetMethod("GetFizz", BindingFlags.Static | BindingFlags.Public));

            var opts = Options.Default.NewBuilder().WithTypeDescriber(m).Build();

            await RunAsyncWriterVariants<_StaticGetters>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var csv = config.CreateAsyncWriter(getWriter()))
                    {
                        await csv.WriteAsync(new _StaticGetters(1));
                        await csv.WriteAsync(new _StaticGetters(2));
                        await csv.WriteAsync(new _StaticGetters(3));
                    }

                    var str = await getStr();
                    Assert.Equal("Bar,Fizz\r\n2,3\r\n2,4\r\n2,5", str);
                }
            );
        }

        [Fact]
        public async Task SimpleAsync()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = await getStr();
                        Assert.Equal("hello,123,456\r\n,789,", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.LineFeed).Build();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = await getStr();
                        Assert.Equal("hello,123,456\n,789,", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).WithRowEnding(RowEndings.CarriageReturn).Build();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = await getStr();
                        Assert.Equal("hello,123,456\r,789,", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task WriteAllAsync()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new[]
                                {
                                new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                                }
                            );
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).Build();

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                                }
                            );
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\rhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\rhello,456,,1980-02-02 01:01:01Z\r,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.LineFeed).Build();

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                                }
                            );
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\nhello,456,,1980-02-02 01:01:01Z\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task WriteAllAsync_Enumerable()
        {
            var rows =
                new[]
                {
                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                };

            // enumerable is sync
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(rows);
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // enumerable is async
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).Build();
                var enumerable = new TestAsyncEnumerable<_WriteAll>(rows, true);

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(enumerable);
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task EmitDefaultValueAsync()
        {
            var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Never).Build();

            await RunAsyncWriterVariants<_EmitDefaultValue>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = config.CreateAsyncWriter(getWriter()))
                    {
                        var rows =
                            new[]
                            {
                                new _EmitDefaultValue { Foo = 1, Bar = _EmitDefaultValue.E.None, Hello = _EmitDefaultValue.E.None, World = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)},
                                new _EmitDefaultValue { Foo = 0, Bar = _EmitDefaultValue.E.Fizz, Hello = null, World = default},
                            };

                        await writer.WriteAllAsync(rows);
                    }

                    var txt = await getStr();
                    Assert.Equal("1,,None,1970-01-01 00:00:00Z\r\n,Fizz,,", txt);
                }
            );
        }

        [Fact]
        public async Task EscapeLargeHeadersAsync()
        {
            // \r\n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r\na,b,c,d,e,f,g,h\r\n", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\ra,b,c,d,e,f,g,h\r", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.LineFeed).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\na,b,c,d,e,f,g,h\n", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\n", txt);
                    }
                );
            }
        }
    }
#pragma warning restore IDE1006
}