using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Cesil.SourceGenerator.Tests
{
    public class SerializerGenerationTests
    {
        [Fact]
        public async Task SimpleAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.WriteMe",
                @"
using System;
using System.Buffers;
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class WriteMe
    {
        [SerializerMember(FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForInt))]
        public int Bar { get; set; }
        [SerializerMember(FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForString))]
        public string Fizz = """";
        [SerializerMember(Name=""Hello"", FormatterType=typeof(WriteMe), FormatterMethodName=nameof(ForDateTime))]
        public DateTime SomeMtd() => new DateTime(2020, 11, 15, 0, 0, 0);

        public WriteMe() { }

        public static bool ForInt(int val, in WriteContext ctx, IBufferWriter<char> buffer)
        {
            var span = buffer.GetSpan(100);
            if(!val.TryFormat(span, out var written))
            {
                return false;
            }

            buffer.Advance(written);
            return true;
        }

        public static bool ForString(string val, in WriteContext ctx, IBufferWriter<char> buffer)
        {
            var span = buffer.GetSpan(val.Length);
            val.AsSpan().CopyTo(span);

            buffer.Advance(val.Length);
            return true;
        }

        public static bool ForDateTime(DateTime val, in WriteContext ctx, IBufferWriter<char> buffer)
        {
            var span = buffer.GetSpan(4);
            if(!val.Year.TryFormat(span, out var written))
            {
                return false;
            }

            buffer.Advance(written);
            return true;
        }
    }
}");

            var members = TypeDescribers.AheadOfTime.EnumerateMembersToSerialize(type);
            Assert.Collection(
                members,
                bar =>
                {
                    Assert.True(bar.IsBackedByGeneratedMethod);
                    Assert.Equal("Bar", bar.Name);
                    Assert.True(bar.EmitDefaultValue);
                    Assert.Equal("__Column_0_Formatter", bar.Formatter.Method.Value.Name);
                    Assert.Equal("__Column_0_Getter", bar.Getter.Method.Value.Name);
                    Assert.False(bar.ShouldSerialize.HasValue);
                },
                fizz =>
                {
                    Assert.True(fizz.IsBackedByGeneratedMethod);
                    Assert.Equal("Fizz", fizz.Name);
                    Assert.True(fizz.EmitDefaultValue);
                    Assert.Equal("__Column_1_Formatter", fizz.Formatter.Method.Value.Name);
                    Assert.Equal("__Column_1_Getter", fizz.Getter.Method.Value.Name);
                    Assert.False(fizz.ShouldSerialize.HasValue);
                },
                hello =>
                {
                    Assert.True(hello.IsBackedByGeneratedMethod);
                    Assert.Equal("Hello", hello.Name);
                    Assert.True(hello.EmitDefaultValue);
                    Assert.Equal("__Column_2_Formatter", hello.Formatter.Method.Value.Name);
                    Assert.Equal("__Column_2_Getter", hello.Getter.Method.Value.Name);
                    Assert.False(hello.ShouldSerialize.HasValue);
                }
            );

            var rows =
                Create(
                    type,
                    r => { r.Bar = 123; r.Fizz = "abcd"; },
                    r => { r.Bar = 456; r.Fizz = "hello world"; },
                    r => { r.Bar = 789; r.Fizz = ""; }
                );

            var csv = Write(type, rows);
            Assert.Equal("Bar,Fizz,Hello\r\n123,abcd,2020\r\n456,hello world,2020\r\n789,,2020", csv);
        }

        [Fact]
        public async Task AllDefaultsAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.Everything",
                @"
using System;
using Cesil;

namespace Foo 
{   
    [Flags]
    public enum WideRowFlagsEnum
    {
        Empty = 0,

        Hello = 1 << 0,
        World = 1 << 1
    }

    public enum WideRowEnum
    {
        None = 0,

        Foo = 1,
        Fizz = 2,
        Bar = 3
    }

    [GenerateSerializer]
    public class Everything
    {
        [SerializerMember]
        public bool Bool { get; set; }
        [SerializerMember]
        public byte Byte { get; set; }
        [SerializerMember]
        public sbyte SByte { get; set; }
        [SerializerMember]
        public short Short { get; set; }
        [SerializerMember]
        public ushort UShort { get; set; }
        [SerializerMember]
        public int Int { get; set; }
        [SerializerMember]
        public uint UInt { get; set; }
        [SerializerMember]
        public long Long { get; set; }
        [SerializerMember]
        public ulong ULong { get; set; }
        [SerializerMember]
        public float Float { get; set; }
        [SerializerMember]
        public double Double { get; set; }
        [SerializerMember]
        public decimal Decimal { get; set; }

        [SerializerMember]
        public bool? NullableBool { get; set; }
        [SerializerMember]
        public byte? NullableByte { get; set; }
        [SerializerMember]
        public sbyte? NullableSByte { get; set; }
        [SerializerMember]
        public short? NullableShort { get; set; }
        [SerializerMember]
        public ushort? NullableUShort { get; set; }
        [SerializerMember]
        public int? NullableInt { get; set; }
        [SerializerMember]
        public uint? NullableUInt { get; set; }
        [SerializerMember]
        public long? NullableLong { get; set; }
        [SerializerMember]
        public ulong? NullableULong { get; set; }
        [SerializerMember]
        public float? NullableFloat { get; set; }
        [SerializerMember]
        public double? NullableDouble { get; set; }
        [SerializerMember]
        public decimal? NullableDecimal { get; set; }

        [SerializerMember]
        public string? String { get; set; }

        [SerializerMember]
        public char Char { get; set; }

        [SerializerMember]
        public char? NullableChar { get; set; }

        [SerializerMember]
        public Guid Guid { get; set; }
        [SerializerMember]
        public Guid? NullableGuid { get; set; }

        [SerializerMember]
        public DateTime DateTime { get; set; }
        [SerializerMember]
        public DateTimeOffset DateTimeOffset { get; set; }

        [SerializerMember]
        public DateTime? NullableDateTime { get; set; }
        [SerializerMember]
        public DateTimeOffset? NullableDateTimeOffset { get; set; }

        [SerializerMember]
        public Uri? Uri { get; set; }

        [SerializerMember]
        public TimeSpan TimeSpan { get; set; }

        [SerializerMember]
        public TimeSpan? NullableTimeSpan { get; set; }

        [SerializerMember]
        public WideRowEnum Enum { get; set; }
        [SerializerMember]
        public WideRowFlagsEnum FlagsEnum { get; set; }

        [SerializerMember]
        public WideRowEnum? NullableEnum { get; set; }
        [SerializerMember]
        public WideRowFlagsEnum? NullableFlagsEnum { get; set; }

        public Everything() { }
    }
}");
            var wideRowEnumType = type.Assembly.GetTypes().Single(t => t.Name == "WideRowEnum");
            var wideRowEnumValues = Enum.GetValues(wideRowEnumType);

            var wideRowFlagsEnumType = type.Assembly.GetTypes().Single(t => t.Name == "WideRowFlagsEnum");
            var wideRowFlagsEnumValues = Enum.GetValues(wideRowFlagsEnumType);

            var nullableType = typeof(Nullable<>).GetTypeInfo();
            var nullableWideRowEnumCons = nullableType.MakeGenericType(wideRowEnumType).GetConstructor(new[] { wideRowEnumType });
            var nullableWideRowFlagsEnumCons = nullableType.MakeGenericType(wideRowFlagsEnumType).GetConstructor(new[] { wideRowFlagsEnumType });

            var enumProp = type.GetProperty("Enum");
            var flagsEnumProp = type.GetProperty("FlagsEnum");
            var nullableEnumProp = type.GetProperty("NullableEnum");
            var nullableFlagsEnumProp = type.GetProperty("NullableFlagsEnum");

            var rows = Create(
                type,
                row1 =>
                {
                    row1.Bool = true;
                    row1.Byte = (byte)1;
                    row1.SByte = (sbyte)-1;
                    row1.Short = (short)-11;
                    row1.UShort = (ushort)11;
                    row1.Int = (int)-111;
                    row1.UInt = (uint)111;
                    row1.Long = (long)-1111;
                    row1.ULong = (ulong)1111;
                    row1.Float = (float)1.2f;
                    row1.Double = (double)3.4;
                    row1.Decimal = (decimal)4.5m;

                    row1.NullableBool = false;
                    row1.NullableByte = (byte)2;
                    row1.NullableSByte = (sbyte)-2;
                    row1.NullableShort = (short)-22;
                    row1.NullableUShort = (ushort)22;
                    row1.NullableInt = (int)-222;
                    row1.NullableUInt = (uint)222;
                    row1.NullableLong = (long)-2222;
                    row1.NullableULong = (ulong)2222;
                    row1.NullableFloat = (float)6.7f;
                    row1.NullableDouble = (double)8.9;
                    row1.NullableDecimal = (decimal)0.1m;

                    row1.String = "hello";

                    row1.Char = 'a';
                    row1.NullableChar = 'b';

                    row1.DateTime = new DateTime(2020, 11, 15, 0, 0, 0, DateTimeKind.Utc);
                    row1.DateTimeOffset = new DateTimeOffset(2020, 11, 15, 0, 0, 0, TimeSpan.Zero);

                    row1.NullableDateTime = new DateTime(2021, 11, 15, 0, 0, 0, DateTimeKind.Utc);
                    row1.NullableDateTimeOffset = new DateTimeOffset(2021, 11, 15, 0, 0, 0, TimeSpan.Zero);

                    row1.Uri = new Uri("https://example.com/example");

                    row1.TimeSpan = new TimeSpan(1, 2, 3);
                    row1.NullableTimeSpan = new TimeSpan(4, 5, 6);

                    row1.Guid = Guid.Parse("6E3687AF-99A8-4415-9CDE-C0D90D182171");
                    row1.NullableGuid = Guid.Parse("7E3687AF-99A8-4415-9CDE-C0D90D182171");

                    enumProp.SetValue(row1, wideRowEnumValues.GetValue(0));
                    flagsEnumProp.SetValue(row1, wideRowFlagsEnumValues.GetValue(0));

                    nullableEnumProp.SetValue(row1, wideRowEnumValues.GetValue(1));
                    nullableFlagsEnumProp.SetValue(row1, wideRowFlagsEnumValues.GetValue(1));
                },
                row2 =>
                {
                    row2.Bool = false;
                    row2.Byte = (byte)3;
                    row2.SByte = (sbyte)-3;
                    row2.Short = (short)-33;
                    row2.UShort = (ushort)33;
                    row2.Int = (int)-333;
                    row2.UInt = (uint)333;
                    row2.Long = (long)-3333;
                    row2.ULong = (ulong)3333;
                    row2.Float = (float)2.3f;
                    row2.Double = (double)4.5;
                    row2.Decimal = (decimal)6.7m;

                    row2.NullableBool = (bool?)null;
                    row2.NullableByte = (byte?)null;
                    row2.NullableSByte = (sbyte?)null;
                    row2.NullableShort = (short?)null;
                    row2.NullableUShort = (ushort?)null;
                    row2.NullableInt = (int?)null;
                    row2.NullableUInt = (uint?)null;
                    row2.NullableLong = (long?)null;
                    row2.NullableULong = (ulong?)null;
                    row2.NullableFloat = (float?)null;
                    row2.NullableDouble = (double?)null;
                    row2.NullableDecimal = (decimal?)null;

                    row2.String = null;

                    row2.Char = 'c';
                    row2.NullableChar = (char?)null;

                    row2.DateTime = new DateTime(2022, 11, 15, 0, 0, 0, DateTimeKind.Utc);
                    row2.DateTimeOffset = new DateTimeOffset(2022, 11, 15, 0, 0, 0, TimeSpan.Zero);

                    row2.NullableDateTime = (DateTime?)null;
                    row2.NullableDateTimeOffset = (DateTimeOffset?)null;

                    row2.Uri = null;

                    row2.TimeSpan = new TimeSpan(7, 8, 9);
                    row2.NullableTimeSpan = (TimeSpan?)null;

                    row2.Guid = Guid.Parse("8E3687AF-99A8-4415-9CDE-C0D90D182171");
                    row2.NullableGuid = (Guid?)null;

                    enumProp.SetValue(row2, wideRowEnumValues.GetValue(1));
                    flagsEnumProp.SetValue(row2, wideRowFlagsEnumValues.GetValue(1));

                    nullableEnumProp.SetValue(row2, null);
                    nullableFlagsEnumProp.SetValue(row2, null);
                }
            );

            var csv = Write(type, rows);

            Assert.Equal("Bool,Byte,SByte,Short,UShort,Int,UInt,Long,ULong,Float,Double,Decimal,NullableBool,NullableByte,NullableSByte,NullableShort,NullableUShort,NullableInt,NullableUInt,NullableLong,NullableULong,NullableFloat,NullableDouble,NullableDecimal,String,Char,NullableChar,Guid,NullableGuid,DateTime,DateTimeOffset,NullableDateTime,NullableDateTimeOffset,Uri,TimeSpan,NullableTimeSpan,Enum,FlagsEnum,NullableEnum,NullableFlagsEnum\r\nTrue,1,-1,-11,11,-111,111,-1111,1111,1.20000005,3.3999999999999999,4.5,False,2,-2,-22,22,-222,222,-2222,2222,6.69999981,8.9000000000000004,0.1,hello,a,b,6e3687af-99a8-4415-9cde-c0d90d182171,7e3687af-99a8-4415-9cde-c0d90d182171,2020-11-15 00:00:00Z,2020-11-15 00:00:00Z,2021-11-15 00:00:00Z,2021-11-15 00:00:00Z,https://example.com/example,01:02:03,04:05:06,None,Empty,Foo,Hello\r\nFalse,3,-3,-33,33,-333,333,-3333,3333,2.29999995,4.5,6.7,,,,,,,,,,,,,,c,,8e3687af-99a8-4415-9cde-c0d90d182171,,2022-11-15 00:00:00Z,2022-11-15 00:00:00Z,,,,07:08:09,,Foo,Hello,,", csv);
        }

        [Fact]
        public async Task ShouldSerializeAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.ShouldSerializeAsync",
                @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class ShouldSerializeAsync
    {
        public bool SerializeByte;
        public static bool SerializeInt;

        [SerializerMember(ShouldSerializeType = typeof(ShouldSerializeAsync), ShouldSerializeMethodName=""ShouldSerializeByte"")]
        public byte Byte { get; set; }
        
        [SerializerMember(ShouldSerializeType = typeof(ShouldSerializeAsync), ShouldSerializeMethodName=""ShouldSerializeInt"")]
        public int Int { get; set; }

        [SerializerMember(ShouldSerializeType = typeof(ShouldSerializeAsync), ShouldSerializeMethodName=""ShouldSerializeShort"")]
        public short Short { get; set; }

        [SerializerMember(ShouldSerializeType = typeof(ShouldSerializeAsync), ShouldSerializeMethodName=""ShouldSerializeLong"")]
        public long Long { get; set; }

        [SerializerMember(ShouldSerializeType = typeof(ShouldSerializeAsync), ShouldSerializeMethodName=""ShouldSerializeDouble"")]
        public double Double { get; set; }

        internal bool ShouldSerializeByte()
        => SerializeByte;

        internal static bool ShouldSerializeInt()
        => SerializeInt;

        internal bool ShouldSerializeShort(in WriteContext ctx)
        => (ctx.RowNumber % 2) == 0;

        internal static bool ShouldSerializeLong(ShouldSerializeAsync row)
        => (row.Long % 2) == 0;

        internal static bool ShouldSerializeDouble(ShouldSerializeAsync row, in WriteContext ctx)
        => (((int)(ctx.RowNumber + row.Double)) % 2) == 1;
    }
}");
            var serializeIntProp = type.GetField("SerializeInt");

            var rows = Create(
                type,
                row1 =>
                {
                    row1.Byte = (byte)1;
                    row1.SerializeByte = true;
                    
                    row1.Int = (int)2;

                    row1.Short = (short)3;

                    row1.Long = (long)4;

                    row1.Double = (double)5.5;
                },
                row2 =>
                {
                    row2.Byte = (byte)2;
                    row2.SerializeByte = false;

                    row2.Int = (int)3;

                    row2.Short = (short)4;

                    row2.Long = (long)5;

                    row2.Double = (double)7.7;
                },
                row3 =>
                {
                    row3.Byte = (byte)3;
                    row3.SerializeByte = true;

                    row3.Int = (int)4;

                    row3.Short = (short)5;

                    row3.Long = (long)6;

                    row3.Double = (double)9.9;
                }
            );

            var csv1 = Write(type, rows);
            serializeIntProp.SetValue(null, true);
            var csv2 = Write(type, rows);

            Assert.Equal("Byte,Int,Short,Long,Double\r\n1,,3,4,5.5\r\n,,,,\r\n3,,5,6,9.9000000000000004", csv1);
            Assert.Equal("Byte,Int,Short,Long,Double\r\n1,2,3,4,5.5\r\n,3,,,\r\n3,4,5,6,9.9000000000000004", csv2);
        }

        [Fact]
        public async Task GetterAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.GetterAsync",
                @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class GetterAsync
    {
        [SerializerMember]
        public string Field = """";

        [SerializerMember]
        public string Prop { get; set; } = """";

        [SerializerMember(Name = ""InstanceMethod1"")]
        public string InstanceMethod() => ""foo"";

        [SerializerMember(Name = ""InstanceMethod2"")]
        public string InstanceMethod(in WriteContext ctx) => ""bar""+ctx.RowNumber;

        [SerializerMember(Name = ""StaticMethod1"")]
        public static string StaticMethod() => ""fizz"";

        [SerializerMember(Name = ""StaticMethod2"")]
        public static string StaticMethod(GetterAsync row) => ""buzz"";

        [SerializerMember(Name = ""StaticMethod3"")]
        public static string StaticMethod(in WriteContext ctx) => ""hello""+ctx.RowNumber;

        [SerializerMember(Name = ""StaticMethod4"")]
        public static string StaticMethod(GetterAsync row, in WriteContext ctx) => ""world""+ctx.RowNumber;
    }
}");
            var rows = Create(
                type,
                row1 =>
                {
                    row1.Field = "abcd";
                    row1.Prop = "efgh";
                },
                row2 =>
                {
                    row2.Field = "ijkl";
                    row2.Prop = "mnop";
                }
            );

            var csv = Write(type, rows);

            Assert.Equal("Field,Prop,InstanceMethod1,InstanceMethod2,StaticMethod1,StaticMethod2,StaticMethod3,StaticMethod4\r\nabcd,efgh,foo,bar0,fizz,buzz,hello0,world0\r\nijkl,mnop,foo,bar1,fizz,buzz,hello1,world1", csv);
        }

        [Fact]
        public async Task OrderAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.OrderAsync",
                @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class OrderAsync
    {
        [SerializerMember(Order = 1)]
        public string? A;

        [SerializerMember(Order = 0)]
        public string? B;

        [SerializerMember(Order = 999)]
        public string? C;

        [SerializerMember]
        public string? D;

        [SerializerMember(Order = 7)]
        public string? E;
    }
}");
            var rows = Create(
                type,
                row1 =>
                {
                    row1.A = "a";
                    row1.B = "b";
                    row1.C = "c";
                    row1.D = "d";
                    row1.E = "e";
                },
                row2 =>
                {
                    row2.A = "1";
                    row2.B = "2";
                    row2.C = "3";
                    row2.D = "4";
                    row2.E = "5";
                }
            );

            var csv = Write(type, rows);

            Assert.Equal("B,A,E,C,D\r\nb,a,e,c,d\r\n2,1,5,3,4", csv);
        }

        [Fact]
        public async Task EmitDefaultValueAsync()
        {
            var type = await RunSourceGeneratorAsync(
                "Foo.EmitDefaultValueAsync",
                @"
using System;
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class EmitDefaultValueAsync
    {
        [SerializerMember(EmitDefaultValue = EmitDefaultValue.No)]
        public int A { get; set; }

        [SerializerMember(EmitDefaultValue = EmitDefaultValue.No)]
        public int? B { get; set; }

        [SerializerMember(EmitDefaultValue = EmitDefaultValue.No)]
        public string? C { get; set; }

        [SerializerMember(EmitDefaultValue = EmitDefaultValue.No)]
        public Guid D { get; set; }

        [SerializerMember(EmitDefaultValue = EmitDefaultValue.No)]
        public Guid? E { get; set; }
    }
}");
            var rows = Create(
                type,
                row1 =>
                {
                    row1.A = 1;
                    row1.B = 0;
                    row1.C = "hello";
                    row1.D = Guid.Parse("35C48B77-D29C-452A-813B-6BA851A8F485");
                    row1.E = (Guid?)Guid.Parse("45C48B77-D29C-452A-813B-6BA851A8F485");
                },
                row2 =>
                {
                    row2.A = 0;
                    row2.B = (int?)null;
                    row2.C = (string)null;
                    row2.D = new Guid();
                    row2.E = (Guid?)null;
                },
                row3 =>
                {
                    row3.A = 1;
                    row3.B = (int?)null;
                    row3.C = "foo";
                    row3.D = new Guid();
                    row3.E = (Guid?)Guid.Parse("45C48B77-D29C-452A-813B-6BA851A8F485");
                }
            );

            var csv = Write(type, rows);

            Assert.Equal("A,B,C,D,E\r\n1,0,hello,35c48b77-d29c-452a-813b-6ba851a8f485,45c48b77-d29c-452a-813b-6ba851a8f485\r\n,,,,\r\n1,,foo,,45c48b77-d29c-452a-813b-6ba851a8f485", csv);
        }


        [Fact]
        public async Task VaryingNullableAnnotationsAsync()
        {
            // disabled
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.DisabledNullableAnnotationsAsync",
                    @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class DisabledNullableAnnotationsAsync
    {
        [SerializerMember]
        public string A { get; set; } = null;
    }
}",
                    NullableContextOptions.Disable
                );

                var rows = Create(
                    type,
                    row1 =>
                    {
                        row1.A = "hello";
                    },
                    row2 =>
                    {
                        row2.A = null;
                    },
                    row3 =>
                    {
                        row3.A = "";
                    }
                );

                var csv = Write(type, rows);

                Assert.Equal("A\r\nhello\r\n\r\n", csv);
            }

            // enabled
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.DisabledNullableAnnotationsAsync",
                    @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class DisabledNullableAnnotationsAsync
    {
        [SerializerMember]
        public string? A { get; set; } = null;
    }
}",
                    NullableContextOptions.Enable
                );

                var rows = Create(
                    type,
                    row1 =>
                    {
                        row1.A = "hello";
                    },
                    row2 =>
                    {
                        row2.A = null;
                    },
                    row3 =>
                    {
                        row3.A = "";
                    }
                );

                var csv = Write(type, rows);

                Assert.Equal("A\r\nhello\r\n\r\n", csv);
            }

            // annotations
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.DisabledNullableAnnotationsAsync",
                    @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class DisabledNullableAnnotationsAsync
    {
        [SerializerMember]
        public string? A { get; set; } = null;
        [SerializerMember]
        public string B { get; set; } = null;
    }
}",
                    NullableContextOptions.Annotations
                );

                var rows = Create(
                    type,
                    row1 =>
                    {
                        row1.A = "hello";
                        row1.B = "world";
                    },
                    row2 =>
                    {
                        row2.A = null;
                        row2.B = null;
                    },
                    row3 =>
                    {
                        row3.A = "";
                        row3.B = "";
                    }
                );

                var csv = Write(type, rows);

                Assert.Equal("A,B\r\nhello,world\r\n,\r\n,", csv);
            }

            // warnings
            {
                var type = await RunSourceGeneratorAsync(
                    "Foo.DisabledNullableAnnotationsAsync",
                    @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class DisabledNullableAnnotationsAsync
    {
        [SerializerMember]
        public string A { get; set; } = null;
    }
}",
                    NullableContextOptions.Warnings
                );

                var rows = Create(
                    type,
                    row1 =>
                    {
                        row1.A = "hello";
                    },
                    row2 =>
                    {
                        row2.A = null;
                    },
                    row3 =>
                    {
                        row3.A = "";
                    }
                );

                var csv = Write(type, rows);

                Assert.Equal("A\r\nhello\r\n\r\n", csv);
            }
        }

        [Fact]
        public async Task DefaultIncludedMembersAsync()
        {
            // public properties included by default, but can be ignored
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.IgnoreMembersAsync",
                        @"
using Cesil;
using System.Runtime.Serialization;

namespace Foo 
{   
    [GenerateSerializer]
    public class IgnoreMembersAsync
    {
        public string? A { get; set; }

        [IgnoreDataMember]
        public string? B { get; set; }

        public string? C { get; set; }
    }
}"
                    );

                var rows = Create(
                        type,
                        row1 =>
                        {
                            row1.A = "hello";
                            row1.B = "fizz";
                            row1.C = "foo";
                        },
                        row2 =>
                        {
                            row2.A = "world";
                            row2.B = "buzz";
                            row2.C = "bar";
                        }
                    );

                var csv = Write(type, rows);

                Assert.Equal("A,C\r\nhello,foo\r\nworld,bar", csv);
            }

            // internal, private, and static properties ignored by default
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.IgnoreMembersAsync",
                        @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class IgnoreMembersAsync
    {
        public string? A { get; set; }

        internal string? B => ""foo"";

        private string? C => ""whatever"";

        public static string? D => ""nada"";

        public string? E { get; set; }
    }
}"
                    );

                var rows = Create(
                        type,
                        row1 =>
                        {
                            row1.A = "hello";
                            row1.E = "world";
                        },
                        row2 =>
                        {
                            row2.A = "fizz";
                            row2.E = "buzz";
                        }
                    );

                var csv = Write(type, rows);

                Assert.Equal("A,E\r\nhello,world\r\nfizz,buzz", csv);
            }

            // public property, but no getter, should be excluded
            {
                var type =
                    await RunSourceGeneratorAsync(
                        "Foo.IgnoreMembersAsync",
                        @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public class IgnoreMembersAsync
    {
        public string? A { get; set; }

        private string? _B;
        public string? B
        {
            set
            {
                _B = value;
            }
        }
    }
}"
                    );

                var rows = Create(
                        type,
                        row1 =>
                        {
                            row1.A = "hello";
                            row1.B = "world";
                        },
                        row2 =>
                        {
                            row2.A = "fizz";
                            row2.B = "buzz";
                        }
                    );

                var csv = Write(type, rows);

                Assert.Equal("A\r\nhello\r\nfizz", csv);
            }
        }

        [Fact]
        public async Task ValueTypeAsync()
        {
            var type =
                    await RunSourceGeneratorAsync(
                        "Foo.IgnoreMembersAsync",
                        @"
using Cesil;

namespace Foo 
{   
    [GenerateSerializer]
    public struct IgnoreMembersAsync
    {
        public string? A { get; set; }

        public string? B { get; set; }
    }
}"
                    );

            var rows = Create(
                    type,
                    row1 =>
                    {
                        row1.A = "hello";
                        row1.B = "world";
                    },
                    row2 =>
                    {
                        row2.A = "fizz";
                        row2.B = "buzz";
                    }
                );

            var csv = Write(type, rows);

            Assert.Equal("A,B\r\nhello,world\r\nfizz,buzz", csv);
        }

        private static string Write(System.Reflection.TypeInfo rowType, ImmutableArray<object> rows)
        {
            var writeImpl = WriteImplOfT.MakeGenericMethod(rowType);

            var ret = writeImpl.Invoke(null, new object[] { rows });

            return (string)ret;
        }

        private static readonly MethodInfo WriteImplOfT = typeof(SerializerGenerationTests).GetMethod(nameof(WriteImpl), BindingFlags.NonPublic | BindingFlags.Static);
        private static string WriteImpl<T>(ImmutableArray<object> rows)
        {
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(TypeDescribers.AheadOfTime).ToOptions();
            var config = Configuration.For<T>(opts);

            using (var str = new StringWriter())
            {
                using (var csv = config.CreateWriter(str))
                {
                    csv.WriteAll(rows.Cast<T>());

                }

                return str.ToString();
            }
        }

        private static ImmutableArray<dynamic> Create(System.Reflection.TypeInfo rowType, params Action<dynamic>[] callbacks)
        {
            var builder = ImmutableArray.CreateBuilder<dynamic>();

            foreach(var callback in callbacks)
            {
                var row = Activator.CreateInstance(rowType);
                callback(row);
                builder.Add(row);
            }

            return builder.ToImmutable();
        }

        private static async Task<System.Reflection.TypeInfo> RunSourceGeneratorAsync(
            string typeName,
            string testFile,
            NullableContextOptions nullableContext = NullableContextOptions.Enable,
            [CallerMemberName] string caller = null
        )
        {
            var serializer = new SerializerGenerator();

            var (producedCompilation, diagnostics) = await TestHelper.RunSourceGeneratorAsync(testFile, serializer, nullableContext, caller);

            Assert.Empty(diagnostics);

            var outputFile = Path.GetTempFileName();

            var res = producedCompilation.Emit(outputFile);

            Assert.Empty(res.Diagnostics);
            Assert.True(res.Success);
            
            var asm = Assembly.LoadFile(outputFile);
            var ret = Assert.Single(asm.GetTypes().Where(t => t.FullName == typeName));

            return ret.GetTypeInfo();
        }
    }
}
