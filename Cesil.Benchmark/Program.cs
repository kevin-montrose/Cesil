using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using Cesil.Benchmark.Benchmarks;
using System.IO;
using System.Linq;

namespace Cesil.Benchmark
{
    internal class Program
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

        public static void Main()
        {
            RunBenchmarks();

            //var w = new BigWrite();
            //w.Setup();
            //w.Cesil();
        }

        private static void RunBenchmarks()
        {
            var config = ManualConfig.CreateEmpty();
            config.Add(JitOptimizationsValidator.DontFailOnError); // ALLOW NON-OPTIMIZED DLLS
            config.Add(DefaultConfig.Instance.GetLoggers().ToArray()); // manual config has no loggers by default
            config.Add(DefaultConfig.Instance.GetExporters().ToArray()); // manual config has no exporters by default
            config.Add(DefaultConfig.Instance.GetColumnProviders().ToArray()); // manual config has no columns by default

            if (File.Exists(BigRead.Path))
            {
                //BenchmarkRunner.Run<BigRead>(config);
                BenchmarkRunner.Run<BigWrite>(config);
            }

            //BenchmarkRunner.Run<InMemoryRead>(config);
        }
    }
}
