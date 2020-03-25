using System;
using System.Globalization;
using System.Linq;

namespace Cesil.Benchmark
{
    [Flags]
    public enum NarrowRowFlagsEnum
    {
        Empty = 0,

        Hello = 1 << 0,
        World = 1 << 1
    }

    public enum NarrowRowEnum
    {
        None = 0,

        Foo = 1,
        Fizz = 2,
        Bar = 3
    }

    internal sealed class NarrowRow<T> : IEquatable<NarrowRow<T>>
    {
        public T Column { get; set; }

        public static NarrowRow<T> Create(Random r)
        {
            var col = CreateValue(r);

            return new NarrowRow<T> { Column = col };
        }

        private static T CreateValue(Random r)
        {
            if (typeof(T) == typeof(byte)) return (T)(object)Create(r, (b, _) => b[0]);
            if (typeof(T) == typeof(sbyte)) return (T)(object)Create(r, (b, _) => (sbyte)b[0]);

            if (typeof(T) == typeof(byte?)) return (T)(object)CreateNullable(r, (b, _) => b[0]);
            if (typeof(T) == typeof(sbyte?)) return (T)(object)CreateNullable(r, (b, _) => (sbyte)b[0]);

            if (typeof(T) == typeof(short)) return (T)(object)Create(r, BitConverter.ToInt16);
            if (typeof(T) == typeof(ushort)) return (T)(object)Create(r, BitConverter.ToUInt16);

            if (typeof(T) == typeof(short?)) return (T)(object)CreateNullable(r, BitConverter.ToInt16);
            if (typeof(T) == typeof(ushort?)) return (T)(object)CreateNullable(r, BitConverter.ToUInt16);

            if (typeof(T) == typeof(int)) return (T)(object)Create(r, BitConverter.ToInt32);
            if (typeof(T) == typeof(uint)) return (T)(object)Create(r, BitConverter.ToUInt32);

            if (typeof(T) == typeof(int?)) return (T)(object)CreateNullable(r, BitConverter.ToInt32);
            if (typeof(T) == typeof(uint?)) return (T)(object)CreateNullable(r, BitConverter.ToUInt32);

            if (typeof(T) == typeof(long)) return (T)(object)Create(r, BitConverter.ToInt64);
            if (typeof(T) == typeof(ulong)) return (T)(object)Create(r, BitConverter.ToUInt64);

            if (typeof(T) == typeof(long?)) return (T)(object)CreateNullable(r, BitConverter.ToInt64);
            if (typeof(T) == typeof(ulong?)) return (T)(object)CreateNullable(r, BitConverter.ToUInt64);

            if (typeof(T) == typeof(float)) return (T)(object)Create(r, BitConverter.ToSingle);

            if (typeof(T) == typeof(float?)) return (T)(object)CreateNullable(r, BitConverter.ToSingle);

            if (typeof(T) == typeof(double)) return (T)(object)Create(r, BitConverter.ToDouble);

            if (typeof(T) == typeof(double?)) return (T)(object)CreateNullable(r, BitConverter.ToDouble);

            if (typeof(T) == typeof(decimal)) return (T)(object)CreateDecimal(r);
            if (typeof(T) == typeof(decimal?)) return (T)(object)(r.Next(2) == 1 ? null : (decimal?)CreateDecimal(r));

            if (typeof(T) == typeof(string)) return (T)(object)CreateString(r);

            if (typeof(T) == typeof(char)) return (T)(object)RandomChar(r);
            if (typeof(T) == typeof(char?)) return (T)(object)(r.Next(2) == 1 ? null : (char?)RandomChar(r));

            if (typeof(T) == typeof(Guid)) return (T)(object)MakeGuid(r);
            if (typeof(T) == typeof(Guid?)) return (T)(object)(r.Next(2) == 1 ? null : (Guid?)MakeGuid(r));

            if (typeof(T) == typeof(DateTime)) return (T)(object)CreateDate(r, (year, month, day, hour, minute, second) => new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
            if (typeof(T) == typeof(DateTime?)) return (T)(object)(CreateNullableDate(r, (year, month, day, hour, minute, second) => new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc)));

            if (typeof(T) == typeof(DateTimeOffset)) return (T)(object)CreateDate(r, (year, month, day, hour, minute, second) => new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));
            if (typeof(T) == typeof(DateTimeOffset?)) return (T)(object)CreateNullableDate(r, (year, month, day, hour, minute, second) => new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));

