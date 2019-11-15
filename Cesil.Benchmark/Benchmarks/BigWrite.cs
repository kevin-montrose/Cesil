using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using C = Csv;
using CH = CsvHelper;

namespace Cesil.Benchmark.Benchmarks
{
    [MemoryDiagnoser]
    [CoreJob]
    public class BigWrite
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
        private static readonly string[] Headers = new[] { nameof(Row.CreationDate), nameof(Row.Url), nameof(Row.Referer), nameof(Row.UserIdentifier), nameof(Row.EventTarget), nameof(Row.EventSource) };
        private static readonly Func<Row, string[]> RowMapper = r => new[] { "" + r.CreationDate, r.Url ?? "", r.Referer ?? "", r.UserIdentifier ?? "", r.EventTarget ?? "", r.EventSource ?? "" };
        private static readonly IBoundConfiguration<Row> Config = Configuration.For<Row>(Options.CreateBuilder(Options.Default).WithWriteTrailingNewLine(WriteTrailingNewLine.Always).ToOptions());
        private const int TakeRows = 10_000;
        private const int Repeat = 100;

        private Row[] ToWrite;

        [GlobalSetup]
        public void Setup()
        {
            using (var fs = File.OpenRead(Path))
            using (var reader = new StreamReader(fs))
            using (var csv = Config.CreateReader(reader))
            {
                ToWrite = csv.EnumerateAll().Take(TakeRows).ToArray();
            }
        }

        [Benchmark]
        public void Cesil()
        {
            using (var str = new StringWriter())
            {
                using (var csv = Config.CreateWriter(str))
                {
                    for (var i = 0; i < Repeat; i++)
                    {
                        csv.WriteAll(ToWrite);
                    }
                }

                GC.KeepAlive(str.ToString());
            }
        }

        [Benchmark]
        public void CsvHelper()
        {
            using (var str = new StringWriter())
            {
                using (var csv = new CH.CsvWriter(str))
                {
                    csv.WriteHeader<Row>();
                    csv.NextRecord();
                    for (var i = 0; i < Repeat; i++)
                    {
                        csv.WriteRecords(ToWrite);
                    }
                }

                GC.KeepAlive(str.ToString());
            }
        }

        [Benchmark]
        public void Csv()
        {
            using (var str = new StringWriter())
            {
                C.CsvWriter.Write(
                    str,
                    Headers,
                    Enumerable.Repeat(ToWrite, Repeat).SelectMany(rs => rs).Select(RowMapper)
                );

                GC.KeepAlive(str.ToString());
            }
        }
    }
}
