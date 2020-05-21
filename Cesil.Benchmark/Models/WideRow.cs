using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Cesil.Benchmark
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

    public class WideRow : IEquatable<WideRow>
    {
        public static IEnumerable<WideRow> ShallowRows { get; private set; }
        public static IEnumerable<WideRow> DeepRows { get; private set; }

        [System.Runtime.Serialization.DataMember(Order = 0)]
        public byte Byte { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 1)]
        public sbyte SByte { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 2)]
        public short Short { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 3)]
        public ushort UShort { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 4)]
        public int Int { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 5)]
        public uint UInt { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 6)]
        public long Long { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 7)]
        public ulong ULong { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 8)]
        public float Float { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 9)]
        public double Double { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 10)]
        public decimal Decimal { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 11)]
        public byte? NullableByte { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 12)]
        public sbyte? NullableSByte { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 13)]
        public short? NullableShort { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 14)]
        public ushort? NullableUShort { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 15)]
        public int? NullableInt { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 16)]
        public uint? NullableUInt { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 17)]
        public long? NullableLong { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 18)]
        public ulong? NullableULong { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 19)]
        public float? NullableFloat { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 20)]
        public double? NullableDouble { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 21)]
        public decimal? NullableDecimal { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 22)]
        public string String { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 23)]
        public char Char { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 24)]
        public char? NullableChar { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 25)]
        public Guid Guid { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 26)]
        public Guid? NullableGuid { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 27)]
        public DateTime DateTime { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 28)]
        public DateTimeOffset DateTimeOffset { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 29)]
        public DateTime? NullableDateTime { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 30)]
        public DateTimeOffset? NullableDateTimeOffset { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 31)]
        public Uri Uri { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 32)]
        public WideRowEnum Enum { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 33)]
        public WideRowFlagsEnum FlagsEnum { get; set; }

        [System.Runtime.Serialization.DataMember(Order = 34)]
        public WideRowEnum? NullableEnum { get; set; }
        [System.Runtime.Serialization.DataMember(Order = 35)]
        public WideRowFlagsEnum? NullableFlagsEnum { get; set; }

        // intentionally not including:
        //  - Version
        //  - Index
        //  - Range

        public WideRow() { }

        public static WideRow Create(Random r)
        {
            var ret = new WideRow();

            ret.Byte = Create(r, (b, _) => b[0]);
            ret.SByte = (sbyte)Create(r, (b, _) => b[0]);
            ret.Short = Create(r, BitConverter.ToInt16);
            ret.UShort = Create(r, BitConverter.ToUInt16);
            ret.Int = Create(r, BitConverter.ToInt32);
            ret.UInt = Create(r, BitConverter.ToUInt32);
            ret.Long = Create(r, BitConverter.ToInt64);
            ret.ULong = Create(r, BitConverter.ToUInt64);
            ret.Float = Create(r, BitConverter.ToSingle);
            ret.Double = Create(r, BitConverter.ToDouble);
            ret.Decimal = CreateDecimal(r);

            ret.NullableByte = CreateNullable(r, (b, _) => b[0]);
            ret.NullableSByte = (sbyte?)CreateNullable(r, (b, _) => b[0]);
            ret.NullableShort = CreateNullable(r, BitConverter.ToInt16);
            ret.NullableUShort = CreateNullable(r, BitConverter.ToUInt16);
            ret.NullableInt = CreateNullable(r, BitConverter.ToInt32);
            ret.NullableUInt = CreateNullable(r, BitConverter.ToUInt32);
            ret.NullableLong = CreateNullable(r, BitConverter.ToInt64);
            ret.NullableULong = CreateNullable(r, BitConverter.ToUInt64);
            ret.NullableFloat = CreateNullable(r, BitConverter.ToSingle);
            ret.NullableDouble = CreateNullable(r, BitConverter.ToDouble);
            ret.NullableDecimal = r.Next(2) == 1 ? null : (decimal?)CreateDecimal(r);

            ret.String = CreateString(r);

            ret.Char = RandomChar(r);
            ret.NullableChar = r.Next(2) == 1 ? null : (char?)RandomChar(r);

            ret.Guid = MakeGuid(r);
            ret.NullableGuid = r.Next(2) == 1 ? null : (Guid?)MakeGuid(r);

            ret.DateTime = CreateDate(r, (year, month, day, hour, minute, second) => new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
            ret.NullableDateTime = CreateNullableDate(r, (year, month, day, hour, minute, second) => new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));

            ret.DateTimeOffset = CreateDate(r, (year, month, day, hour, minute, second) => new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));
            ret.NullableDateTimeOffset = CreateNullableDate(r, (year, month, day, hour, minute, second) => new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));

            var domains = new[] { "example.com", "github.com", "stackoverflow.com" };
            var paths = new[] { "", "foo", "foo/bar" };
            var queries = new[] { "", "fizz=buzz", "nope=whatever&nada=bits" };

            var d = domains[r.Next(domains.Length)];
            var p = paths[r.Next(paths.Length)];
            var q = queries[r.Next(queries.Length)];

            ret.Uri = new Uri($"https://{d}/{p}?{q}");

            ret.Enum = System.Enum.GetValues(typeof(WideRowEnum)).Cast<WideRowEnum>().ElementAt(r.Next(System.Enum.GetValues(typeof(WideRowEnum)).Length));
            ret.NullableEnum = r.Next(2) == 1 ? null : (WideRowEnum?)System.Enum.GetValues(typeof(WideRowEnum)).Cast<WideRowEnum>().ElementAt(r.Next(System.Enum.GetValues(typeof(WideRowEnum)).Length));

            switch (r.Next(3))
            {
                case 0: ret.FlagsEnum = WideRowFlagsEnum.Empty; break;
                case 1: ret.FlagsEnum = WideRowFlagsEnum.Hello; break;
                case 2: ret.FlagsEnum = WideRowFlagsEnum.Hello | WideRowFlagsEnum.World; break;
                default: throw new Exception();
            }

            switch (r.Next(4))
            {
                case 0: ret.NullableFlagsEnum = WideRowFlagsEnum.Empty; break;
                case 1: ret.NullableFlagsEnum = WideRowFlagsEnum.Hello; break;
                case 2: ret.NullableFlagsEnum = WideRowFlagsEnum.Hello | WideRowFlagsEnum.World; break;
                case 3: ret.NullableFlagsEnum = null; break;
                default: throw new Exception();
            }

            return ret;
        }

        private static char RandomChar(Random r)
        {
            char ret;

tryAgain:

            switch (r.Next(3))
            {
                // ascii
                case 0: ret = (char)r.Next(128); break;

                // low utf16
                case 1: ret = (char)r.Next(0xD800); break;

                // high utf16
                case 2: ret = (char)(r.Next(0xFFFF - 0xE000) + 0xE000); break;

                default: throw new Exception();
            }

            if (char.IsWhiteSpace(ret)) goto tryAgain;

            return ret;
        }

        private static string CreateString(Random r)
        {
            string ret;

tryAgain:

            var strType = r.Next(4);
            switch (strType)
            {
                case 0: ret = null; break;
                case 1: ret = ""; break;
                case 2:
                    {
                        var buff = new char[r.Next(16) + 1];
                        for (var i = 0; i < buff.Length; i++)
                        {
                            buff[i] = (char)r.Next(128);
                        }

                        ret = new string(buff);
                    }
                    break;
                case 3:
                    {
                        var buff = new char[r.Next(100) + 1];
                        for (var i = 0; i < buff.Length; i++)
                        {
                            buff[i] = (char)r.Next(128);
                        }

                        ret = new string(buff);
                    }
                    break;
                default: throw new Exception();
            }

            if (ret != null)
            {
                var addSpecial = r.Next(5) == 4;
                if (addSpecial)
                {
                    var pos = r.Next(ret.Length);

                    switch (r.Next(3))
                    {
                        case 0: ret = ret.Substring(0, pos) + '"' + ret.Substring(pos); break;
                        case 1: ret = ret.Substring(0, pos) + ',' + ret.Substring(pos); break;
                        case 2: ret = ret.Substring(0, pos) + "\r\n" + ret.Substring(pos); break;
                        default: throw new Exception();
                    }
                }
            }

            if (ret != null && ret.Length > 0)
            {
                // CSVHelper wants to escape things with leading or trailing whitespace, which is... a thing but don't do that maybe?
                if (ret[0] == ' ') goto tryAgain;
                if (ret[ret.Length - 1] == ' ') goto tryAgain;
            }

            return ret;
        }

        private static decimal CreateDecimal(Random r)
        {
            var lo = Create(r, BitConverter.ToInt32);
            var mid = Create(r, BitConverter.ToInt32);
            var high = Create(r, BitConverter.ToInt32);

            var neg = r.Next(2) == 1;

            var scale = (byte)(r.Next() % 29);

            return new decimal(lo, mid, high, neg, scale);
        }

        private static unsafe T Create<T>(Random r, Func<byte[], int, T> maker)
            where T : unmanaged
        {
            var buff = new byte[sizeof(T)];

            r.NextBytes(buff);

            return maker(buff, 0);
        }

        private static T? CreateNullable<T>(Random r, Func<byte[], int, T> maker)
            where T : unmanaged
        {
            var n = r.Next(2);
            if (n == 1) return null;

            return Create(r, maker);
        }

        private static Guid MakeGuid(Random r)
        {
            var buff = new byte[16];
            r.NextBytes(buff);

            return new Guid(buff);
        }

        private static T CreateDate<T>(Random r, Func<int, int, int, int, int, int, T> maker)
            where T : struct
        {
            var years = Enumerable.Range(0, 9999);
            var months = Enumerable.Range(1, 12);
            var days = Enumerable.Range(1, 31);
            var hours = Enumerable.Range(0, 24);
            var minutes = Enumerable.Range(0, 60);
            var seconds = Enumerable.Range(0, 60);

            while (true)
            {
                try
                {
                    var y = years.ElementAt(r.Next(years.Count()));
                    var m = months.ElementAt(r.Next(months.Count()));
                    var d = days.ElementAt(r.Next(days.Count()));
                    var h = hours.ElementAt(r.Next(hours.Count()));
                    var mins = minutes.ElementAt(r.Next(minutes.Count()));
                    var s = seconds.ElementAt(r.Next(seconds.Count()));

                    return maker(y, m, d, h, mins, s);
                }
                catch { }
            }
        }

        private static T? CreateNullableDate<T>(Random r, Func<int, int, int, int, int, int, T> maker)
            where T : struct
        {
            var n = r.Next(2);
            if (n == 1) return null;

            return CreateDate(r, maker);
        }

        public static void Initialize()
        {
            if (ShallowRows != null && DeepRows != null) return;

            const int NUM_SHALLOW = 10;
            const int NUM_DEEP = 10_000;

            // init shallow
            {
                var r = MakeRandom();
                ShallowRows = Enumerable.Range(0, NUM_SHALLOW).Select(_ => Create(r)).ToList();
            }

            // init deep
            {
                var r = MakeRandom();
                DeepRows = Enumerable.Range(0, NUM_DEEP).Select(_ => Create(r)).ToList();
            }

            static Random MakeRandom()
            {
                return new Random(2020_02_04);
            }
        }

        public bool Equals(WideRow other)
        {
            if (other == null) return false;

            var a = this.Byte == other.Byte;
            var b = this.Char == other.Char;
            var c = this.DateTime == other.DateTime;
            var d = this.DateTimeOffset == other.DateTimeOffset;
            var e = this.Decimal == other.Decimal;
            var f = DoublesEquivalent(this.Double, other.Double);
            var g = this.Enum == other.Enum;
            var h = this.FlagsEnum == other.FlagsEnum;
            var i = FloatsEquivalent(this.Float, other.Float);
            var j = this.Guid == other.Guid;
            var k = this.Int == other.Int;
            var l = this.Long == other.Long;
            var m = this.NullableByte == other.NullableByte;
            var n = this.NullableChar == other.NullableChar;
            var o = this.NullableDateTime == other.NullableDateTime;
            var p = this.NullableDateTimeOffset == other.NullableDateTimeOffset;
            var q = this.NullableDecimal == other.NullableDecimal;
            var r = NullablesEquivalent(this.NullableDouble, other.NullableDouble, DoublesEquivalent);
            var s = this.NullableEnum == other.NullableEnum;
            var t = this.NullableFlagsEnum == other.NullableFlagsEnum;
            var u = NullablesEquivalent(this.NullableFloat, other.NullableFloat, FloatsEquivalent);
            var v = this.NullableGuid == other.NullableGuid;
            var w = this.NullableInt == other.NullableInt;
            var x = this.NullableLong == other.NullableLong;
            var y = this.NullableSByte == other.NullableSByte;
            var z = this.NullableShort == other.NullableShort;
            var aa = this.NullableUInt == other.NullableUInt;
            var ab = this.NullableULong == other.NullableULong;
            var ac = this.NullableUShort == other.NullableUShort;
            var ad = this.SByte == other.SByte;
            var ae = this.Short == other.Short;
            var af = this.String == other.String;
            var ag = this.UInt == other.UInt;
            var ah = this.ULong == other.ULong;
            var ai = ((this.Uri != null && other.Uri != null && this.Uri.Equals(other.Uri)) || this.Uri == other.Uri);
            var aj = this.UShort == other.UShort;

            return a && b && c && d && e && f && g && h && i && j && k && l && m && n && o && p && q && r && s && t && u && v && w && x && y && z &&
                  aa && ab && ac && ad && ae && af && ag && ah && ai && aj;
        }

        private static bool NullablesEquivalent<T>(T? a, T? b, Func<T, T, bool> nonNull)
            where T : struct
        {
            if (a == null)
            {
                if (b == null)
                {
                    return true;
                }

                return false;
            }
            else
            {
                if (b == null)
                {
                    return false;
                }

                return nonNull(a.Value, b.Value);
            }
        }

        private static bool FloatsEquivalent(float a, float b)
        {
            if (float.IsNaN(a))
            {
                if (float.IsNaN(b))
                {
                    return true;
                }

                return false;
            }
            else
            {
                if (float.IsNaN(b))
                {
                    return false;
                }

                return a == b;
            }
        }

        private static bool DoublesEquivalent(double a, double b)
        {
            if (double.IsNaN(a))
            {
                if (double.IsNaN(b))
                {
                    return true;
                }

                return false;
            }
            else
            {
                if (double.IsNaN(b))
                {
                    return false;
                }

                return a == b;
            }
        }

        public override bool Equals(object obj)
        => Equals(obj as WideRow);

        public override int GetHashCode()
        => 0;   // just for everything to get an Equals call
    }

    internal class WideRowMapping : CsvHelper.Configuration.ClassMap<WideRow>
    {
        public WideRowMapping() : base()
        {
            Map(r => r.Byte).Index(0).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.SByte).Index(1).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.Short).Index(2).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.UShort).Index(3).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.Int).Index(4).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.UInt).Index(5).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.Long).Index(6).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.ULong).Index(7).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.Float).Index(8).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("G9");
            Map(r => r.Double).Index(9).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("G17");
            Map(r => r.Decimal).Index(10).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);

            Map(r => r.NullableByte).Index(11).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.NullableSByte).Index(12).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.NullableShort).Index(13).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.NullableUShort).Index(14).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.NullableInt).Index(15).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.NullableUInt).Index(16).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.NullableLong).Index(17).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.NullableULong).Index(18).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(r => r.NullableFloat).Index(19).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("G9");
            Map(r => r.NullableDouble).Index(20).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("G17");
            Map(r => r.NullableDecimal).Index(21).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);

            Map(r => r.String).Index(22);
            Map(r => r.Char).Index(23);
            Map(r => r.NullableChar).Index(24);

            Map(r => r.Guid).Index(25).TypeConverterOption.Format("D");
            Map(r => r.NullableGuid).Index(26).TypeConverterOption.Format("D");

            Map(r => r.DateTime).Index(27).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("u").TypeConverterOption.DateTimeStyles(DateTimeStyles.AssumeUniversal);
            Map(r => r.DateTimeOffset).Index(28).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("u");

            Map(r => r.NullableDateTime).Index(29).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("u").TypeConverterOption.DateTimeStyles(DateTimeStyles.AssumeUniversal);
            Map(r => r.NullableDateTimeOffset).Index(30).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("u");

            Map(r => r.Uri).Index(31);

            Map(r => r.Enum).Index(32);
            Map(r => r.FlagsEnum).Index(33);

            Map(r => r.NullableEnum).Index(34);
            Map(r => r.NullableFlagsEnum).Index(35);
        }
    }
}
