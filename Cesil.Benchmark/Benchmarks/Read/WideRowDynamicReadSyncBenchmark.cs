using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    [BenchmarkCategory("Read", "Dynamic")]
    public class WideRowDynamicReadSyncBenchmark
    {
        [ParamsSource(nameof(KnownRowSet))]
        public string RowSet { get; set; }

        public IEnumerable<string> KnownRowSet => new[] { nameof(WideRow.ShallowRows), nameof(WideRow.DeepRows) };

        private IBoundConfiguration<WideRow> StaticConfig;
        private IBoundConfiguration<dynamic> DynamicConfig;

        private string CSV;

        [GlobalSetup]
        public void Initialize()
        {
            WideRow.Initialize();

            if (StaticConfig != null && DynamicConfig != null) return;

            DynamicConfig = Configuration.ForDynamic();
            StaticConfig = Configuration.For<WideRow>();

            CSV = MakeCSV();
        }

        public void InitializeAndTest()
        {
            foreach (var rows in KnownRowSet)
            {
                StaticConfig = null;
                DynamicConfig = null;
                CSV = null;

                RowSet = rows;
                Initialize();

                var staticHashes = new List<int>();
                using (var str = new StringReader(CSV))
                using (var csv = StaticConfig.CreateReader(str))
                {
                    foreach (var row in csv.EnumerateAll())
                    {
                        var h = HashAllMembers(row);
                        staticHashes.Add(h);
                    }
                }

                var dynamicHashes = new List<int>();
                using (var str = new StringReader(CSV))
                using (var csv = DynamicConfig.CreateReader(str))
                {
                    foreach (var row in csv.EnumerateAll())
                    {
                        var h = HashAllMembers(row);
                        dynamicHashes.Add(h);
                    }
                }

                if (staticHashes.Count != dynamicHashes.Count) throw new Exception();

                for (var i = 0; i < staticHashes.Count; i++)
                {
                    if (staticHashes[i] != dynamicHashes[i]) throw new Exception();
                }
            }
        }

        [Benchmark(Baseline = true)]
        public void Static()
        {
            using (var str = new StringReader(CSV))
            using (var csv = StaticConfig.CreateReader(str))
            {
                foreach (var row in csv.EnumerateAll())
                {
                    HashAllMembers(row);
                }
            }
        }

        [Benchmark()]
        public void Dynamic()
        {
            using (var str = new StringReader(CSV))
            using (var csv = DynamicConfig.CreateReader(str))
            {
                foreach (var row in csv.EnumerateAll())
                {
                    HashAllMembers(row);
                }
            }
        }

        private string MakeCSV()
        {
            using (var str = new StringWriter())
            {
                using (var csv = StaticConfig.CreateWriter(str))
                {
                    csv.WriteAll(GetRows(RowSet));
                }

                return str.ToString();
            }
        }

        private IEnumerable<WideRow> GetRows(string forRowSet)
        {
            switch (forRowSet)
            {
                case nameof(WideRow.DeepRows): return WideRow.DeepRows;
                case nameof(WideRow.ShallowRows): return WideRow.ShallowRows;
                default: throw new InvalidOperationException();
            }
        }

        private int HashAllMembers(dynamic row)
        {
            byte a = row.Byte;
            sbyte b = row.SByte;
            short c = row.Short;
            ushort d = row.UShort;
            int e = row.Int;
            uint f = row.UInt;
            long g = row.Long;
            ulong h = row.ULong;
            float i = row.Float;
            double j = row.Double;
            decimal k = row.Decimal;

            byte? l = row.NullableByte;
            sbyte? m = row.NullableSByte;
            short? n = row.NullableShort;
            ushort? o = row.NullableUShort;
            int? p = row.NullableInt;
            uint? q = row.NullableUInt;
            long? r = row.NullableLong;
            ulong? s = row.NullableULong;
            float? t = row.NullableFloat;
            double? u = row.NullableDouble;
            decimal? v = row.NullableDecimal;

            string w = row.String;
            char x = row.Char;
            char? y = row.NullableChar;

            Guid z = row.Guid;
            Guid? aa = row.NullableGuid;

            DateTime ab = row.DateTime;
            DateTimeOffset ac = row.DateTimeOffset;

            DateTime? ad = row.NullableDateTime;
            DateTimeOffset? ae = row.NullableDateTimeOffset;

            Uri af = row.Uri;

            WideRowEnum ag = row.Enum;
            WideRowFlagsEnum ah = row.FlagsEnum;

            WideRowEnum? ai = row.NullableEnum;
            WideRowFlagsEnum? aj = row.NullableFlagsEnum;

            var hash1 = HashCode.Combine(a, b, c, d, e, f, g, h);
            var hash2 = HashCode.Combine(i, j, k, l, m, n, o, p);
            var hash3 = HashCode.Combine(q, r, s, t, u, v, w, x);
            var hash4 = HashCode.Combine(y, z, aa, ab, ac, ad, ae, af);
            var hash5 = HashCode.Combine(ag, ah, ai, aj);

            var ret = HashCode.Combine(hash1, hash2, hash3, hash4, hash5);

            return ret;
        }
    }
}
