using System;
using System.Buffers;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Cesil.Tests
{
    public static class Helpers
    {
        public static int GetNumberExpectedDisposableTestCases(object o)
        {
            var onType = o.GetType();
            var mtds = onType.GetMethods();

            var expectedTestCases = mtds.Length;
            expectedTestCases--;    // object.Equals
            expectedTestCases--;    // object.GetHashCode
            expectedTestCases--;    // object.GetType
            expectedTestCases--;    // object.ToString

            expectedTestCases--;    // for either IDisposable.Dispose or IAsyncDisposable.DisposeAsync

            DontCount(onType, typeof(ITestableDisposable), ref expectedTestCases);
            DontCount(onType, typeof(ITestableAsyncDisposable), ref expectedTestCases);
            DontCount(onType, typeof(ITestableAsyncProvider), ref expectedTestCases);
            DontCount(onType, typeof(IDynamicMetaObjectProvider), ref expectedTestCases);
            DontCount(onType, typeof(IDynamicRowOwner), ref expectedTestCases);

            return expectedTestCases;

            static void DontCount(
                Type onType,
                Type fromInterface,
                ref int count
            )
            {
                if (onType.GetInterfaces().Any(i => i == fromInterface))
                {
                    var map = onType.GetInterfaceMap(fromInterface);

                    foreach (var targetMtd in map.TargetMethods)
                    {
                        var decl = targetMtd.DeclaringType;
                        if (decl == fromInterface) continue;

                        if (targetMtd.IsPublic)
                        {
                            count--;
                        }
                    }
                }
            }
        }

        internal static void RunSyncReaderVariants<T>(Options opts, Action<BoundConfigurationBase<T>, Func<string, IReaderAdapter>> run)
        => RunSyncReaderVariantsInner(
            opt => (BoundConfigurationBase<T>)Configuration.For<T>(opt),
            opts,
            run,
            1
        );

        internal static void RunSyncDynamicReaderVariants(Options opts, Action<BoundConfigurationBase<dynamic>, Func<string, IReaderAdapter>> run, int expectedRuns = 1)
        => RunSyncReaderVariantsInner(
            opt => (BoundConfigurationBase<dynamic>)Configuration.ForDynamic(opt),
            opts,
            run,
            expectedRuns
        );

        private class SyncReaderAdapter_CharNode : ReadOnlySequenceSegment<char>
        {
            public SyncReaderAdapter_CharNode(ReadOnlyMemory<char> m)
            {
                this.Memory = m;
                this.RunningIndex = 0;
            }

            public SyncReaderAdapter_CharNode Append(ReadOnlyMemory<char> m)
            {
                var ret = new SyncReaderAdapter_CharNode(m);
                ret.RunningIndex = this.Memory.Length;

                this.Next = ret;

                return ret;
            }
        }

        private class SyncReaderAdapter_ByteNode : ReadOnlySequenceSegment<byte>
        {
            public SyncReaderAdapter_ByteNode(ReadOnlyMemory<byte> m)
            {
                this.Memory = m;
                this.RunningIndex = 0;
            }

            public SyncReaderAdapter_ByteNode Append(ReadOnlyMemory<byte> m)
            {
                var ret = new SyncReaderAdapter_ByteNode(m);
                ret.RunningIndex = this.Memory.Length;

                this.Next = ret;

                return ret;
            }
        }

        private static readonly Func<string, IReaderAdapter>[] SyncReaderAdapters =
            new Func<string, IReaderAdapter>[]
            {
                str => new TextReaderAdapter(new StringReader(str)),
                str =>
                {
                    // single segment ReadOnlySequence

                    var bytes = str.AsMemory();

                    var seq = new ReadOnlySequence<char>(bytes);

                    return new ReadOnlyCharSequenceAdapter(seq);
                },
                str =>
                {
                    // multi segment ReadOnlySequence

                    var bytes = str.AsMemory();

                    var firstThirdIx = bytes.Length / 3;
                    var secondThirdIx = firstThirdIx * 2;

                    if(firstThirdIx >= bytes.Length || secondThirdIx >= bytes.Length)
                    {
                        // not big enough, just do single again
                        var seq = new ReadOnlySequence<char>(bytes);

                        return new ReadOnlyCharSequenceAdapter(seq);
                    }

                    var m1 = bytes.Slice(0, firstThirdIx);
                    var m2 = bytes.Slice(firstThirdIx, secondThirdIx - firstThirdIx);
                    var m3 = bytes.Slice(secondThirdIx);

                    if(m1.Length == 0 || m2 .Length == 0 || m3.Length == 0)
                    {
                        // not big enough, just do single again
                        var seq = new ReadOnlySequence<char>(bytes);

                        return new ReadOnlyCharSequenceAdapter(seq);
                    }

                    var s1 = new SyncReaderAdapter_CharNode(m1);
                    var s2 = s1.Append(m2);
                    var s3 = s2.Append(m3);

                    var multiSeq = new ReadOnlySequence<char>(s1, 0, s3, s3.Memory.Length);

                    return new ReadOnlyCharSequenceAdapter(multiSeq);
                },
                str =>
                {
                    var bytes = Encoding.UTF8.GetBytes(str);
                    var seq = new ReadOnlySequence<byte>(bytes);

                    return new ReadOnlyByteSequenceAdapter(seq, Encoding.UTF8);
                },
                str =>
                {
                    var bytes = Encoding.UTF8.GetBytes(str).AsMemory();

                    var firstThirdIx = bytes.Length / 3;
                    var secondThirdIx = firstThirdIx * 2;

                    if(firstThirdIx >= bytes.Length || secondThirdIx >= bytes.Length)
                    {
                        // not big enough, just do single again
                        var seq = new ReadOnlySequence<byte>(bytes);

                        return new ReadOnlyByteSequenceAdapter(seq, Encoding.UTF8);
                    }

                    var m1 = bytes.Slice(0, firstThirdIx);
                    var m2 = bytes.Slice(firstThirdIx, secondThirdIx - firstThirdIx);
                    var m3 = bytes.Slice(secondThirdIx);

                    if(m1.Length == 0 || m2 .Length == 0 || m3.Length == 0)
                    {
                        // not big enough, just do single again
                        var seq = new ReadOnlySequence<byte>(bytes);

                        return new ReadOnlyByteSequenceAdapter(seq, Encoding.UTF8);
                    }

                    var s1 = new SyncReaderAdapter_ByteNode(m1);
                    var s2 = s1.Append(m2);
                    var s3 = s2.Append(m3);

                    var multiSeq = new ReadOnlySequence<byte>(s1, 0, s3, s3.Memory.Length);

                    return new ReadOnlyByteSequenceAdapter(multiSeq, Encoding.UTF8);
                }
            };

        private static void RunSyncReaderVariantsInner<T>(
            Func<Options, BoundConfigurationBase<T>> bindConfig,
            Options opts,
            Action<BoundConfigurationBase<T>, Func<string, IReaderAdapter>> run,
            int expectedRuns
        )
        {
            foreach (var maker in SyncReaderAdapters)
            {
                var defaultConfig = bindConfig(opts);
                var smallBufferConfig = bindConfig(Options.CreateBuilder(opts).WithReadBufferSizeHint(1).ToOptions());

                // default buffer
                {
                    var runCount = 0;
                    run(defaultConfig, str => { runCount++; return maker(str); });

                    Assert.Equal(expectedRuns, runCount);
                }

                // small buffer
                {
                    var runCount = 0;
                    run(smallBufferConfig, str => { runCount++; return maker(str); });

                    Assert.Equal(expectedRuns, runCount);
                }

                // leaks
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakDetectorConfig = bindConfig(Options.CreateBuilder(opts).WithMemoryPool(leakDetector).ToOptions());

                    var runCount = 0;
                    run(leakDetectorConfig, str => { runCount++; return maker(str); });

                    Assert.Equal(expectedRuns, runCount);
                    Assert.Equal(0, leakDetector.OutstandingRentals);
                }
            }
        }

        internal static Task RunAsyncDynamicReaderVariants(
            Options opts,
            Func<BoundConfigurationBase<dynamic>,
            Func<string, ValueTask<IAsyncReaderAdapter>>, Task> run,
            bool checkRunCounts = true,
            bool releasePinsAcrossYields = true,
            bool cancellable = true
        )
        => RunAsyncReaderVariantsInnerAsync(
            opts => (BoundConfigurationBase<dynamic>)Configuration.ForDynamic(opts),
            opts,
            run,
            checkRunCounts,
            releasePinsAcrossYields,
            cancellable
        );

        internal static Task RunAsyncReaderVariants<T>(Options opts, Func<BoundConfigurationBase<T>, Func<string, ValueTask<IAsyncReaderAdapter>>, Task> run, bool checkRunCounts = true, bool releasePinsAcrossYields = true, bool cancellable = true)
        => RunAsyncReaderVariantsInnerAsync(
            opts => (BoundConfigurationBase<T>)Configuration.For<T>(opts),
            opts,
            run,
            checkRunCounts,
            releasePinsAcrossYields,
            cancellable
        );

        private static readonly Func<string, ValueTask<IAsyncReaderAdapter>>[] AsyncReaderAdapters =
            new Func<string, ValueTask<IAsyncReaderAdapter>>[]
            {
                str => new ValueTask<IAsyncReaderAdapter>(new AsyncTextReaderAdapter(new StringReader(str))),
                async str =>
                {
                    var pipe = new Pipe();
                    var writer = pipe.Writer;
                    var bytes = Encoding.UTF8.GetBytes(str);

                    await writer.WriteAsync(bytes.AsMemory());
                    writer.Complete();

                    return new PipeReaderAdapter(pipe.Reader, Encoding.UTF8);
                }
            };

        private sealed class AsyncInstrumentedPinReaderAdapter : IAsyncReaderAdapter
        {
            private IAsyncReaderAdapter Inner;

            public bool WaitingForUnpin { get; private set; }
            public SemaphoreSlim Semaphore { get; }

            public AsyncInstrumentedPinReaderAdapter(IAsyncReaderAdapter inner)
            {
                Inner = inner;
                Semaphore = new SemaphoreSlim(0, 1);
            }

            public void UnpinObserved()
            {
                WaitingForUnpin = false;
                Semaphore.Release();
            }

            public bool IsDisposed => Inner == null;

            public async ValueTask DisposeAsync()
            {
                if (!IsDisposed)
                {
                    await Inner.DisposeAsync();
                    Inner = null;
                }
            }

            public async ValueTask<int> ReadAsync(Memory<char> into, CancellationToken cancel)
            {
                WaitingForUnpin = true;
                var gotIt = await Semaphore.WaitAsync(TimeSpan.FromSeconds(1));
                if (!gotIt)
                {
                    throw new Exception("Did not observe unpin in time, probably held a pin over an await");
                }

                return await Inner.ReadAsync(into, cancel);
            }
        }

        private static async Task RunAsyncReaderVariantsInnerAsync<T>(
            Func<Options, BoundConfigurationBase<T>> bindConfig,
            Options opts,
            Func<BoundConfigurationBase<T>, Func<string, ValueTask<IAsyncReaderAdapter>>, Task> run,
            bool checkRunCounts,
            bool releasePinsAcrossYields,
            bool cancellable
        )
        {
            foreach (var maker in AsyncReaderAdapters)
            {
                // the easy one
                await RunOnce(bindConfig, maker, opts, run, checkRunCounts);

                // with a small buffer
                var smallBufferOpts = Options.CreateBuilder(opts).WithReadBufferSizeHint(1).ToOptions();
                await RunOnce(bindConfig, maker, smallBufferOpts, run, checkRunCounts);

                // checking that we don't hold any pins across awaits
                if (releasePinsAcrossYields)
                {
                    await RunCheckPins(bindConfig, maker, opts, run);
                }

                // in DEBUG we have some special stuff built in so we can go ham on different async paths
#if DEBUG
                await RunForcedAsyncVariantsWithBaseConfig(bindConfig, maker, opts, run, checkRunCounts);
                await RunForcedAsyncVariantsWithBaseConfig(bindConfig, maker, smallBufferOpts, run, checkRunCounts);
#endif

                if (cancellable)
                {
                    // in DEBUG we have some special stuff to explore all the cancellation points
#if DEBUG
                    await RunForcedCancelVariantsWithBaseConfig(bindConfig, maker, opts, run);
                    await RunForcedCancelVariantsWithBaseConfig(bindConfig, maker, smallBufferOpts, run);
#endif
                }
            }

            static async Task RunForcedCancelVariantsWithBaseConfig(
                Func<Options, BoundConfigurationBase<T>> bind,
                Func<string, ValueTask<IAsyncReaderAdapter>> readerMaker,
                Options baseOpts,
                Func<BoundConfigurationBase<T>, Func<string, ValueTask<IAsyncReaderAdapter>>, Task> run
            )
            {
                // defaults
                {
                    var config = bind(baseOpts);

                    int cancelPoints;
                    {
                        var wrappedConfig = new AsyncCancelControlConfig<T>(config);
                        await run(wrappedConfig, str => { return readerMaker(str); });
                        cancelPoints = wrappedConfig.CancelCounter - 1;
                    }

                    // walk each cancellation point
                    var forceUpTo = 0;
                    while (true)
                    {
                        var wrappedConfig = new AsyncCancelControlConfig<T>(config);
                        wrappedConfig.DoCancelAfter = forceUpTo;

                        try
                        {
                            await run(wrappedConfig, str => { return readerMaker(str); });
                        }
                        catch (Exception e)
                        {
                            if (e is AggregateException ae)
                            {
                                OperationCanceledException x = null;

                                foreach (var i in ae.InnerExceptions)
                                {
                                    if (i is OperationCanceledException oce)
                                    {
                                        x = oce;
                                        break;
                                    }
                                }

                                Assert.NotNull(x);
                            }
                            else
                            {
                                Assert.IsType<OperationCanceledException>(e);
                            }
                        }

                        Assert.Equal(PoisonType.Cancelled, wrappedConfig.Poison);

                        if (cancelPoints > forceUpTo)
                        {
                            forceUpTo++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }


                // leak detection
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakDetectorConfig = bind(Options.CreateBuilder(baseOpts).WithMemoryPool(leakDetector).ToOptions());

                    int cancelPoints;
                    {
                        var wrappedConfig = new AsyncCancelControlConfig<T>(leakDetectorConfig);
                        await run(wrappedConfig, str => { return readerMaker(str); });

                        Assert.Equal(0, leakDetector.OutstandingRentals);

                        cancelPoints = wrappedConfig.CancelCounter - 1;
                    }

                    // walk each cancellation point
                    var forceUpTo = 0;
                    while (true)
                    {
                        var wrappedConfig = new AsyncCancelControlConfig<T>(leakDetectorConfig);
                        wrappedConfig.DoCancelAfter = forceUpTo;

                        try
                        {
                            await run(wrappedConfig, str => { return readerMaker(str); });
                        }
                        catch (Exception e)
                        {
                            if (e is AggregateException ae)
                            {
                                OperationCanceledException x = null;

                                foreach (var i in ae.InnerExceptions)
                                {
                                    if (i is OperationCanceledException oce)
                                    {
                                        x = oce;
                                        break;
                                    }
                                }

                                Assert.NotNull(x);
                            }
                            else
                            {
                                Assert.IsType<OperationCanceledException>(e);
                            }
                        }

                        Assert.Equal(PoisonType.Cancelled, wrappedConfig.Poison);

                        Assert.Equal(0, leakDetector.OutstandingRentals);

                        if (cancelPoints > forceUpTo)
                        {
                            forceUpTo++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            static async Task RunCheckPins(
                    Func<Options, BoundConfigurationBase<T>> bind,
                    Func<string, ValueTask<IAsyncReaderAdapter>> readerMaker,
                    Options baseOpts,
                    Func<BoundConfigurationBase<T>, Func<string, ValueTask<IAsyncReaderAdapter>>, Task> run
                )
            {
                AsyncInstrumentedPinReaderAdapter adapter = null;
                AsyncInstrumentedPinConfig<T> config = null;
                var running = true;

                var monitoringThread =
                    new Thread(
                        () =>
                        {
                            while (running)
                            {
                                if (adapter == null || config == null)
                                {
                                    Thread.Sleep(0);
                                    continue;
                                }

                                if (adapter.WaitingForUnpin)
                                {
                                    if (config.IsUnpinned)
                                    {
                                        adapter.UnpinObserved();
                                        Thread.Sleep(0);
                                    }
                                }
                            }
                        }
                    );
                monitoringThread.Name = "RunCheckPins.Montor";
                monitoringThread.Start();

                // this will force us to await EVERY SINGLE TIME
                //   and also make the await block until we explicitly
                //   signal to continue
                //
                // before we signal, we'll confirm that the StateMachine has been
                //   unpinned
                config = new AsyncInstrumentedPinConfig<T>(bind(baseOpts));

                try
                {
                    int runCount = 0;
                    await run(
                        config,
                        async str =>
                        {
                            runCount++;
                            var innerAdapter = await readerMaker(str);
                            adapter = new AsyncInstrumentedPinReaderAdapter(innerAdapter);
                            return adapter;
                        }
                    );
                }
                finally
                {
                    running = false;
                }

                monitoringThread.Join();
            }

            static async Task RunOnce(
                Func<Options, BoundConfigurationBase<T>> bind,
                Func<string, ValueTask<IAsyncReaderAdapter>> readerMaker,
                Options baseOpts,
                Func<BoundConfigurationBase<T>, Func<string, ValueTask<IAsyncReaderAdapter>>, Task> run,
                bool checkRunCounts
            )
            {
                //run the test once
                {
                    var config = bind(baseOpts);

                    int runCount = 0;
                    await run(config, str => { runCount++; return readerMaker(str); });

                    if (checkRunCounts)
                    {
                        Assert.Equal(1, runCount);
                    }
                }

                //run the test with a small buffer
                {
                    var smallBufferOpts = Options.CreateBuilder(baseOpts).WithReadBufferSizeHint(1).ToOptions();
                    var smallBufferConfig = bind(smallBufferOpts);

                    int runCount = 0;
                    await run(smallBufferConfig, str => { runCount++; return readerMaker(str); });
                    if (checkRunCounts)
                    {
                        Assert.Equal(1, runCount);
                    }
                }

                //run the test once, but look for leaks
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakDetectorConfig = bind(Options.CreateBuilder(baseOpts).WithMemoryPool(leakDetector).ToOptions());

                    int runCount = 0;
                    await run(leakDetectorConfig, str => { runCount++; return readerMaker(str); });
                    if (checkRunCounts)
                    {
                        Assert.Equal(1, runCount);
                    }
                    Assert.Equal(0, leakDetector.OutstandingRentals);
                }
            }

            static async Task RunForcedAsyncVariantsWithBaseConfig(
                Func<Options, BoundConfigurationBase<T>> bind,
                Func<string, ValueTask<IAsyncReaderAdapter>> readerMaker,
                Options baseOpts,
                Func<BoundConfigurationBase<T>, Func<string, ValueTask<IAsyncReaderAdapter>>, Task> run,
                bool checkRunCounts
            )
            {
                var config = bind(baseOpts);

                //walk each async transition point
                var forceUpTo = 0;
                while (true)
                {
                    var wrappedConfig = new AsyncCountingAndForcingConfig<T>(config);
                    wrappedConfig.GoAsyncAfter = forceUpTo;

                    int runCount = 0;
                    await run(wrappedConfig, str => { runCount++; return readerMaker(str); });
                    if (checkRunCounts)
                    {
                        Assert.Equal(1, runCount);
                    }

                    if (wrappedConfig.AsyncCounter >= forceUpTo)
                    {
                        forceUpTo++;
                    }
                    else
                    {
                        break;
                    }
                }

                //the same, but check for leaks this time

                forceUpTo = 0;
                while (true)
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakDetectorConfig = bind(Options.CreateBuilder(baseOpts).WithMemoryPool(leakDetector).ToOptions());

                    var wrappedConfig = new AsyncCountingAndForcingConfig<T>(leakDetectorConfig);
                    wrappedConfig.GoAsyncAfter = forceUpTo;

                    int runCount = 0;
                    await run(wrappedConfig, str => { runCount++; return readerMaker(str); });
                    if (checkRunCounts)
                    {
                        Assert.Equal(1, runCount);
                    }
                    Assert.Equal(0, leakDetector.OutstandingRentals);

                    if (wrappedConfig.AsyncCounter >= forceUpTo)
                    {
                        forceUpTo++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        internal static void RunSyncWriterVariants<T>(
           Options baseOptions,
           Action<BoundConfigurationBase<T>, Func<IWriterAdapter>, Func<string>> run
        )
        => RunSyncWriterVariantsInner(opt => (BoundConfigurationBase<T>)Configuration.For<T>(opt), baseOptions, run);

        internal static void RunSyncDynamicWriterVariants(
           Options baseOptions,
           Action<BoundConfigurationBase<dynamic>, Func<IWriterAdapter>, Func<string>> run
        )
        => RunSyncWriterVariantsInner(opt => (BoundConfigurationBase<dynamic>)Configuration.ForDynamic(opt), baseOptions, run);

        private sealed class HelperBufferWriter<T> : IBufferWriter<T>
        {
            public List<T> Data;

            private T[] Arr;

            public HelperBufferWriter(int backingSize)
            {
                Arr = new T[backingSize];
                Data = new List<T>();
            }

            public void Advance(int count)
            {
                Data.AddRange(Arr.Take(count));
            }

            public Memory<T> GetMemory(int sizeHint = 0)
            => Arr.AsMemory();

            public Span<T> GetSpan(int sizeHint = 0)
            => GetMemory(sizeHint).Span;
        }

        private static readonly Func<(IWriterAdapter Adapter, Func<string> Getter)>[] SyncWriterAdapters
            = new Func<(IWriterAdapter Adapter, Func<string> Getter)>[]
            {
                () =>
                {
                    var writer = new StringWriter();
                    var adapter = new TextWriterAdapter(writer);

                    Func<string> getter =
                        () =>
                        {
                            return writer.ToString();
                        };

                    return (adapter, getter);
                },
                () =>
                {
                    var writer = new HelperBufferWriter<char>(4098);
                    var adapter = new BufferWriterCharAdapter(writer);

                    Func<string> getter =
                        () =>
                        {
                            return new string(writer.Data.ToArray());
                        };

                    return (adapter, getter);
                },
                () =>
                {
                    var writer = new HelperBufferWriter<char>(4);
                    var adapter = new BufferWriterCharAdapter(writer);

                    Func<string> getter =
                        () =>
                        {
                            return new string(writer.Data.ToArray());
                        };

                    return (adapter, getter);
                },
                () =>
                {
                    var pipe = new Pipe();
                    var adapter = new BufferWriterByteAdapter(pipe.Writer, Encoding.UTF32);

                    Func<string> getter =
                        () =>
                        {
                            pipe.Writer.Complete();

                            var bytes = new List<byte>();

                            while(pipe.Reader.TryRead(out var res))
                            {
                                foreach(var seq in res.Buffer)
                                {
                                    bytes.AddRange(seq.ToArray());
                                }

                                if(res.IsCompleted || res.IsCanceled)
                                {
                                    break;
                                }
                            }

                            var byteArr = bytes.ToArray();
                            return Encoding.UTF32.GetString(byteArr);
                        };

                    return (adapter, getter);
                }
            };

        internal static void RunSyncWriterVariantsInner<T>(
            Func<Options, BoundConfigurationBase<T>> bindConfig,
            Options baseOptions,
            Action<BoundConfigurationBase<T>, Func<IWriterAdapter>, Func<string>> run
        )
        {
            var defaultConfig = bindConfig(baseOptions);
            var noBufferConfig = bindConfig(Options.CreateBuilder(baseOptions).WithWriteBufferSizeHint(0).ToOptions());

            foreach (var maker in SyncWriterAdapters)
            {
                // default
                var (writer, getter) = maker();
                {
                    var gotWriter = 0;
                    var gotString = 0;
                    run(defaultConfig, () => { gotWriter++; return writer; }, () => { gotString++; return getter(); });

                    Assert.Equal(1, gotWriter);
                    Assert.Equal(1, gotString);
                }

                // no buffer
                (writer, getter) = maker();
                {
                    var gotWriter = 0;
                    var gotString = 0;
                    run(noBufferConfig, () => { gotWriter++; return writer; }, () => { gotString++; return getter(); });

                    Assert.Equal(1, gotWriter);
                    Assert.Equal(1, gotString);
                }

                // default, leaks
                (writer, getter) = maker();
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakConfig = bindConfig(Options.CreateBuilder(baseOptions).WithMemoryPool(leakDetector).ToOptions());

                    var gotWriter = 0;
                    var gotString = 0;
                    run(leakConfig, () => { gotWriter++; return writer; }, () => { gotString++; return getter(); });

                    Assert.Equal(1, gotWriter);
                    Assert.Equal(1, gotString);
                    Assert.Equal(0, leakDetector.OutstandingRentals);
                }

                // no buffer, leaks
                (writer, getter) = maker();
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakConfig = bindConfig(Options.CreateBuilder(baseOptions).WithMemoryPool(leakDetector).WithWriteBufferSizeHint(0).ToOptions());

                    var gotWriter = 0;
                    var gotString = 0;
                    run(leakConfig, () => { gotWriter++; return writer; }, () => { gotString++; return getter(); });

                    Assert.Equal(1, gotWriter);
                    Assert.Equal(1, gotString);
                    // you'd think we'd expect NO rentals, but there are cases where we _have_ to buffer
                    //       so just deal with it
                    Assert.Equal(0, leakDetector.OutstandingRentals);
                }
            }
        }

        internal static Task RunAsyncWriterVariants<T>(
            Options baseOptions,
            Func<BoundConfigurationBase<T>, Func<IAsyncWriterAdapter>, Func<ValueTask<string>>, Task> run,
            bool cancellable = true
        )
        => RunAsyncWriterVariantsInnerAsync(
                (Options opts) =>
                    (BoundConfigurationBase<T>)
                    Configuration.For<T>(opts)
                ,
                baseOptions,
                run,
                cancellable
            );

        internal static Task RunAsyncDynamicWriterVariants(
            Options baseOptions,
            Func<BoundConfigurationBase<dynamic>, Func<IAsyncWriterAdapter>, Func<ValueTask<string>>, Task> run,
            bool cancellable = true
        )
        => RunAsyncWriterVariantsInnerAsync(
            (Options opts) =>
                (BoundConfigurationBase<dynamic>)
                    Configuration.ForDynamic(opts),
                        baseOptions,
                        run,
                        cancellable
                    );

        private static readonly Func<(IAsyncWriterAdapter Adapter, Func<ValueTask<string>> Getter)>[] AsyncWriterAdapters =
            new Func<(IAsyncWriterAdapter Adapter, Func<ValueTask<string>> Getter)>[]
            {
                () =>
                {
                    var writer = new StringWriter();
                    var adapter = new AsyncTextWriterAdapter(writer);

                    Func<ValueTask<string>> getter =
                        () =>
                        {
                            writer.Flush();
                            writer.Close();
                            return new ValueTask<string>(writer.ToString());
                        };

                    return (adapter, getter);
                },
                () =>
                {
                    var pipe = new Pipe();
                    var reader = pipe.Reader;
                    var writer = pipe.Writer;
                    var adapter = new PipeWriterAdapter(writer, Encoding.UTF8, MemoryPool<char>.Shared);

                    Func<ValueTask<string>> getter =
                        async () =>
                        {
                            writer.Complete();

                            var bytes = new List<byte>();

                            var complete = false;

                            while(!complete)
                            {
                                var res = await reader.ReadAsync();
                                foreach(var seg in res.Buffer)
                                {
                                    bytes.AddRange(seg.ToArray());
                                }

                                complete = res.IsCompleted;
                            }

                            var str = Encoding.UTF8.GetString(bytes.ToArray());

                            return str;
                        };

                    return (adapter, getter);
                }
            };

        private static async Task RunAsyncWriterVariantsInnerAsync<T>(
            Func<Options, BoundConfigurationBase<T>> bindConfig,
            Options opts,
            Func<BoundConfigurationBase<T>, Func<IAsyncWriterAdapter>, Func<ValueTask<string>>, Task> run,
            bool cancellable
        )
        {
            foreach (var maker in AsyncWriterAdapters)
            {
                var smallBufferOpts = Options.CreateBuilder(opts).WithWriteBufferSizeHint(0).ToOptions();

                await RunOnce(bindConfig, maker, opts, run);
                await RunOnce(bindConfig, maker, smallBufferOpts, run);

                // in DEBUG we have some special stuff built in so we can go ham on different async paths
#if DEBUG
                await RunForcedAsyncVariantsWithBaseConfig(bindConfig, maker, opts, run);
                await RunForcedAsyncVariantsWithBaseConfig(bindConfig, maker, smallBufferOpts, run);
#endif
                // in DEBUG we have some special stuff built in so we can go ham on different cancellation points
                if (cancellable)
                {
#if DEBUG
                    await RunForcedCancelVariantsWithBaseConfig(bindConfig, maker, opts, run);
                    await RunForcedCancelVariantsWithBaseConfig(bindConfig, maker, smallBufferOpts, run);
#endif
                }
            }

            static async Task RunForcedCancelVariantsWithBaseConfig(
                Func<Options, BoundConfigurationBase<T>> bind,
                Func<(IAsyncWriterAdapter Adapter, Func<ValueTask<string>> Getter)> maker,
                Options baseOpts,
                Func<BoundConfigurationBase<T>, Func<IAsyncWriterAdapter>, Func<ValueTask<string>>, Task> run
            )
            {
                // defaults
                {
                    var config = bind(baseOpts);

                    int cancelPoints;
                    {
                        var wrappedConfig = new AsyncCancelControlConfig<T>(config);
                        var (adapter, getter) = maker();
                        await using (adapter)
                        {
                            await run(wrappedConfig, () => { return adapter; }, () => { return getter(); });
                        }
                        cancelPoints = wrappedConfig.CancelCounter - 1;
                    }

                    // walk each cancellation point
                    var forceUpTo = 0;
                    while (true)
                    {
                        var wrappedConfig = new AsyncCancelControlConfig<T>(config);
                        wrappedConfig.DoCancelAfter = forceUpTo;

                        var (adapter, getter) = maker();

                        await using (adapter)
                        {
                            try
                            {
                                await run(wrappedConfig, () => { return adapter; }, () => { return getter(); });
                            }
                            catch (Exception e)
                            {
                                if (e is AggregateException ae)
                                {
                                    OperationCanceledException x = null;

                                    foreach (var i in ae.InnerExceptions)
                                    {
                                        if (i is OperationCanceledException oce)
                                        {
                                            x = oce;
                                            break;
                                        }
                                    }

                                    Assert.NotNull(x);
                                }
                                else
                                {
                                    Assert.IsType<OperationCanceledException>(e);
                                }
                            }
                        }

                        Assert.Equal(PoisonType.Cancelled, wrappedConfig.Poison);

                        if (cancelPoints > forceUpTo)
                        {
                            forceUpTo++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                // leaks
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakDetectorConfig = bind(Options.CreateBuilder(baseOpts).WithMemoryPool(leakDetector).ToOptions());

                    int cancelPoints;
                    {
                        var wrappedConfig = new AsyncCancelControlConfig<T>(leakDetectorConfig);
                        var (adapter, getter) = maker();
                        await using (adapter)
                        {
                            await run(wrappedConfig, () => { return adapter; }, () => { return getter(); });
                        }
                        cancelPoints = wrappedConfig.CancelCounter - 1;
                    }

                    Assert.Equal(0, leakDetector.OutstandingRentals);

                    // walk each cancellation point
                    var forceUpTo = 0;
                    while (true)
                    {
                        var wrappedConfig = new AsyncCancelControlConfig<T>(leakDetectorConfig);
                        wrappedConfig.DoCancelAfter = forceUpTo;

                        var (adapter, getter) = maker();

                        await using (adapter)
                        {
                            try
                            {
                                await run(wrappedConfig, () => { return adapter; }, () => { return getter(); });
                            }
                            catch (Exception e)
                            {
                                if (e is AggregateException ae)
                                {
                                    OperationCanceledException x = null;

                                    foreach (var i in ae.InnerExceptions)
                                    {
                                        if (i is OperationCanceledException oce)
                                        {
                                            x = oce;
                                            break;
                                        }
                                    }

                                    Assert.NotNull(x);
                                }
                                else
                                {
                                    Assert.IsType<OperationCanceledException>(e);
                                }
                            }

                            Assert.Equal(0, leakDetector.OutstandingRentals);
                        }

                        Assert.Equal(PoisonType.Cancelled, wrappedConfig.Poison);

                        if (cancelPoints > forceUpTo)
                        {
                            forceUpTo++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            static async Task RunOnce(
                Func<Options, BoundConfigurationBase<T>> bind,
                Func<(IAsyncWriterAdapter Adapter, Func<ValueTask<string>> Getter)> maker,
                Options baseOpts,
                Func<BoundConfigurationBase<T>, Func<IAsyncWriterAdapter>, Func<ValueTask<string>>, Task> run
            )
            {
                // run the test once
                {
                    var config = bind(baseOpts);

                    int writerCount = 0;
                    int stringCount = 0;
                    var (adapter, getter) = maker();
                    await using (adapter)
                    {
                        await run(config, () => { writerCount++; return adapter; }, () => { stringCount++; return getter(); });
                    }

                    Assert.Equal(1, writerCount);
                    Assert.Equal(1, stringCount);
                }

                // run the test with a small buffer
                {
                    var smallBufferOpts = Options.CreateBuilder(baseOpts).WithWriteBufferSizeHint(1).ToOptions();
                    var smallBufferConfig = bind(smallBufferOpts);

                    int writerCount = 0;
                    int stringCount = 0;
                    var (adapter, getter) = maker();
                    await using (adapter)
                    {
                        await run(smallBufferConfig, () => { writerCount++; return adapter; }, () => { stringCount++; return getter(); });
                    }

                    Assert.Equal(1, writerCount);
                    Assert.Equal(1, stringCount);
                }

                // run the test once, but look for leaks
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakDetectorConfig = bind(Options.CreateBuilder(baseOpts).WithMemoryPool(leakDetector).ToOptions());

                    int writerCount = 0;
                    int stringCount = 0;
                    var (adapter, getter) = maker();
                    await using (adapter)
                    {
                        await run(leakDetectorConfig, () => { writerCount++; return adapter; }, () => { stringCount++; return getter(); });
                    }

                    Assert.Equal(1, writerCount);
                    Assert.Equal(1, stringCount);

                    Assert.Equal(0, leakDetector.OutstandingRentals);
                }
            }

            static async Task RunForcedAsyncVariantsWithBaseConfig(
                Func<Options, BoundConfigurationBase<T>> bind,
                Func<(IAsyncWriterAdapter Adapter, Func<ValueTask<string>> Getter)> maker,
                Options baseOpts,
                Func<BoundConfigurationBase<T>, Func<IAsyncWriterAdapter>, Func<ValueTask<string>>, Task> run
            )
            {
                var config = bind(baseOpts);

                // walk each async transition point
                var forceUpTo = 0;
                while (true)
                {
                    var wrappedConfig = new AsyncCountingAndForcingConfig<T>(config);
                    wrappedConfig.GoAsyncAfter = forceUpTo;

                    int writerCount = 0;
                    int stringCount = 0;
                    var (adapter, getter) = maker();
                    await using (adapter)
                    {
                        await run(wrappedConfig, () => { writerCount++; return adapter; }, () => { stringCount++; return getter(); });
                    }

                    Assert.Equal(1, writerCount);
                    Assert.Equal(1, stringCount);

                    if (wrappedConfig.AsyncCounter >= forceUpTo)
                    {
                        forceUpTo++;
                    }
                    else
                    {
                        break;
                    }
                }

                // the same, but check for leaks this time
                forceUpTo = 0;
                while (true)
                {
                    var leakDetector = new TrackedMemoryPool<char>();
                    var leakDetectorConfig = bind(Options.CreateBuilder(baseOpts).WithMemoryPool(leakDetector).ToOptions());

                    var wrappedConfig = new AsyncCountingAndForcingConfig<T>(leakDetectorConfig);
                    wrappedConfig.GoAsyncAfter = forceUpTo;

                    int writerCount = 0;
                    int stringCount = 0;
                    var (adapter, getter) = maker();
                    await using (adapter)
                    {
                        await run(wrappedConfig, () => { writerCount++; return adapter; }, () => { stringCount++; return getter(); });
                    }

                    Assert.Equal(1, writerCount);
                    Assert.Equal(1, stringCount);

                    Assert.Equal(0, leakDetector.OutstandingRentals);

                    if (wrappedConfig.AsyncCounter >= forceUpTo)
                    {
                        forceUpTo++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }
}
