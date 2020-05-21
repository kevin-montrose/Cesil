using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    [BenchmarkCategory("Internals")]
    public class MemberOrderHelperBenchmark
    {
        [ParamsSource(nameof(DataSets))]
        public string DataSet { get; set; }

        [ParamsSource(nameof(DataSetSizes))]
        public int DataSetSize { get; set; }

        [ParamsSource(nameof(NoOrderPercents))]
        public double NoOrderPrecent { get; set; }

        public IEnumerable<string> DataSets => new[] { "InOrder", "InReverseOrder", "RandomOrder" };

        public IEnumerable<double> NoOrderPercents => new[] { 0, 0.25, 0.5, 0.75, 1 };

        public IEnumerable<int> DataSetSizes => new[] { 1, 5, 10, 50 };

        private IEnumerable<(int? Order, string Value)> Data;
        private IEnumerable<string> ExpectedFrontInOrder;
        private IEnumerable<string> ExpectedBackAnyOrder;

        private Comparison<(string _, int? Position)> Comparer;

        [GlobalSetup]
        public void Initialize()
        {
            var random = new Random(2020_05_20);

            var data = new List<(int? Order, string Value)>();
            for (var i = 0; i < DataSetSize; i++)
            {
                var hasOrder = random.NextDouble() > NoOrderPrecent;

                int? order;
                if (hasOrder)
                {
                    switch (DataSet)
                    {
                        case "InOrder": order = i; break;
                        case "InReverseOrder": order = DataSetSize - i; break;
                        case "RandomOrder": order = random.Next(); break;
                        default: throw new Exception();
                    }
                }
                else
                {
                    order = null;
                }

                var valueStr = "__" + i;
                data.Add((order, valueStr));
            }

            Data = data;

            ExpectedFrontInOrder = data.Where(d => d.Order != null).OrderBy(d => d.Order.Value).Select(x => x.Value).ToList();
            ExpectedBackAnyOrder = data.Where(d => d.Order == null).Select(x => x.Value).ToList();

            Comparer =
                (a, b) =>
                {
                    if (a.Position == null && b.Position == null) return 0;

                    if (a.Position == null) return 1;
                    if (b.Position == null) return -1;

                    return a.Position.Value.CompareTo(b.Position.Value);
                };
        }

        public void InitializeAndTest()
        {
            foreach (var set in DataSets)
            {
                foreach (var perc in NoOrderPercents)
                {
                    foreach (var size in DataSetSizes)
                    {
                        DataSet = set;
                        DataSetSize = size;
                        NoOrderPrecent = perc;

                        Data = null;
                        ExpectedFrontInOrder = null;
                        ExpectedBackAnyOrder = null;

                        Initialize();

                        var naive = DoNaive();
                        if (!Matches(naive)) throw new Exception();

                        var helper = DoHelper();
                        if (!Matches(helper)) throw new Exception();
                    }
                }
            }

            bool Matches(IEnumerable<string> e)
            {
                var front = e.Take(ExpectedFrontInOrder.Count()).ToList();

                if (!front.SequenceEqual(ExpectedFrontInOrder)) return false;

                var back = e.Skip(ExpectedFrontInOrder.Count()).ToList();

                if (back.Count != ExpectedBackAnyOrder.Count()) return false;

                foreach (var item in back)
                {
                    if (!ExpectedBackAnyOrder.Contains(item)) return false;
                }

                foreach (var item in ExpectedBackAnyOrder)
                {
                    if (!back.Contains(item)) return false;
                }

                return true;
            }
        }

        [Benchmark(Baseline = true)]
        public void Naive()
        {
            Benchmark(DoNaive());
        }

        [Benchmark]
        public void Helper()
        {
            Benchmark(DoHelper());
        }

        private static int Benchmark(IEnumerable<string> e)
        {
            // our use case does at least one count
            //     and typically one iteration
            var hash = e.Count();
            foreach (var item in e)
            {
                hash ^= item.GetHashCode();
            }

            return hash;
        }

        private IEnumerable<string> DoNaive()
        {
            // this is a way of implementing the EnumerateXXX() members of DefaultTypeDescriber

            var buffer = new List<(string Member, int? Position)>();
            foreach (var data in Data)
            {
                buffer.Add((data.Value, data.Order));
            }

            buffer.Sort(Comparer);

            return Map(buffer);

            static IEnumerable<string> Map(List<(string Member, int? Position)> ret)
            {
                foreach (var (member, _) in ret)
                {
                    yield return member;
                }
            }
        }

        private IEnumerable<string> DoHelper()
        {
            var helper = MemberOrderHelper<string>.Create();

            foreach (var data in Data)
            {
                helper.Add(data.Order, data.Value);
            }

            return helper;
        }
    }
}
