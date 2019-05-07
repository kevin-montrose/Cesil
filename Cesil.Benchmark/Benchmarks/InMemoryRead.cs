using BenchmarkDotNet.Attributes;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Cesil.Benchmark.Benchmarks
{
    [MemoryDiagnoser]
    [CoreJob]
    public class InMemoryRead
    {
        private const string CSV =
@"Hello,World,Fizz,Buzz
123,4.56,2018-03-05,""hello""
123,,2018-03-05,""he""""llo""
123,4.56,2018-03-05,hello
123,,2018-03-05,
123,4.56,2018-03-05,foo
123,,2018-03-05,bar
123,4.56,2018-03-05,""""""""
";

        private class Row
        {
            public string Buzz { get; set; }
            public DateTime Fizz { get; set; }
            public int Hello { get; set; }
            public double? World { get; set; }
        }

        private static readonly IBoundConfiguration<Row> Config = Configuration.For<Row>();

        [Benchmark]
        public void Cesil()
        {
            using (var reader = Config.CreateReader(new StringReader(CSV)))
            {
                var rows = reader.EnumerateAll().ToList();
                CheckCorrectness(rows);
            }
        }

        [Benchmark]
        public void CsvHelper()
        {
            using (var reader = new CsvReader(new StringReader(CSV)))
            {
                var rows = reader.GetRecords<Row>().ToList();
                CheckCorrectness(rows);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckCorrectness(List<Row> rows)
        {
            if (rows.Count != 7) throw new Exception();

            if (!rows.All(r => r.Hello == 123)) throw new Exception();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (i % 2 == 0)
                {
                    if (row.World != 4.56) throw new Exception();
                }
                else
                {
                    if (row.World != null) throw new Exception();
                }
            }

            if (!rows.All(r => r.Fizz == new DateTime(2018, 03, 05))) throw new Exception();

            if (rows[0].Buzz != "hello") throw new Exception();
            if (rows[1].Buzz != "he\"llo") throw new Exception();
            if (rows[2].Buzz != "hello") throw new Exception();
            if (!string.IsNullOrEmpty(rows[3].Buzz)) throw new Exception();
            if (rows[4].Buzz != "foo") throw new Exception();
            if (rows[5].Buzz != "bar") throw new Exception();
            if (rows[6].Buzz != "\"") throw new Exception();
        }
    }
}
