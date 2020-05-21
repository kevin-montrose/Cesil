using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    [BenchmarkCategory("Read")]
    public class WideRowReadSyncBenchmark
    {
        [ParamsSource(nameof(KnownRowSet))]
        public string RowSet { get; set; }

        public IEnumerable<string> KnownRowSet => new[] { nameof(WideRow.ShallowRows), nameof(WideRow.DeepRows) };

        private IBoundConfiguration<WideRow> CesilConfig;

        private CsvHelper.Configuration.CsvConfiguration CsvHelperConfig;

        private string CSV;

        private Func<TextReader, IEnumerable<WideRow>> DoCsvHelper;
        private Func<TextReader, IEnumerable<WideRow>> DoCesil;

        [GlobalSetup]
        public void Initialize()
        {
            WideRow.Initialize();

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

            DoCsvHelper = GetReadFunc(nameof(CsvHelper));
            DoCesil = GetReadFunc(nameof(Cesil));
        }

        public void InitializeAndTest()
        {
            foreach (var set in KnownRowSet)
            {
                RowSet = set;
                Initialize();

                var toRead = CSV;

                IEnumerable<WideRow> csvHelper, cesil;
                using (var str = new StringReader(toRead))
                {
                    csvHelper = DoCsvHelper(str);
                }
                using (var str = new StringReader(toRead))
                {
                    cesil = DoCesil(str);
                }

                if (csvHelper.Count() != cesil.Count()) throw new Exception();

                var rowCount = csvHelper.Count();

                for (var j = 0; j < rowCount; j++)
                {
                    var fRow = csvHelper.ElementAt(j);
                    var sRow = cesil.ElementAt(j);

                    if (!fRow.Equals(sRow)) throw new Exception();
                }

            }
        }

        [Benchmark(Baseline = true)]
        public void CsvHelper()
        {
            using (var str = new StringReader(CSV))
            {
                DoCsvHelper(str);
            }
        }

        [Benchmark]
        public void Cesil()
        {
            using (var str = new StringReader(CSV))
            {
                DoCesil(str);
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
