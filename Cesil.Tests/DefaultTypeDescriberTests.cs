using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class DefaultTypeDescriberTests
    {
        [Fact]
        public void GetCellsForDynamicRowFormatterCaching()
        {
            var span = new DynamicCellValue[15];

            // DynamicRow
            {
                var td = new DefaultTypeDescriber();
                var row = GetDynamicRow();

                var count = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(3, count);

                row.Dispose();
            }

            // DynamicRowRange
            {
                var td = new DefaultTypeDescriber();
                var row = GetDynamicRow();
                var range = (((dynamic)row)[1..]) as object;

                var count = td.GetCellsForDynamicRow(default, range, span);
                Assert.Equal(2, count);

                row.Dispose();
            }

            // ExpandoObject
            {
                var td = new DefaultTypeDescriber();

                var row = new System.Dynamic.ExpandoObject();
                ((IDictionary<string, dynamic>)row)["A"] = 1;
                ((IDictionary<string, dynamic>)row)["B"] = 2;
                ((IDictionary<string, dynamic>)row)["C"] = 3;

                var count = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(3, count);
            }

            // other
            {
                var td = new DefaultTypeDescriber();

                var row = new FakeExpandoObject();
                ((IDictionary<string, dynamic>)row)["A"] = 1;
                ((IDictionary<string, dynamic>)row)["B"] = 2;
                ((IDictionary<string, dynamic>)row)["C"] = 3;

                var count = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(3, count);
            }

            static DynamicRow GetDynamicRow()
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();

                return CesilUtils.EnumerateDynamicFromString("A,B,C\r\n1,2,3", opts).Single() as DynamicRow;
            }
        }

        private sealed class _DisabledCachingDueToSubClassing : DefaultTypeDescriber
        {
            public bool Include { get; set; }

            protected override bool ShouldIncludeCell(string name, in WriteContext context, dynamic row)
            => Include;
        }

        [Fact]
        public void DisabledCachingDueToSubClassing()
        {
            var span = new DynamicCellValue[15];

            // GetCellsForDynamicRow, DynamicRow
            {
                var td = new _DisabledCachingDueToSubClassing();

                var row = GetDynamicRow();

                td.Include = true;
                var count1 = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(3, count1);

                td.Include = false;
                var count2 = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(0, count2);

                row.Dispose();
            }

            // GetCellsForDynamicRow, DynamicRowRange
            {
                var td = new _DisabledCachingDueToSubClassing();

                var row = GetDynamicRow();
                var range = (((dynamic)row)[1..]) as object;

                td.Include = true;
                var count1 = td.GetCellsForDynamicRow(default, range, span);
                Assert.Equal(2, count1);

                td.Include = false;
                var count2 = td.GetCellsForDynamicRow(default, range, span);
                Assert.Equal(0, count2);

                row.Dispose();
            }

            // GetCellsForDynamicRow, ExpandoObject
            {
                var td = new _DisabledCachingDueToSubClassing();

                var row = new System.Dynamic.ExpandoObject();
                ((IDictionary<string, dynamic>)row)["A"] = 1;
                ((IDictionary<string, dynamic>)row)["B"] = 2;
                ((IDictionary<string, dynamic>)row)["C"] = 3;

                td.Include = true;
                var count1 = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(3, count1);

                td.Include = false;
                var count2 = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(0, count2);
            }

            // GetCellsForDynamicRow, other dynamic
            {
                var td = new _DisabledCachingDueToSubClassing();

                var row = new FakeExpandoObject();
                ((IDictionary<string, dynamic>)row)["A"] = 1;
                ((IDictionary<string, dynamic>)row)["B"] = 2;
                ((IDictionary<string, dynamic>)row)["C"] = 3;

                td.Include = true;
                var count1 = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(3, count1);

                td.Include = false;
                var count2 = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(0, count2);
            }

            // GetCellsForDynamicRow, plain .NET type
            {
                var td = new _DisabledCachingDueToSubClassing();

                var row = new { A = 1, B = 2, C = 3 };

                td.Include = true;
                var count1 = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(3, count1);

                td.Include = false;
                var count2 = td.GetCellsForDynamicRow(default, row, span);
                Assert.Equal(0, count2);
            }

            static DynamicRow GetDynamicRow()
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();

                return CesilUtils.EnumerateDynamicFromString("A,B,C\r\n1,2,3", opts).Single() as DynamicRow;
            }
        }

        private int _NamelessParameter()
        => 1;

        [Fact]
        public void NamelessParameter()
        {
            var mtd = typeof(DefaultTypeDescriberTests).GetMethod(nameof(_NamelessParameter), BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mtd);

            var ret = mtd.ReturnParameter;
            Assert.NotNull(ret);
            Assert.Null(ret.Name);

            Assert.Throws<InvalidOperationException>(() => DefaultTypeDescriber.GetName(ret));
        }

        private record _Records1;

        private record _Records2(int A);

        private record _Records3(int A)
        {
            public string B { get; init; }
        }

        private record _Records4(int A)
        {
            public string B { get; init; }

            public _Records4(int a, string b): this(a)
            {
                A = a;
                B = b;
            }
        }

        private record _Records5(string B): _Records4(B.Length)
        {
            public double C { get; set; }
        }

        [Fact]
        public void Records_Deserialize()
        {
            // _Records1
            {
                var ip = TypeDescribers.Default.GetInstanceProvider(typeof(_Records1).GetTypeInfo());
                var mems = TypeDescribers.Default.EnumerateMembersToDeserialize(typeof(_Records1).GetTypeInfo());

                Assert.True(ip.Constructor.HasValue);
                Assert.False(ip.ConstructorTakesParameters);
                Assert.Empty(mems);
            }

            // _Records2
            {
                var ip = TypeDescribers.Default.GetInstanceProvider(typeof(_Records2).GetTypeInfo());
                var mems = TypeDescribers.Default.EnumerateMembersToDeserialize(typeof(_Records2).GetTypeInfo());

                Assert.True(ip.Constructor.HasValue);
                Assert.True(ip.ConstructorTakesParameters);
                Assert.Collection(
                    ip.Constructor.Value.GetParameters(),
                    a =>
                    {
                        Assert.Equal("A", a.Name);
                    }
                );
                Assert.Collection(
                    mems,
                    a =>
                    {
                        Assert.True(a.Setter.ConstructorParameter.HasValue);
                        Assert.Equal("A", a.Setter.ConstructorParameter.Value.Name);
                    }
                );
            }

            // _Records3
            {
                var ip = TypeDescribers.Default.GetInstanceProvider(typeof(_Records3).GetTypeInfo());
                var mems = TypeDescribers.Default.EnumerateMembersToDeserialize(typeof(_Records3).GetTypeInfo());

                Assert.True(ip.Constructor.HasValue);
                Assert.True(ip.ConstructorTakesParameters);
                Assert.Collection(
                    ip.Constructor.Value.GetParameters(),
                    a => Assert.Equal("A", a.Name)
                );
                Assert.Collection(
                    mems,
                    a =>
                    {
                        Assert.True(a.Setter.ConstructorParameter.HasValue);
                        Assert.Equal("A", a.Setter.ConstructorParameter.Value.Name);
                    },
                    b =>
                    {
                        Assert.True(b.Setter.Method.HasValue);
                        Assert.Equal(typeof(_Records3).GetProperty(nameof(_Records3.B)).SetMethod, b.Setter.Method.Value);
                    }
                );
            }

            // _Records4
            {
                var ip = TypeDescribers.Default.GetInstanceProvider(typeof(_Records4).GetTypeInfo());
                var mems = TypeDescribers.Default.EnumerateMembersToDeserialize(typeof(_Records4).GetTypeInfo());

                Assert.True(ip.Constructor.HasValue);
                Assert.True(ip.ConstructorTakesParameters);
                Assert.Collection(
                    ip.Constructor.Value.GetParameters(),
                    a => Assert.Equal("A", a.Name)
                );
                Assert.Collection(
                    mems,
                    a =>
                    {
                        Assert.True(a.Setter.ConstructorParameter.HasValue);
                        Assert.Equal("A", a.Setter.ConstructorParameter.Value.Name);
                    },
                    b =>
                    {
                        Assert.True(b.Setter.Method.HasValue);
                        Assert.Equal(typeof(_Records4).GetProperty(nameof(_Records4.B)).SetMethod, b.Setter.Method.Value);
                    }
                );
            }

            // _Records5
            {
                var ip = TypeDescribers.Default.GetInstanceProvider(typeof(_Records5).GetTypeInfo());
                var mems = TypeDescribers.Default.EnumerateMembersToDeserialize(typeof(_Records5).GetTypeInfo());

                Assert.True(ip.Constructor.HasValue);
                Assert.True(ip.ConstructorTakesParameters);
                Assert.Collection(
                    ip.Constructor.Value.GetParameters(),
                    b => Assert.Equal("B", b.Name)
                );
                Assert.Collection(
                    mems,
                    a=>
                    {
                        Assert.True(a.Setter.Method.HasValue);
                        Assert.Equal(typeof(_Records5).GetProperty(nameof(_Records5.A)).SetMethod, a.Setter.Method.Value);
                    },
                    b =>
                    {
                        Assert.True(b.Setter.ConstructorParameter.HasValue);
                        Assert.Equal("B", b.Setter.ConstructorParameter.Value.Name);
                    },
                    c =>
                    {
                        Assert.True(c.Setter.Method.HasValue);
                        Assert.Equal(typeof(_Records5).GetProperty(nameof(_Records5.C)).SetMethod, c.Setter.Method.Value);
                    }
                );
            }
        }

        [Fact]
        public void Records_Serialize()
        {
            // _Records1
            {
                var mems = TypeDescribers.Default.EnumerateMembersToSerialize(typeof(_Records1).GetTypeInfo());

                Assert.Empty(mems);
            }

            // _Records2
            {
                var mems = TypeDescribers.Default.EnumerateMembersToSerialize(typeof(_Records2).GetTypeInfo());

                Assert.Collection(
                    mems,
                    a =>
                    {
                        Assert.True(a.Getter.Method.HasValue);
                        Assert.Equal(typeof(_Records2).GetProperty(nameof(_Records2.A)).GetMethod, a.Getter.Method.Value);
                    }
                );
            }

            // _Records3
            {
                var mems = TypeDescribers.Default.EnumerateMembersToSerialize(typeof(_Records3).GetTypeInfo());

                Assert.Collection(
                    mems,
                    a =>
                    {
                        Assert.True(a.Getter.Method.HasValue);
                        Assert.Equal(typeof(_Records3).GetProperty(nameof(_Records3.A)).GetMethod, a.Getter.Method.Value);
                    },
                    b =>
                    {
                        Assert.True(b.Getter.Method.HasValue);
                        Assert.Equal(typeof(_Records3).GetProperty(nameof(_Records3.B)).GetMethod, b.Getter.Method.Value);
                    }
                );
            }

            // _Records4
            {
                var mems = TypeDescribers.Default.EnumerateMembersToSerialize(typeof(_Records4).GetTypeInfo());

                Assert.Collection(
                    mems,
                    a =>
                    {
                        Assert.True(a.Getter.Method.HasValue);
                        Assert.Equal(typeof(_Records4).GetProperty(nameof(_Records4.A)).GetMethod, a.Getter.Method.Value);
                    },
                    b =>
                    {
                        Assert.True(b.Getter.Method.HasValue);
                        Assert.Equal(typeof(_Records4).GetProperty(nameof(_Records4.B)).GetMethod, b.Getter.Method.Value);
                    }
                );
            }

            // _Records5
            {
                var mems = TypeDescribers.Default.EnumerateMembersToSerialize(typeof(_Records5).GetTypeInfo());

                Assert.Collection(
                    mems,
                    a =>
                    {
                        Assert.True(a.Getter.Method.HasValue);
                        Assert.Equal(typeof(_Records5).GetProperty(nameof(_Records5.A)).GetMethod, a.Getter.Method.Value);
                    },
                    b =>
                    {
                        Assert.True(b.Getter.Method.HasValue);
                        Assert.Equal(typeof(_Records5).GetProperty(nameof(_Records5.B)).GetMethod, b.Getter.Method.Value);
                    },
                    c =>
                    {
                        Assert.True(c.Getter.Method.HasValue);
                        Assert.Equal(typeof(_Records5).GetProperty(nameof(_Records5.C)).GetMethod, c.Getter.Method.Value);
                    }
                );
            }
        }

        [Fact]
        public void DelegateCache()
        {
            Action foo = () => { };

            var td = new DefaultTypeDescriber();
            var dc = (IDelegateCache)td;

            Assert.False(dc.TryGetDelegate<string, Action>("hello", out _));

            dc.AddDelegate("hello", foo);
            Assert.True(dc.TryGetDelegate<string, Action>("hello", out var bar));
            Assert.Same(foo, bar);
        }

        [Fact]
        public void ClearCache()
        {
            Action foo = () => { };

            var td = new DefaultTypeDescriber();
            var dc = (IDelegateCache)td;

            dc.AddDelegate("hello", foo);
            Assert.True(dc.TryGetDelegate<string, Action>("hello", out var bar));
            Assert.Same(foo, bar);

            td.ClearCache();

            Assert.False(dc.TryGetDelegate<string, Action>("hello", out _));

            // tests that this is availabe on TypeDescribers.Default
            {
                TypeDescribers.Default.ClearCache();
            }
        }

        private sealed class _IgnoredShouldSerializes
        {
            public string Prop1 { get; set; }
            public bool ShouldSerializeProp1()
            => true;

            // ignore, not a bool
            public string Prop2 { get; set; }
            public int ShouldSerializeProp2()
            => 4;

            // ignore, instance and takes a param
            public string Prop3 { get; set; }
            public bool ShouldSerializeProp3(_IgnoredShouldSerializes _)
            => true;

            public string Prop4 { get; set; }
            public static bool ShouldSerializeProp4()
            => true;

            public string Prop5 { get; set; }
            public static bool ShouldSerializeProp5(_IgnoredShouldSerializes row)
            => true;

            // ignore, instance and takes 2 params
            public string Prop6 { get; set; }
            public static bool ShouldSerializeProp6(_IgnoredShouldSerializes row, string _)
            => true;
        }

        [Fact]
        public void IgnoredShouldSerializes()
        {
            var members = TypeDescribers.Default.EnumerateMembersToSerialize(typeof(_IgnoredShouldSerializes).GetTypeInfo());

            Assert.Equal(6, members.Count());

            foreach (var mem in members)
            {
                var hasShouldSerialize = mem.ShouldSerialize.HasValue;
                var canHaveShouldSerialize = !(mem.Name == nameof(_IgnoredShouldSerializes.Prop2) || mem.Name == nameof(_IgnoredShouldSerializes.Prop3) || mem.Name == nameof(_IgnoredShouldSerializes.Prop6));

                Assert.True(canHaveShouldSerialize == hasShouldSerialize, mem.Name);
            }
        }

        private sealed class _ShouldntSerializeWeirdProperties
        {
            public string this[int index]
            {
                get
                {
                    return index.ToString();
                }
            }

            public string SetOnly
            {
                set { }
            }

            public int Normal { get; set; }
        }

        [Fact]
        public void ShouldntSerializeWeirdProperties()
        {
            var members = TypeDescribers.Default.EnumerateMembersToSerialize(typeof(_ShouldntSerializeWeirdProperties).GetTypeInfo());

            Assert.Collection(
                members,
                m =>
                {
                    Assert.Equal(nameof(_ShouldntSerializeWeirdProperties.Normal), m.Name);
                }
            );
        }

