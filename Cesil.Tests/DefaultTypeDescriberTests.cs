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
        public void DeserializableMember_GetDefaultParser()
        {
            // string
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(string).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<string>)Delegate.CreateDelegate(typeof(Parse<string>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal("123", v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal("-123", v2);

                Assert.True(del("foo", default, out var v3));
                Assert.Equal("foo", v3);
            }

            // enum
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(_TestEnum).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<_TestEnum>)Delegate.CreateDelegate(typeof(Parse<_TestEnum>), mtd);

                Assert.True(del(_TestEnum.None.ToString(), default, out var v1));
                Assert.Equal(_TestEnum.None, v1);

                Assert.True(del(_TestEnum.Foo.ToString(), default, out var v2));
                Assert.Equal(_TestEnum.Foo, v2);

                Assert.True(del(_TestEnum.Bar.ToString(), default, out var v3));
                Assert.Equal(_TestEnum.Bar, v3);

                Assert.False(del("foo", default, out _));

                Assert.False(del("123", default, out _));
            }

            // flags enum
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(_TestFlagsEnum).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<_TestFlagsEnum>)Delegate.CreateDelegate(typeof(Parse<_TestFlagsEnum>), mtd);

                Assert.True(del(_TestFlagsEnum.None.ToString(), default, out var v1));
                Assert.Equal(_TestFlagsEnum.None, v1);

                Assert.True(del(_TestFlagsEnum.Multi.ToString(), default, out var v2));
                Assert.Equal(_TestFlagsEnum.Multi, v2);

                Assert.True(del((_TestFlagsEnum.First | _TestFlagsEnum.Second).ToString(), default, out var v3));
                Assert.Equal((_TestFlagsEnum.First | _TestFlagsEnum.Second), v3);

                Assert.False(del("foo", default, out _));

                Assert.False(del("123", default, out _));
            }

            // char
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(char).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<char>)Delegate.CreateDelegate(typeof(Parse<char>), mtd);

                Assert.True(del("t", default, out var v1));
                Assert.Equal('t', v1);
                
                Assert.False(del("foo", default, out _));
            }

            // bool
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(bool).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<bool>)Delegate.CreateDelegate(typeof(Parse<bool>), mtd);

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
                var mtd = DeserializableMember.GetDefaultParser(typeof(byte).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<byte>)Delegate.CreateDelegate(typeof(Parse<byte>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((byte)123, v1);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // sbyte
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(sbyte).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<sbyte>)Delegate.CreateDelegate(typeof(Parse<sbyte>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((sbyte)123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal((sbyte)-123, v2);

                Assert.False(del("foo", default, out _));
            }

            // short
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(short).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<short>)Delegate.CreateDelegate(typeof(Parse<short>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((short)123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal((short)-123, v2);

                Assert.False(del("foo", default, out _));
            }

            // ushort
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(ushort).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<ushort>)Delegate.CreateDelegate(typeof(Parse<ushort>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((ushort)123, v1);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // int
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(int).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<int>)Delegate.CreateDelegate(typeof(Parse<int>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal(123, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal(-123, v2);

                Assert.False(del("foo", default, out _));
            }

            // uint
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(uint).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<uint>)Delegate.CreateDelegate(typeof(Parse<uint>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((uint)123, v1);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // long
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(long).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<long>)Delegate.CreateDelegate(typeof(Parse<long>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal(123L, v1);

                Assert.True(del("-123", default, out var v2));
                Assert.Equal(-123L, v2);

                Assert.False(del("foo", default, out _));
            }

            // ulong
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(ulong).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<ulong>)Delegate.CreateDelegate(typeof(Parse<ulong>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((ulong)123, v1);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // float
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(float).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<float>)Delegate.CreateDelegate(typeof(Parse<float>), mtd);

                Assert.True(del("123.45", default, out var v1));
                Assert.Equal(123.45f, v1);

                Assert.True(del("-123.45", default, out var v2));
                Assert.Equal(-123.45f, v2);

                Assert.False(del("foo", default, out _));
            }

            // double
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(double).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<double>)Delegate.CreateDelegate(typeof(Parse<double>), mtd);

                Assert.True(del("123.45", default, out var v1));
                Assert.Equal(123.45, v1);

                Assert.True(del("-123.45", default, out var v2));
                Assert.Equal(-123.45, v2);

                Assert.False(del("foo", default, out _));
            }

            // decimal
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(decimal).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<decimal>)Delegate.CreateDelegate(typeof(Parse<decimal>), mtd);

                Assert.True(del("123.45", default, out var v1));
                Assert.Equal(123.45m, v1);

                Assert.True(del("-123.45", default, out var v2));
                Assert.Equal(-123.45m, v2);

                Assert.False(del("foo", default, out _));
            }

            // Guid
            {
                var shouldMatch = Guid.Parse("fe754e30-49c2-4875-b905-cbd6f237ddfd");

                var mtd = DeserializableMember.GetDefaultParser(typeof(Guid).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Guid>)Delegate.CreateDelegate(typeof(Parse<Guid>), mtd);

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

            // TimeSpan
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(TimeSpan).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<TimeSpan>)Delegate.CreateDelegate(typeof(Parse<TimeSpan>), mtd);

                // max
                Assert.True(del(TimeSpan.MaxValue.ToString("c"), default, out var v1));
                Assert.Equal(TimeSpan.MaxValue, v1);

                // min
                Assert.True(del(TimeSpan.MinValue.ToString("c"), default, out var v2));
                Assert.Equal(TimeSpan.MinValue, v2);
            }
        }

        [Fact]
        public void DeserializableMember_GetDefaultParser_Nullable()
        {
            // enum?
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(_TestEnum?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<_TestEnum?>)Delegate.CreateDelegate(typeof(Parse<_TestEnum?>), mtd);

                Assert.True(del(_TestEnum.None.ToString(), default, out var v1));
                Assert.Equal((_TestEnum?)_TestEnum.None, v1);

                Assert.True(del(_TestEnum.Foo.ToString(), default, out var v2));
                Assert.Equal((_TestEnum?)_TestEnum.Foo, v2);

                Assert.True(del(_TestEnum.Bar.ToString(), default, out var v3));
                Assert.Equal((_TestEnum?)_TestEnum.Bar, v3);

                Assert.True(del("", default, out var v4));
                Assert.Equal((_TestEnum?)null, v4);

                Assert.False(del("foo", default, out _));

                Assert.False(del("123", default, out _));
            }

            // flags enum?
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(_TestFlagsEnum?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<_TestFlagsEnum?>)Delegate.CreateDelegate(typeof(Parse<_TestFlagsEnum?>), mtd);

                Assert.True(del(((_TestFlagsEnum?)_TestFlagsEnum.None).ToString(), default, out var v1));
                Assert.Equal((_TestFlagsEnum?)_TestFlagsEnum.None, v1);

                Assert.True(del(((_TestFlagsEnum?)_TestFlagsEnum.Multi).ToString(), default, out var v2));
                Assert.Equal((_TestFlagsEnum?)_TestFlagsEnum.Multi, v2);

                Assert.True(del(((_TestFlagsEnum?)(_TestFlagsEnum.First | _TestFlagsEnum.Second)).ToString(), default, out var v3));
                Assert.Equal((_TestFlagsEnum?)(_TestFlagsEnum.First | _TestFlagsEnum.Second), v3);

                Assert.True(del("", default, out var v4));
                Assert.Equal((_TestFlagsEnum?)null, v4);

                Assert.False(del("foo", default, out _));

                Assert.False(del("123", default, out _));
            }

            // char?
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(char?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<char?>)Delegate.CreateDelegate(typeof(Parse<char?>), mtd);

                Assert.True(del("t", default, out var v1));
                Assert.Equal('t', v1.Value);

                Assert.True(del("", default, out var v2));
                Assert.Equal((char?)null, v2);

                Assert.False(del("foo", default, out _));
            }

            // bool?
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(bool?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<bool?>)Delegate.CreateDelegate(typeof(Parse<bool?>), mtd);

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
                var mtd = DeserializableMember.GetDefaultParser(typeof(byte?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<byte?>)Delegate.CreateDelegate(typeof(Parse<byte?>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((byte?)123, v1);

                Assert.True(del("", default, out var v2));
                Assert.Equal((byte?)null, v2);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // sbyte?
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(sbyte?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<sbyte?>)Delegate.CreateDelegate(typeof(Parse<sbyte?>), mtd);

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
                var mtd = DeserializableMember.GetDefaultParser(typeof(short?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<short?>)Delegate.CreateDelegate(typeof(Parse<short?>), mtd);

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
                var mtd = DeserializableMember.GetDefaultParser(typeof(ushort?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<ushort?>)Delegate.CreateDelegate(typeof(Parse<ushort?>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((ushort?)123, v1);

                Assert.True(del("", default, out var v2));
                Assert.Equal((ushort?)null, v2);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // int?
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(int?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<int?>)Delegate.CreateDelegate(typeof(Parse<int?>), mtd);

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
                var mtd = DeserializableMember.GetDefaultParser(typeof(uint?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<uint?>)Delegate.CreateDelegate(typeof(Parse<uint?>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((uint?)123, v1);

                Assert.True(del("", default, out var v2));
                Assert.Equal((uint?)null, v2);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // long?
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(long?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<long?>)Delegate.CreateDelegate(typeof(Parse<long?>), mtd);

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
                var mtd = DeserializableMember.GetDefaultParser(typeof(ulong?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<ulong?>)Delegate.CreateDelegate(typeof(Parse<ulong?>), mtd);

                Assert.True(del("123", default, out var v1));
                Assert.Equal((ulong?)123, v1);

                Assert.True(del("", default, out var v2));
                Assert.Equal((ulong?)null, v2);

                Assert.False(del("-123", default, out _));

                Assert.False(del("foo", default, out _));
            }

            // float?
            {
                var mtd = DeserializableMember.GetDefaultParser(typeof(float?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<float?>)Delegate.CreateDelegate(typeof(Parse<float?>), mtd);

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
                var mtd = DeserializableMember.GetDefaultParser(typeof(double?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<double?>)Delegate.CreateDelegate(typeof(Parse<double?>), mtd);

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
                var mtd = DeserializableMember.GetDefaultParser(typeof(decimal?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<decimal?>)Delegate.CreateDelegate(typeof(Parse<decimal?>), mtd);

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

                var mtd = DeserializableMember.GetDefaultParser(typeof(Guid?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<Guid?>)Delegate.CreateDelegate(typeof(Parse<Guid?>), mtd);

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
                var mtd = DeserializableMember.GetDefaultParser(typeof(TimeSpan?).GetTypeInfo());
                Assert.NotNull(mtd);

                var del = (Parse<TimeSpan?>)Delegate.CreateDelegate(typeof(Parse<TimeSpan?>), mtd);

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
        }

        class _Deserialize
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
                a => Assert.Same(typeof(_Deserialize).GetProperty(nameof(_Deserialize.ShouldBePresent1)).SetMethod, a.Setter),
                a => Assert.Same(typeof(_Deserialize).GetProperty(nameof(_Deserialize.ShouldBePresent2)).SetMethod, a.Setter)
            );
            
            // parser
            Assert.Collection(
                cols,
                a => Assert.Same(DeserializableMember.GetDefaultParser(typeof(string).GetTypeInfo()), a.Parser),
                a => Assert.Same(DeserializableMember.GetDefaultParser(typeof(int?).GetTypeInfo()), a.Parser)
            );

            var mtd = typeof(_Deserialize).GetMethod("ResetShouldBePresent2", BindingFlags.Instance | BindingFlags.NonPublic);

            // reset 
            Assert.Collection(
                cols,
                a => Assert.Null(a.Reset),
                a => Assert.Same(mtd, a.Reset)
            );
        }

        class _DeserializeDataMember
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
                a => Assert.Same(typeof(_DeserializeDataMember).GetField("Field"), a.Field),
                a => Assert.Same(typeof(_DeserializeDataMember).GetProperty(nameof(_DeserializeDataMember.Hello)).SetMethod, a.Setter),
                a => Assert.Same(typeof(_DeserializeDataMember).GetProperty(nameof(_DeserializeDataMember.Foo)).SetMethod, a.Setter),
                a => Assert.Same(typeof(_DeserializeDataMember).GetProperty(nameof(_DeserializeDataMember.Yeaaaah)).SetMethod, a.Setter)
            );

            // setters actually work
            {
                // Field
                {
                    var x = new _DeserializeDataMember();
                    var field = cols[0].Field;
                    field.SetValue(x, (int?)-123);
                    Assert.Equal((int?)-123, x.Field);
                }

                // HELLO
                {
                    var x = new _DeserializeDataMember();
                    var hello = cols[1].Setter;
                    hello.Invoke(x, new object[] { (double)1.23 });
                    Assert.Equal(1.23, x.Hello);
                }

                // Foo
                {
                    var x = new _DeserializeDataMember();
                    var foo = cols[2].Setter;
                    foo.Invoke(x, new object[] { "bar" });
                    Assert.Equal("bar", x.Foo);
                }

                // Yeaaaah
                {
                    var x = new _DeserializeDataMember();
                    var yeaaaah = cols[3].Setter;
                    yeaaaah.Invoke(x, new object[] { (decimal?)12.34m });
                    Assert.Equal((decimal?)12.34m, x.GetYeah());
                }
            }

            // parser
            Assert.Collection(
                cols,
                a => Assert.Same(DeserializableMember.GetDefaultParser(typeof(int?).GetTypeInfo()), a.Parser),
                a => Assert.Same(DeserializableMember.GetDefaultParser(typeof(double).GetTypeInfo()), a.Parser),
                a => Assert.Same(DeserializableMember.GetDefaultParser(typeof(string).GetTypeInfo()), a.Parser),
                a => Assert.Same(DeserializableMember.GetDefaultParser(typeof(decimal?).GetTypeInfo()), a.Parser)
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

        class CharWriter: IBufferWriter<char>
        {
            private readonly PipeWriter Inner;

            public CharWriter(PipeWriter inner)
            {
                Inner = inner;
            }

            public void Advance(int count)
            => Inner.Advance(count * sizeof(char));

            public Memory<char> GetMemory(int sizeHint = 0)
            {
                throw new NotImplementedException();
            }

            public Span<char> GetSpan(int sizeHint = 0)
            {
                var bytes = Inner.GetSpan(sizeHint * sizeof(char));
                var chars = MemoryMarshal.Cast<byte, char>(bytes);

                return chars;
            }

            public ValueTask<FlushResult> FlushAsync()
            => Inner.FlushAsync();
        }

        [Fact]
        public async Task SerializableMember_GetDefaultFormatter()
        {
            string BufferToString(ReadOnlySequence<byte> buff)
            {
                var bytes = new List<byte>();
                foreach(var b in buff)
                {
                    bytes.AddRange(b.ToArray());
                }

                var byteArray = bytes.ToArray();
                var byteSpan = new Span<byte>(byteArray);
                var charSpan = MemoryMarshal.Cast<byte, char>(byteSpan);

                return new string(charSpan);
            }

            // string
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = SerializableMember.GetDefaultFormatter(typeof(string).GetTypeInfo());
                var res = mtd.Invoke(null, new object[] { "foo", default(WriteContext), writer });
                var resBool = (bool)res;
                Assert.True(resBool);

                await writer.FlushAsync();

                Assert.True(reader.TryRead(out var buff));
                Assert.Equal("foo", BufferToString(buff.Buffer));
                reader.AdvanceTo(buff.Buffer.End);
            }

            // enum
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = SerializableMember.GetDefaultFormatter(typeof(_TestEnum).GetTypeInfo());

                // Bar
                {
                    var res = mtd.Invoke(null, new object[] { _TestEnum.Bar, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.Bar.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // Foo
                {
                    var res = mtd.Invoke(null, new object[] { _TestEnum.Foo, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.Foo.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // None
                {
                    var res = mtd.Invoke(null, new object[] { _TestEnum.None, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.None.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // bad value
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum)int.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.False(resBool);
                }
            }

            // flags enum
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = SerializableMember.GetDefaultFormatter(typeof(_TestFlagsEnum).GetTypeInfo());

                // First
                {
                    var res = mtd.Invoke(null, new object[] { _TestFlagsEnum.First, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestFlagsEnum.First.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // Multi
                {
                    var res = mtd.Invoke(null, new object[] { _TestFlagsEnum.Multi, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestFlagsEnum.Multi.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // First | Fourth
                {
                    var res = mtd.Invoke(null, new object[] { _TestFlagsEnum.First | _TestFlagsEnum.Fourth, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((_TestFlagsEnum.First | _TestFlagsEnum.Fourth).ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // bad value
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum)int.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.False(resBool);
                }
            }

            // char
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = SerializableMember.GetDefaultFormatter(typeof(char).GetTypeInfo());

                // value
                {
                    var res = mtd.Invoke(null, new object[] { 'D', default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("D", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // bool
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = SerializableMember.GetDefaultFormatter(typeof(bool).GetTypeInfo());

                // true
                {
                    var res = mtd.Invoke(null, new object[] { true, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("True", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // false
                {
                    var res = mtd.Invoke(null, new object[] { false, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(byte).GetTypeInfo());
                
                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (byte)123, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(sbyte).GetTypeInfo());
                
                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte)123, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte)-123, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(short).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { short.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("32767", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { short.MinValue, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(ushort).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { ushort.MaxValue, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(int).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { int.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("2147483647", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { int.MinValue, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(uint).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { uint.MaxValue, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(long).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { long.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("9223372036854775807", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { long.MinValue, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(ulong).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { ulong.MaxValue, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(float).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { 12.34f, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34f.ToString("G9"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { -12.34f, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(double).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { 12.34, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34.ToString("G17"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { -12.34, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34).ToString("G17"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // very long
                {
                    var res = mtd.Invoke(null, new object[] { 0.84551240822557006, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(decimal).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { 12.34m, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34m.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { -12.34m, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34m).ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // large
                {
                    var res = mtd.Invoke(null, new object[] { 79_228_162_514_264_337_593_543_950_335m, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(DateTime).GetTypeInfo());

                // max
                {
                    var res = mtd.Invoke(null, new object[] { DateTime.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTime.MaxValue.ToUniversalTime().ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { DateTime.MinValue, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(DateTimeOffset).GetTypeInfo());

                // max
                {
                    var res = mtd.Invoke(null, new object[] { DateTimeOffset.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTimeOffset.MaxValue.ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { DateTimeOffset.MinValue, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(TimeSpan).GetTypeInfo());

                // max
                {
                    var res = mtd.Invoke(null, new object[] { TimeSpan.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(TimeSpan.MaxValue.ToString("c"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { TimeSpan.MinValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(TimeSpan.MinValue.ToString("c"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }
        }

        [Fact]
        public async Task SerializableMember_GetDefaultFormatter_Nullable()
        {
            string BufferToString(ReadOnlySequence<byte> buff)
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

            // enum?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = SerializableMember.GetDefaultFormatter(typeof(_TestEnum?).GetTypeInfo());

                // Bar
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)_TestEnum.Bar, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.Bar.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // Foo
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)_TestEnum.Foo, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.Foo.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // None
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)_TestEnum.None, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestEnum.None.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)null, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }

                // bad value
                {
                    var res = mtd.Invoke(null, new object[] { (_TestEnum?)int.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.False(resBool);
                }
            }

            // flags enum?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = SerializableMember.GetDefaultFormatter(typeof(_TestFlagsEnum?).GetTypeInfo());

                // First
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum?)_TestFlagsEnum.First, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestFlagsEnum.First.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // Multi
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum?)_TestFlagsEnum.Multi, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(_TestFlagsEnum.Multi.ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // First | Fourth
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum?)_TestFlagsEnum.First | _TestFlagsEnum.Fourth, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((_TestFlagsEnum.First | _TestFlagsEnum.Fourth).ToString(), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum?)null, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }

                // bad value
                {
                    var res = mtd.Invoke(null, new object[] { (_TestFlagsEnum)int.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.False(resBool);
                }
            }

            // char?
            {
                var pipe = new Pipe();
                var writer = new CharWriter(pipe.Writer);
                var reader = pipe.Reader;

                var mtd = SerializableMember.GetDefaultFormatter(typeof(char?).GetTypeInfo());

                // value
                {
                    var res = mtd.Invoke(null, new object[] { (char?)'D', default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("D", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (char?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(bool?).GetTypeInfo());

                // true
                {
                    var res = mtd.Invoke(null, new object[] { (bool?)true, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("True", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // false
                {
                    var res = mtd.Invoke(null, new object[] { (bool?)false, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("False", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (bool?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(byte?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (byte?)123, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (byte?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(sbyte?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte?)123, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte?)-123, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-123", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (sbyte?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(short?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (short?)short.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("32767", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (short?)short.MinValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-32768", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (short?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(ushort?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (ushort?)ushort.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("65535", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (ushort?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(int?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (int?)int.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("2147483647", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (int?)int.MinValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-2147483648", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (int?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(uint?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (uint?)uint.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("4294967295", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (uint?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(long?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (long?)long.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("9223372036854775807", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (long?)long.MinValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("-9223372036854775808", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (long?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(ulong?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (ulong?)ulong.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("18446744073709551615", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (ulong?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(float?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (float?)12.34f, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34f.ToString("G9"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (float?)-12.34f, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34f).ToString("G9"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (float?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(double?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (double?)12.34, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34.ToString("G17"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (double?)-12.34, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34).ToString("G17"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // very long
                {
                    var res = mtd.Invoke(null, new object[] { 0.84551240822557006, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("0.84551240822557006", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (double?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(decimal?).GetTypeInfo());

                // positive
                {
                    var res = mtd.Invoke(null, new object[] { (decimal?)12.34m, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(12.34m.ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // negative
                {
                    var res = mtd.Invoke(null, new object[] { (decimal?)-12.34m, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((-12.34m).ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // large
                {
                    var res = mtd.Invoke(null, new object[] { (decimal?)79_228_162_514_264_337_593_543_950_335m, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal((79_228_162_514_264_337_593_543_950_335m).ToString(CultureInfo.InvariantCulture), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (decimal?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(DateTime?).GetTypeInfo());

                // max
                {
                    var res = mtd.Invoke(null, new object[] { (DateTime?)DateTime.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTime.MaxValue.ToUniversalTime().ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { (DateTime?)DateTime.MinValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTime.MinValue.ToUniversalTime().ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (DateTime?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(DateTimeOffset?).GetTypeInfo());

                // max
                {
                    var res = mtd.Invoke(null, new object[] { (DateTimeOffset?)DateTimeOffset.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTimeOffset.MaxValue.ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { (DateTimeOffset?)DateTimeOffset.MinValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(DateTimeOffset.MinValue.ToString("u"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (DateTimeOffset?)null, default(WriteContext), writer });
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

                var mtd = SerializableMember.GetDefaultFormatter(typeof(TimeSpan?).GetTypeInfo());

                // max
                {
                    var res = mtd.Invoke(null, new object[] { (TimeSpan?)TimeSpan.MaxValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(TimeSpan.MaxValue.ToString("c"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // min
                {
                    var res = mtd.Invoke(null, new object[] { (TimeSpan?)TimeSpan.MinValue, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal(TimeSpan.MinValue.ToString("c"), BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                // null
                {
                    var res = mtd.Invoke(null, new object[] { (TimeSpan?)null, default(WriteContext), writer });
                    var resBool = (bool)res;
                    Assert.True(resBool);

                    await writer.FlushAsync();

                    Assert.False(reader.TryRead(out _));
                }
            }
        }

        class _Serialize
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
                a => Assert.Same(typeof(_Serialize).GetProperty(nameof(_Serialize.GetButNoSet)).GetMethod, a.Getter),
                a => Assert.Same(typeof(_Serialize).GetProperty(nameof(_Serialize.GetAndSet)).GetMethod, a.Getter),
                a => Assert.Same(typeof(_Serialize).GetProperty(nameof(_Serialize.ShouldSerializeProp)).GetMethod, a.Getter),
                a => Assert.Same(typeof(_Serialize).GetProperty(nameof(_Serialize.ShouldSerializeStaticProp)).GetMethod, a.Getter)
            );

            // should serialize
            Assert.Collection(
                cols,
                a => Assert.Null(a.ShouldSerialize),
                b => Assert.Null(b.ShouldSerialize),
                c => Assert.Same(typeof(_Serialize).GetMethod(nameof(_Serialize.ShouldSerializeShouldSerializeProp)), c.ShouldSerialize),
                d => Assert.Same(typeof(_Serialize).GetMethod(nameof(_Serialize.ShouldSerializeShouldSerializeStaticProp)), d.ShouldSerialize)
            );

            // formatter
            Assert.Collection(
                cols,
                a => Assert.Same(SerializableMember.GetDefaultFormatter(typeof(int).GetTypeInfo()), a.Formatter),
                b => Assert.Same(SerializableMember.GetDefaultFormatter(typeof(string).GetTypeInfo()), b.Formatter),
                c => Assert.Same(SerializableMember.GetDefaultFormatter(typeof(char?).GetTypeInfo()), c.Formatter),
                d => Assert.Same(SerializableMember.GetDefaultFormatter(typeof(DateTimeOffset).GetTypeInfo()), d.Formatter)
            );
        }

        class _SerializeDataMember
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
                a => Assert.Same(typeof(_SerializeDataMember).GetField("Field"), a.Field),
                a => Assert.Same(typeof(_SerializeDataMember).GetProperty("WORLD").GetMethod, a.Getter),
                a => Assert.Same(typeof(_SerializeDataMember).GetProperty("Bar").GetMethod, a.Getter),
                a => Assert.Same(typeof(_SerializeDataMember).GetProperty("Yeaaaah", BindingFlags.Instance | BindingFlags.NonPublic).GetMethod, a.Getter)
            );

            // should serialize
            Assert.Collection(
                cols,
                a => Assert.Null(a.ShouldSerialize),
                a => Assert.Same(typeof(_SerializeDataMember).GetMethod(nameof(_SerializeDataMember.ShouldSerializeWORLD)), a.ShouldSerialize),
                a => Assert.Null(a.ShouldSerialize),
                a => Assert.Null(a.ShouldSerialize)
            );

            // formatter
            Assert.Collection(
                cols,
                a => Assert.Same(SerializableMember.GetDefaultFormatter(typeof(int?).GetTypeInfo()), a.Formatter),
                a => Assert.Same(SerializableMember.GetDefaultFormatter(typeof(double).GetTypeInfo()), a.Formatter),
                a => Assert.Same(SerializableMember.GetDefaultFormatter(typeof(string).GetTypeInfo()), a.Formatter),
                a => Assert.Same(SerializableMember.GetDefaultFormatter(typeof(StringComparison).GetTypeInfo()), a.Formatter)
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
    }
#pragma warning restore IDE1006
}
