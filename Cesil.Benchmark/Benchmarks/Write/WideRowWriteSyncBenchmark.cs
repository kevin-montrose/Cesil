using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    public class WideRowWriteSyncBenchmark
    {
        [ParamsSource(nameof(KnownRowSet))]
        public string RowSet { get; set; }

        [ParamsSource(nameof(KnownLibraries))]
        public string Library { get; set; }

        public IEnumerable<string> KnownRowSet => new[] { nameof(WideRow.ShallowRows), nameof(WideRow.DeepRows) };

        public IEnumerable<string> KnownLibraries => new[] { nameof(Cesil), nameof(CsvHelper) };

        private IBoundConfiguration<WideRow> CesilConfig;

        private CsvHelper.Configuration.CsvConfiguration CsvHelperConfig;

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
        }

        public void InitializeAndTest()
        {
            Initialize();

            foreach (var row in KnownRowSet)
            {
                var rows = GetRows(row);

                var shouldMatch =
                    KnownLibraries
                        .Select(
                            x =>
                            {
                                using (var str = new StringWriter())
                                {
                                    GetWriter(x)(rows, str);

                                    var res = str.ToString();

                                    return (Library: x, Text: res);
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

        [Benchmark]
        public void Run()
        {
            var rows = GetRows(RowSet);
            var writerRows = GetWriter(Library);

            writerRows(rows, TextWriter.Null);
        }

        private Action<IEnumerable<WideRow>, TextWriter> GetWriter(string forLibrary)
        {
            switch (forLibrary)
            {
                case nameof(CsvHelper): return WriteWithCsvHelper;
                case nameof(Cesil): return WriteWithCesil;
                default: throw new InvalidOperationException();
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

        private void WriteWithCesil(IEnumerable<WideRow> rows, TextWriter into)
        {
            using (var writer = CesilConfig.CreateWriter(into))
            {
                writer.WriteAll(rows);
            }
        }

        private void WriteWithCsvHelper(IEnumerable<WideRow> rows, TextWriter into)
        {
            using (var writer = new CsvHelper.CsvWriter(into, CsvHelperConfig))
            {
                writer.WriteRecords(rows);
            }
        }
    }
}