#pragma warning disable IDE0051
        private sealed class _IgnoredResets
        {
            public string Prop1 { get; set; }
            private void ResetProp1() { }

            // no reset, takes an arg as an instance method
            public string Prop2 { get; set; }
            private void ResetProp2(string arg) { }

            public string Prop3 { get; set; }
            private static void ResetProp3() { }

            public string Prop4 { get; set; }
            private static void ResetProp4(_IgnoredResets _) { }

            // no reset, takes two args as an instance method
            public string Prop5 { get; set; }
            private static void ResetProp5(_IgnoredResets _, string __) { }
        }
#pragma warning restore IDE0051

        [Fact]
        public void IgnoredResets()
        {
            var members = TypeDescribers.Default.EnumerateMembersToDeserialize(typeof(_IgnoredResets).GetTypeInfo());

            Assert.Equal(5, members.Count());

            foreach (var mem in members)
            {
                var hasReset = mem.Reset.HasValue;
                var canHaveReset = !(mem.Name == nameof(_IgnoredResets.Prop2) || mem.Name == nameof(_IgnoredResets.Prop5));

                Assert.True(canHaveReset == hasReset, mem.Name);
            }
        }

        [Fact]
        public void DefaultParserFormatterSymmetry()
        {
            var formatters = Formatter.TypeFormatters.Keys;
            var parsers = Parser.TypeParsers.Keys;

            Assert.All(formatters, f => parsers.Contains(f));
            Assert.All(parsers, p => formatters.Contains(p));
        }

        private delegate bool Parse<T>(ReadOnlySpan<char> foo, in ReadContext ctx, out T val);

        private enum _TestEnum
        {
            None = 0,

            Foo,
            Bar
        }

        [Flags]
        private enum _TestFlagsEnum
        {
            None = 0,

            First = 1,
            Second = 2,
            Third = 4,
            Fourth = 8,

            Multi = First | Third
        }

        [Fact]
        public void Parser_GetDefault()
        {
            // string
            {
                var mtd = Parser.GetDefault(typeof(string).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<string>)Delegate.CreateDelegate(typeof(Parse<string>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal("123", v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal("-123", v2);

                Assert.True(del("foo", default, out var v3));
                Assert.Equal("foo", v3);
            }

            // Version
            {
                var mtd = Parser.GetDefault(typeof(Version).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Version>)Delegate.CreateDelegate(typeof(Parse<Version>), mtd.Method.Value);

                var a = new Version();
                var b = new Version("1.0");
                var c = new Version(1, 2);
                var d = new Version(1, 2, 3);
                var e = new Version(1, 2, 3, 4);
                var f = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

                Assert.True(del(a.ToString(), default, out var v1));
                Assert.Equal(a, v1);

                Assert.True(del(b.ToString(), default, out var v2));
                Assert.Equal(b, v2);

                Assert.True(del(c.ToString(), default, out var v3));
                Assert.Equal(c, v3);

                Assert.True(del(d.ToString(), default, out var v4));
                Assert.Equal(d, v4);

                Assert.True(del(e.ToString(), default, out var v5));
                Assert.Equal(e, v5);

                Assert.True(del(f.ToString(), default, out var v6));
                Assert.Equal(f, v6);
            }

            // Uri
            {
                var mtd = Parser.GetDefault(typeof(Uri).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Uri>)Delegate.CreateDelegate(typeof(Parse<Uri>), mtd.Method.Value);

                var a = new Uri("/", UriKind.RelativeOrAbsolute);
                var b = new Uri("/foo", UriKind.RelativeOrAbsolute);
                var c = new Uri("/foo?p", UriKind.RelativeOrAbsolute);
                var d = new Uri("/foo?p#e", UriKind.RelativeOrAbsolute);
                var e = new Uri("file://local.bar/foo?p#e", UriKind.RelativeOrAbsolute);
                var f = new Uri("https://local.bar:12345/foo?p#e", UriKind.RelativeOrAbsolute);

                Assert.True(del(a.ToString(), default, out var v1));
                Assert.Equal(a, v1);

                Assert.True(del(b.ToString(), default, out var v2));
                Assert.Equal(b, v2);

                Assert.True(del(c.ToString(), default, out var v3));
                Assert.Equal(c, v3);

                Assert.True(del(d.ToString(), default, out var v4));
                Assert.Equal(d, v4);

                Assert.True(del(e.ToString(), default, out var v5));
                Assert.Equal(e, v5);

                Assert.True(del(f.ToString(), default, out var v6));
                Assert.Equal(f, v6);
            }

            // enum
            {
                var mtd = Parser.GetDefault(typeof(_TestEnum).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<_TestEnum>)Delegate.CreateDelegate(typeof(Parse<_TestEnum>), mtd.Method.Value);

                Assert.True(del(_TestEnum.None.ToString(), default, out var v1));
                Assert.Equal(_TestEnum.None, v1);

                Assert.True(del(_TestEnum.Foo.ToString(), default, out var v2));
                Assert.Equal(_TestEnum.Foo, v2);

                Assert.True(del(_TestEnum.Bar.ToString(), default, out var v3));
                Assert.Equal(_TestEnum.Bar, v3);

                Assert.True(del("foo", default, out var v4));
                Assert.Equal(_TestEnum.Foo, v4);

                Assert.False(del("Fizz", default, out _));

                Assert.False(del("123", default, out _));
            }

            // flags enum
            {
                var mtd = Parser.GetDefault(typeof(_TestFlagsEnum).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<_TestFlagsEnum>)Delegate.CreateDelegate(typeof(Parse<_TestFlagsEnum>), mtd.Method.Value);

                Assert.True(del(_TestFlagsEnum.None.ToString(), default, out var v1));
                Assert.Equal(_TestFlagsEnum.None, v1);

                Assert.True(del(_TestFlagsEnum.Multi.ToString(), default, out var v2));
                Assert.Equal(_TestFlagsEnum.Multi, v2);

                Assert.True(del((_TestFlagsEnum.First | _TestFlagsEnum.Second).ToString(), default, out var v3));
                Assert.Equal((_TestFlagsEnum.First | _TestFlagsEnum.Second), v3);

                Assert.False(del("foo", default, out _));

                Assert.True(del("first", default, out var v4));
                Assert.Equal(_TestFlagsEnum.First, v4);

                Assert.False(del("123", default, out _));
            }

            // char
            {
                var mtd = Parser.GetDefault(typeof(char).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<char>)Delegate.CreateDelegate(typeof(Parse<char>), mtd.Method.Value);

                Assert.True(del("t", default, out var v1));
                Assert.Equal('t', v1);

                Assert.False(del("foo", default, out _));
            }

            // nint
            {
                var mtd = Parser.GetDefault(typeof(nint).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<nint>)Delegate.CreateDelegate(typeof(Parse<nint>), mtd.Method.Value);

                Assert.True(del("0", default, out var v1));
                Assert.Equal((nint)0, v1);

                Assert.True(del(nint.MinValue.ToString(CultureInfo.InvariantCulture), default, out var v2));
                Assert.Equal(nint.MinValue, v2);

                Assert.True(del(nint.MaxValue.ToString(CultureInfo.InvariantCulture), default, out var v3));
                Assert.Equal(nint.MaxValue, v3);

                Assert.False(del("foo", default, out _));
            }

            // nuint
            {
                var mtd = Parser.GetDefault(typeof(nuint).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<nuint>)Delegate.CreateDelegate(typeof(Parse<nuint>), mtd.Method.Value);

                Assert.True(del("0", default, out var v1));
                Assert.Equal((nuint)0, v1);

                Assert.True(del(nuint.MinValue.ToString(CultureInfo.InvariantCulture), default, out var v2));
                Assert.Equal(nuint.MinValue, v2);

                Assert.True(del(nuint.MaxValue.ToString(CultureInfo.InvariantCulture), default, out var v3));
                Assert.Equal(nuint.MaxValue, v3);

                Assert.False(del("foo", default, out _));
            }

            // bool
            {
                var mtd = Parser.GetDefault(typeof(bool).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<bool>)Delegate.CreateDelegate(typeof(Parse<bool>), mtd.Method.Value);

                Assert.True(del("true", default, out var v1));
                Assert.True(v1);

                Assert.True(del("false", default, out var v2));
                Assert.False(v2);

                Assert.True(del("True", default, out var v3));
                Assert.True(v3);

                Assert.True(del("False", default, out var v4));
                Assert.False(v4);

                Assert.False(del("foo", default, out _));
            }

            // byte
            {
                var mtd = Parser.GetDefault(typeof(byte).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<byte>)Delegate.CreateDelegate(typeof(Parse<byte>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((byte)123, v1);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // sbyte
            {
                var mtd = Parser.GetDefault(typeof(sbyte).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<sbyte>)Delegate.CreateDelegate(typeof(Parse<sbyte>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((sbyte)123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal((sbyte)-123, v2);

                Assert.False(del("foo", default, out _));
            }

            // short
            {
                var mtd = Parser.GetDefault(typeof(short).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<short>)Delegate.CreateDelegate(typeof(Parse<short>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((short)123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal((short)-123, v2);

                Assert.False(del("foo", default, out _));
            }

            // ushort
            {
                var mtd = Parser.GetDefault(typeof(ushort).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<ushort>)Delegate.CreateDelegate(typeof(Parse<ushort>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((ushort)123, v1);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // int
            {
                var mtd = Parser.GetDefault(typeof(int).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<int>)Delegate.CreateDelegate(typeof(Parse<int>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal(123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal(-123, v2);

                Assert.False(del("foo", default, out _));
            }

            // uint
            {
                var mtd = Parser.GetDefault(typeof(uint).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<uint>)Delegate.CreateDelegate(typeof(Parse<uint>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((uint)123, v1);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // long
            {
                var mtd = Parser.GetDefault(typeof(long).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<long>)Delegate.CreateDelegate(typeof(Parse<long>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal(123L, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal(-123L, v2);

                Assert.False(del("foo", default, out _));
            }

            // ulong
            {
                var mtd = Parser.GetDefault(typeof(ulong).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<ulong>)Delegate.CreateDelegate(typeof(Parse<ulong>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((ulong)123, v1);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // float
            {
                var mtd = Parser.GetDefault(typeof(float).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<float>)Delegate.CreateDelegate(typeof(Parse<float>), mtd.Method.Value);

                Assert.True(del("123.45", default, out var v1));
                Assert.Equal(123.45f, v1);

                Assert.True(del("-123.45", default, out var v2));
                Assert.Equal(-123.45f, v2);

                Assert.False(del("foo", default, out _));
            }

            // double
            {
                var mtd = Parser.GetDefault(typeof(double).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<double>)Delegate.CreateDelegate(typeof(Parse<double>), mtd.Method.Value);

                Assert.True(del("123.45", default, out var v1));
                Assert.Equal(123.45, v1);

                Assert.True(del("-123.45", default, out var v2));
                Assert.Equal(-123.45, v2);

                Assert.False(del("foo", default, out _));
            }

            // decimal
            {
                var mtd = Parser.GetDefault(typeof(decimal).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<decimal>)Delegate.CreateDelegate(typeof(Parse<decimal>), mtd.Method.Value);

                Assert.True(del("123.45", default, out var v1));
                Assert.Equal(123.45m, v1);

                Assert.True(del("-123.45", default, out var v2));
                Assert.Equal(-123.45m, v2);

                Assert.False(del("foo", default, out _));
            }

            // Guid
            {
                var shouldMatch = Guid.Parse("fe754e30-49c2-4875-b905-cbd6f237ddfd");

                var mtd = Parser.GetDefault(typeof(Guid).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Guid>)Delegate.CreateDelegate(typeof(Parse<Guid>), mtd.Method.Value);

                Assert.True(del("fe754e30-49c2-4875-b905-cbd6f237ddfd", default, out var v1));
                Assert.Equal(shouldMatch, v1);

                Assert.True(del("fe754e3049c24875b905cbd6f237ddfd", default, out var v2));
                Assert.Equal(shouldMatch, v2);

                Assert.True(del("{fe754e30-49c2-4875-b905-cbd6f237ddfd}", default, out var v3));
                Assert.Equal(shouldMatch, v3);

                Assert.True(del("(fe754e30-49c2-4875-b905-cbd6f237ddfd)", default, out var v4));
                Assert.Equal(shouldMatch, v4);

                Assert.True(del("{0xfe754e30,0x49c2,0x4875,{0xb9,0x05,0xcb,0xd6,0xf2,0x37,0xdd,0xfd}}", default, out var v5));
                Assert.Equal(shouldMatch, v5);

                Assert.False(del("foo", default, out _));
            }

            // DateTime
            {
                var mtd = Parser.GetDefault(typeof(DateTime).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<DateTime>)Delegate.CreateDelegate(typeof(Parse<DateTime>), mtd.Method.Value);

                // max
                {
                    var res = del(DateTime.MaxValue.ToString(), default, out var v1);
                    Assert.True(res);
                    Assert.Equal(DateTime.MaxValue.ToString(), v1.ToString());
                }

                // min
                {
                    var res = del(DateTime.MinValue.ToString(), default, out var v1);
                    Assert.True(res);
                    Assert.Equal(DateTime.MinValue.ToString(), v1.ToString());
                }

            }

            // DateTimeOffset
            {
                var mtd = Parser.GetDefault(typeof(DateTimeOffset).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<DateTimeOffset>)Delegate.CreateDelegate(typeof(Parse<DateTimeOffset>), mtd.Method.Value);

                // max
                {
                    var res = del(DateTimeOffset.MaxValue.ToString(), default, out var v1);
                    Assert.True(res);
                    Assert.Equal(DateTimeOffset.MaxValue.ToString(), v1.ToString());
                }

                // min
                {
                    var res = del(DateTimeOffset.MinValue.ToString(), default, out var v1);
                    Assert.True(res);
                    Assert.Equal(DateTimeOffset.MinValue.ToString(), v1.ToString());
                }
            }

            // TimeSpan
            {
                var mtd = Parser.GetDefault(typeof(TimeSpan).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<TimeSpan>)Delegate.CreateDelegate(typeof(Parse<TimeSpan>), mtd.Method.Value);

                // max
                Assert.True(del(TimeSpan.MaxValue.ToString("c"), default, out var v1));
                Assert.Equal(TimeSpan.MaxValue, v1);

                // min
                Assert.True(del(TimeSpan.MinValue.ToString("c"), default, out var v2));
                Assert.Equal(TimeSpan.MinValue, v2);
            }

            // Index
            {
                var mtd = Parser.GetDefault(typeof(Index).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Index>)Delegate.CreateDelegate(typeof(Parse<Index>), mtd.Method.Value);

                // start
                Assert.True(del(((Index)1).ToString(), default, out var v1));
                Assert.Equal(new Index(1), v1);

                // end 
                Assert.True(del((^1).ToString(), default, out var v2));
                Assert.Equal(^1, v2);

                // malformed, empty
                Assert.False(del("", default, out _));

                // malformed, not int
                Assert.False(del("abc", default, out _));

                // malformed, not int ^
                Assert.False(del("^abc", default, out _));
            }

            // Range
            {
                var mtd = Parser.GetDefault(typeof(Range).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Range>)Delegate.CreateDelegate(typeof(Parse<Range>), mtd.Method.Value);

                // start-start
                var startStart = 1..4;
                Assert.True(del(startStart.ToString(), default, out var v1));
                Assert.Equal(startStart, v1);

                // start-end
                var startEnd = 1..^5;
                Assert.True(del(startEnd.ToString(), default, out var v2));
                Assert.Equal(startEnd, v2);

                // end-start
                var endStart = ^1..5;
                Assert.True(del(endStart.ToString(), default, out var v3));
                Assert.Equal(endStart, v3);

                // end-end
                var endEnd = ^1..^9;
                Assert.True(del(endEnd.ToString(), default, out var v4));
                Assert.Equal(endEnd, v4);

                // start-open
                var startOpen = 1..;
                Assert.True(del(startOpen.ToString(), default, out var v5));
                Assert.Equal(startOpen, v5);

                // open-start
                var openStart = ..5;
                Assert.True(del(openStart.ToString(), default, out var v6));
                Assert.Equal(openStart, v6);

                // open-open
                var openOpen = ..;
                Assert.True(del(openOpen.ToString(), default, out var v7));
                Assert.Equal(openOpen, v7);

                // malformed, empty
                Assert.False(del("", default, out _));

                // malformed, start not int
                Assert.False(del("abc..123", default, out _));

                // malformed, end not int
                Assert.False(del("123..abc", default, out _));

                // malformed, single dot
                Assert.False(del("123.abc", default, out _));
            }
        }

        [Fact]
        public void Parser_GetDefault_Nullable()
        {
            // enum?
            {
                var mtd = Parser.GetDefault(typeof(_TestEnum?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<_TestEnum?>)Delegate.CreateDelegate(typeof(Parse<_TestEnum?>), mtd.Method.Value);

                Assert.True(del(_TestEnum.None.ToString(), default, out var v1));
                Assert.Equal((_TestEnum?)_TestEnum.None, v1);

                Assert.True(del(_TestEnum.Foo.ToString(), default, out var v2));
                Assert.Equal((_TestEnum?)_TestEnum.Foo, v2);

                Assert.True(del(_TestEnum.Bar.ToString(), default, out var v3));
                Assert.Equal((_TestEnum?)_TestEnum.Bar, v3);

                Assert.True(del("", default, out var v4));
                Assert.Equal((_TestEnum?)null, v4);

                Assert.True(del("foo", default, out var v5));
                Assert.Equal(_TestEnum.Foo, v5);

                Assert.False(del("Fizz", default, out _));

                Assert.False(del("123", default, out _));
            }

            // flags enum?
            {
                var mtd = Parser.GetDefault(typeof(_TestFlagsEnum?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<_TestFlagsEnum?>)Delegate.CreateDelegate(typeof(Parse<_TestFlagsEnum?>), mtd.Method.Value);

                Assert.True(del(((_TestFlagsEnum?)_TestFlagsEnum.None).ToString(), default, out var v1));
                Assert.Equal((_TestFlagsEnum?)_TestFlagsEnum.None, v1);

                Assert.True(del(((_TestFlagsEnum?)_TestFlagsEnum.Multi).ToString(), default, out var v2));
                Assert.Equal((_TestFlagsEnum?)_TestFlagsEnum.Multi, v2);

                Assert.True(del(((_TestFlagsEnum?)(_TestFlagsEnum.First | _TestFlagsEnum.Second)).ToString(), default, out var v3));
                Assert.Equal((_TestFlagsEnum?)(_TestFlagsEnum.First | _TestFlagsEnum.Second), v3);

                Assert.True(del("", default, out var v4));
                Assert.Equal((_TestFlagsEnum?)null, v4);

                Assert.False(del("foo", default, out _));

                Assert.True(del("first", default, out var v5));
                Assert.Equal(_TestFlagsEnum.First, v5);

                Assert.False(del("123", default, out _));
            }

            // char?
            {
                var mtd = Parser.GetDefault(typeof(char?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<char?>)Delegate.CreateDelegate(typeof(Parse<char?>), mtd.Method.Value);

                Assert.True(del("t", default, out var v1));
                Assert.Equal('t', v1.Value);

                Assert.True(del("", default, out var v2));
                Assert.Equal((char?)null, v2);

                Assert.False(del("foo", default, out _));
            }

            // nint?
            {
                var mtd = Parser.GetDefault(typeof(nint?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<nint?>)Delegate.CreateDelegate(typeof(Parse<nint?>), mtd.Method.Value);

                Assert.True(del(nint.MinValue.ToString(CultureInfo.InvariantCulture), default, out var v1));
                Assert.Equal(nint.MinValue, v1.Value);

                Assert.True(del("0", default, out var v2));
                Assert.Equal((nint)0, v2.Value);

                Assert.True(del(nint.MaxValue.ToString(CultureInfo.InvariantCulture), default, out var v3));
                Assert.Equal(nint.MaxValue, v3.Value);

                Assert.True(del("", default, out var v5));
                Assert.Equal((nint?)null, v5);

                Assert.False(del("foo", default, out _));
            }

            // nuint?
            {
                var mtd = Parser.GetDefault(typeof(nuint?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<nuint?>)Delegate.CreateDelegate(typeof(Parse<nuint?>), mtd.Method.Value);

                Assert.True(del(nuint.MinValue.ToString(CultureInfo.InvariantCulture), default, out var v1));
                Assert.Equal(nuint.MinValue, v1.Value);

                Assert.True(del("0", default, out var v2));
                Assert.Equal((nuint)0, v2.Value);

                Assert.True(del(nuint.MaxValue.ToString(CultureInfo.InvariantCulture), default, out var v3));
                Assert.Equal(nuint.MaxValue, v3.Value);

                Assert.True(del("", default, out var v5));
                Assert.Equal((nuint?)null, v5);

                Assert.False(del("foo", default, out _));
            }

            // bool?
            {
                var mtd = Parser.GetDefault(typeof(bool?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<bool?>)Delegate.CreateDelegate(typeof(Parse<bool?>), mtd.Method.Value);

                Assert.True(del("true", default, out var v1));
                Assert.True(v1.Value);

                Assert.True(del("false", default, out var v2));
                Assert.False(v2.Value);

                Assert.True(del("True", default, out var v3));
                Assert.True(v3.Value);

                Assert.True(del("False", default, out var v4));
                Assert.False(v4.Value);

                Assert.True(del("", default, out var v5));
                Assert.Equal((bool?)null, v5);

                Assert.False(del("foo", default, out _));
            }

            // byte?
            {
                var mtd = Parser.GetDefault(typeof(byte?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<byte?>)Delegate.CreateDelegate(typeof(Parse<byte?>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((byte?)123, v1);

                Assert.True(del("", default, out var v2));
                Assert.Equal((byte?)null, v2);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // sbyte?
            {
                var mtd = Parser.GetDefault(typeof(sbyte?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<sbyte?>)Delegate.CreateDelegate(typeof(Parse<sbyte?>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((sbyte?)123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal((sbyte?)-123, v2);

                Assert.True(del("", default, out var v3));
                Assert.Equal((sbyte?)null, v3);

                Assert.False(del("foo", default, out _));
            }

            // short?
            {
                var mtd = Parser.GetDefault(typeof(short?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<short?>)Delegate.CreateDelegate(typeof(Parse<short?>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((short?)123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal((short?)-123, v2);

                Assert.True(del("", default, out var v3));
                Assert.Equal((short?)null, v3);

                Assert.False(del("foo", default, out _));
            }

            // ushort?
            {
                var mtd = Parser.GetDefault(typeof(ushort?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<ushort?>)Delegate.CreateDelegate(typeof(Parse<ushort?>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((ushort?)123, v1);

                Assert.True(del("", default, out var v2));
                Assert.Equal((ushort?)null, v2);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // int?
            {
                var mtd = Parser.GetDefault(typeof(int?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<int?>)Delegate.CreateDelegate(typeof(Parse<int?>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((int?)123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal((int?)-123, v2);

                Assert.True(del("", default, out var v3));
                Assert.Equal((int?)null, v3);

                Assert.False(del("foo", default, out _));
            }

            // uint?
            {
                var mtd = Parser.GetDefault(typeof(uint?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<uint?>)Delegate.CreateDelegate(typeof(Parse<uint?>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((uint?)123, v1);

                Assert.True(del("", default, out var v2));
                Assert.Equal((uint?)null, v2);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // long?
            {
                var mtd = Parser.GetDefault(typeof(long?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<long?>)Delegate.CreateDelegate(typeof(Parse<long?>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((long?)123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal((long?)-123, v2);

                Assert.True(del("", default, out var v3));
                Assert.Equal((long?)null, v3);

                Assert.False(del("foo", default, out _));
            }

            // ulong?
            {
                var mtd = Parser.GetDefault(typeof(ulong?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<ulong?>)Delegate.CreateDelegate(typeof(Parse<ulong?>), mtd.Method.Value);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((ulong?)123, v1);

                Assert.True(del("", default, out var v2));
                Assert.Equal((ulong?)null, v2);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // float?
            {
                var mtd = Parser.GetDefault(typeof(float?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<float?>)Delegate.CreateDelegate(typeof(Parse<float?>), mtd.Method.Value);

                Assert.True(del("123.45", default, out var v1));
                Assert.Equal((float?)123.45f, v1);

                Assert.True(del("-123.45", default, out var v2));
                Assert.Equal((float?)-123.45f, v2);

                Assert.True(del("", default, out var v3));
                Assert.Equal((float?)null, v3);

                Assert.False(del("foo", default, out _));
            }

            // double?
            {
                var mtd = Parser.GetDefault(typeof(double?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<double?>)Delegate.CreateDelegate(typeof(Parse<double?>), mtd.Method.Value);

                Assert.True(del("123.45", default, out var v1));
                Assert.Equal((double?)123.45, v1);

                Assert.True(del("-123.45", default, out var v2));
                Assert.Equal((double?)-123.45, v2);

                Assert.True(del("", default, out var v3));
                Assert.Equal((double?)null, v3);

                Assert.False(del("foo", default, out _));
            }

            // decimal?
            {
                var mtd = Parser.GetDefault(typeof(decimal?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<decimal?>)Delegate.CreateDelegate(typeof(Parse<decimal?>), mtd.Method.Value);

                Assert.True(del("123.45", default, out var v1));
                Assert.Equal((decimal?)123.45m, v1);

                Assert.True(del("-123.45", default, out var v2));
                Assert.Equal((decimal?)-123.45m, v2);

                Assert.True(del("", default, out var v3));
                Assert.Equal((decimal?)null, v3);

                Assert.False(del("foo", default, out _));
            }

            // Guid?
            {
                var shouldMatch = Guid.Parse("fe754e30-49c2-4875-b905-cbd6f237ddfd");

                var mtd = Parser.GetDefault(typeof(Guid?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Guid?>)Delegate.CreateDelegate(typeof(Parse<Guid?>), mtd.Method.Value);

                Assert.True(del("fe754e30-49c2-4875-b905-cbd6f237ddfd", default, out var v1));
                Assert.Equal((Guid?)shouldMatch, v1);

                Assert.True(del("fe754e3049c24875b905cbd6f237ddfd", default, out var v2));
                Assert.Equal((Guid?)shouldMatch, v2);

                Assert.True(del("{fe754e30-49c2-4875-b905-cbd6f237ddfd}", default, out var v3));
                Assert.Equal((Guid?)shouldMatch, v3);

                Assert.True(del("(fe754e30-49c2-4875-b905-cbd6f237ddfd)", default, out var v4));
                Assert.Equal((Guid?)shouldMatch, v4);

                Assert.True(del("{0xfe754e30,0x49c2,0x4875,{0xb9,0x05,0xcb,0xd6,0xf2,0x37,0xdd,0xfd}}", default, out var v5));
                Assert.Equal((Guid?)shouldMatch, v5);

                Assert.True(del("", default, out var v6));
                Assert.Equal((Guid?)null, v6);

                Assert.False(del("foo", default, out _));
            }

            // TimeSpan?
            {
                var mtd = Parser.GetDefault(typeof(TimeSpan?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<TimeSpan?>)Delegate.CreateDelegate(typeof(Parse<TimeSpan?>), mtd.Method.Value);

                // max
                Assert.True(del(TimeSpan.MaxValue.ToString("c"), default, out var v1));
                Assert.Equal((TimeSpan?)TimeSpan.MaxValue, v1);

                // min
                Assert.True(del(TimeSpan.MinValue.ToString("c"), default, out var v2));
                Assert.Equal((TimeSpan?)TimeSpan.MinValue, v2);

                // null
                Assert.True(del("", default, out var v3));
                Assert.Equal((TimeSpan?)null, v3);
            }

            // DateTime?
            {
                var mtd = Parser.GetDefault(typeof(DateTime?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<DateTime?>)Delegate.CreateDelegate(typeof(Parse<DateTime?>), mtd.Method.Value);

                // max
                {
                    var res = del(DateTime.MaxValue.ToString(), default, out var v1);
                    Assert.True(res);
                    Assert.Equal(DateTime.MaxValue.ToString(), v1.ToString());
                }

                // min
                {
                    var res = del(DateTime.MinValue.ToString(), default, out var v1);
                    Assert.True(res);
                    Assert.Equal(DateTime.MinValue.ToString(), v1.ToString());
                }

                // null
                {
                    Assert.True(del("", default, out var v3));
                    Assert.Equal((DateTime?)null, v3);
                }

            }

            // DateTimeOffset
            {
                var mtd = Parser.GetDefault(typeof(DateTimeOffset?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<DateTimeOffset?>)Delegate.CreateDelegate(typeof(Parse<DateTimeOffset?>), mtd.Method.Value);

                // max
                {
                    var res = del(DateTimeOffset.MaxValue.ToString(), default, out var v1);
                    Assert.True(res);
                    Assert.Equal(DateTimeOffset.MaxValue.ToString(), v1.ToString());
                }

                // min
                {
                    var res = del(DateTimeOffset.MinValue.ToString(), default, out var v1);
                    Assert.True(res);
                    Assert.Equal(DateTimeOffset.MinValue.ToString(), v1.ToString());
                }

                // null
                {
                    Assert.True(del("", default, out var v3));
                    Assert.Equal((DateTimeOffset?)null, v3);
                }
            }

            // Index?
            {
                var mtd = Parser.GetDefault(typeof(Index?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Index?>)Delegate.CreateDelegate(typeof(Parse<Index?>), mtd.Method.Value);

                // start
                Assert.True(del(((Index?)1).ToString(), default, out var v1));
                Assert.Equal(new Index(1), v1);

                // end 
                Assert.True(del(((Index?)(^1)).ToString(), default, out var v2));
                Assert.Equal(^1, v2);

                // null
                Assert.True(del("", default, out var v3));
                Assert.Equal((Index?)null, v3);

                // malformd
                Assert.False(del("abc", default, out _));
            }

            // Range?
            {
                var mtd = Parser.GetDefault(typeof(Range?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Range?>)Delegate.CreateDelegate(typeof(Parse<Range?>), mtd.Method.Value);

                // start-start
                Range? startStart = 1..4;
                Assert.True(del(startStart.ToString(), default, out var v1));
                Assert.Equal(startStart, v1);

                // start-end
                Range? startEnd = 1..^5;
                Assert.True(del(startEnd.ToString(), default, out var v2));
                Assert.Equal(startEnd, v2);

                // end-start
                Range? endStart = ^1..5;
                Assert.True(del(endStart.ToString(), default, out var v3));
                Assert.Equal(endStart, v3);

                // end-end
                Range? endEnd = ^1..^9;
                Assert.True(del(endEnd.ToString(), default, out var v4));
                Assert.Equal(endEnd, v4);

                // null
                Assert.True(del("", default, out var v5));
                Assert.Equal((Range?)null, v5);

                // malformd
                Assert.False(del("abc", default, out _));
            }
        }

        private class _Deserialize
        {
            public string ShouldBePresent1 { get; set; }
            public int Nope { get; private set; }
            public int? ShouldBePresent2 { private get; set; }

#pragma warning disable 0649
            public long? FieldNotIncluded;
#pragma warning restore 0649

#pragma warning disable IDE0051
            private void ResetShouldBePresent2()
            {
                ShouldBePresent2 = 2;
            }
#pragma warning restore IDE0051
        }

        [Fact]
        public void DefaultDescriber_Deserialize()
        {
            var cols = TypeDescribers.Default.EnumerateMembersToDeserialize(typeof(_Deserialize).GetTypeInfo()).ToList();

            // names and size
            Assert.Collection(
                cols,
                a => Assert.Equal(nameof(_Deserialize.ShouldBePresent1), a.Name),
                b => Assert.Equal(nameof(_Deserialize.ShouldBePresent2), b.Name)
            );

            // setters
            Assert.Collection(
                cols,
                a => Assert.Same(typeof(_Deserialize).GetProperty(nameof(_Deserialize.ShouldBePresent1)).SetMethod, a.Setter.Method.Value),
                a => Assert.Same(typeof(_Deserialize).GetProperty(nameof(_Deserialize.ShouldBePresent2)).SetMethod, a.Setter.Method.Value)
            );

            // parser
            Assert.Collection(
                cols,
                a => Assert.Same(Parser.GetDefault(typeof(string).GetTypeInfo()), a.Parser),
                a => Assert.Same(Parser.GetDefault(typeof(int?).GetTypeInfo()), a.Parser)
            );

            var mtd = typeof(_Deserialize).GetMethod("ResetShouldBePresent2", BindingFlags.Instance | BindingFlags.NonPublic);

            // reset 
            Assert.Collection(
                cols,
                a => Assert.False(a.Reset.HasValue),
                a => Assert.Same(mtd, a.Reset.Value.Method.Value)
            );
        }

        private class _DeserializeDataMember
        {
            [DataMember(Order = 3)]
            public string Foo { get; set; }

            [IgnoreDataMember]
            public int? Nope { get; set; }

            [DataMember(Name = "HELLO", Order = 2)]
            public double Hello { get; private set; }

#pragma warning disable 0649
            [DataMember(Order = 1, IsRequired = true)]
            public int? Field;
#pragma warning restore 0649

            public decimal? Yeaaaah { private get; set; }

            internal decimal? GetYeah() => Yeaaaah;
        }

        [Fact]
        public void DefaultDescriber_DeserializeDataMemember()
        {
            var cols = TypeDescribers.Default.EnumerateMembersToDeserialize(typeof(_DeserializeDataMember).GetTypeInfo()).ToList();

            // names and size
            Assert.Collection(
                cols,
                a => Assert.Equal(nameof(_DeserializeDataMember.Field), a.Name),
                b => Assert.Equal("HELLO", b.Name),
                c => Assert.Equal(nameof(_DeserializeDataMember.Foo), c.Name),
                d => Assert.Equal(nameof(_DeserializeDataMember.Yeaaaah), d.Name)
            );

            // setters
            Assert.Collection(
                cols,
                a => Assert.Same(typeof(_DeserializeDataMember).GetField("Field"), a.Setter.Field.Value),
                a => Assert.Same(typeof(_DeserializeDataMember).GetProperty(nameof(_DeserializeDataMember.Hello)).SetMethod, a.Setter.Method.Value),
                a => Assert.Same(typeof(_DeserializeDataMember).GetProperty(nameof(_DeserializeDataMember.Foo)).SetMethod, a.Setter.Method.Value),
                a => Assert.Same(typeof(_DeserializeDataMember).GetProperty(nameof(_DeserializeDataMember.Yeaaaah)).SetMethod, a.Setter.Method.Value)
            );

            // setters actually work
            {
                // Field
                {
                    var x = new _DeserializeDataMember();
                    var field = cols[0].Setter.Field.Value;
                    field.SetValue(x, (int?)-123);
                    Assert.Equal((int?)-123, x.Field);
                }

                // HELLO
                {
                    var x = new _DeserializeDataMember();
                    var hello = cols[1].Setter.Method.Value;
                    hello.Invoke(x, new object[] { (double)1.23 });
                    Assert.Equal(1.23, x.Hello);
                }

                // Foo
                {
                    var x = new _DeserializeDataMember();
                    var foo = cols[2].Setter.Method.Value;
                    foo.Invoke(x, new object[] { "bar" });
                    Assert.Equal("bar", x.Foo);
                }

                // Yeaaaah
                {
                    var x = new _DeserializeDataMember();
                    var yeaaaah = cols[3].Setter.Method.Value;
                    yeaaaah.Invoke(x, new object[] { (decimal?)12.34m });
                    Assert.Equal((decimal?)12.34m, x.GetYeah());
                }
            }

            // parser
            Assert.Collection(
                cols,
                a => Assert.Same(Parser.GetDefault(typeof(int?).GetTypeInfo()), a.Parser),
                a => Assert.Same(Parser.GetDefault(typeof(double).GetTypeInfo()), a.Parser),
                a => Assert.Same(Parser.GetDefault(typeof(string).GetTypeInfo()), a.Parser),
                a => Assert.Same(Parser.GetDefault(typeof(decimal?).GetTypeInfo()), a.Parser)
            );

            // isRequired
            Assert.Collection(
                cols,
                a => Assert.True(a.IsRequired),
                a => Assert.False(a.IsRequired),
                a => Assert.False(a.IsRequired),
                a => Assert.False(a.IsRequired)
            );
        }

        [Fact]
        public async Task Formatter_GetDefault()
        {
            var ctx = WriteContext.WritingColumn(Options.Default, 0, ColumnIdentifier.Create(0), null);

            // string
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(string).GetTypeInfo()).Method.Value;
                var res = mtd.Invoke(null, new object[] { "foo", ctx, writer });
                var resBool = (bool)res;
                Assert.True(resBool);

                await writer.FlushAsync();

                Assert.True(reader.TryRead(out var buff));
                Assert.Equal("foo", BufferToString(buff.Buffer));
                reader.AdvanceTo(buff.Buffer.End);
            }

            // Version
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(Version).GetTypeInfo()).Method.Value;

                var a = new Version();
                var b = new Version("1.0");
                var c = new Version(1, 2);
                var d = new Version(1, 2, 3);
                var e = new Version(1, 2, 3, 4);
                var f = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

                {
                    var res = mtd.Invoke(null, new object[] { a, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(a.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { b, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(b.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { c, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(c.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { d, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(d.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { e, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(e.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { f, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(f.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // Uri
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(Uri).GetTypeInfo()).Method.Value;

                var a = new Uri("/", UriKind.RelativeOrAbsolute);
                var b = new Uri("/foo", UriKind.RelativeOrAbsolute);
                var c = new Uri("/foo?p", UriKind.RelativeOrAbsolute);
                var d = new Uri("/foo?p#e", UriKind.RelativeOrAbsolute);
                var e = new Uri("file://local.bar/foo?p#e", UriKind.RelativeOrAbsolute);
                var f = new Uri("https://local.bar:12345/foo?p#e", UriKind.RelativeOrAbsolute);

                {
                    var res = mtd.Invoke(null, new object[] { a, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(a.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { b, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(b.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { c, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(c.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { d, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(d.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { e, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(e.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                {
                    var res = mtd.Invoke(null, new object[] { f, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(f.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // enum
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(_TestEnum).GetTypeInfo()).Method.Value;

                // Bar
                {
                    var res = mtd.Invoke(null, new object[] { _TestEnum.Bar, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.Bar.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // Foo
                {
                    var res = mtd.Invoke(null, new object[] { _TestEnum.Foo, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.Foo.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // None
                {
                    var res = mtd.Invoke(null, new object[] { _TestEnum.None, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.None.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // bad value
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum)int.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.False(resBool);
                }
            }

            // flags enum
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(_TestFlagsEnum).GetTypeInfo()).Method.Value;

                // First
                {
                    var res = mtd.Invoke(null, new object[] { _TestFlagsEnum.First, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestFlagsEnum.First.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // Multi
                {
                    var res = mtd.Invoke(null, new object[] { _TestFlagsEnum.Multi, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestFlagsEnum.Multi.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // First | Fourth
                {
                    var res = mtd.Invoke(null, new object[] { _TestFlagsEnum.First | _TestFlagsEnum.Fourth, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((_TestFlagsEnum.First | _TestFlagsEnum.Fourth).ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // bad value
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum)int.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.False(resBool);
                }
            }

            // char
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(char).GetTypeInfo()).Method.Value;

                // value
                {
                    var res = mtd.Invoke(null, new object[] { 'D', ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("D", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // nint
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(nint).GetTypeInfo()).Method.Value;

                // min
                {
                    var res = mtd.Invoke(null, new object[] { nint.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(nint.MinValue.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // max
                {
                    var res = mtd.Invoke(null, new object[] { nint.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(nint.MaxValue.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // nuint
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(nuint).GetTypeInfo()).Method.Value;

                // min
                {
                    var res = mtd.Invoke(null, new object[] { nuint.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(nuint.MinValue.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // max
                {
                    var res = mtd.Invoke(null, new object[] { nuint.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(nuint.MaxValue.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // bool
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(bool).GetTypeInfo()).Method.Value;

                // true
                {
                    var res = mtd.Invoke(null, new object[] { true, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("True", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // false
                {
                    var res = mtd.Invoke(null, new object[] { false, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("False", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // byte
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(byte).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (byte)123, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // sbyte
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(sbyte).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte)123, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte)-123, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // short
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(short).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { short.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("32767", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { short.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-32768", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // ushort
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(ushort).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { ushort.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("65535", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // int
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(int).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { int.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("2147483647", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { int.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-2147483648", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // uint
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(uint).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { uint.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("4294967295", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // long
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(long).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { long.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("9223372036854775807", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { long.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-9223372036854775808", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // ulong
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(ulong).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { ulong.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("18446744073709551615", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // float
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(float).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { 12.34f, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34f.ToString("G9"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { -12.34f, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34f).ToString("G9"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // double
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(double).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { 12.34, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34.ToString("G17"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { -12.34, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34).ToString("G17"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // very long
                {
                    var res = mtd.Invoke(null, new object[] { 0.84551240822557006, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("0.84551240822557006", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // decimal
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(decimal).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { 12.34m, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34m.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { -12.34m, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34m).ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // large
                {
                    var res = mtd.Invoke(null, new object[] { 79_228_162_514_264_337_593_543_950_335m, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((79_228_162_514_264_337_593_543_950_335m).ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // DateTime
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(DateTime).GetTypeInfo()).Method.Value;

                // max
                {
                    var res = mtd.Invoke(null, new object[] { DateTime.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTime.MaxValue.ToUniversalTime().ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { DateTime.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTime.MinValue.ToUniversalTime().ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // DateTimeOffset
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(DateTimeOffset).GetTypeInfo()).Method.Value;

                // max
                {
                    var res = mtd.Invoke(null, new object[] { DateTimeOffset.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTimeOffset.MaxValue.ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { DateTimeOffset.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTimeOffset.MinValue.ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // TimeSpan
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(TimeSpan).GetTypeInfo()).Method.Value;

                // max
                {
                    var res = mtd.Invoke(null, new object[] { TimeSpan.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(TimeSpan.MaxValue.ToString("c"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { TimeSpan.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(TimeSpan.MinValue.ToString("c"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // Index
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(Index).GetTypeInfo()).Method.Value;

                // start
                {
                    var i = new Index(15, false);

                    var res = mtd.Invoke(null, new object[] { i, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(i.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // end
                {
                    var i = new Index(22, true);

                    var res = mtd.Invoke(null, new object[] { i, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(i.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // Range
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(Range).GetTypeInfo()).Method.Value;

                // open ended
                {
                    var r = ..;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // left
                {
                    var r = 3..;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // left end
                {
                    var r = ^3..;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // right
                {
                    var r = ..3;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // right end
                {
                    var r = ..^3;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // closed
                {
                    var r = 1..4;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // closed, left end
                {
                    var r = ^1..4;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // closed, right end
                {
                    var r = 1..^4;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // closed, both end
                {
                    var r = ^1..^4;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }


            static string BufferToString(ReadOnlySequence<byte> buff)
            {
                var bytes = new List<byte>();
                foreach (var b in buff)
                {
                    bytes.AddRange(b.ToArray());
                }

                var byteArray = bytes.ToArray();
                var byteSpan = new Span<byte>(byteArray);
                var charSpan = MemoryMarshal.Cast<byte, char>(byteSpan);

                return new string(charSpan);
            }

        }

        [Fact]
        public async Task Formatter_GetDefault_Nullable()
        {
            var ctx = WriteContext.WritingColumn(Options.Default, 0, ColumnIdentifier.Create(0), null);

            // enum?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(_TestEnum?).GetTypeInfo()).Method.Value;

                // Bar
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)_TestEnum.Bar, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.Bar.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // Foo
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)_TestEnum.Foo, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.Foo.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // None
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)_TestEnum.None, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.None.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }

                // bad value
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)int.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.False(resBool);
                }
            }

            // flags enum?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(_TestFlagsEnum?).GetTypeInfo()).Method.Value;

                // First
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum?)_TestFlagsEnum.First, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestFlagsEnum.First.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // Multi
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum?)_TestFlagsEnum.Multi, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestFlagsEnum.Multi.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // First | Fourth
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum?)_TestFlagsEnum.First | _TestFlagsEnum.Fourth, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((_TestFlagsEnum.First | _TestFlagsEnum.Fourth).ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }

                // bad value
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum)int.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.False(resBool);
                }
            }

            // char?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(char?).GetTypeInfo()).Method.Value;

                // value
                {
                    var res = mtd.Invoke(null, new object[] { (char?)'D', ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("D", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (char?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // nint?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(nint?).GetTypeInfo()).Method.Value;

                // min
                {
                    var res = mtd.Invoke(null, new object[] { (nint?)nint.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(nint.MinValue.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // max
                {
                    var res = mtd.Invoke(null, new object[] { (nint?)nint.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(nint.MaxValue.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (nint?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // nuint?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(nuint?).GetTypeInfo()).Method.Value;

                // min
                {
                    var res = mtd.Invoke(null, new object[] { (nuint?)nuint.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(nuint.MinValue.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // max
                {
                    var res = mtd.Invoke(null, new object[] { (nuint?)nuint.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(nuint.MaxValue.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (nuint?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // bool?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(bool?).GetTypeInfo()).Method.Value;

                // true
                {
                    var res = mtd.Invoke(null, new object[] { (bool?)true, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("True", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // false
                {
                    var res = mtd.Invoke(null, new object[] { (bool?)false, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("False", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (bool?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // byte?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(byte?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (byte?)123, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (byte?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // sbyte?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(sbyte?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte?)123, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte?)-123, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // short?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(short?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (short?)short.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("32767", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (short?)short.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-32768", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (short?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // ushort?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(ushort?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (ushort?)ushort.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("65535", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (ushort?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // int?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(int?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (int?)int.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("2147483647", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (int?)int.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-2147483648", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (int?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // uint?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(uint?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (uint?)uint.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("4294967295", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (uint?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // long?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(long?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (long?)long.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("9223372036854775807", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (long?)long.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-9223372036854775808", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (long?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // ulong?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(ulong?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (ulong?)ulong.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("18446744073709551615", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (ulong?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // float?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(float?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (float?)12.34f, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34f.ToString("G9"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (float?)-12.34f, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34f).ToString("G9"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (float?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // double?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(double?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (double?)12.34, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34.ToString("G17"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (double?)-12.34, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34).ToString("G17"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // very long
                {
                    var res = mtd.Invoke(null, new object[] { 0.84551240822557006, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("0.84551240822557006", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (double?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }


            // decimal?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(decimal?).GetTypeInfo()).Method.Value;

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (decimal?)12.34m, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34m.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (decimal?)-12.34m, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34m).ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // large
                {
                    var res = mtd.Invoke(null, new object[] { (decimal?)79_228_162_514_264_337_593_543_950_335m, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((79_228_162_514_264_337_593_543_950_335m).ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (decimal?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // DateTime?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(DateTime?).GetTypeInfo()).Method.Value;

                // max
                {
                    var res = mtd.Invoke(null, new object[] { (DateTime?)DateTime.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTime.MaxValue.ToUniversalTime().ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { (DateTime?)DateTime.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTime.MinValue.ToUniversalTime().ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (DateTime?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // DateTimeOffset?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(DateTimeOffset?).GetTypeInfo()).Method.Value;

                // max
                {
                    var res = mtd.Invoke(null, new object[] { (DateTimeOffset?)DateTimeOffset.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTimeOffset.MaxValue.ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { (DateTimeOffset?)DateTimeOffset.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTimeOffset.MinValue.ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (DateTimeOffset?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // TimeSpan?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(TimeSpan?).GetTypeInfo()).Method.Value;

                // max
                {
                    var res = mtd.Invoke(null, new object[] { (TimeSpan?)TimeSpan.MaxValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(TimeSpan.MaxValue.ToString("c"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { (TimeSpan?)TimeSpan.MinValue, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(TimeSpan.MinValue.ToString("c"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (TimeSpan?)null, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // Index?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(Index?).GetTypeInfo()).Method.Value;

                // start
                {
                    Index? i = new Index(15, false);

                    var res = mtd.Invoke(null, new object[] { i, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(i.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // end
                {
                    Index? i = new Index(22, true);

                    var res = mtd.Invoke(null, new object[] { i, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(i.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }


                // null
                {
                    Index? i = null;

                    var res = mtd.Invoke(null, new object[] { i, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            // Range?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = Formatter.GetDefault(typeof(Range?).GetTypeInfo()).Method.Value;

                // open ended
                {
                    Range? r = ..;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // left
                {
                    Range? r = 3..;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // left end
                {
                    Range? r = ^3..;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // right
                {
                    Range? r = ..3;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // right end
                {
                    Range? r = ..^3;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // closed
                {
                    Range? r = 1..4;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // closed, left end
                {
                    Range? r = ^1..4;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // closed, right end
                {
                    Range? r = 1..^4;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // closed, both end
                {
                    Range? r = ^1..^4;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(r.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }


                // null
                {
                    Range? r = null;

                    var res = mtd.Invoke(null, new object[] { r, ctx, writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }

            static string BufferToString(ReadOnlySequence<byte> buff)
            {
                var bytes = new List<byte>();
                foreach (var b in buff)
                {
                    bytes.AddRange(b.ToArray());
                }

                var byteArray = bytes.ToArray();
                var byteSpan = new Span<byte>(byteArray);
                var charSpan = MemoryMarshal.Cast<byte, char>(byteSpan);

                return new string(charSpan);
            }
        }

        private class _Formatter_Failable : IBufferWriter<char>
        {
            private readonly char[] Buffer;

            public _Formatter_Failable(int maxSize)
            {
                Buffer = new char[maxSize];
            }

            public void Advance(int count)
            {
                Array.Clear(Buffer, 0, Buffer.Length);
            }

            public Memory<char> GetMemory(int sizeHint = 0)
            => Buffer.AsMemory();

            public Span<char> GetSpan(int sizeHint = 0)
            => GetMemory(sizeHint).Span;
        }

        [Fact]
        public void Formatter_FailableDefaults()
        {
            // uri
            {
                var a = new Uri("https://example.com/");
                var needed = a.ToString().Length;
                var mtd = Formatter.GetDefault(typeof(Uri).GetTypeInfo()).Method.Value;

                for (var i = 0; i < needed; i++)
                {
                    var res = (bool)mtd.Invoke(null, new object[] { a, default(WriteContext), new _Formatter_Failable(i) });
                    Assert.False(res);
                }
            }

            // bool
            {
                var a = true;
                var needed = a.ToString().Length;
                var mtd = Formatter.GetDefault(typeof(bool).GetTypeInfo()).Method.Value;

                for (var i = 0; i < needed; i++)
                {
                    var res = (bool)mtd.Invoke(null, new object[] { a, default(WriteContext), new _Formatter_Failable(i) });
                    Assert.False(res);
                }

                var b = false;
                needed = b.ToString().Length;
                for (var i = 0; i < needed; i++)
                {
                    var res = (bool)mtd.Invoke(null, new object[] { b, default(WriteContext), new _Formatter_Failable(i) });
                    Assert.False(res);
                }
            }

            // char
            {
                var a = 'c';
                var needed = 1;
                var mtd = Formatter.GetDefault(typeof(char).GetTypeInfo()).Method.Value;

                for (var i = 0; i < needed; i++)
                {
                    var res = (bool)mtd.Invoke(null, new object[] { a, default(WriteContext), new _Formatter_Failable(i) });
                    Assert.False(res);
                }
            }
        }

        private class _Serialize
        {
            public int GetButNoSet { get; }
            public string GetAndSet { get; set; }
            public string SetAndPrivateGet { set; private get; }

            public char? ShouldSerializeProp { get; }
            public DateTimeOffset ShouldSerializeStaticProp { get; }

#pragma warning disable 0649
            public Guid? Field;
#pragma warning restore 0649

            public bool ShouldSerializeShouldSerializeProp() => true;
            public static bool ShouldSerializeShouldSerializeStaticProp() => true;
        }

        [Fact]
        public void DefaultDescriber_Serialize()
        {
            var cols = TypeDescribers.Default.EnumerateMembersToSerialize(typeof(_Serialize).GetTypeInfo()).ToList();

            // names and size
            Assert.Collection(
                cols,
                a => Assert.Equal(nameof(_Serialize.GetButNoSet), a.Name),
                b => Assert.Equal(nameof(_Serialize.GetAndSet), b.Name),
                c => Assert.Equal(nameof(_Serialize.ShouldSerializeProp), c.Name),
                d => Assert.Equal(nameof(_Serialize.ShouldSerializeStaticProp), d.Name)
            );

            // getters
            Assert.Collection(
                cols,
                a => Assert.Same(typeof(_Serialize).GetProperty(nameof(_Serialize.GetButNoSet)).GetMethod, a.Getter.Method.Value),
                a => Assert.Same(typeof(_Serialize).GetProperty(nameof(_Serialize.GetAndSet)).GetMethod, a.Getter.Method.Value),
                a => Assert.Same(typeof(_Serialize).GetProperty(nameof(_Serialize.ShouldSerializeProp)).GetMethod, a.Getter.Method.Value),
                a => Assert.Same(typeof(_Serialize).GetProperty(nameof(_Serialize.ShouldSerializeStaticProp)).GetMethod, a.Getter.Method.Value)
            );

            // should serialize
            Assert.Collection(
                cols,
                a => Assert.False(a.ShouldSerialize.HasValue),
                b => Assert.False(b.ShouldSerialize.HasValue),
                c => Assert.Same(typeof(_Serialize).GetMethod(nameof(_Serialize.ShouldSerializeShouldSerializeProp)), c.ShouldSerialize.Value.Method.Value),
                d => Assert.Same(typeof(_Serialize).GetMethod(nameof(_Serialize.ShouldSerializeShouldSerializeStaticProp)), d.ShouldSerialize.Value.Method.Value)
            );

            // formatter
            Assert.Collection(
                cols,
                a => Assert.Same(Formatter.GetDefault(typeof(int).GetTypeInfo()), a.Formatter),
                b => Assert.Same(Formatter.GetDefault(typeof(string).GetTypeInfo()), b.Formatter),
                c => Assert.Same(Formatter.GetDefault(typeof(char?).GetTypeInfo()), c.Formatter),
                d => Assert.Same(Formatter.GetDefault(typeof(DateTimeOffset).GetTypeInfo()), d.Formatter)
            );
        }

        private class _SerializeDataMember
        {
            [DataMember(Order = 3)]
            public string Bar { get; set; }

            [IgnoreDataMember]
            public int? Yep { get; set; }

            [DataMember(Name = "world", Order = 2)]
            public double WORLD { get; private set; }

#pragma warning disable 0649
            [DataMember(Order = 1)]
            public int? Field;
#pragma warning restore 0649

            [DataMember(Order = 999, EmitDefaultValue = false)]
#pragma warning disable IDE0051
            private StringComparison Yeaaaah { get; }
#pragma warning restore IDE0051

            public bool ShouldSerializeWORLD() => true;
        }

        [Fact]
        public void DefaultDescriber_SerializeDataMember()
        {
            var cols = TypeDescribers.Default.EnumerateMembersToSerialize(typeof(_SerializeDataMember).GetTypeInfo()).ToList();

            // names & order
            Assert.Collection(
                cols,
                a => Assert.Equal("Field", a.Name),
                a => Assert.Equal("world", a.Name),
                a => Assert.Equal("Bar", a.Name),
                a => Assert.Equal("Yeaaaah", a.Name)
            );

            // getters
            Assert.Collection(
                cols,
                a => Assert.Same(typeof(_SerializeDataMember).GetField("Field"), a.Getter.Field.Value),
                a => Assert.Same(typeof(_SerializeDataMember).GetProperty("WORLD").GetMethod, a.Getter.Method.Value),
                a => Assert.Same(typeof(_SerializeDataMember).GetProperty("Bar").GetMethod, a.Getter.Method.Value),
                a => Assert.Same(typeof(_SerializeDataMember).GetProperty("Yeaaaah", BindingFlags.Instance | BindingFlags.NonPublic).GetMethod, a.Getter.Method.Value)
            );

            // should serialize
            Assert.Collection(
                cols,
                a => Assert.False(a.ShouldSerialize.HasValue),
                a => Assert.Same(typeof(_SerializeDataMember).GetMethod(nameof(_SerializeDataMember.ShouldSerializeWORLD)), a.ShouldSerialize.Value.Method.Value),
                a => Assert.False(a.ShouldSerialize.HasValue),
                a => Assert.False(a.ShouldSerialize.HasValue)
            );

            // formatter
            Assert.Collection(
                cols,
                a => Assert.Same(Formatter.GetDefault(typeof(int?).GetTypeInfo()).Method.Value, a.Formatter.Method.Value),
                a => Assert.Same(Formatter.GetDefault(typeof(double).GetTypeInfo()).Method.Value, a.Formatter.Method.Value),
                a => Assert.Same(Formatter.GetDefault(typeof(string).GetTypeInfo()).Method.Value, a.Formatter.Method.Value),
                a => Assert.Same(Formatter.GetDefault(typeof(StringComparison).GetTypeInfo()).Method.Value, a.Formatter.Method.Value)
            );

            // emitDefaultValue
            Assert.Collection(
                cols,
                a => Assert.True(a.EmitDefaultValue),
                a => Assert.True(a.EmitDefaultValue),
                a => Assert.True(a.EmitDefaultValue),
                a => Assert.False(a.EmitDefaultValue)
            );
        }

        private class _Formatter_InsufficientMemoryDoesntThrow : IBufferWriter<char>
        {
            public static readonly IBufferWriter<char> Singleton = new _Formatter_InsufficientMemoryDoesntThrow();

            private _Formatter_InsufficientMemoryDoesntThrow() { }

            public void Advance(int count)
            {
                throw new NotImplementedException();
            }

            public Memory<char> GetMemory(int sizeHint = 0)
            => Memory<char>.Empty;

            public Span<char> GetSpan(int sizeHint = 0)
            => Span<char>.Empty;
        }

        [Fact]
        public void Formatter_InsufficientMemoryDoesntThrow()
        {
            Try<bool>();
            Try<bool?>(false);

            Try<byte>();
            Try<byte?>(1);

            Try<sbyte>();
            Try<sbyte?>(1);

            Try<short>();
            Try<short?>(1);

            Try<ushort>();
            Try<ushort?>(1);

            Try<int>();
            Try<int?>(1);

            Try<uint>();
            Try<uint?>(1);

            Try<long>();
            Try<long?>(1);

            Try<ulong>();
            Try<ulong?>(1);

            Try<float>();
            Try<float?>(1);

            Try<double>();
            Try<double?>(1);

            Try<decimal>();
            Try<decimal?>(1);

            Try<string>("foo");

            Try<Uri>(new Uri("https://example.com"));

            Try<DateTime>();
            Try<DateTime?>(DateTime.UtcNow);

            Try<DateTimeOffset>();
            Try<DateTimeOffset?>(DateTimeOffset.UtcNow);

            Try<TimeSpan>();
            Try<TimeSpan?>(TimeSpan.Zero);

            Try<Guid>();
            Try<Guid?>(Guid.NewGuid());

            Try<Version>(new Version(1, 2, 3));

            Try<StringComparison>();
            Try<StringComparison?>(StringComparison.Ordinal);

            Try<AttributeTargets>();
            Try<AttributeTargets?>(AttributeTargets.Assembly);

            static void Try<T>(T def = default)
            {
                var ctx = WriteContext.WritingColumn(Options.Default, 0, ColumnIdentifier.Create(0), null);

                var mtd = Formatter.GetDefault(typeof(T).GetTypeInfo()).Method.Value;

                var res = (bool)mtd.Invoke(null, new object[] { def, ctx, _Formatter_InsufficientMemoryDoesntThrow.Singleton });

                Assert.False(res);
            }
        }
    }
#pragma warning restore IDE1006
}
