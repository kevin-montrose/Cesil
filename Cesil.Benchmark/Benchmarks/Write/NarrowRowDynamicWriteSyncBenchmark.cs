using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    [BenchmarkCategory("Write", "Dynamic")]
    public class NarrowRowDynamicWriteSyncBenchmark
    {
        private const int NUM_SHALLOW = 10;
        private const int NUM_DEEP = 10_000;

        [ParamsSource(nameof(KnownRowSets))]
        public string RowSet { get; set; }

        [ParamsSource(nameof(KnownTypes))]
        public Type Type { get; set; }

        public IEnumerable<string> KnownRowSets => new[] { "ShallowRows", "DeepRows" };

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

        private Action<TextWriter> WriteStaticRows;
        private Action<TextWriter> WriteDynamicRows_Static;
        private Action<TextWriter> WriteDynamicRows_Cesil;
        private Action<TextWriter> WriteDynamicRows_ExpandoObject;
        private Action<TextWriter> WriteDynamicRows_Custom;

        [GlobalSetup]
        public void Initialize()
        {
            WriteStaticRows = Make(MakeStaticRowWriter_Mtd);
            WriteDynamicRows_Static = Make(MakeDynamicStaticRowWriter_Mtd);
            WriteDynamicRows_Cesil = Make(MakeDynamicCesilRowWriter_Mtd);
            WriteDynamicRows_ExpandoObject = Make(MakeDynamicExpandoRowWriter_Mtd);
            WriteDynamicRows_Custom = Make(MakeDynamicCustomRowWriter_Mtd);

            Action<TextWriter> Make(MethodInfo mtd)
            {
                var bound = mtd.MakeGenericMethod(Type);
                var res = bound.Invoke(this, Array.Empty<object>());

                return (Action<TextWriter>)res;
            }
        }

        public void InitializeAndTest()
        {
            foreach(var type in KnownTypes)
            {
                Type = type;
                foreach(var rows in KnownRowSets)
                {
                    RowSet = rows;

                    WriteStaticRows = WriteDynamicRows_Static = WriteDynamicRows_Cesil = WriteDynamicRows_ExpandoObject = WriteDynamicRows_Custom = null;

                    Initialize();

                    var s = Write(WriteStaticRows);
                    var d = Write(WriteDynamicRows_Static);
                    var c = Write(WriteDynamicRows_Cesil);
                    var e = Write(WriteDynamicRows_ExpandoObject);
                    var e2 = Write(WriteDynamicRows_Custom);

                    if (s != d) throw new Exception();
                    if (s != c) throw new Exception();
                    if (s != e) throw new Exception();
                    if (s != e2) throw new Exception();
                }
            }

            static string Write(Action<TextWriter> del)
            {
                using (var txt = new StringWriter())
                {
                    del(txt);

                    return txt.ToString();
                }
            }
        }

        [Benchmark(Baseline = true)]
        public void Static()
        {
            WriteStaticRows(TextWriter.Null);
        }

        [Benchmark]
        public void Dynamic_Static()
        {
            WriteDynamicRows_Static(TextWriter.Null);
        }

        [Benchmark]
        public void Dynamic_Cesil()
        {
            WriteDynamicRows_Cesil(TextWriter.Null);
        }

        [Benchmark]
        public void Dynamic_ExpandoObject()
        {
            WriteDynamicRows_ExpandoObject(TextWriter.Null);
        }

        [Benchmark]
        public void Dynamic_Custom()
        {
            WriteDynamicRows_Custom(TextWriter.Null);
        }

        private static MethodInfo MakeStaticRowWriter_Mtd = typeof(NarrowRowDynamicWriteSyncBenchmark).GetMethod(nameof(MakeStaticRowWriter), BindingFlags.NonPublic | BindingFlags.Instance);
        private Action<TextWriter> MakeStaticRowWriter<T>()
        {
            var rows = GetStaticRows<T>();

            var config = Configuration.For<NarrowRow<T>>();

            return
                (txt) =>
                {
                    using (var csv = config.CreateWriter(txt))
                    {
                        csv.WriteAll(rows);
                    }
                };
        }

        private static MethodInfo MakeDynamicStaticRowWriter_Mtd = typeof(NarrowRowDynamicWriteSyncBenchmark).GetMethod(nameof(MakeDynamicStaticRowWriter), BindingFlags.NonPublic | BindingFlags.Instance);
        private Action<TextWriter> MakeDynamicStaticRowWriter<T>()
        {
            var rows = GetStaticRows<T>();

            return MakeDynamicWriter(rows);
        }

        private static MethodInfo MakeDynamicCesilRowWriter_Mtd = typeof(NarrowRowDynamicWriteSyncBenchmark).GetMethod(nameof(MakeDynamicCesilRowWriter), BindingFlags.NonPublic | BindingFlags.Instance);
        private Action<TextWriter> MakeDynamicCesilRowWriter<T>()
        {
            IEnumerable<dynamic> rows;

            {
                var staticRows = GetStaticRows<T>();
                var staticConfig = Configuration.For<NarrowRow<T>>();

                string csvTxt;
                
                using (var txt = new StringWriter())
                {
                    using (var csv = staticConfig.CreateWriter(txt))
                    {
                        csv.WriteAll(staticRows);
                    }

                    csvTxt = txt.ToString();
                }

                // have to slap an extra new line on there because this is ambiguous
                var lastRow = (object)staticRows.Last().Column;
                if (lastRow == null || lastRow.Equals(""))
                {
                    csvTxt += Environment.NewLine;
                }

                var opts = Options.CreateBuilder(Options.DynamicDefault).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
                var noDisposeConfig = Configuration.ForDynamic(opts);
                using (var txt = new StringReader(csvTxt))
                using (var csv = noDisposeConfig.CreateReader(txt))
                {
                    rows = csv.ReadAll();
                }
            }

            return MakeDynamicWriter(rows);
        }

        private static MethodInfo MakeDynamicExpandoRowWriter_Mtd = typeof(NarrowRowDynamicWriteSyncBenchmark).GetMethod(nameof(MakeDynamicExpandoRowWriter), BindingFlags.NonPublic | BindingFlags.Instance);
        private Action<TextWriter> MakeDynamicExpandoRowWriter<T>()
        {
            IEnumerable<dynamic> rows;

            {
                var staticRows = GetStaticRows<T>();

                var expandos = new List<ExpandoObject>();

                foreach(var row in staticRows)
                {
                    var expandoObj = new ExpandoObject();
                    var expando = (IDictionary<string, dynamic>)expandoObj;
                    expando[nameof(row.Column)] = row.Column;

                    expandos.Add(expandoObj);
                }

                rows = expandos;
            }

            return MakeDynamicWriter(rows);
        }

        private static MethodInfo MakeDynamicCustomRowWriter_Mtd = typeof(NarrowRowDynamicWriteSyncBenchmark).GetMethod(nameof(MakeDynamicCustomRowWriter), BindingFlags.NonPublic | BindingFlags.Instance);
        private Action<TextWriter> MakeDynamicCustomRowWriter<T>()
        {
            IEnumerable<dynamic> rows;

            {
                var staticRows = GetStaticRows<T>();

                var expandos = new List<FakeExpandoObject>();

                foreach (var row in staticRows)
                {
                    var expandoObj = new FakeExpandoObject();
                    var expando = (IDictionary<string, dynamic>)expandoObj;
                    expando[nameof(row.Column)] = row.Column;

                    expandos.Add(expandoObj);
                }

                rows = expandos;
            }

            return MakeDynamicWriter(rows);
        }

        private static Action<TextWriter> MakeDynamicWriter(IEnumerable<dynamic> rows)
        {
            var config = Configuration.ForDynamic();

            return
                (txt) =>
                {
                    using (var csv = config.CreateWriter(txt))
                    {
                        csv.WriteAll(rows);
                    }
                };
        }

        private IEnumerable<NarrowRow<T>> GetStaticRows<T>()
        {
            var r = MakeRandom();
            IEnumerable<NarrowRow<T>> rows;

            switch (RowSet)
            {
                case "ShallowRows": rows = Enumerable.Range(0, NUM_SHALLOW).Select(_ => NarrowRow<T>.Create(r)).ToArray(); break;
                case "DeepRows": rows = Enumerable.Range(0, NUM_DEEP).Select(_ => NarrowRow<T>.Create(r)).ToArray(); break;
                default: throw new Exception();
            }

            return rows;
        }

        private static Random MakeRandom()
        => new Random(2020_03_04);
    }
}
