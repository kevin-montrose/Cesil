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
        public static void Main()
        {
            InitializeAndTest();

            RunBenchmarks();

            //    var foo = new WideRowDynamicWriteSyncBenchmark();
            //    foo.RowSet = "ShallowRows";
            //    foo.Initialize();

            //    for (var i = 0; i < 1_000; i++)
            //    {
            //        foo.Dynamic_Static();
            //    }
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
            Log(nameof(NarrowRowDynamicReadSyncBenchmark), () => new NarrowRowDynamicReadSyncBenchmark().InitializeAndTest());

            Log(nameof(WideRowDynamicWriteSyncBenchmark), () => new WideRowDynamicWriteSyncBenchmark().InitializeAndTest());



            Log(nameof(NameLookupBenchmark), () => new NameLookupBenchmark().InitializeAndTest());

            Log(nameof(NeedsEncodeBenchmark), () => new NeedsEncodeBenchmark().InitializeAndTest());

            Log(nameof(WideRowWriteSyncBenchmark), () => new WideRowWriteSyncBenchmark().InitializeAndTest());
            Log(nameof(NarrowRowWriteSyncBenchmark), () => new NarrowRowWriteSyncBenchmark().InitializeAndTest());

            Log(nameof(WideRowReadSyncBenchmark), () => new WideRowReadSyncBenchmark().InitializeAndTest());
            Log(nameof(NarrowRowReadSyncBenchmark), () => new NarrowRowReadSyncBenchmark().InitializeAndTest());

            Log(nameof(WideRowDynamicReadSyncBenchmark), () => new WideRowDynamicReadSyncBenchmark().InitializeAndTest());
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

            //BenchmarkRunner.Run(typeof(Program).Assembly, config);
            BenchmarkRunner.Run<WideRowDynamicWriteSyncBenchmark>(config);
        }
    }
}
