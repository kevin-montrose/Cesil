using System;
using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;

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
            InitializeAndTest();

            RunBenchmarks();
        }

        private static void Log(string value, Action act)
        {
            Console.WriteLine($"[{DateTime.UtcNow:u}] {value} starting...");
            var sw = Stopwatch.StartNew();
            act();
            sw.Stop();
            Console.WriteLine($"[{DateTime.UtcNow:u}] \tFinished (duration {sw.Elapsed})");
        }

        [Conditional("DEBUG")]
        private static void InitializeAndTest()
        {
            Log(nameof(NameLookupBenchmark.InitializeAndTest), () => new NameLookupBenchmark().InitializeAndTest());

            Log(nameof(NeedsEncodeBenchmark.InitializeAndTest), () => new NeedsEncodeBenchmark().InitializeAndTest());

            Log(nameof(WideRowWriteSyncBenchmark.InitializeAndTest), () => new WideRowWriteSyncBenchmark().InitializeAndTest());
            Log(nameof(NarrowRowWriteSyncBenchmark.InitializeAndTest), () => new NarrowRowWriteSyncBenchmark().InitializeAndTest());

            Log(nameof(WideRowReadSyncBenchmark.InitializeAndTest), () => new WideRowReadSyncBenchmark().InitializeAndTest());
            Log(nameof(NarrowRowReadSyncBenchmark.InitializeAndTest), () => new NarrowRowReadSyncBenchmark().InitializeAndTest());

            Log(nameof(WideRowDynamicReadSyncBenchmark.InitializeAndTest), () => new WideRowDynamicReadSyncBenchmark().InitializeAndTest());
        }

        private static void RunBenchmarks()
        {
            var config = ManualConfig.CreateEmpty();
            config.Add(JitOptimizationsValidator.DontFailOnError); // ALLOW NON-OPTIMIZED DLLS
            config.Add(DefaultConfig.Instance.GetLoggers().ToArray()); // manual config has no loggers by default
            config.Add(DefaultConfig.Instance.GetExporters().ToArray()); // manual config has no exporters by default
            config.Add(DefaultConfig.Instance.GetColumnProviders().ToArray()); // manual config has no columns by default
            config.Add(MemoryDiagnoser.Default); // include GC columns
            config.Add(
                new CsvExporter(
                    CsvSeparator.CurrentCulture,
                    new BenchmarkDotNet.Reports.SummaryStyle(
                        true, 
                        BenchmarkDotNet.Columns.SizeUnit.B,
                        BenchmarkDotNet.Horology.TimeUnit.Nanosecond
                    )
                )
            );

            BenchmarkRunner.Run(typeof(Program).Assembly, config);
        }
    }
}
