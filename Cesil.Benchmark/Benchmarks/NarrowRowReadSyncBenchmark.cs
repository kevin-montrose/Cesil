using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    public class NarrowRowReadSyncBenchmark
    {
        [ParamsSource(nameof(KnownTypes))]
        public Type Type { get; set; }

        [ParamsSource(nameof(KnownRowSet))]
        public string RowSet { get; set; }

        [ParamsSource(nameof(KnownLibraries))]
        public string Library { get; set; }

        public IEnumerable<string> KnownRowSet =>
            new[]
            {
                "ShallowRows",
                "DeepRows"
            };

        public IEnumerable<string> KnownLibraries => new[] { nameof(Cesil), nameof(CsvHelper) };

        public IEnumerable<Type> KnownTypes =>
            new[]
            {
                typeof(byte),
                typeof(sbyte),

                typeof(byte?),
                typeof(sbyte?),

                typeof(short),
                typeof(ushort),

                typeof(short?),
                typeof(ushort?),

                typeof(int),
                typeof(uint),

                typeof(int?),
                typeof(uint?),

                typeof(long),
                typeof(ulong),

                typeof(long?),
                typeof(ulong?),

                typeof(float),

                typeof(float?),

                typeof(double),

                typeof(double?),

                typeof(decimal),

                typeof(decimal?),

                typeof(string),

                typeof(char),

                typeof(char?),

                typeof(Guid),

                typeof(Guid?),

                typeof(DateTime),

                typeof(DateTime?),

                typeof(DateTimeOffset),

                typeof(DateTimeOffset?),

                typeof(Uri),

                typeof(NarrowRowEnum),

                typeof(NarrowRowEnum?),

                typeof(NarrowRowFlagsEnum),

                typeof(NarrowRowFlagsEnum?)
            };

        private Func<IEnumerable<object>> DoRun;

        [GlobalSetup]
        public void Initialize()
        {
            DoRun = MakeRun(Type, RowSet, Library);
        }

        public void InitializeAndTest()
        {
            foreach (var type in KnownTypes)
            {
                foreach (var row in KnownRowSet)
                {
                    var sets =
                        KnownLibraries
                            .Select(
                                lib =>
                                {
                                    var del = MakeRun(type, row, lib);

                                    return (Rows: del(), Library: lib);
                                }
                            )
                            .ToList();


                    for (var i = 1; i < sets.Count; i++)
                    {
                        var first = sets[0];
                        var second = sets[i];

                        if (first.Rows.Count() != second.Rows.Count()) throw new Exception();

                        for (var j = 0; j < first.Rows.Count(); j++)
                        {
                            var fRow = first.Rows.ElementAt(j);
                            var sRow = second.Rows.ElementAt(j);

                            if (fRow is NarrowRow<string> fStrRow)
                            {
                                // have to special case this, CsvHelper doesn't have a built in way to make "" be null

                                var sStrRow = (NarrowRow<string>)sRow;

                                var fStr = fStrRow.Column ?? "";
                                var sStr = sStrRow.Column ?? "";

                                if (!fStr.Equals(sStr)) throw new Exception();
                            }
                            else
                            {
                                if (!fRow.Equals(sRow)) throw new Exception();
                            }
                        }
                    }
                }
            }
        }

        [Benchmark]
        public void Run()
        {
            DoRun();
        }

        private Func<IEnumerable<object>> MakeRun(Type type, string rowSet, string lib)
        {
            var genMtd = MakeRunGenericMtd.MakeGenericMethod(type);

            var del = genMtd.Invoke(null, new object[] { rowSet, lib });

            return (Func<IEnumerable<object>>)del;
        }

        private static MethodInfo MakeRunGenericMtd = typeof(NarrowRowReadSyncBenchmark).GetMethod(nameof(MakeRunGeneric), BindingFlags.NonPublic | BindingFlags.Static);
        private static Func<IEnumerable<object>> MakeRunGeneric<T>(string rowSet, string lib)
        {
            const int NUM_SHALLOW = 10;
            const int NUM_DEEP = 10_000;

            string csv;
            {
                IEnumerable<NarrowRow<T>> rows;

                var r = MakeRandom();
                switch (rowSet)
                {
                    case "ShallowRows": rows = Enumerable.Range(0, NUM_SHALLOW).Select(_ => NarrowRow<T>.Create(r)).ToArray(); break;
                    case "DeepRows": rows = Enumerable.Range(0, NUM_DEEP).Select(_ => NarrowRow<T>.Create(r)).ToArray(); break;
                    default: throw new Exception();
                }

                var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();
                var config = Configuration.For<NarrowRow<T>>(opts);

                using (var str = new StringWriter())
                {
                    using (var writer = config.CreateWriter(str))
                    {
                        writer.WriteAll(rows);
                    }

                    csv = str.ToString();
                }
            }

            switch (lib)
            {
                case nameof(Cesil):
                    {
                        var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();
                        var config = Configuration.For<NarrowRow<T>>(opts);
                        return
                            () =>
                            {
                                var ret = new List<object>();

                                using (var str = new StringReader(csv))
                                using (var csv = config.CreateReader(str))
                                {
                                    var rows = csv.EnumerateAll();
                                    foreach (var row in rows)
                                    {
                                        ret.Add(row);
                                    }

                                    return ret;
                                }

                            };
                    }
                case nameof(CsvHelper):
                    {
                        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
                        config.RegisterClassMap<NarrowRowMapping<T>>();
                        config.IgnoreBlankLines = false;

                        return
                            () =>
                            {
                                var ret = new List<object>();

                                using (var str = new StringReader(csv))
                                using (var csv = new CsvHelper.CsvReader(str, config))
                                {
                                    var rows = csv.GetRecords<NarrowRow<T>>();
                                    foreach (var row in rows)
                                    {
                                        ret.Add(row);
                                    }

                                    return ret;
                                }
                            };
                    }
                default: throw new Exception();
            }

            static Random MakeRandom()
            {
                return new Random(2020_03_06);
            }
        }
    }
}
