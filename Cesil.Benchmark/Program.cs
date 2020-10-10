using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Validators;
using CommandLine;
using Perfolizer.Horology;

namespace Cesil.Benchmark
{
    internal class Program
    {
        public class Options
        {
            // advanced options for running the _same_ set of benchmarks a bunch of times over different points in git history

            [Option(
                'g',
                "git-repository",
                HelpText = "Url for the Cesil git repository",
                Required = false
            )]
            public string GitRepo { get; set; }

            [Option(
                'b',
                "git-branch",
                HelpText = "Branch name for the Cesil git repository",
                Default = "main",
                Required = false
            )]
            public string GitBranch { get; set; }

            [Option(
                'h',
                "git-hashes",
                HelpText = "Git commit hashes to explore, or ranges to explore.  Ranges are specified with a dash separator, like 1234-5678.  Defaults to all commits.",
                Required = false
            )]
            public IEnumerable<string> GitHashes { get; set; }

            [Option(
                'r',
                "git-benchmark-hash",
                HelpText = "Git commit that contains the version of the benchmarks to run.  Defaults to most recent commit.",
                Required = false
            )]
            public string GitBenchmarkHash { get; set; }

            // options just for running the benchmarks

            [Option(
                'c',
                "compare-to-library",
                Default = new string[0],
                HelpText = "Libraries to compare Cesil to",
                Required = false,
                Separator = ','
            )]
            public IEnumerable<string> CompareToOtherLibraries { get; set; }

            [Option(
                'o',
                "output",
                HelpText = "File to write a summary of the run to",
                Required = false
            )]
            public string OutputPath { get; set; }

            [Option(
                'i',
                "include-benchmark",
                HelpText = "Benchmark to run, if not specified all benchmarks are run",
                Default = new string[0],
                Required = false,
                Separator = ','
            )]
            public IEnumerable<string> IncludeBenchmarks { get; set; }

            [Option(
                'e',
                "exclude-benchmark",
                HelpText = "Benchmark to not run",
                Default = new string[0],
                Required = false,
                Separator = ','
            )]
            public IEnumerable<string> ExcludeBenchmarks { get; set; }

            [Option(
                'j',
                "include-category",
                HelpText = "Category of benchmarks to run, if not specified all categories are runnable",
                Default = new string[0],
                Required = false,
                Separator = ','
            )]
            public IEnumerable<string> IncludeCategories { get; set; }

            [Option(
                'f',
                "exclude-category",
                HelpText = "Category of benchmarks to not run",
                Default = new string[0],
                Required = false,
                Separator = ','
            )]
            public IEnumerable<string> ExcludeCategories { get; set; }

            // debug-y options

            [Option(
                't',
                "test",
#if DEBUG
                Default = "True",
#else
                Default = "False",
#endif
                HelpText = "If set runs validation checks before running benchmarks, defaults to true in DEBUG builds",
                Required = false
            )]
            public string Test { get; set; }

            [Option(
                'd',
                "debug",
#if DEBUG
                Default = "True",
#else
                Default = "False",
#endif
                HelpText = "If set, will launch a debugger once args are parsed",
                Required = false
            )]
            public string Debug { get; set; }
        }

        public static void Main(string[] args)
        {
            using var proc = Process.GetCurrentProcess();

            Console.WriteLine($"Starting Process: {proc.Id}");

            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunOptions)
                .WithNotParsed(HandleParseError);
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            Console.Error.WriteLine("Invalid arguments");
            Console.Error.WriteLine("-----------------");

            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }

