using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    [BenchmarkCategory("Write", "OneMillion", "ComparesLibraries")]
    public class WriteOneMillionRowsBenchmark
    {
        private IBoundConfiguration<WideRow> CesilConfig;

        private CsvHelper.Configuration.CsvConfiguration CsvHelperConfig;

        private Action<IEnumerable<WideRow>, TextWriter> DoCsvHelper;
        private Action<IEnumerable<WideRow>, TextWriter> DoCesil;

        private IEnumerable<WideRow> Rows;

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

            Rows = WideRow.OneMillion;

            DoCsvHelper = GetWriter(nameof(CsvHelper));
            DoCesil = GetWriter(nameof(Cesil));
        }

        public void InitializeAndTest()
        {
            Initialize();

            var csvHelper = GetText(WideRow.OneMillion, DoCsvHelper);
            var cesil = GetText(WideRow.OneMillion, DoCesil);

            if (csvHelper != cesil) throw new Exception();

            static string GetText(IEnumerable<WideRow> rows, Action<IEnumerable<WideRow>, TextWriter> del)
            {
                using (var str = new StringWriter())
                {
                    del(rows, str);

                    return str.ToString();
                }
            }
        }

        [Benchmark]
        public void CsvHelper()
        {
            DoCsvHelper(Rows, TextWriter.Null);
        }

        [Benchmark]
        public void Cesil()
        {
            DoCesil(Rows, TextWriter.Null);
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
