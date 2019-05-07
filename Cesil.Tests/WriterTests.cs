using System;
using System.Buffers;
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
    public class WriterTests
    {
        struct _UserDefinedEmitDefaultValue_ValueType
        {
            public int Value { get; set; }
        }

        struct _UserDefinedEmitDefaultValue_ValueType_Equatable: IEquatable<_UserDefinedEmitDefaultValue_ValueType_Equatable>
        {
            public static int EqualsCallCount = 0;

            public int Value { get; set; }

            public bool Equals(_UserDefinedEmitDefaultValue_ValueType_Equatable other)
            {
                EqualsCallCount++;

                return Value == other.Value;
            }
        }

        struct _UserDefinedEmitDefaultValue_ValueType_Operator
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
                if(obj is _UserDefinedEmitDefaultValue_ValueType_Operator o)
                {
                    return this == o;
                }

                return false;
            }

            public override int GetHashCode()
            => Value;
        }

        class _UserDefinedEmitDefaultValue1
        {
            public string Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public _UserDefinedEmitDefaultValue_ValueType Bar { get; set; }
        }

        class _UserDefinedEmitDefaultValue2
        {
            public string Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public _UserDefinedEmitDefaultValue_ValueType_Equatable Bar { get; set; }
        }

        class _UserDefinedEmitDefaultValue3
        {
            public string Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public _UserDefinedEmitDefaultValue_ValueType_Operator Bar { get; set; }
        }

        class _UserDefinedEmitDefaultValue_TypeDescripter: DefaultTypeDescriber
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

            protected override MethodInfo GetFormatter(TypeInfo forType, PropertyInfo property)
            {
                if(forType == typeof(_UserDefinedEmitDefaultValue1).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue1.Bar))
                {
                    return typeof(_UserDefinedEmitDefaultValue_TypeDescripter).GetMethod(nameof(Format_UserDefinedEmitDefaultValue_ValueType), BindingFlags.Public | BindingFlags.Static);
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue2).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue2.Bar))
                {
                    return typeof(_UserDefinedEmitDefaultValue_TypeDescripter).GetMethod(nameof(Format_UserDefinedEmitDefaultValue_ValueType_Equatable), BindingFlags.Public | BindingFlags.Static);
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue3).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue3.Bar))
                {
                    return typeof(_UserDefinedEmitDefaultValue_TypeDescripter).GetMethod(nameof(Format_UserDefinedEmitDefaultValue_ValueType_Operator), BindingFlags.Public | BindingFlags.Static);
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

        class _Context
        {
            [DataMember(Order = 1)]
            public string Foo { get; set; }
            [DataMember(Order = 2)]
            public int Bar { get; set; }
        }

        private static List<string> _Context_FormatFoo_Records;
        public static bool _Context_FormatFoo(string data, in WriteContext ctx, IBufferWriter<char> writer)
        {
            _Context_FormatFoo_Records.Add($"{ctx.RowNumber},{ctx.ColumnName},{ctx.ColumnNumber},{data},{ctx.Context}");

            writer.Write(data.AsSpan());

            return true;
        }

        private static List<string> _Context_FormatBar_Records;
        public static bool _Context_FormatBar(int data, in WriteContext ctx, IBufferWriter<char> writer)
        {
            _Context_FormatBar_Records.Add($"{ctx.RowNumber},{ctx.ColumnName},{ctx.ColumnNumber},{data},{ctx.Context}");

            var asStr = data.ToString();
            writer.Write(asStr.AsSpan());

            return true;
        }

        [Fact]
        public void Context()
        {
            var formatFoo = typeof(WriterTests).GetMethod(nameof(_Context_FormatFoo));
            var formatBar = typeof(WriterTests).GetMethod(nameof(_Context_FormatBar));

            var describer = new ManualTypeDescriber(false);
            describer.SetExplicitParameterlessConstructor(typeof(_Context).GetConstructor(Type.EmptyTypes));
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

        class _CommentEscape
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

        class _Simple
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

        class _WriteAll
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

        class _Headers
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
                var opts =Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturnLineFeed).WithWriteTrailingNewLine(WriteTrailingNewLines.Always).Build();

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

        class _EscapeLargeHeaders
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

        class _MultiSegmentValue_TypeDescriber : DefaultTypeDescriber
        {
            protected override MethodInfo GetFormatter(TypeInfo forType, PropertyInfo property)
            {
                var ret = typeof(_MultiSegmentValue_TypeDescriber).GetMethod(nameof(TryFormatStringCrazy));

                return ret;
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

        class _MultiSegmentValue
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

        class _ShouldSerialize
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

        class _StaticGetters
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
            m.SetExplicitParameterlessConstructor(typeof(_StaticGetters).GetConstructor(Type.EmptyTypes));
            m.AddExplicitGetter(typeof(_StaticGetters).GetTypeInfo(), "Bar", typeof(_StaticGetters).GetMethod("GetBar", BindingFlags.Static | BindingFlags.Public));
            m.AddExplicitGetter(typeof(_StaticGetters).GetTypeInfo(), "Fizz", typeof(_StaticGetters).GetMethod("GetFizz", BindingFlags.Static | BindingFlags.Public));

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

        class _EmitDefaultValue
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
        public async Task UserDefinedEmitDefaultValueAsync()
        {
            var opts = Options.Default.NewBuilder().WithTypeDescriber(new _UserDefinedEmitDefaultValue_TypeDescripter()).Build();

            // not equatable
            await RunAsyncWriterVariants<_UserDefinedEmitDefaultValue1>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue1 { Foo = "hello", Bar = default });
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue1 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType { Value = 2 } });
                    }

                    var res = getStr();
                    Assert.Equal("Bar,Foo\r\n,hello\r\n2,world", res);
                }
            );

            // equatable
            await RunAsyncWriterVariants<_UserDefinedEmitDefaultValue2>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    _UserDefinedEmitDefaultValue_ValueType_Equatable.EqualsCallCount = 0;

                    using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue2 { Foo = "hello", Bar = default });
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue2 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Equatable { Value = 2 } });
                    }

                    var res = getStr();
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

                    using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue3 { Foo = "hello", Bar = default });
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue3 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Operator { Value = 2 } });
                    }

                    var res = getStr();
                    Assert.Equal("Bar,Foo\r\n,hello\r\n2,world", res);
                    Assert.Equal(2, _UserDefinedEmitDefaultValue_ValueType_Operator.OperatorCallCount);
                }
            );
        }

        [Fact]
        public async Task ContextAsync()
        {
            var formatFoo = typeof(WriterTests).GetMethod(nameof(_Context_FormatFoo));
            var formatBar = typeof(WriterTests).GetMethod(nameof(_Context_FormatBar));

            var describer = new ManualTypeDescriber(false);
            describer.SetExplicitParameterlessConstructor(typeof(_Context).GetConstructor(Type.EmptyTypes));
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

                        using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer, "context!"))
                        {
                            await csv.WriteAsync(new _Context { Bar = 123, Foo = "whatever" });
                            await csv.WriteAsync(new _Context { Bar = 456, Foo = "indeed" });
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

                await RunAsyncWriterVariants<_Context>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        _Context_FormatFoo_Records = new List<string>();
                        _Context_FormatBar_Records = new List<string>();

                        using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer, "context!"))
                        {
                            await csv.WriteAsync(new _Context { Bar = 123, Foo = "whatever" });
                            await csv.WriteAsync(new _Context { Bar = 456, Foo = "indeed" });
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

                        var txt = getString();
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

                        var txt = getString();
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

                        var txt = getString();
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

                        var txt = getString();
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
                        await using(var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\n", txt);
                    }
                );

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using(var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = getString();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.Default.NewBuilder().WithWriteHeader(WriteHeaders.Always).WithRowEnding(RowEndings.CarriageReturn).Build();

                await RunAsyncWriterVariants< _Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Headers { Foo = "hello", Bar = 123 });
                            await writer.WriteAsync(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = getStr();
                        Assert.Equal("Foo,Bar\rhello,123\rfoo,789", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants< _Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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
            await RunAsyncWriterVariants< _MultiSegmentValue>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = config.CreateAsyncWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat('c', 5_000)) };
                        await writer.WriteAsync(row);
                    }

                    var txt = getStr();
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

                    var txt = getStr();
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

                    var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                    var txt = getStr();
                    Assert.Equal("Foo,Bar\r\n,\r\n,world\r\n4,\r\n,buzz\r\n10,bonzai", txt);
                }
            );
        }

        [Fact]
        public async Task StaticGettersAsync()
        {
            var m = new ManualTypeDescriber();
            m.SetExplicitParameterlessConstructor(typeof(_StaticGetters).GetConstructor(Type.EmptyTypes));
            m.AddExplicitGetter(typeof(_StaticGetters).GetTypeInfo(), "Bar", typeof(_StaticGetters).GetMethod("GetBar", BindingFlags.Static | BindingFlags.Public));
            m.AddExplicitGetter(typeof(_StaticGetters).GetTypeInfo(), "Fizz", typeof(_StaticGetters).GetMethod("GetFizz", BindingFlags.Static | BindingFlags.Public));

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

                    var str = getStr();
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

                await RunAsyncWriterVariants< _Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                await RunAsyncWriterVariants< _WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(rows);
                        }

                        var txt = getStr();
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

                        var txt = getStr();
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

                    var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
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

                        var txt = getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\n", txt);
                    }
                );
            }
        }
    }
#pragma warning restore IDE1006
}