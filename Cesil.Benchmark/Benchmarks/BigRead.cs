using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using C = Csv;
using CSH = CsvHelper;


namespace Cesil.Benchmark.Benchmarks
{
    [MemoryDiagnoser]
    [CoreJob]
    public class BigRead
    {
        private class Row
        {
            public long CreationDate { get; set; }
            public string Url { get; set; }
            public string Referer { get; set; }
            public string UserIdentifier { get; set; }
            public string EventTarget { get; set; }
            public string EventSource { get; set; }
        }

        public const string Path = @"D:\InternalRefs.csv";
        private static readonly IBoundConfiguration<Row> Config = Configuration.For<Row>();
        private const int TakeRows = 100_000;

        [Benchmark]
        public void Cesil()
        {
            long poorHash = 0;

            using (var fs = File.OpenRead(Path))
            using (var reader = new StreamReader(fs))
            using (var csv = Config.CreateReader(reader))
            {
                foreach (var row in csv.EnumerateAll().Take(TakeRows))
                {
                    poorHash += row.CreationDate;
                }
            }

            //System.Diagnostics.Debug.WriteLine("Cesil: " + poorHash);
        }

        [Benchmark]
        public void CsvHelper()
        {
            long poorHash = 0;

            using (var fs = File.OpenRead(Path))
            using (var reader = new StreamReader(fs))
            using (var csv = new CSH.CsvReader(reader))
            {
                foreach (var row in csv.GetRecords<Row>().Take(TakeRows))
                {
                    poorHash += row.CreationDate;
                }
            }

            //System.Diagnostics.Debug.WriteLine("CSVHelper: " + poorHash);
        }

        [Benchmark]
        public void Csv()
        {
            long poorHash = 0;

            using (var fs = File.OpenRead(Path))
            {
                foreach (var row in C.CsvReader.ReadFromStream(fs).Take(TakeRows))
                {
                    // Csv doesn't have a mapper... so do something roughly fair?
                    poorHash += long.Parse(row[nameof(Row.CreationDate)]);
                }
            }

            //System.Diagnostics.Debug.WriteLine("CSVHelper: " + poorHash);
        }
    }
}