            Environment.Exit(-1);
        }

        private static void RunOptions(Options opts)
        {
            Console.WriteLine($"Got options: {CommandLine.Parser.Default.FormatCommandLine(opts)}");

            if ((opts.Debug?.Equals("true", StringComparison.InvariantCultureIgnoreCase) ?? false) && !Debugger.IsAttached)
            {
                Debugger.Break();
            }

            if (!string.IsNullOrEmpty(opts.GitRepo))
            {
                ExploreGitHistory(opts);
            }
            else
            {
                RunBenchmarks(opts);
            }
        }

        private static void ExploreGitHistory(Options opts)
        {
            Console.WriteLine(nameof(ExploreGitHistory));

            var repo = opts.GitRepo;
            var branch = opts.GitBranch;
            var hashes = opts.GitHashes;

            var name = $"{nameof(Cesil)}.{nameof(Benchmark)}-{Guid.NewGuid().ToString().Replace("-", "")}";
            var dir = Path.Combine(Path.GetTempPath(), name);
            Directory.CreateDirectory(dir);

            var latest = Path.Combine(dir, "latest");

            CloneRepository(latest, repo, branch);

            var allCommits = GetAllCommitHashes(latest);
            var selectedCommits = GetSelectedCommitHashes(hashes, allCommits);

            var benchmarkCommit = opts.GitBenchmarkHash;
            if (string.IsNullOrEmpty(benchmarkCommit))
            {
                benchmarkCommit = allCommits.Last();
            }

            if (!allCommits.Contains(benchmarkCommit))
            {
                Console.Error.WriteLine($"Could not find benchmark commit {benchmarkCommit}");
                Environment.Exit(-1);
            }

            var validCommits = GetValidCommits(latest, selectedCommits, benchmarkCommit);

            var resultsDir = Path.Combine(dir, "results");
            var results = RunBenchmarksForCommits(opts, latest, resultsDir, validCommits, benchmarkCommit);
            var summary = SummarizeResults(results, validCommits);

            var csv = CesilUtils.WriteDynamicToString(summary);

            Console.WriteLine("Results");
            Console.WriteLine("-------");
            Console.WriteLine(csv);

            if (!string.IsNullOrEmpty(opts.OutputPath))
            {
                File.WriteAllText(opts.OutputPath, csv);
            }

            Environment.Exit(0);

            static List<dynamic> SummarizeResults(Dictionary<string, List<BenchmarkResultRow>> results, IEnumerable<string> validCommits)
            {
                var distinctBenchmarks = results.SelectMany(l => l.Value.Select(x => (x.Name, x.Parameters))).Distinct().OrderBy(x => x).ToList();
                var distinctLibraries = results.SelectMany(l => l.Value.Select(x => x.Library)).Distinct().OrderBy(x => x).ToList();

                var ret = new List<dynamic>();

                foreach (var commit in validCommits)
                {
                    if (!results.TryGetValue(commit, out var res))
                    {
                        res = new List<BenchmarkResultRow>();
                    }

                    var row = new ExpandoObject();
                    var rowDict = (IDictionary<string, dynamic>)row;

                    rowDict["Ordinal"] = ret.Count;
                    rowDict["Commit"] = commit;
                    foreach (var bench in distinctBenchmarks)
                    {
                        var relevant = res.Where(r => r.Name == bench.Name && r.Parameters == bench.Parameters).ToList();
                        foreach (var library in distinctLibraries)
                        {
                            var r = relevant.SingleOrDefault(r => r.Library == library);
                            var runtimeNS = r?.MedianNanoseconds.ToString();
                            var allocBytes = r?.AllocatedBytes.ToString();

                            rowDict[library + ":" + bench.Name + ":" + bench.Parameters + "_RuntimeNanoseconds"] = runtimeNS;
                            rowDict[library + ":" + bench.Name + ":" + bench.Parameters + "_AllocatedBytes"] = allocBytes;
                        }
                    }

                    ret.Add(row);
                }

                return ret;
            }

            static Dictionary<string, List<BenchmarkResultRow>> RunBenchmarksForCommits(Options opts, string inDirectory, string outDirectory, IEnumerable<string> commits, string benchmarkCommit)
            {
                var ret = new Dictionary<string, List<BenchmarkResultRow>>();

                Directory.CreateDirectory(outDirectory);

                foreach (var commit in commits)
                {
                    var outFile = Path.Combine(outDirectory, $"{commit.Substring(0, 7)}.csv");

                    var newOpts =
                        new Options
                        {
                            CompareToOtherLibraries = opts.CompareToOtherLibraries,
                            ExcludeBenchmarks = opts.ExcludeBenchmarks,
                            ExcludeCategories = opts.ExcludeCategories,
                            IncludeBenchmarks = opts.IncludeBenchmarks,
                            IncludeCategories = opts.IncludeCategories,
                            Test = opts.Test,
                            OutputPath = outFile
                        };

                    var args = CommandLine.Parser.Default.FormatCommandLine(newOpts);

                    // roll everything back
                    var resetRes = RunCommand(inDirectory, "git reset --hard HEAD");
                    CheckCommandResult(resetRes);

                    // get this specific commit
                    var rollbackRes = RunCommand(inDirectory, $"git checkout {commit}");
                    CheckCommandResult(rollbackRes);

                    // checkout the target version of the benchmark alongside this commit
                    var benchmarkRes = RunCommand(inDirectory, $"git checkout {benchmarkCommit} -- .\\Cesil.Benchmark\\");
                    CheckCommandResult(benchmarkRes);

                    // compile
                    var compileRes = RunCommand(inDirectory, "dotnet build -c RELEASE");
                    CheckCommandResult(compileRes);

                    // run the benchmarks (potentially with a retry)
                    var runCommand = $"dotnet run -c RELEASE --no-build -- {args}";
                    var benchmarkDir = Path.Combine(inDirectory, "Cesil.Benchmark");

                    var retry = true;
runBenchmark:
                    var runRes = RunCommand(benchmarkDir, runCommand);
                    if (runRes.ExitCode != 0)
                    {
                        Console.Error.WriteLine("An error occurred");
                        Console.Error.WriteLine();
                        Console.Error.WriteLine("Output");
                        Console.Error.WriteLine(runRes.Output);
                        Console.Error.WriteLine();
                        if (!retry)
                        {
                            Console.Error.WriteLine("Skipping");
                            continue;
                        }

                        retry = false;

                        Thread.Sleep(5_000);
                        goto runBenchmark;
                    }

                    var results = CesilUtils.EnumerateFromFile<BenchmarkResultRow>(outFile);

                    ret[commit] = results.ToList();
                }

                return ret;
            }

            static List<string> GetValidCommits(string inDirectory, IEnumerable<string> allCommits, string benchmarkCommit)
            {
                var ret = new List<string>();

                foreach (var commit in allCommits)
                {
                    // roll everything back
                    var resetRes = RunCommand(inDirectory, "git reset --hard HEAD");
                    CheckCommandResult(resetRes);

                    // get this specific commit
                    var rollbackRes = RunCommand(inDirectory, $"git checkout {commit}");
                    CheckCommandResult(rollbackRes);

                    // checkout the target version of the benchmark alongside this commit
                    var benchmarkRes = RunCommand(inDirectory, $"git checkout {benchmarkCommit} -- .\\Cesil.Benchmark\\");
                    CheckCommandResult(benchmarkRes);

                    // does it compile?
                    var compileRes = RunCommand(inDirectory, "dotnet build -c RELEASE");
                    if (compileRes.ExitCode == 0)
                    {
                        // if so, we should try it
                        ret.Add(commit);
                    }
                }

                return ret;
            }

            static List<string> GetSelectedCommitHashes(IEnumerable<string> hashes, List<string> allCommits)
            {
                if (!hashes.Any())
                {
                    return allCommits;
                }

                var selectedCommitsToTry = new HashSet<string>();

                foreach (var hash in hashes)
                {
                    var isRange = hash.Contains('-');
                    if (!isRange)
                    {
                        var commits = allCommits.Where(a => a.StartsWith(hash, StringComparison.InvariantCultureIgnoreCase)).ToList();
                        if (commits.Count != 1)
                        {
                            Console.Error.WriteLine($"Couldn't find single commit for hash: {hash}");
                            Environment.Exit(-1);
                        }

                        selectedCommitsToTry.Add(commits.Single());
                    }
                    else
                    {
                        var ix = hash.IndexOf('-');
                        var startCommit = hash.Substring(0, ix);
                        var endCommit = hash.Substring(ix + 1);

                        var startCommitsIx = allCommits.Select((a, b) => new { Hash = a, Index = b }).Where(a => a.Hash.StartsWith(startCommit, StringComparison.InvariantCultureIgnoreCase)).ToList();
                        if (startCommitsIx.Count != 1)
                        {
                            Console.Error.WriteLine($"Couldn't find single commit for hash: {startCommit} (in range {hash})");
                            Environment.Exit(-1);
                        }

                        var startIx = startCommitsIx[0].Index;

                        var endCommitIx = allCommits.Select((a, b) => new { Hash = a, Index = b }).Where(a => a.Hash.StartsWith(endCommit, StringComparison.InvariantCultureIgnoreCase)).ToList();
                        if (endCommitIx.Count != 1)
                        {
                            Console.Error.WriteLine($"Couldn't find single commit for hash: {endCommit} (in range {hash})");
                            Environment.Exit(-1);
                        }

                        var endIx = endCommitIx[0].Index;

                        if (startIx > endIx)
                        {
                            Console.Error.WriteLine($"Range has negative length: {hash}");
                            Environment.Exit(-1);
                        }

                        for (var i = startIx; i <= endIx; i++)
                        {
                            var toAdd = allCommits[i];
                            selectedCommitsToTry.Add(toAdd);
                        }
                    }
                }

                var inOrder = selectedCommitsToTry.OrderBy(i => allCommits.IndexOf(i)).ToList();

                return inOrder;
            }

            static List<string> GetAllCommitHashes(string fromDir)
            {
                var ret = new List<string>();

                var command = $"git log --reverse --all";

                var (res, output) = RunCommand(fromDir, command);

                foreach (var line in output.Split("\n"))
                {
                    if (!line.StartsWith("commit "))
                    {
                        continue;
                    }

                    var end = line.IndexOf(' ', "commit ".Length);
                    if (end == -1)
                    {
                        end = line.Length;
                    }

                    var hash = line["commit ".Length..end];

                    ret.Add(hash.Trim());
                }

                return ret;
            }


            static void CloneRepository(string intoDir, string url, string branch)
            {
                Console.WriteLine(nameof(CloneRepository));

                Directory.CreateDirectory(intoDir);

                var command = $"git clone -b {branch} {url} .";

                var res = RunCommand(intoDir, command);

                CheckCommandResult(res);
            }

            static void CheckCommandResult((int ExitCode, string Output) tuple)
            {
                Console.WriteLine(nameof(CheckCommandResult));

                var (res, output) = tuple;

                if (res != 0)
                {
                    Console.Error.WriteLine("An error occurred");
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Output");
                    Console.Error.WriteLine(output);

                    Environment.Exit(-1);
                }
            }

            static (int ExitCode, string Output) RunCommand(string inDir, string command)
            {
                Console.WriteLine(nameof(RunCommand));

                var procInfo = new ProcessStartInfo();
                procInfo.WorkingDirectory = inDir;
                procInfo.FileName = "cmd.exe";
                procInfo.Arguments = $"/C {command}";
                procInfo.CreateNoWindow = false;
                procInfo.UseShellExecute = false;
                procInfo.RedirectStandardOutput = true;
                procInfo.RedirectStandardError = true;

                var proc = Process.Start(procInfo);

                var outRes = new StringBuilder();
                var outReader =
                    new Thread(
                        () =>
                        {
                            while (!proc.HasExited)
                            {
                                try
                                {
                                    var line = proc.StandardOutput.ReadLine();
                                    if (!string.IsNullOrEmpty(line))
                                    {
                                        Console.WriteLine($"[Proc {proc.Id}]: {line}");
                                    }

                                    outRes.AppendLine(line);
                                }
                                catch { }
                            }

                            try
                            {
                                string line;
                                while ((line = proc.StandardOutput.ReadLine()) != null)
                                {
                                    if (!string.IsNullOrEmpty(line))
                                    {
                                        Console.WriteLine($"[Proc {proc.Id}]: {line}");
                                    }

                                    outRes.AppendLine(line);
                                }
                            }
                            catch { }
                        }
                    );

                outReader.Start();

                proc.WaitForExit();
                outReader.Join();

                var res = proc.ExitCode;

                return (res, outRes.ToString());
            }
        }

        private static void RunBenchmarks(Options opts)
        {
            var benchmarks = FindBenchmarks(opts.IncludeBenchmarks, opts.ExcludeBenchmarks, opts.IncludeCategories, opts.ExcludeCategories);

            if (opts.Test?.Equals("true", StringComparison.InvariantCultureIgnoreCase) ?? false)
            {
                InitializeAndTest(benchmarks);
            }

            var libraries = new HashSet<string>(opts.CompareToOtherLibraries);
            libraries.Add("Cesil");

            Func<BenchmarkCase, bool> filter =
                (benchmark) =>
                {
                    if (benchmark.Descriptor.HasCategory("ComparesLibraries"))
                    {
                        // if we're COMPARING libraries, we need to check against the options to select the appropriate libraries
                        return libraries.Contains(benchmark.Descriptor.WorkloadMethod.Name);
                    }

                    return true;
                };

            var results = RunBenchmarks(benchmarks, filter);
            var extract = ExtractRelevantDetails(results, libraries);

            Console.WriteLine();
            Console.WriteLine($"BenchmarkName      Parameters      Library      MedianNanonseconds      AllocatedBytes");
            foreach (var row in extract)
            {
                Console.WriteLine();

                Write("BenchmarkName".Length, row.Name);
                Write("Parameters".Length, row.Parameters);
                Write("Library".Length, row.Library);
                Write("MedianNanonseconds".Length, row.MedianNanoseconds);
                Write("AllocatedBytes".Length, row.AllocatedBytes);
            }

            if (opts.OutputPath != null)
            {
                try
                {
                    CesilUtils.WriteToFile(extract, opts.OutputPath);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Failed to write output to file");
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);

                    Environment.Exit(-1);
                }
            }

            Environment.Exit(0);

            static void Write<T>(int headerLen, T value)
            {
                var equiv = value.ToString();
                if (equiv.Length > headerLen + 5)
                {
                    // need to make it so that when we add "..." the length will equal headerLen + 5
                    //   so equivNeeds to be headerLen + 2 chars long

                    equiv = equiv.Substring(0, headerLen + 2);
                    equiv += "...";
                }
                else
                {
                    equiv += new string(' ', (headerLen + 5) - equiv.Length);
                }

                Console.Write(equiv);
                Console.Write(" ");
            }
        }

        private static List<BenchmarkResultRow> ExtractRelevantDetails(
            Dictionary<string, IEnumerable<BenchmarkReport>> results,
            IEnumerable<string> libraries)
        {
            var output = new List<BenchmarkResultRow>();

            foreach (var res in results)
            {
                foreach (var set in res.Value)
                {
                    var method = set.BenchmarkCase.Descriptor.WorkloadMethod.Name;

                    var paramsStr = "";
                    if (set.BenchmarkCase.HasParameters)
                    {
                        var first = true;
                        for (var i = 0; i < set.BenchmarkCase.Parameters.Count; i++)
                        {
                            if (!first)
                            {
                                paramsStr += ",";
                            }

                            var p = set.BenchmarkCase.Parameters[i];
                            paramsStr += p.Name + "=" + p.Value;

                            first = false;
                        }
                    }

                    var name = res.Key;

                    var medianNS = set.ResultStatistics.Median;
                    double allocatedBytes;

                    if (set.Metrics.TryGetValue("Allocated Memory", out var allocatedMetric))
                    {
                        switch (allocatedMetric.Descriptor.Unit)
                        {
                            case "B": allocatedBytes = allocatedMetric.Value; break;
                            case "KB": allocatedBytes = 1024 * allocatedMetric.Value; break;
                            case "MB": allocatedBytes = 1024 * 1024 * allocatedMetric.Value; break;
                            case "GB": allocatedBytes = 1024 * 1024 * 1024 * allocatedMetric.Value; break;
                            default: throw new Exception($"Unexpected unit: {allocatedMetric.Descriptor.Unit}");
                        }
                    }
                    else
                    {
                        allocatedBytes = 0;
                    }

                    string library;
                    if (libraries.Contains(method))
                    {
                        library = method;
                    }
                    else
                    {
                        library = nameof(Cesil);
                    }

                    output.Add(new BenchmarkResultRow { Name = name, Parameters = paramsStr, Library = library, MedianNanoseconds = medianNS, AllocatedBytes = allocatedBytes });
                }
            }

            return output.OrderBy(n => n.Name).ThenBy(n => n.Parameters).ThenBy(n => n.Library).ToList();
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

        private static IEnumerable<Type> FindBenchmarks(IEnumerable<string> include, IEnumerable<string> exclude, IEnumerable<string> includeCategories, IEnumerable<string> excludeCategories)
        {
            var benchmarks = typeof(Program).Assembly.GetTypes().Where(t => t.IsClass && t.GetMethods().Any(m => m.GetCustomAttribute<BenchmarkAttribute>() != null)).ToList();

            var allIncludes = benchmarks.Select(b => b.Name).ToList();
            var allCategories = benchmarks.Select(b => b.GetCustomAttribute<BenchmarkCategoryAttribute>()).Where(b => b != null).SelectMany(b => b.Categories).Distinct().ToList();

            if (!include.Any())
            {
                include = allIncludes;
            }

            if (!includeCategories.Any())
            {
                includeCategories = allCategories;
            }

            var ret = new List<Type>();

            foreach (var benchmark in benchmarks)
            {
                var category = benchmark.GetCustomAttribute<BenchmarkCategoryAttribute>();

                bool categoryIncluded;

                if (category != null)
                {
                    categoryIncluded =
                        category.Categories.Any(c => includeCategories.Contains(c)) &&
                        !category.Categories.Any(c => excludeCategories.Contains(c));
                }
                else
                {
                    categoryIncluded = true;
                }

                var benchmarkIncluded =
                    include.Contains(benchmark.Name) &&
                    !exclude.Contains(benchmark.Name);

                var keep = categoryIncluded && benchmarkIncluded;

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

                ret.Add(benchmark);
            }

            return ret;
        }

        private static void InitializeAndTest(IEnumerable<Type> needChecking)
        {
            const string TEST_METHOD_NAME = nameof(InitializeAndTest);

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

        private static Dictionary<string, IEnumerable<BenchmarkReport>> RunBenchmarks(IEnumerable<Type> benchmarks, Func<BenchmarkCase, bool> filter)
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
            config.AddFilter(
                new SimpleFilter(filter)
            );
            config.AddJob(
                Job.Default
                    .WithToolchain(InProcessEmitToolchain.Instance)
            );

            var ret = new Dictionary<string, IEnumerable<BenchmarkReport>>();

            foreach (var b in benchmarks)
            {
                var summary = BenchmarkRunner.Run(b, config);
                ret[b.Name] = summary.Reports;
            }

            return ret;
        }
    }
}
