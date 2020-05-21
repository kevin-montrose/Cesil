using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    [BenchmarkCategory("Read", "Dynamic")]
    public class NarrowRowDynamicReadSyncBenchmark
    {
        [ParamsSource(nameof(KnownTypes))]
        public Type Type { get; set; }

        [ParamsSource(nameof(KnownRowSets))]
        public string RowSet { get; set; }

        public IEnumerable<string> KnownRowSets =>
            new[]
            {
                "ShallowRows",
                "DeepRows"
            };

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

        private Func<object> DoRunStatic;
        private Func<object> DoRunDynamic;

        [GlobalSetup]
        public void Initialize()
        {
            DoRunStatic = MakeStaticRun(Type, RowSet);
            DoRunDynamic = MakeDynamicRun(Type, RowSet);
        }

        public void InitializeAndTest()
        {
            foreach (var type in KnownTypes)
            {
                foreach (var row in KnownRowSets)
                {
                    DoRunStatic = null;
                    DoRunDynamic = null;

                    Type = type;
                    RowSet = row;

                    Initialize();

                    var staticRows = DoRunStatic();
                    var dynamicRows = DoRunDynamic();

                    CheckSame(type, staticRows, dynamicRows);
                }
            }
        }

        private static void CheckSame(Type t, object staticRows, object dynamicRows)
        {
            var mtd = CheckSameGenericMtd.MakeGenericMethod(t);

            mtd.Invoke(null, new[] { staticRows, dynamicRows });
        }

        private static readonly MethodInfo CheckSameGenericMtd = typeof(NarrowRowDynamicReadSyncBenchmark).GetMethod(nameof(CheckSameGeneric), BindingFlags.NonPublic | BindingFlags.Static);
        private static void CheckSameGeneric<T>(List<T> staticRows, List<T> dynamicRows)
        {
            if (staticRows.Count != dynamicRows.Count)
            {
                throw new Exception();
            }

            for (var i = 0; i < staticRows.Count; i++)
            {
                var s = staticRows[i];
                var d = dynamicRows[i];

                if (s == null && d != null) throw new Exception();
                if (s != null && d == null) throw new Exception();
                if (s == null && d == null) continue;

                if (!s.Equals(d)) throw new Exception();
            }
        }

        [Benchmark(Baseline = true)]
        public void Static()
        {
            DoRunStatic();
        }

        [Benchmark]
        public void Dynamic()
        {
            DoRunDynamic();
        }

        private Func<object> MakeStaticRun(Type type, string rowSet)
        {
            var genMtd = MakeStaticRunGenericMtd.MakeGenericMethod(type);

            var del = genMtd.Invoke(null, new object[] { rowSet });

            return (Func<object>)del;
        }

        private Func<object> MakeDynamicRun(Type type, string rowSet)
        {
            var genMtd = MakeDynamicRunGenericMtd.MakeGenericMethod(type);

            var del = genMtd.Invoke(null, new object[] { rowSet });

            return (Func<object>)del;
        }

        private static string MakeCsv<T>(string rowSet)
        {
            const int NUM_SHALLOW = 10;
            const int NUM_DEEP = 10_000;

            string csv;
            {
                IEnumerable<NarrowRow<T>> rows;

                var r = new Random(2020_04_20);
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

            return csv;
        }

        private static MethodInfo MakeStaticRunGenericMtd = typeof(NarrowRowDynamicReadSyncBenchmark).GetMethod(nameof(MakeStaticRunGeneric), BindingFlags.NonPublic | BindingFlags.Static);
        private static Func<object> MakeStaticRunGeneric<T>(string rowSet)
        {
            var csv = MakeCsv<T>(rowSet);

            var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();
            var config = Configuration.For<NarrowRow<T>>(opts);
            return
                () =>
                {
                    var ret = new List<T>();

                    using (var str = new StringReader(csv))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.EnumerateAll();
                        foreach (var row in rows)
                        {
                            ret.Add(row.Column);
                        }

                        return ret;
                    }
                };
        }

        private static MethodInfo MakeDynamicRunGenericMtd = typeof(NarrowRowDynamicReadSyncBenchmark).GetMethod(nameof(MakeDynamicRunGeneric), BindingFlags.NonPublic | BindingFlags.Static);
        private static Func<object> MakeDynamicRunGeneric<T>(string rowSet)
        {
            var csv = MakeCsv<T>(rowSet);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();
            var config = Configuration.ForDynamic(opts);
            return
                () =>
                {
                    var ret = new List<T>();

                    using (var str = new StringReader(csv))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.EnumerateAll();
                        foreach (var row in rows)
                        {
                            var col = row.Column;
                            var typed = (T)col;
                            ret.Add(typed);
                        }

                        return ret;
                    }
                };
        }
    }
}
