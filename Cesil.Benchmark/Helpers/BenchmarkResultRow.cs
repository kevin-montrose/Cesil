namespace Cesil.Benchmark
{
    internal class BenchmarkResultRow
    {
        public string Name { get; set; }

        public string Parameters { get; set; }

        public string Library { get; set; }

        public double MedianNanoseconds { get; set; }

        public double AllocatedBytes { get; set; }

    }
}
