using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cesil.Tests
{
    public static class Helpers
    {
        private const int MAX_TO_TEST_EXHAUSTIVELY = 8;

        private class BoolArrayComparer : IEqualityComparer<bool[]>
        {
            public static readonly BoolArrayComparer Singleton = new BoolArrayComparer();

            private BoolArrayComparer() { }

            public bool Equals(bool[] x, bool[] y)
            {
                if (x.Length != y.Length) return false;

                for (var i = 0; i < x.Length; i++)
                {
                    var a = x[i];
                    var b = y[i];

                    if (a != b) return false;
                }

                return true;
            }

            public int GetHashCode(bool[] obj)
            {
                var ret = obj.Length;
                for (var i = 0; i < obj.Length; i++)
                {
                    ret *= 17;
                    ret += obj[i].GetHashCode();
                }

                return ret;
            }
        }

        public static int GetNumberExpectedDisposableTestCases(object o)
        {
            var mtds = o.GetType().GetMethods();

            var expectedTestCases = mtds.Length;
            expectedTestCases--;    // object.Equals
            expectedTestCases--;    // object.GetHashCode
            expectedTestCases--;    // object.GetType
            expectedTestCases--;    // object.ToString
            expectedTestCases--;    // IDisposable.Dispose

            // don't count what ITestableDisposable exposes, if it's not implemented explicitly.
            if (o.GetType().GetInterfaces().Any(i => i == typeof(ITestableDisposable)))
            {
                var map = o.GetType().GetInterfaceMap(typeof(ITestableDisposable));

                for(var i = 0; i < map.TargetMethods.Length; i++)
                {
                    if(map.TargetMethods[i].IsPublic)
                    {
                        expectedTestCases--;
                    }
                }
            }

            // don't count what ITestableAsyncDisposable exposes, if it's not implemented explicitly.
            if (o.GetType().GetInterfaces().Any(i => i == typeof(ITestableAsyncDisposable)))
            {
                var map = o.GetType().GetInterfaceMap(typeof(ITestableAsyncDisposable));

                for (var i = 0; i < map.TargetMethods.Length; i++)
                {
                    if (map.TargetMethods[i].IsPublic)
                    {
                        expectedTestCases--;
                    }
                }
            }

            // don't count IDynamicMetaObjectProvider methods
            if (o.GetType().GetInterfaces().Any(i => i == typeof(IDynamicMetaObjectProvider)))
            {
                var map = o.GetType().GetInterfaceMap(typeof(IDynamicMetaObjectProvider));

                for (var i = 0; i < map.TargetMethods.Length; i++)
                {
                    if (map.TargetMethods[i].IsPublic)
                    {
                        expectedTestCases--;
                    }
                }
            }

            // don't count IDynamicRowOwner methods
            if (o.GetType().GetInterfaces().Any(i => i == typeof(IDynamicRowOwner)))
            {
                var map = o.GetType().GetInterfaceMap(typeof(IDynamicRowOwner));

                for (var i = 0; i < map.TargetMethods.Length; i++)
                {
                    if (map.TargetMethods[i].IsPublic)
                    {
                        expectedTestCases--;
                    }
                }
            }

            return expectedTestCases;
        }

        public static void RunSyncReaderVariants<T>(Options opts, Action<IBoundConfiguration<T>, Func<string, TextReader>> run)
        {
            var defaultConfig = Configuration.For<T>(opts);
            var smallBufferConfig = Configuration.For<T>(opts.NewBuilder().WithReadBufferSizeHint(1).Build());
            
            // default buffer
            {
                var runCount = 0;
                run(defaultConfig, str => { runCount++; return new StringReader(str); });

                Assert.Equal(1, runCount);
            }

            // small buffer
            {
                var runCount = 0;
                run(smallBufferConfig, str => { runCount++; return new StringReader(str); });

                Assert.Equal(1, runCount);
            }

            // leaks
            {
                var leakDetector = new TrackedMemoryPool<char>();
                var leakDetectorConfig = Configuration.For<T>(opts.NewBuilder().WithMemoryPool(leakDetector).Build());

                var runCount = 0;
                run(defaultConfig, str => { runCount++; return new StringReader(str); });

                Assert.Equal(1, runCount);
                Assert.Equal(0, leakDetector.OutstandingRentals);
            }
        }

        public static void RunSyncDynamicReaderVariants(Options opts, Action<IBoundConfiguration<dynamic>, Func<string, TextReader>> run, int expectedRuns = 1)
        {
            var defaultConfig = Configuration.ForDynamic(opts);
            var smallBufferConfig = Configuration.ForDynamic(opts.NewBuilder().WithReadBufferSizeHint(1).Build());

            // default buffer
            {
                var runCount = 0;
                run(defaultConfig, str => { runCount++; return new StringReader(str); });

                Assert.Equal(expectedRuns, runCount);
            }

            // small buffer
            {
                var runCount = 0;
                run(smallBufferConfig, str => { runCount++; return new StringReader(str); });

                Assert.Equal(expectedRuns, runCount);
            }

            // leaks
            {
                var leakDetector = new TrackedMemoryPool<char>();
                var leakDetectorConfig = Configuration.ForDynamic(opts.NewBuilder().WithMemoryPool(leakDetector).Build());

                var runCount = 0;
                run(defaultConfig, str => { runCount++; return new StringReader(str); });

                Assert.Equal(expectedRuns, runCount);
                Assert.Equal(0, leakDetector.OutstandingRentals);
            }
        }

        public static async Task RunAsyncDynamicReaderVariants(Options opts, Func<IBoundConfiguration<dynamic>, Func<string, TextReader>, Task> run, int expectedRuns = 1)
        {
            var defaultConfig = Configuration.ForDynamic(opts);
            var smallBufferConfig = Configuration.ForDynamic(opts.NewBuilder().WithReadBufferSizeHint(1).Build());

            // default buffer
            {
                var runCount = 0;
                await run(defaultConfig, str => { runCount++; return new StringReader(str); });

                Assert.Equal(expectedRuns, runCount);
            }

            // small buffer
            {
                var runCount = 0;
                await run(smallBufferConfig, str => { runCount++; return new StringReader(str); });

                Assert.Equal(expectedRuns, runCount);
            }

            // leaks
            {
                var leakDetector = new TrackedMemoryPool<char>();
                var leakDetectorConfig = Configuration.ForDynamic(opts.NewBuilder().WithMemoryPool(leakDetector).Build());

                var runCount = 0;
                await run(defaultConfig, str => { runCount++; return new StringReader(str); });

                Assert.Equal(expectedRuns, runCount);
                Assert.Equal(0, leakDetector.OutstandingRentals);
            }
        }

        public static async Task RunAsyncReaderVariants<T>(Options opts, Func<IBoundConfiguration<T>, Func<string, TextReader>, Task> run)
        {
            var defaultConfig = Configuration.For<T>(opts);
            var smallBufferConfig = Configuration.For<T>(opts.NewBuilder().WithReadBufferSizeHint(1).Build());

            // default buffer
            {
                // probably sync
                {
                    var runCount = 0;
                    await run(defaultConfig, str => { runCount++; return new StringReader(str); });
                    Assert.Equal(1, runCount);

                    // leaks
                    {
                        var leakDetector = new TrackedMemoryPool<char>();
                        var leakConfig = Configuration.For<T>(opts.NewBuilder().WithMemoryPool(leakDetector).Build());

                        runCount = 0;
                        await run(leakConfig, str => { runCount++; return new StringReader(str); });
                        Assert.Equal(1, runCount);
                        Assert.Equal(0, leakDetector.OutstandingRentals);
                    }
                }

                // async
                {
                    var runCount = 0;
                    await run(defaultConfig, str => { runCount++; return new ForcedAsyncReader(new StringReader(str)); });
                    Assert.Equal(1, runCount);

                    // leaks
                    {
                        var leakDetector = new TrackedMemoryPool<char>();
                        var leakConfig = Configuration.For<T>(opts.NewBuilder().WithMemoryPool(leakDetector).Build());

                        runCount = 0;
                        await run(leakConfig, str => { runCount++; return new ForcedAsyncReader(new StringReader(str)); });
                        Assert.Equal(1, runCount);
                        Assert.Equal(0, leakDetector.OutstandingRentals);
                    }
                }

                // figure out how many chances we have to go async in this test
                AsyncCounterReader reader = null;
                await run(defaultConfig, str => { return reader ??= new AsyncCounterReader(new StringReader(str)); });
                var numAsyncCalls = reader.Count;

                var runAllCombos = numAsyncCalls <= MAX_TO_TEST_EXHAUSTIVELY;
                if (runAllCombos)
                {
                    // how many different ways could this flow, sync vs async?
                    var combos = EnumerateAsynchoronousCompletionOptions(numAsyncCalls);
                    foreach (var combo in combos)
                    {
                        var didRun = false;
                        await run(defaultConfig, str => { didRun = true; return new ConfigurableSyncAsyncReader(combo, new StringReader(str)); });

                        Assert.True(didRun);

                        // leaks
                        {
                            var leakDetector = new TrackedMemoryPool<char>();
                            var leakConfig = Configuration.For<T>(opts.NewBuilder().WithMemoryPool(leakDetector).Build());

                            var runCount = 0;
                            await run(leakConfig, str => { runCount++; return new ConfigurableSyncAsyncReader(combo, new StringReader(str)); });
                            Assert.Equal(1, runCount);
                            Assert.Equal(0, leakDetector.OutstandingRentals);
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < numAsyncCalls; i++)
                    {
                        var combo = new bool[numAsyncCalls];
                        combo[i] = true;

                        var didRun = false;
                        await run(defaultConfig, str => { didRun = true; return new ConfigurableSyncAsyncReader(combo, new StringReader(str)); });

                        Assert.True(didRun);

                        // leaks
                        {
                            var leakDetector = new TrackedMemoryPool<char>();
                            var leakConfig = Configuration.For<T>(opts.NewBuilder().WithMemoryPool(leakDetector).Build());

                            var runCount = 0;
                            await run(leakConfig, str => { runCount++; return new ConfigurableSyncAsyncReader(combo, new StringReader(str)); });
                            Assert.Equal(1, runCount);
                            Assert.Equal(0, leakDetector.OutstandingRentals);
                        }
                    }
                }
            }

            // very small buffer
            {
                // probably sync
                {
                    var runCount = 0;
                    await run(smallBufferConfig, str => { runCount++; return new StringReader(str); });
                    Assert.Equal(1, runCount);

                    // leaks
                    {
                        var leakDetector = new TrackedMemoryPool<char>();
                        var leakConfig = Configuration.For<T>(opts.NewBuilder().WithReadBufferSizeHint(1).WithMemoryPool(leakDetector).Build());

                        runCount = 0;
                        await run(leakConfig, str => { runCount++; return new StringReader(str); });
                        Assert.Equal(1, runCount);
                        Assert.Equal(0, leakDetector.OutstandingRentals);
                    }
                }

                // async
                {
                    var runCount = 0;
                    await run(smallBufferConfig, str => { runCount++; return new ForcedAsyncReader(new StringReader(str)); });
                    Assert.Equal(1, runCount);

                    // leaks
                    {
                        var leakDetector = new TrackedMemoryPool<char>();
                        var leakConfig = Configuration.For<T>(opts.NewBuilder().WithReadBufferSizeHint(1).WithMemoryPool(leakDetector).Build());

                        runCount = 0;
                        await run(leakConfig, str => { runCount++; return new ForcedAsyncReader(new StringReader(str)); });
                        Assert.Equal(1, runCount);
                    }
                }

                // figure out how many chances we have to go async in this test
                AsyncCounterReader reader = null;
                await run(smallBufferConfig, str => { return reader ??= new AsyncCounterReader(new StringReader(str)); });
                var numAsyncCalls = reader.Count;

                var runAllCombos = numAsyncCalls <= MAX_TO_TEST_EXHAUSTIVELY;
                if (runAllCombos)
                {
                    // how many different ways could this flow, sync vs async?
                    var combos = EnumerateAsynchoronousCompletionOptions(numAsyncCalls);
                    foreach (var combo in combos)
                    {
                        var didRun = false;
                        await run(smallBufferConfig, str => { didRun = true; return new ConfigurableSyncAsyncReader(combo, new StringReader(str)); });

                        Assert.True(didRun);

                        // leaks
                        {
                            var leakDetector = new TrackedMemoryPool<char>();
                            var leakConfig = Configuration.For<T>(opts.NewBuilder().WithReadBufferSizeHint(1).WithMemoryPool(leakDetector).Build());

                            var runCount = 0;
                            await run(leakConfig, str => { runCount++; return new ConfigurableSyncAsyncReader(combo, new StringReader(str)); });
                            Assert.Equal(1, runCount);
                            Assert.Equal(0, leakDetector.OutstandingRentals);
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < numAsyncCalls; i++)
                    {
                        var combo = new bool[numAsyncCalls];
                        combo[i] = true;

                        var didRun = false;
                        await run(defaultConfig, str => { didRun = true; return new ConfigurableSyncAsyncReader(combo, new StringReader(str)); });

                        Assert.True(didRun);

                        // leaks
                        {
                            var leakDetector = new TrackedMemoryPool<char>();
                            var leakConfig = Configuration.For<T>(opts.NewBuilder().WithReadBufferSizeHint(1).WithMemoryPool(leakDetector).Build());

                            var runCount = 0;
                            await run(leakConfig, str => { runCount++; return new ConfigurableSyncAsyncReader(combo, new StringReader(str)); });
                            Assert.Equal(1, runCount);
                            Assert.Equal(0, leakDetector.OutstandingRentals);
                        }
                    }
                }
            }
        }

        public static void RunSyncWriterVariants<T>(
           Options baseOptions,
           Action<IBoundConfiguration<T>, Func<TextWriter>, Func<string>> run
        )
        {
            var defaultConfig = Configuration.For<T>(baseOptions);
            var noBufferConfig = Configuration.For<T>(baseOptions.NewBuilder().WithWriteBufferSizeHint(0).Build());

            // default
            using (var writer = new StringWriter())
            {
                var gotWriter = 0;
                var gotString = 0;
                run(defaultConfig, () => { gotWriter++; return writer; }, () => { gotString++; return writer.ToString(); });

                Assert.Equal(1, gotWriter);
                Assert.Equal(1, gotString);
            }

            // no buffer
            using (var writer = new StringWriter())
            {
                var gotWriter = 0;
                var gotString = 0;
                run(noBufferConfig, () => { gotWriter++; return writer; }, () => { gotString++; return writer.ToString(); });

                Assert.Equal(1, gotWriter);
                Assert.Equal(1, gotString);
            }

            // default, leaks
            using (var writer = new StringWriter())
            {
                var leakDetector = new TrackedMemoryPool<char>();
                var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithMemoryPool(leakDetector).Build());

                var gotWriter = 0;
                var gotString = 0;
                run(leakConfig, () => { gotWriter++; return writer; }, () => { gotString++; return writer.ToString(); });

                Assert.Equal(1, gotWriter);
                Assert.Equal(1, gotString);
                Assert.Equal(0, leakDetector.OutstandingRentals);
            }

            // no buffer, leaks
            using (var writer = new StringWriter())
            {
                var leakDetector = new TrackedMemoryPool<char>();
                var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithMemoryPool(leakDetector).WithWriteBufferSizeHint(0).Build());

                var gotWriter = 0;
                var gotString = 0;
                run(leakConfig, () => { gotWriter++; return writer; }, () => { gotString++; return writer.ToString(); });

                Assert.Equal(1, gotWriter);
                Assert.Equal(1, gotString);
                // you'd think we'd expect NO rentals, but there are cases where we _have_ to buffer
                //       so just deal with it
                Assert.Equal(0, leakDetector.OutstandingRentals);
            }
        }

        public static async Task RunAsyncWriterVariants<T>(
            Options baseOptions,
            Func<IBoundConfiguration<T>, Func<TextWriter>, Func<string>, Task> run
        )
        {
            var defaultConfig = Configuration.For<T>(baseOptions);
            var noBufferConfig = Configuration.For<T>(baseOptions.NewBuilder().WithWriteBufferSizeHint(0).Build());

            // sync or async
            {
                var runCount = 0;

                // sync!
                using (var str = new StringWriter())
                {
                    var task = run(defaultConfig, () => str, () => { runCount++; str.Flush(); str.Close(); return str.ToString(); });
                    await task;
                }

                // sync, no buffer!
                using (var str = new StringWriter())
                {
                    var task = run(noBufferConfig, () => str, () => { runCount++; str.Flush(); str.Close(); return str.ToString(); });
                    await task;
                }

                // sync, leaks
                using (var str = new StringWriter())
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithMemoryPool(leakDetector).Build());

                    var task = run(leakConfig, () => str, () => { runCount++; str.Flush(); str.Close(); return str.ToString(); });
                    await task;

                    Assert.Equal(0, leakDetector.OutstandingRentals);
                }

                // sync, no buffer, leaks
                using (var str = new StringWriter())
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithWriteBufferSizeHint(0).WithMemoryPool(leakDetector).Build());

                    var task = run(leakConfig, () => str, () => { runCount++; str.Flush(); str.Close(); return str.ToString(); });
                    await task;

                    Assert.Equal(0, leakDetector.OutstandingRentals);
                }

                // async!
                using (var str = new StringWriter())
                using (var slow = new ForcedAsyncWriter(str))
                {
                    var task = run(defaultConfig, () => slow, () => { runCount++; str.Flush(); str.Close(); return str.ToString(); });
                    await task;
                }

                // async, no buffer!
                using (var str = new StringWriter())
                using (var slow = new ForcedAsyncWriter(str))
                {
                    var task = run(noBufferConfig, () => slow, () => { runCount++; str.Flush(); str.Close(); return str.ToString(); });
                    await task;
                }

                // async, leaks
                using (var str = new StringWriter())
                using (var slow = new ForcedAsyncWriter(str))
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithMemoryPool(leakDetector).Build());

                    var task = run(leakConfig, () => slow, () => { runCount++; str.Flush(); str.Close(); return str.ToString(); });
                    await task;

                    Assert.Equal(0, leakDetector.OutstandingRentals);
                }

                // async, leaks, no buffer!
                using (var str = new StringWriter())
                using (var slow = new ForcedAsyncWriter(str))
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithWriteBufferSizeHint(0).WithMemoryPool(leakDetector).Build());

                    var task = run(leakConfig, () => slow, () => { runCount++; str.Flush(); str.Close(); return str.ToString(); });
                    await task;

                    Assert.Equal(0, leakDetector.OutstandingRentals);
                }

                Assert.Equal(8, runCount);
            }

            // async, default buffer
            {
                // figure out how many chances we have to go async in this test
                int numAsyncCalls;
                using (var str = new StringWriter())
                using (var writer = new AsyncCounterWriter(str))
                {
                    var task = run(defaultConfig, () => writer, () => { str.Flush(); str.Close(); return str.ToString(); });
                    await task;

                    numAsyncCalls = writer.Count;
                }

                var runAllCombos = numAsyncCalls <= MAX_TO_TEST_EXHAUSTIVELY;
                if (runAllCombos)
                {
                    // how many different ways could this flow, sync vs async?
                    var combos = EnumerateAsynchoronousCompletionOptions(numAsyncCalls);
                    foreach (var combo in combos)
                    {
                        using (var str = new StringWriter())
                        using (var writer = new ConfigurableSyncAsyncWriter(combo, str))
                        {
                            var didRun = false;
                            await run(defaultConfig, () => writer, () => { didRun = true; str.Flush(); str.Close(); return str.ToString(); });

                            Assert.True(didRun);
                        }

                        // leaks
                        {
                            using (var str = new StringWriter())
                            using (var writer = new ConfigurableSyncAsyncWriter(combo, str))
                            {
                                var leakDetector = new TrackedMemoryPool<char>();
                                var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithMemoryPool(leakDetector).Build());

                                var didRun = false;
                                await run(defaultConfig, () => writer, () => { didRun = true; str.Flush(); str.Close(); return str.ToString(); });

                                Assert.True(didRun);
                                Assert.Equal(0, leakDetector.OutstandingRentals);
                            }
                        }
                    }
                }
                else
                {
                    // too many combos to reasonably try them all, but lets at least try all the different change over points
                    for (var i = 0; i < numAsyncCalls; i++)
                    {
                        var combo = new bool[numAsyncCalls];
                        combo[i] = true;

                        using (var str = new StringWriter())
                        using (var writer = new ConfigurableSyncAsyncWriter(combo, str))
                        {
                            var didRun = false;
                            await run(defaultConfig, () => writer, () => { didRun = true; str.Flush(); str.Close(); return str.ToString(); });

                            Assert.True(didRun);
                        }

                        // leaks
                        {
                            using (var str = new StringWriter())
                            using (var writer = new ConfigurableSyncAsyncWriter(combo, str))
                            {
                                var leakDetector = new TrackedMemoryPool<char>();
                                var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithMemoryPool(leakDetector).Build());

                                var didRun = false;
                                await run(defaultConfig, () => writer, () => { didRun = true; str.Flush(); str.Close(); return str.ToString(); });

                                Assert.True(didRun);
                                Assert.Equal(0, leakDetector.OutstandingRentals);
                            }
                        }
                    }
                }
            }

            // async, no buffer
            {
                // figure out how many chances we have to go async in this test
                int numAsyncCalls;
                using (var str = new StringWriter())
                using (var writer = new AsyncCounterWriter(str))
                {
                    var task = run(noBufferConfig, () => writer, () => { str.Flush(); str.Close(); return str.ToString(); });
                    await task;

                    numAsyncCalls = writer.Count;
                }

                var runAllCombos = numAsyncCalls <= MAX_TO_TEST_EXHAUSTIVELY;
                if (runAllCombos)
                {
                    // how many different ways could this flow, sync vs async?
                    var combos = EnumerateAsynchoronousCompletionOptions(numAsyncCalls);
                    foreach (var combo in combos)
                    {
                        using (var str = new StringWriter())
                        using (var writer = new ConfigurableSyncAsyncWriter(combo, str))
                        {
                            var didRun = false;
                            await run(noBufferConfig, () => writer, () => { didRun = true; str.Flush(); str.Close(); return str.ToString(); });

                            Assert.True(didRun);
                        }

                        // leaks
                        {
                            using (var str = new StringWriter())
                            using (var writer = new ConfigurableSyncAsyncWriter(combo, str))
                            {
                                var leakDetector = new TrackedMemoryPool<char>();
                                var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithWriteBufferSizeHint(0).WithMemoryPool(leakDetector).Build());

                                var didRun = false;
                                await run(defaultConfig, () => writer, () => { didRun = true; str.Flush(); str.Close(); return str.ToString(); });

                                Assert.True(didRun);
                                Assert.Equal(0, leakDetector.OutstandingRentals);
                            }
                        }
                    }
                }
                else
                {
                    // too many combos to reasonably try them all, but lets at least try all the different change over points
                    for (var i = 0; i < numAsyncCalls; i++)
                    {
                        var combo = new bool[numAsyncCalls];
                        combo[i] = true;

                        using (var str = new StringWriter())
                        using (var writer = new ConfigurableSyncAsyncWriter(combo, str))
                        {
                            var didRun = false;
                            await run(noBufferConfig, () => writer, () => { didRun = true; str.Flush(); str.Close(); return str.ToString(); });

                            Assert.True(didRun);
                        }

                        // leaks
                        {
                            using (var str = new StringWriter())
                            using (var writer = new ConfigurableSyncAsyncWriter(combo, str))
                            {
                                var leakDetector = new TrackedMemoryPool<char>();
                                var leakConfig = Configuration.For<T>(baseOptions.NewBuilder().WithWriteBufferSizeHint(0).WithMemoryPool(leakDetector).Build());

                                var didRun = false;
                                await run(defaultConfig, () => writer, () => { didRun = true; str.Flush(); str.Close(); return str.ToString(); });

                                Assert.True(didRun);
                                Assert.Equal(0, leakDetector.OutstandingRentals);
                            }
                        }
                    }
                }
            }
        }

        private static readonly HashSet<bool[]> TRUE_PERM = new HashSet<bool[]>(new[] { new[] { true } }, BoolArrayComparer.Singleton);
        private static readonly HashSet<bool[]> FALSE_PERM = new HashSet<bool[]>(new[] { new[] { false } }, BoolArrayComparer.Singleton);
        private static readonly Dictionary<bool[], HashSet<bool[]>> MemoizedPermutations = new Dictionary<bool[], HashSet<bool[]>>(BoolArrayComparer.Singleton);

        private static List<bool[]> EnumerateAsynchoronousCompletionOptions(int numAsyncCalls)
        {

            return
                Enumerable.Range(1, numAsyncCalls)
                    .AsParallel()
                    .SelectMany(
                        i =>
                        {
                            var subRet = new HashSet<bool[]>(BoolArrayComparer.Singleton);

                            var shouldBeAsync = new bool[numAsyncCalls];
                            for (var j = 0; j < i; j++)
                            {
                                shouldBeAsync[j] = true;
                            }

                            var perms = PermutationsOf(shouldBeAsync);
                            foreach (var perm in perms)
                            {
                                subRet.Add(perm);
                            }

                            return subRet;
                        }
                    )
                    .Distinct(BoolArrayComparer.Singleton)
                    .ToList();

            // finally, a chance to use my degree
            HashSet<bool[]> PermutationsOf(bool[] e)
            {
                lock (MemoizedPermutations)
                {
                    if (MemoizedPermutations.TryGetValue(e, out var earlyRet))
                    {
                        return earlyRet;
                    }
                }

                var a = e.ToArray();

                if (a.Length == 1)
                {
                    if (a[0] == true) return TRUE_PERM;

                    return FALSE_PERM;
                }

                var perms =
                    Enumerable
                        .Range(0, a.Length)
                        .SelectMany(
                            i =>
                            {
                                var subRet = new HashSet<bool[]>(BoolArrayComparer.Singleton);

                                var item = a[i];
                                var rest = a.Take(i).Concat(a.Skip(i + 1)).ToArray();

                                foreach (var right in PermutationsOf(rest))
                                {
                                    var perm = new[] { item }.Concat(right).ToArray();
                                    subRet.Add(perm);
                                }

                                return subRet;
                            }
                        )
                        .ToHashSet(BoolArrayComparer.Singleton);

                lock (MemoizedPermutations)
                {
                    MemoizedPermutations[e] = perms;
                }

                return perms;
            }
        }
    }
}
