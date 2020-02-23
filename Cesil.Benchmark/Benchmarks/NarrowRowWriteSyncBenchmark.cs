using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    public class NarrowRowWriteSyncBenchmark
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

        private Action<TextWriter> DoRun;

        [GlobalSetup]
        public void Initialize()
        {
            DoRun = MakeRun(Type, RowSet, Library);
        }

        private static Action<TextWriter> MakeRun(Type type, string rowSet, string library)
        {
            var mtd = MakeRunGenericMtd.MakeGenericMethod(type);

            var ret = mtd.Invoke(null, new[] { rowSet, library });

            return (Action<TextWriter>)ret;
        }

        private static MethodInfo MakeRunGenericMtd = typeof(NarrowRowWriteSyncBenchmark).GetMethod(nameof(MakeRunGeneric), BindingFlags.NonPublic | BindingFlags.Static);
        private static Action<TextWriter> MakeRunGeneric<T>(string rowSet, string library)
        {
            const int NUM_SHALLOW = 10;
            const int NUM_DEEP = 10_000;

            IEnumerable<NarrowRow<T>> rows;

            var r = MakeRandom();
            switch (rowSet)
            {
                case "ShallowRows": rows = Enumerable.Range(0, NUM_SHALLOW).Select(_ => NarrowRow<T>.Create(r)).ToArray(); break;
                case "DeepRows": rows = Enumerable.Range(0, NUM_DEEP).Select(_ => NarrowRow<T>.Create(r)).ToArray(); break;
                default: throw new Exception();
            }

            Action<IEnumerable<NarrowRow<T>>, TextWriter> writeDel;
            switch (library)
            {
                case nameof(Cesil):
                    {
                        var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();
                        var config = Configuration.For<NarrowRow<T>>(opts);
                        writeDel = MakeWriteWithCesilDel(config);
                    }
                    break;
                case nameof(CsvHelper):
                    {
                        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
                        config.RegisterClassMap<NarrowRowMapping<T>>();
                        writeDel = MakeWriteWithCsvHelper(config);
                    }
                    break;
                default: throw new Exception();
            }

            return writer => writeDel(rows, writer);

            static Random MakeRandom()
            {
                return new Random(2020_03_04);
            }

            static Action<IEnumerable<NarrowRow<T>>, TextWriter> MakeWriteWithCesilDel(IBoundConfiguration<NarrowRow<T>> config)
            {
                return
                    (IEnumerable<NarrowRow<T>> rows, TextWriter into) =>
                    {
                        using (var writer = config.CreateWriter(into))
                        {
                            writer.WriteAll(rows);
                        }
                    };
            }

            static Action<IEnumerable<NarrowRow<T>>, TextWriter> MakeWriteWithCsvHelper(CsvHelper.Configuration.CsvConfiguration config)
            {
                return
                    (IEnumerable<NarrowRow<T>> rows, TextWriter into) =>
                    {
                        using (var writer = new CsvHelper.CsvWriter(into, config))
                        {
                            writer.WriteRecords(rows);
                        }
                    };
            }
        }

        public void InitializeAndTest()
        {
            foreach (var row in KnownRowSet)
            {
                foreach (var type in KnownTypes)
                {
                    var shouldMatch =
                        KnownLibraries
                            .Select(
                                lib =>
                                {
                                    var act = MakeRun(type, row, lib);

                                    using (var str = new StringWriter())
                                    {
                                        act(str);

                                        var res = str.ToString();

                                        return (Library: lib, Text: res);
                                    }
                                }
                            )
                            .ToList();

                    var allSame = shouldMatch.Select(x => x.Text).Distinct();

                    if (allSame.Count() > 1)
                    {
                        var cesil = shouldMatch.Single(s => s.Library == nameof(Cesil)).Text;

                        foreach (var other in shouldMatch)
                        {
                            if (other.Library == nameof(Cesil)) continue;

                            var otherText = other.Text;

                            var firstDiff = -1;

                            for (var i = 0; i < Math.Min(otherText.Length, cesil.Length); i++)
                            {
                                var cC = cesil[i];
                                var oC = otherText[i];

                                if (cC != oC)
                                {
                                    firstDiff = i;
                                    break;
                                }
                            }

                            if (firstDiff != -1)
                            {
                                var beforeDiff = Math.Max(firstDiff - 20, 0);
                                var afterDiff = Math.Min(Math.Min(otherText.Length, cesil.Length), firstDiff + 20);

                                var segmentInOther = otherText.Substring(beforeDiff, afterDiff - beforeDiff);
                                var segmentInCesil = cesil.Substring(beforeDiff, afterDiff - beforeDiff);

                                throw new InvalidCastException($"Different libraries are configured to produce different CSV, benchmark isn't valid\r\n{segmentInCesil}\r\n\r\nvs\r\n\r\n{segmentInOther}");
                            }
                        }

                        throw new InvalidOperationException($"Different libraries are configured to produce different CSV, benchmark isn't valid");
                    }
                }
            }
        }

        [Benchmark]
        public void Run()
        {
            DoRun(TextWriter.Null);
        }
    }
}
