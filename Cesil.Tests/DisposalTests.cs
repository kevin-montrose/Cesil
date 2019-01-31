﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class DisposalTests
    {
        class _IDisposable
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void IDisposable()
        {
            var implementors =
                typeof(Options).Assembly
                    .GetTypes()
                    .Where(t => t.GetInterfaces().Any(x => x == typeof(IDisposable)))
                    .Where(t => !t.IsInterface)
                    .Where(t => !t.IsAbstract)
                    .Where(t => !t.Name.Contains("<")); // exclude compiler generated classes, let's assume they get it right?

            foreach (var t in implementors)
            {
                if (t == typeof(Reader<>))
                {
                    IDisposable_Reader();
                }
                else if (t == typeof(ReaderStateMachine))
                {
                    IDisposable_ReaderStateMachine();
                }
                else if (t == typeof(Writer<>))
                {
                    IDisposable_Writer();
                }
                else if (t == typeof(BufferWithPushback))
                {
                    IDisposable_BufferWithPushback();
                }
                else if (t == typeof(MaybeInPlaceBuffer<>))
                {
                    IDisposable_MaybeInPlaceBuffer();
                }
                else if (t == typeof(Partial<>))
                {
                    IDisposable_Partial();
                }
                else if (t == typeof(RowEndingDetector<>))
                {
                    IDisposable_RowEndingDetector();
                }
                else if (t == typeof(HeadersReader<>))
                {
                    IDisposable_HeadersReader();
                }
                else if (t == typeof(HeadersReader<>.HeaderEnumerator))
                {
                    IDisposable_HeaderEnumerator();
                }
                else if (t == typeof(ReaderStateMachine.CharacterLookup))
                {
                    IDisposable_CharacterLookup();
                } else if (t == typeof(MaxSizedBufferWriter))
                {
                    IDisposable_MaxSizedBufferWriter();
                }
                else
                {
                    throw new XunitException($"No test configured for .Dispose() on {t.Name}");
                }
            }

            // test for Reader
            void IDisposable_Reader()
            {
                // double dispose does not error
                {
                    var r = MakeReader();
                    r.Dispose();
                    r.Dispose();
                }

                // assert throws after dispose
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)r).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var r = MakeReader())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(r);
                    }
                }

                // EnumerateAll
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.EnumerateAll());
                    testCases++;
                }

                // ReadAll
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.ReadAll());
                    testCases++;
                }

                // ReadAll pre-allocated
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.ReadAll(new List<_IDisposable>()));
                    testCases++;
                }

                // TryRead
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.TryRead(out _));
                    testCases++;
                }

                // TryReadWithReuse
                {
                    var r = MakeReader();
                    r.Dispose();
                    _IDisposable x = null;
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadWithReuse(ref x));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                IReader<_IDisposable> MakeReader()
                {
                    return
                        Configuration.For<_IDisposable>(Options.Default)
                            .CreateReader(
                                new StringReader("")
                            );
                }
            }

            // test for ReaderStateMachine
            void IDisposable_ReaderStateMachine()
            {
                // double dispose does not error
                {
                    var r = MakeReader();
                    r.Dispose();
                    r.Dispose();
                }

                // assert throws after dispose
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)r).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var r = MakeReader())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(r);
                    }
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                ReaderStateMachine MakeReader()
                {
                    return
                        new ReaderStateMachine(MemoryPool<char>.Shared, 'a', 'b', 'c', RowEndings.CarriageReturnLineFeed, ReadHeaders.Always, 'd');
                }
            }

            // test for Writer
            void IDisposable_Writer()
            {
                // double dispose does not error
                {
                    var w = MakeWriter();
                    w.Dispose();
                    w.Dispose();
                }

                // assert throws after dispose
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)w).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var w = MakeWriter())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(w);
                    }
                }

                // Write
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.Write(new _IDisposable()));
                    testCases++;
                }

                // WriteAll
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteAll(new[] { new _IDisposable() }));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a writer that's "good to go"
                IWriter<_IDisposable> MakeWriter()
                {
                    return
                        Configuration.For<_IDisposable>(Options.Default)
                            .CreateWriter(
                                new StringWriter()
                            );
                }
            }

            // test for BufferWithPushback
            void IDisposable_BufferWithPushback()
            {
                // double dispose does not error
                {
                    var b = MakeBuffer();
                    b.Dispose();
                    b.Dispose();
                }

                // assert throws after dispose
                {
                    var b = MakeBuffer();
                    b.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)b).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var b = MakeBuffer())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(b);
                    }
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a buffer that's "good to go"
                BufferWithPushback MakeBuffer()
                {
                    return new BufferWithPushback(MemoryPool<char>.Shared, 64);
                }
            }

            // test for MaybeInPlaceBuffer
            void IDisposable_MaybeInPlaceBuffer()
            {
                // double dispose does not error
                {
                    var b = MakeBuffer();
                    b.Dispose();
                    b.Dispose();
                }

                // assert throws after dispose
                {
                    var b = MakeBuffer();
                    b.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)b).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var b = MakeBuffer())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(b);
                    }
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a buffer that's "good to go"
                MaybeInPlaceBuffer<char> MakeBuffer()
                {
                    return new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared);
                }
            }

            // test for Partial
            void IDisposable_Partial()
            {
                // double dispose does not error
                {
                    var p = MakePartial();
                    p.Dispose();
                    p.Dispose();
                }

                // assert throws after dispose
                {
                    var p = MakePartial();
                    p.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)p).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var p = MakePartial())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(p);
                    }
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a partial that's "good to go"
                Partial<_IDisposable> MakePartial()
                {
                    return new Partial<_IDisposable>(MemoryPool<char>.Shared);
                }
            }

            // test for RowEndingDetector
            void IDisposable_RowEndingDetector()
            {
                // double dispose does not error
                {
                    var d = MakeDetector();
                    d.Dispose();
                    d.Dispose();
                }

                // assert throws after dispose
                {
                    var d = MakeDetector();
                    d.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)d).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var d = MakeDetector())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(d);
                    }
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a partial that's "good to go"
                RowEndingDetector<_IDisposable> MakeDetector()
                {
                    return new RowEndingDetector<_IDisposable>(
                        Configuration.For<_IDisposable>(),
                        ReaderStateMachine.MakeCharacterLookup(MemoryPool<char>.Shared, 'a', 'b', 'c', 'd'),
                        TextReader.Null
                    );
                }
            }

            // test for HeadersReader
            void IDisposable_HeadersReader()
            {
                // double dispose does not error
                {
                    var r = MakeReader();
                    r.Dispose();
                    r.Dispose();
                }

                // assert throws after dispose
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)r).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var r = MakeReader())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(r);
                    }
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a partial that's "good to go"
                HeadersReader<_IDisposable> MakeReader()
                {
                    var config = Configuration.For<_IDisposable>();

                    return
                        new HeadersReader<_IDisposable>(
                            config,
                            ReaderStateMachine.MakeCharacterLookup(MemoryPool<char>.Shared, 'a', 'b', 'c', 'd'),
                            TextReader.Null,
                            new BufferWithPushback(MemoryPool<char>.Shared, 64)
                        );
                }
            }

            // test HeaderEnumerator
            void IDisposable_HeaderEnumerator()
            {
                // double dispose does not error
                {
                    var e = MakeEnumerator();
                    e.Dispose();
                    e.Dispose();
                }

                // assert throws after dispose
                {
                    var e = MakeEnumerator();
                    e.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)e).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var e = MakeEnumerator())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(e);
                    }
                }

                // Current
                {
                    var e = MakeEnumerator();
                    e.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => e.Current);
                    testCases++;
                }

                // MoveNext
                {
                    var e = MakeEnumerator();
                    e.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => e.MoveNext());
                    testCases++;
                }

                // Reset
                {
                    var e = MakeEnumerator();
                    e.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => e.Reset());
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a partial that's "good to go"
                HeadersReader<_IDisposable>.HeaderEnumerator MakeEnumerator()
                {
                    return new HeadersReader<_IDisposable>.HeaderEnumerator(0, ReadOnlyMemory<char>.Empty);
                }
            }

            // test CharacterLookup
            void IDisposable_CharacterLookup()
            {
                // double dispose does not error
                {
                    var l = MakeLookup();
                    l.Dispose();
                    l.Dispose();
                }

                // assert throws after dispose
                {
                    var l = MakeLookup();
                    l.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)l).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var l = MakeLookup())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(l);
                    }
                }
                
                Assert.Equal(expectedTestCases, testCases);

                // make a partial that's "good to go"
                ReaderStateMachine.CharacterLookup MakeLookup()
                {
                    return ReaderStateMachine.MakeCharacterLookup(MemoryPool<char>.Shared, 'a', 'b', 'c', 'd');
                }
            }

            // test CharacterLookup
            void IDisposable_MaxSizedBufferWriter()
            {
                // double dispose does not error
                {
                    var w = MakeWriter();
                    w.Dispose();
                    w.Dispose();
                }

                // assert throws after dispose
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)w).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var w = MakeWriter())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(w);
                    }
                }

                // Advance
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.Advance(0));
                    testCases++;
                }

                // GetMemory
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.GetMemory(0));
                    testCases++;
                }

                // GetSpan
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.GetSpan(0));
                    testCases++;
                }
                
                Assert.Equal(expectedTestCases, testCases);

                // make a partial that's "good to go"
                MaxSizedBufferWriter MakeWriter()
                {
                    return new MaxSizedBufferWriter(MemoryPool<char>.Shared, null);
                }
            }
        }
        
        [Fact]
        public async Task IAsyncDisposable()
        {
            var implementors =
                typeof(Options).Assembly
                    .GetTypes()
                    .Where(t => t.GetInterfaces().Any(x => x == typeof(IAsyncDisposable)))
                    .Where(t => !t.IsInterface)
                    .Where(t => !t.IsAbstract)
                    .Where(t => !t.Name.Contains("<")); // exclude compiler generated classes, let's assume they get it right?

            foreach (var t in implementors)
            {
                if(t == typeof(AsyncReader<>))
                {
                    await IAsyncDisposable_AsyncReaderAsync();
                }
                else if (t== typeof(AsyncWriter<>))
                {
                    await IAsyncDisposable_AsyncWriterAsync();
                }
                else if(t == typeof(AsyncEnumerator<>))
                {
                    await IAsyncDisposable_AsyncEnumeratorAsync();
                }
                else
                {
                    throw new XunitException($"No test configured for .Dispose() on {t.Name}");
                }
            }

            // test async reader
            async Task IAsyncDisposable_AsyncReaderAsync()
            {
                // double dispose does not error
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    await r.DisposeAsync();
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    await using (var r = MakeReader())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(r);
                    }
                }

                // EnumerateAllAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.EnumerateAllAsync());
                    testCases++;
                }

                // ReadAllAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.ReadAllAsync());
                    testCases++;
                }

                // ReadAllAsync pre-allocated
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.ReadAllAsync(new List<_IDisposable>()));
                    testCases++;
                }

                // TryReadAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadAsync());
                    testCases++;
                }

                // TryReadWithReuseAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => { _IDisposable x = null; r.TryReadWithReuseAsync(ref x); });
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                IAsyncReader<_IDisposable> MakeReader()
                {
                    return
                        Configuration.For<_IDisposable>()
                            .CreateAsyncReader(TextReader.Null);
                }
            }

            // test async writer
            async Task IAsyncDisposable_AsyncWriterAsync()
            {
                // double dispose does not error
                {
                    var w = MakeWriter();
                    await w.DisposeAsync();
                    await w.DisposeAsync();
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    await using (var w = MakeWriter())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(w);
                    }
                }

                // WriteAllAsync(IAsyncEnumerable)
                {
                    var w = MakeWriter();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteAllAsync(default(IAsyncEnumerable<_IDisposable>)));
                    testCases++;
                }

                // WriteAllAsync(IEnumerable)
                {
                    var w = MakeWriter();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteAllAsync(default(IEnumerable<_IDisposable>)));
                    testCases++;
                }

                // WriteAsync
                {
                    var w = MakeWriter();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteAsync(default(_IDisposable)));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                IAsyncWriter<_IDisposable> MakeWriter()
                {
                    return
                        Configuration.For<_IDisposable>()
                            .CreateAsyncWriter(TextWriter.Null);
                }
            }

            // test for AsyncEnumerator
            async Task IAsyncDisposable_AsyncEnumeratorAsync()
            {
                // double dispose does not error
                {
                    var e = MakeEnumerator();
                    await e.DisposeAsync();
                    await e.DisposeAsync();
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    await using (var e = MakeEnumerator())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(e);
                    }
                }

                // Current
                {
                    var e = MakeEnumerator();
                    await e.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => e.Current);
                    testCases++;
                }

                // MoveNextAsync
                {
                    var e = MakeEnumerator();
                    await e.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => e.MoveNextAsync());
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                IAsyncEnumerator<_IDisposable> MakeEnumerator()
                {
                    return
                        Configuration.For<_IDisposable>()
                            .CreateAsyncReader(new StringReader("foo\r\nbar"))
                            .EnumerateAllAsync()
                            .GetAsyncEnumerator();
                }
            }
        }
    }
#pragma warning restore IDE1006
}
