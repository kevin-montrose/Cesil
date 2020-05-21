using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using Perfolizer.Horology;

namespace Cesil.Benchmark
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            // real dumb way to select benchmarks
            // 
            // can include by BenchmarkCategory by just passing the name
            //     or exclude by passing -<name>
            //
            // empty is treated as include all
            //
            // * matches everything and -* excludes everything
            //
            // exclusions trump inclusions
            args = args.Length == 0 ? new[] { "*" } : args;

            var include = new List<string>();
            var exclude = new List<string>();

            foreach (var arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    exclude.Add(arg.Substring(1));
                }
                else
                {
                    include.Add(arg);
                }
            }

            var benchmarks = FindBenchmarks(include, exclude);

            InitializeAndTest(benchmarks);

            RunBenchmarks(benchmarks);
        }

        private static void Log(string value, Action act)
        {
            Log($"{value} starting...");
            var sw = Stopwatch.StartNew();
            act();
            sw.Stop();
            Log($" \tFinished (duration {sw.Elapsed})");
        }

        private static void Log(string value)
        {
            Console.WriteLine($"[{DateTime.UtcNow:u}] {value}");
        }

        private static IEnumerable<Type> FindBenchmarks(IEnumerable<string> matchCategories, IEnumerable<string> dontMatchCategories)
        {
            var benchmarks = typeof(Program).Assembly.GetTypes().Where(t => t.IsClass && t.GetMethods().Any(m => m.GetCustomAttribute<BenchmarkAttribute>() != null)).ToList();

            foreach (var benchmark in benchmarks)
            {
                var category = benchmark.GetCustomAttribute<BenchmarkCategoryAttribute>();

                var keep = false;
                if (category != null)
                {
                    foreach (var c in matchCategories)
                    {
                        if (c == "*")
                        {
                            keep = true;
                            break;
                        }

                        if (category.Categories.Any(x => x.Equals(c, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            keep = true;
                            break;
                        }
                    }

                    foreach (var c in dontMatchCategories)
                    {
                        if (c == "*")
                        {
                            keep = false;
                            break;
                        }

                        if (category.Categories.Any(x => x.Equals(c, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            keep = false;
                            break;
                        }
                    }
                }
                else
                {
                    keep = true;
                }

                if (!keep) continue;

                var withAttrs = benchmark.GetMethods().Where(m => m.GetCustomAttribute<BenchmarkAttribute>() != null).ToList();
                var baselines = withAttrs.Where(m => m.GetCustomAttribute<BenchmarkAttribute>()?.Baseline ?? false).ToList();

                if (baselines.Count == 0)
                {
                    throw new Exception($"Benchmark {benchmark.Name} does not have a {nameof(BenchmarkAttribute.Baseline)}");
                }

                if (baselines.Count > 1)
                {
                    throw new Exception($"Benchmark {benchmark.Name} has multiple {nameof(BenchmarkAttribute.Baseline)} = true");
                }
            }

            return benchmarks;
        }

        [Conditional("DEBUG")]
        private static void InitializeAndTest(IEnumerable<Type> needChecking)
        {
            const string TEST_METHOD_NAME = "InitializeAndTest";

            foreach (var b in needChecking)
            {
                var name = b.Name;
                var test = b.GetMethod("InitializeAndTest");
                if (test == null)
                {
                    throw new Exception($"Benchmark {name} doesn't have a {TEST_METHOD_NAME} method");
                }

                if (test.GetParameters().Length != 0)
                {
                    throw new Exception($"Benchmark {name} has a {TEST_METHOD_NAME} that takes parameters");
                }

                var inst = Activator.CreateInstance(b);
                var del = (Action)Delegate.CreateDelegate(typeof(Action), inst, test);

                Log($"Test of {name}", del);
            }
        }

        private static void RunBenchmarks(IEnumerable<Type> benchmarks)
        {
            var config = ManualConfig.CreateEmpty();
            config.AddValidator(JitOptimizationsValidator.DontFailOnError); // ALLOW NON-OPTIMIZED DLLS
            config.AddLogger(DefaultConfig.Instance.GetLoggers().ToArray()); // manual config has no loggers by default
            config.AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray()); // manual config has no columns by default
            config.AddDiagnoser(MemoryDiagnoser.Default); // include GC columns
            config.AddExporter(
                new CsvExporter(
                    CsvSeparator.CurrentCulture,
                    new SummaryStyle(
                        CultureInfo.InvariantCulture,
                        true,
                        SizeUnit.B,
                        TimeUnit.Nanosecond
                    )
                )
            );
            config.AddExporter(HtmlExporter.Default);

            foreach (var b in benchmarks)
            {
                BenchmarkRunner.Run(b, config);
            }
        }
    }
}
