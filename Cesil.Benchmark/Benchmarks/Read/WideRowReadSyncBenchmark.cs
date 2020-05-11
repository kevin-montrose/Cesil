using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    public class WideRowReadSyncBenchmark
    {
        [ParamsSource(nameof(KnownRowSet))]
        public string RowSet { get; set; }

        [ParamsSource(nameof(KnownLibraries))]
        public string Library { get; set; }

        public IEnumerable<string> KnownRowSet => new[] { nameof(WideRow.ShallowRows), nameof(WideRow.DeepRows) };

        public IEnumerable<string> KnownLibraries => new[] { nameof(Cesil), nameof(CsvHelper) };

        private IBoundConfiguration<WideRow> CesilConfig;

        private CsvHelper.Configuration.CsvConfiguration CsvHelperConfig;

        private string CSV;

        [GlobalSetup]
        public void Initialize()
        {
            WideRow.Initialize();

            if (CesilConfig != null && CsvHelperConfig != null) return;

            // Configure Cesil
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();
                CesilConfig = Configuration.For<WideRow>(opts);
            }

            // Configure CsvHelper
            {
                CsvHelperConfig = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
                CsvHelperConfig.RegisterClassMap<WideRowMapping>();
            }

            CSV = MakeCSV();
        }

        public void InitializeAndTest()
        {
            foreach (var set in KnownRowSet)
            {
                RowSet = set;
                Library = nameof(Cesil);

                Initialize();

                var toRead = CSV;

                var res =
                    KnownLibraries
                        .Select(
                            lib =>
                            {
                                var f = GetReadFunc(lib);
                                using (var str = new StringReader(toRead))
                                {
                                    var rows = f(str);

                                    return (Library: lib, Rows: rows);
                                }
                            }
                        )
                        .ToList();

                for (var i = 1; i < res.Count; i++)
                {
                    var first = res[0];
                    var second = res[i];

                    if (first.Rows.Count() != second.Rows.Count()) throw new Exception();

                    for (var j = 0; j < first.Rows.Count(); j++)
                    {
                        var fRow = first.Rows.ElementAt(j);
                        var sRow = second.Rows.ElementAt(j);

                        if (!fRow.Equals(sRow)) throw new Exception();
                    }
                }
            }
        }

        [Benchmark]
        public void Run()
        {
            var f = GetReadFunc(Library);
            using (var str = new StringReader(CSV))
            {
                f(str);
            }
        }

        private string MakeCSV()
        {
            using (var str = new StringWriter())
            {
                using (var csv = CesilConfig.CreateWriter(str))
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

        private Func<TextReader, IEnumerable<WideRow>> GetReadFunc(string lib)
        {
            switch (lib)
            {
                case nameof(Cesil):
                    return (reader) =>
                    {
                        using (var csv = CesilConfig.CreateReader(reader))
                        {
                            return csv.ReadAll();
                        }
                    };
                case nameof(CsvHelper):
                    return (reader) =>
                    {
                        using (var csv = new CsvHelper.CsvReader(reader, CsvHelperConfig))
                        {
                            return csv.GetRecords<WideRow>().ToList();
                        }
                    };

                default: throw new Exception();
            }
        }
    }
}