            var domains = new[] { "example.com", "github.com", "stackoverflow.com" };
            var paths = new[] { "", "foo", "foo/bar" };
            var queries = new[] { "", "fizz=buzz", "nope=whatever&nada=bits" };

            var d = domains[r.Next(domains.Length)];
            var p = paths[r.Next(paths.Length)];
            var q = queries[r.Next(queries.Length)];

            if (typeof(T) == typeof(Uri)) return (T)(object)new Uri($"https://{d}/{p}?{q}");

            if (typeof(T) == typeof(NarrowRowEnum)) return (T)(object)System.Enum.GetValues(typeof(NarrowRowEnum)).Cast<NarrowRowEnum>().ElementAt(r.Next(System.Enum.GetValues(typeof(NarrowRowEnum)).Length));
            if (typeof(T) == typeof(NarrowRowEnum?)) return (T)(object)(r.Next(2) == 1 ? null : (NarrowRowEnum?)System.Enum.GetValues(typeof(NarrowRowEnum)).Cast<NarrowRowEnum>().ElementAt(r.Next(System.Enum.GetValues(typeof(NarrowRowEnum)).Length)));

            if (typeof(T) == typeof(NarrowRowFlagsEnum))
            {
                switch (r.Next(3))
                {
                    case 0: return (T)(object)NarrowRowFlagsEnum.Empty;
                    case 1: return (T)(object)NarrowRowFlagsEnum.Hello;
                    case 2: return (T)(object)(NarrowRowFlagsEnum.Hello | NarrowRowFlagsEnum.World);
                    default: throw new Exception();
                }
            }

            if (typeof(T) == typeof(NarrowRowFlagsEnum?))
            {
                switch (r.Next(4))
                {
                    case 0: return (T)(object)NarrowRowFlagsEnum.Empty;
                    case 1: return (T)(object)NarrowRowFlagsEnum.Hello;
                    case 2: return (T)(object)(NarrowRowFlagsEnum.Hello | NarrowRowFlagsEnum.World);
                    case 3: return (T)(object)default(NarrowRowFlagsEnum?);
                    default: throw new Exception();
                }
            }

            throw new Exception();
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

        private static unsafe V Create<V>(Random r, Func<byte[], int, V> maker)
            where V : unmanaged
        {
            var buff = new byte[sizeof(V)];

            r.NextBytes(buff);

            return maker(buff, 0);
        }

        private static V? CreateNullable<V>(Random r, Func<byte[], int, V> maker)
            where V : unmanaged
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

        private static V CreateDate<V>(Random r, Func<int, int, int, int, int, int, V> maker)
            where V : struct
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

        private static V? CreateNullableDate<V>(Random r, Func<int, int, int, int, int, int, V> maker)
            where V : struct
        {
            var n = r.Next(2);
            if (n == 1) return null;

            return CreateDate(r, maker);
        }

        public override bool Equals(object obj)
        => Equals(obj as NarrowRow<T>);

        public bool Equals(NarrowRow<T> other)
        {
            if (other == null) return false;

            if (Column == null)
            {
                return other.Column == null;
            }

            return this.Column.Equals(other.Column);
        }

        public override int GetHashCode()
        => 0;
    }

    internal class NarrowRowMapping<T> : CsvHelper.Configuration.ClassMap<NarrowRow<T>>
    {
        public NarrowRowMapping() : base()
        {
            if (typeof(T) == typeof(float) || typeof(T) == typeof(float?))
            {
                Map(r => r.Column).Index(0).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("G9");
            }
            else if (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
            {
                Map(r => r.Column).Index(0).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture).TypeConverterOption.Format("G17");
            }
            else if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
            {
                Map(r => r.Column).Index(0).TypeConverterOption.CultureInfo(CultureInfo.InstalledUICulture).TypeConverterOption.Format("u").TypeConverterOption.DateTimeStyles(DateTimeStyles.AssumeUniversal);
            }
            else if (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
            {
                Map(r => r.Column).Index(0).TypeConverterOption.CultureInfo(CultureInfo.InstalledUICulture).TypeConverterOption.Format("u");
            }
            else
            {
                Map(r => r.Column).Index(0).TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            }
        }
    }
}
