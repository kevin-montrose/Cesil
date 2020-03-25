using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class DisposalTests
    {
        private static IEnumerable<TypeInfo> ShouldThrowOnUseAfterDispose<TDisposeInterface>()
        {
            var disposeI = typeof(TDisposeInterface).GetTypeInfo();
            if (!disposeI.IsInterface)
            {
                throw new Exception();
            }

            var cesilAssembly = typeof(Options).Assembly;
            var cesilTypes = cesilAssembly.GetTypes().Select(t => t.GetTypeInfo()).ToList();

            var concreteTypes =
                cesilTypes
                    .Where(t => !t.IsInterface)
                    .Where(t => !t.IsAbstract)
                    .Where(t => !t.Name.Contains("<"))  // filter out compiler generated classes
                    .ToList();

            var cesilPublicInterfaces =
                cesilTypes
                    .Where(t => t.IsPublic)
                    .Where(t => t.IsInterface)
                    .Where(t => !t.Name.Contains("<"))  // filter out compiler generated classes
                    .ToHashSet();

            var ret = new List<TypeInfo>();

            foreach (var t in concreteTypes)
            {
                var implementedInterfaces = t.ImplementedInterfaces;
                if (!implementedInterfaces.Any(i => i == disposeI))
                {
                    continue;
                }

#if RELEASE
                // Only types that are either public or leak out wrapped in public interfaces (either from System
                //   or Cesil) need to be checked.
                //
                // In DEBUG builds we check for use-after-dispose in all types, but that's a nasty perf
                //   penalty for RELEASE (and DEBUG tests should flush out these bugs anyway).
                //
                // Famous last words I know.

                var isPublic = t.IsPublic;
                var implementsSystemInterface =
                    implementedInterfaces.Any(
                        i =>
                        {
                            if (i == disposeI) return false;

                            TypeInfo iGen;

                            if (i.IsConstructedGenericType)
                            {
                                iGen = i.GetGenericTypeDefinition().GetTypeInfo();
                            }
                            else
                            {
                                iGen = i.GetTypeInfo();
                            }

                            return iGen.FullName.StartsWith("System.");
                        }
                    );
                var implementsCesilInterface =
                    implementedInterfaces.Any(
                        i =>
                        {
                            TypeInfo iGen;

                            if (i.IsConstructedGenericType)
                            {
                                iGen = i.GetGenericTypeDefinition().GetTypeInfo();
                            }
                            else
                            {
                                iGen = i.GetTypeInfo();
                            }

                            return cesilPublicInterfaces.Contains(iGen);
                        }
                    );
                var doesNotEscape = t.GetCustomAttribute<DoesNotEscapeAttribute>() != null;

                var shouldInclude = (isPublic || implementsSystemInterface || implementsCesilInterface) && !doesNotEscape;
                if (!shouldInclude)
                {
                    continue;
                }
#endif

                ret.Add(t);
            }

            return ret;
        }

        private sealed class _FakeOwner : IDynamicRowOwner
        {
            public Options Options { get; set; }
            public object Context { get; set; }

            public int MinimumExpectedColumns { get; set; }

            public NameLookup AcquireNameLookup()
            => NameLookup.Empty;

            public void ReleaseNameLookup() { }

            public void Remove(DynamicRow row) { }
        }

        private class _IDisposable
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void IDisposable()
        {
            var implementors = ShouldThrowOnUseAfterDispose<IDisposable>();

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
                else if (t == typeof(Partial))
                {
                    IDisposable_Partial();
                }
                else if (t == typeof(RowEndingDetector))
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
                else if (t == typeof(CharacterLookup))
                {
                    IDisposable_CharacterLookup();
                }
                else if (t == typeof(MaxSizedBufferWriter))
                {
                    IDisposable_MaxSizedBufferWriter();
                }
                else if (t == typeof(DynamicReader))
                {
                    IDisposable_DynamicReader();
                }
                else if (t == typeof(DynamicRow))
                {
                    IDisposable_DynamicRow();
                }
                else if (t == typeof(DynamicRowEnumerator<>))
                {
                    IDisposable_DynamicRowEnumerator();
                }
                else if (t == typeof(Enumerable<>))
                {
                    IDisposable_Enumerable();
                }
                else if (t == typeof(DynamicWriter))
                {
                    IDisposable_DynamicWriter();
                }
                else if (t == typeof(DynamicRow.DynamicColumnEnumerator))
                {
                    IDisposable_DynamicColumnEnumerator();
                }
                else if (t == typeof(TextWriterAdapter))
                {
                    IDisposable_TextWriterAdapter();
                }
                else if (t == typeof(TextReaderAdapter))
                {
                    IDisposable_TextReaderAdapter();
                }
                else if (t == typeof(BufferWriterCharAdapter))
                {
                    IDisposable_BufferWriterAdapter();
                }
                else if (t == typeof(ReadOnlyCharSequenceAdapter))
                {
                    IDisposable_ReadOnlySequenceAdapter();
                }
                else if (t == typeof(DynamicRowMemberNameEnumerator))
                {
                    IDisposable_DynamicRowMemberNameEnumerator();
                }
                else if (t == typeof(ReadOnlyByteSequenceAdapter))
                {
                    IDisposable_ReadOnlyByteSequenceAdapter();
                }
                else if (t == typeof(BufferWriterByteAdapter))
                {
                    IDisposable_BufferWriterByteAdapter();
                }
                else if (t == typeof(ReaderStateMachine.PinHandle))
                {
                    // intentionally NOT testing, this is plain as hell wrapper
                    //   that is for making things exception safe
                }
                else if (t == typeof(RequiredSet) || t == typeof(DynamicRowConstructor) || t == typeof(NeedsHoldRowConstructor<,>) || t == typeof(SimpleRowConstructor<>))
                {
                    // intentionally NOT testing, these are all just proxies for RequireSet and aren't really disposable
                }
                else if (t == typeof(UnmanagedLookupArray<>))
                {
                    IDisposable_UnmanagedLookupArray();
                }
                else if (t == typeof(EmptyMemoryOwner))
                {
                    // intentionally NOT testing, the empty owner has no resources to release but has to 
                }
                else if (t == typeof(PassthroughRowEnumerator))
                {
                    IDisposable_PassthroughRowEnumerator();
                }
                else if (t == typeof(NameLookup))
                {
                    // intentionally NOT testing, this is ref counted so this
                    //   test wouldn't be sufficient
                }
                else
                {
                    throw new XunitException($"No test configured for .Dispose() on {t.Name}");
                }
            }

            void IDisposable_PassthroughRowEnumerator()
            {
                // double dispose does not error
                {
                    var a = MakeEnumerator();
                    a.Dispose();
                    a.Dispose();
                }

                // assert throws after dispose
                {
                    var a = MakeEnumerator();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)a).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var a = MakeEnumerator())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(a);
                    }
                }

                // Current
                {
                    var a = MakeEnumerator();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => a.Current);
                    testCases++;
                }

                // MoveNext
                {
                    var a = MakeEnumerator();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => a.MoveNext());
                    testCases++;
                }

                // Reset
                {
                    var a = MakeEnumerator();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => a.Reset());
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make an adapter that's "good to go"
                static PassthroughRowEnumerator MakeEnumerator()
                {
                    var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                    var config = Configuration.ForDynamic(opts);

                    var r = config.CreateReader(new StringReader("a,b,c"));

                    r.TryRead(out var row);

                    IEnumerable<dynamic> e = row;
                    var ee = e.GetEnumerator();

                    return (PassthroughRowEnumerator)ee;
                }
            }

            void IDisposable_UnmanagedLookupArray()
            {
                // double dispose does not error
                {
                    var a = MakeLookupAdapter();
                    a.Dispose();
                    a.Dispose();
                }

                // assert throws after dispose
                {
                    var a = MakeLookupAdapter();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)a).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var a = MakeLookupAdapter())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(a);
                    }
                }

                Assert.Equal(expectedTestCases, testCases);

                // make an adapter that's "good to go"
                static UnmanagedLookupArray<int> MakeLookupAdapter()
                {
                    return new UnmanagedLookupArray<int>(MemoryPool<char>.Shared, 10);
                }
            }

            // test for BufferWriterByteAdapter
            void IDisposable_BufferWriterByteAdapter()
            {
                // double dispose does not error
                {
                    var a = MakeAdapter();
                    a.Dispose();
                    a.Dispose();
                }

                // assert throws after dispose
                {
                    var a = MakeAdapter();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)a).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var a = MakeAdapter())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(a);
                    }
                }

                // Write(ReadOnlySpan(char))
                {
                    var a = MakeAdapter();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => a.Write(ReadOnlySpan<char>.Empty));
                    testCases++;
                }

                // Write(char)
                {
                    var a = MakeAdapter();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => a.Write('c'));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make an adapter that's "good to go"
                static BufferWriterByteAdapter MakeAdapter()
                {
                    return new BufferWriterByteAdapter(default, Encoding.UTF8);
                }
            }

            // test for ReadOnlyByteSequenceAdapter
            void IDisposable_ReadOnlyByteSequenceAdapter()
            {
                // double dispose does not error
                {
                    var a = MakeAdapter();
                    a.Dispose();
                    a.Dispose();
                }

                // assert throws after dispose
                {
                    var a = MakeAdapter();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)a).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var a = MakeAdapter())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(a);
                    }
                }

                // Read
                {
                    var a = MakeAdapter();
                    a.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => a.Read(default));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make an adapter that's "good to go"
                static ReadOnlyByteSequenceAdapter MakeAdapter()
                {
                    return new ReadOnlyByteSequenceAdapter(default, Encoding.UTF8);
                }
            }

            // test for DynamicRowMemberNameEnumerator
            static void IDisposable_DynamicRowMemberNameEnumerator()
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

                // make an enumerator that's "good to go"
                static DynamicRowMemberNameEnumerator MakeEnumerator()
                {
                    var row = new DynamicRow();
                    var owner = new _FakeOwner();
                    var cols = new[] { "foo" };
                    row.Init(owner, 0, null, TypeDescribers.Default, true, cols, 0, MemoryPool<char>.Shared);

                    return new DynamicRowMemberNameEnumerator(row);
                }
            }

            // test for ReadOnlySequenceAdapter
            static void IDisposable_ReadOnlySequenceAdapter()
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

                // Read
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.Read(Span<char>.Empty));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a writer that's "good to go"
                static ReadOnlyCharSequenceAdapter MakeReader()
                {
                    return new ReadOnlyCharSequenceAdapter(ReadOnlySequence<char>.Empty);
                }
            }

            // test for BufferWriterAdapter
            static void IDisposable_BufferWriterAdapter()
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

                // Write(char)
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.Write('c'));
                    testCases++;
                }

                // Write(Span<char>)
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.Write(Span<char>.Empty));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a writer that's "good to go"
                static BufferWriterCharAdapter MakeWriter()
                {
                    return new BufferWriterCharAdapter(new CharWriter(new Pipe().Writer));
                }
            }

            // test for TextReaderAdapter
            static void IDisposable_TextReaderAdapter()
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

                // Read
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.Read(Span<char>.Empty));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a writer that's "good to go"
                static TextReaderAdapter MakeReader()
                {
                    return new TextReaderAdapter(TextReader.Null);
                }
            }

            // test for TextWriterAdapter
            static void IDisposable_TextWriterAdapter()
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
                    using (var r = MakeWriter())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(r);
                    }
                }

                // Write(char)
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.Write('c'));
                    testCases++;
                }

                // Write(ReadOnlySpan<char>)
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.Write(ReadOnlySpan<char>.Empty));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a writer that's "good to go"
                static TextWriterAdapter MakeWriter()
                {
                    return new TextWriterAdapter(TextWriter.Null);
                }
            }

            // test for Reader
            static void IDisposable_Reader()
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

                // TryReadWithComment
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadWithComment());
                    testCases++;
                }

                // TryReadWithComment
                {
                    var r = MakeReader();
                    r.Dispose();
                    _IDisposable x = null;
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadWithCommentReuse(ref x));
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
                static IReader<_IDisposable> MakeReader()
                {
                    return
                        Configuration.For<_IDisposable>(Options.Default)
                            .CreateReader(
                                new StringReader("")
                            );
                }
            }

            // test for ReaderStateMachine
            static void IDisposable_ReaderStateMachine()
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
                static ReaderStateMachine MakeReader()
                {
                    var ret = new ReaderStateMachine();
                    ret.Initialize(
                        CharacterLookup.MakeCharacterLookup(Options.Default, out _),
                        'a',
                        'b',
                        RowEnding.CarriageReturnLineFeed,
                        ReadHeader.Always,
                        false,
                        false,
                        false
                    );
                    return ret;
                }
            }

            // test for Writer
            static void IDisposable_Writer()
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


                // WriteComment
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteComment("foo"));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a writer that's "good to go"
                static IWriter<_IDisposable> MakeWriter()
                {
                    return
                        Configuration.For<_IDisposable>(Options.Default)
                            .CreateWriter(
                                new StringWriter()
                            );
                }
            }

            // test for BufferWithPushback
            static void IDisposable_BufferWithPushback()
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
                static BufferWithPushback MakeBuffer()
                {
                    return
                        new BufferWithPushback(
                            MemoryPool<char>.Shared,
                            64
                        );
                }
            }

            // test for MaybeInPlaceBuffer
            static void IDisposable_MaybeInPlaceBuffer()
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
                static MaybeInPlaceBuffer<char> MakeBuffer()
                {
                    return new MaybeInPlaceBuffer<char>(MemoryPool<char>.Shared);
                }
            }

            // test for Partial
            static void IDisposable_Partial()
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
                static Partial MakePartial()
                {
                    return new Partial(MemoryPool<char>.Shared);
                }
            }

            // test for RowEndingDetector
            static void IDisposable_RowEndingDetector()
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
                static RowEndingDetector MakeDetector()
                {
                    return new RowEndingDetector(
                        new ReaderStateMachine(),
                        Options.Default,
                        CharacterLookup.MakeCharacterLookup(Options.Default, out _),
                        new TextReaderAdapter(TextReader.Null)
                    );
                }
            }

            // test for HeadersReader
            static void IDisposable_HeadersReader()
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
                static HeadersReader<_IDisposable> MakeReader()
                {
                    var config = (ConcreteBoundConfiguration<_IDisposable>)Configuration.For<_IDisposable>();

                    return
                        new HeadersReader<_IDisposable>(
                            new ReaderStateMachine(),
                            config,
                            CharacterLookup.MakeCharacterLookup(Options.Default, out _),
                            new TextReaderAdapter(TextReader.Null),
                            new BufferWithPushback(
                                MemoryPool<char>.Shared,
                                64
                            ),
                            RowEnding.CarriageReturnLineFeed
                        );
                }
            }

            // test HeaderEnumerator
            static void IDisposable_HeaderEnumerator()
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
                static HeadersReader<_IDisposable>.HeaderEnumerator MakeEnumerator()
                {
                    return new HeadersReader<_IDisposable>.HeaderEnumerator(0, ReadOnlyMemory<char>.Empty, WhitespaceTreatments.Preserve);
                }
            }

            // test CharacterLookup
            static void IDisposable_CharacterLookup()
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
                static CharacterLookup MakeLookup()
                {
                    return CharacterLookup.MakeCharacterLookup(Options.Default, out _);
                }
            }

            // test CharacterLookup
            static void IDisposable_MaxSizedBufferWriter()
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
                static MaxSizedBufferWriter MakeWriter()
                {
                    return new MaxSizedBufferWriter(MemoryPool<char>.Shared, null);
                }
            }

            // test for DynamicReader
            static void IDisposable_DynamicReader()
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
                    Assert.Throws<ObjectDisposedException>(() => r.ReadAll(new List<dynamic>()));
                    testCases++;
                }

                // TryRead
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.TryRead(out _));
                    testCases++;
                }

                // TryReadWithComment
                {
                    var r = MakeReader();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadWithComment());
                    testCases++;
                }

                // TryReadWithComment
                {
                    var r = MakeReader();
                    r.Dispose();
                    dynamic x = null;
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadWithCommentReuse(ref x));
                    testCases++;
                }

                // TryReadWithReuse
                {
                    var r = MakeReader();
                    r.Dispose();
                    dynamic x = null;
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadWithReuse(ref x));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static IReader<dynamic> MakeReader()
                {
                    return
                        Configuration.ForDynamic(Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions())
                            .CreateReader(new StringReader(""));
                }
            }

            // test for DynamicRow
            static void IDisposable_DynamicRow()
            {
                // double dispose does not error
                {
                    var r = MakeRow();
                    r.Dispose();
                    r.Dispose();
                }

                // assert throws after dispose
                {
                    var r = MakeRow();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)r).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var r = MakeRow())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(r);
                    }
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static DynamicRow MakeRow()
                {
                    return new DynamicRow();
                }
            }

            // test for DynamicRowEnumerator
            static void IDisposable_DynamicRowEnumerator()
            {
                // double dispose does not error
                {
                    var r = MakeDynamicRowEnumerator();
                    r.Dispose();
                    r.Dispose();
                }

                // assert throws after dispose
                {
                    var r = MakeDynamicRowEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)r).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var r = MakeDynamicRowEnumerator())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(r);
                    }
                }

                // Current
                {
                    var r = MakeDynamicRowEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.Current);
                    testCases++;
                }

                // MoveNext
                {
                    var r = MakeDynamicRowEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.MoveNext());
                    testCases++;
                }

                // Reset
                {
                    var r = MakeDynamicRowEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.Reset());
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static IEnumerator<string> MakeDynamicRowEnumerator()
                {
                    var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                    var config = Configuration.ForDynamic(opts);

                    var r = config.CreateReader(new StringReader("a,b,c"));

                    r.TryRead(out var row);

                    IEnumerable<string> e = row;
                    var ee = e.GetEnumerator();

                    return ee;
                }
            }

            // test for Enumerator
            static void IDisposable_Enumerable()
            {
                // double dispose does not error
                {
                    var r = MakeEnumerable();
                    r.Dispose();
                    r.Dispose();
                }

                // assert throws after dispose
                {
                    var r = MakeEnumerable();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)r).AssertNotDisposed());
                }

                // this is a weird one, so were doing this manually

                // GetEnumerator
                {
                    var r = MakeEnumerable();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.GetEnumerator());
                }

                // Current
                {
                    var r = MakeEnumerable().GetEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.Current);
                }

                // MoveNext
                {
                    var r = MakeEnumerable().GetEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.MoveNext());
                }

                // Reset
                {
                    var r = MakeEnumerable().GetEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.Reset());
                }

                // make a reader that's "good to go"
                static Enumerable<_IDisposable> MakeEnumerable()
                {
                    var config = Configuration.For<_IDisposable>();

                    var r = config.CreateReader(new StringReader("a\r\nb\r\nc"));

                    return (Enumerable<_IDisposable>)r.EnumerateAll();
                }
            }

            // test for DynamicWriter
            static void IDisposable_DynamicWriter()
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

                // WriteComment
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteComment("foo"));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a writer that's "good to go"
                static IWriter<dynamic> MakeWriter()
                {
                    var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

                    return
                        Configuration.ForDynamic(opts)
                            .CreateWriter(
                                new StringWriter()
                            );
                }
            }

            // test DynamicColumnEnumerator
            static void IDisposable_DynamicColumnEnumerator()
            {
                // double dispose does not error
                {
                    var r = MakeEnumerator();
                    r.Dispose();
                    r.Dispose();
                }

                // assert throws after dispose
                {
                    var r = MakeEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableDisposable)r).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    using (var r = MakeEnumerator())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(r);
                    }
                }

                // Current
                {
                    var r = MakeEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.Current);
                    testCases++;
                }

                // MoveNext
                {
                    var r = MakeEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.MoveNext());
                    testCases++;
                }

                // Reset
                {
                    var r = MakeEnumerator();
                    r.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => r.Reset());
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static IEnumerator<ColumnIdentifier> MakeEnumerator()
                {
                    var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                    var config = Configuration.ForDynamic(opts);

                    var r = config.CreateReader(new StringReader("a,b,c"));

                    var row = r.ReadAll().Single();

                    var dynRow = row as DynamicRow;

                    var e = dynRow.Columns.GetEnumerator();

                    return e;
                }
            }
        }

        [Fact]
        public async Task IAsyncDisposable()
        {
            var implementors = ShouldThrowOnUseAfterDispose<IAsyncDisposable>();

            foreach (var t in implementors)
            {
                if (t == typeof(AsyncReader<>))
                {
                    await IAsyncDisposable_AsyncReaderAsync();
                }
                else if (t == typeof(AsyncWriter<>))
                {
                    await IAsyncDisposable_AsyncWriterAsync();
                }
                else if (t == typeof(AsyncEnumerable<>))
                {
                    await IAsyncDisposable_AsyncEnumerableAsync();
                }
                else if (t == typeof(AsyncDynamicReader))
                {
                    await IAsyncDisposable_AsyncDynamicReaderAsync();
                }
                else if (t == typeof(AsyncDynamicWriter))
                {
                    await IAsyncDisposable_AsyncDynamicWriterAsync();
                }
                else if (t == typeof(AsyncTextWriterAdapter))
                {
                    await IAsyncDisposable_TextWriterAsyncAdapater();
                }
                else if (t == typeof(AsyncTextReaderAdapter))
                {
                    await IAsyncDisposable_TextReaderAsyncAdapter();
                }
                else if (t == typeof(PipeReaderAdapter))
                {
                    await IAsyncDisposable_PipeReaderAdapter();
                }
                else if (t == typeof(PipeWriterAdapter))
                {
                    await IAsyncDisposable_PipeWriterAdapter();
                }
                else if (t == typeof(AsyncEnumerableAdapter<>))
                {
                    await IAsyncDisposable_AsyncEnumerableAdapter();
                }
                else
                {
                    throw new XunitException($"No test configured for .DisposeAsync() on {t.Name}");
                }
            }

            // test AsyncEnumerableAdapter
            static async Task IAsyncDisposable_AsyncEnumerableAdapter()
            {
                // double dispose does not error
                {
                    var w = MakeAsyncEnumerableAdapter();
                    await w.DisposeAsync();
                    await w.DisposeAsync();
                }

                // assert throws after dispose
                {
                    var w = MakeAsyncEnumerableAdapter();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableAsyncDisposable)w).AssertNotDisposed());
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    await using (var w = MakeAsyncEnumerableAdapter())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(w);
                    }
                }

                // Current
                {
                    var r = MakeAsyncEnumerableAdapter();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.Current);
                    testCases++;
                }

                // GetAsyncEnumerator
                {
                    var r = MakeAsyncEnumerableAdapter();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.GetAsyncEnumerator());
                    testCases++;
                }

                // MoveNextAsync
                {
                    var r = MakeAsyncEnumerableAdapter();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.MoveNextAsync());
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a writer that's "good to go"
                static AsyncEnumerableAdapter<_IDisposable> MakeAsyncEnumerableAdapter()
                {
                    return new AsyncEnumerableAdapter<_IDisposable>(new _IDisposable[0]);
                }
            }

            // test pipe writer adapter
            static async Task IAsyncDisposable_AsyncEnumerableAsync()
            {
                // double dispose does not error
                {
                    var w = MakeAsyncEnumerable();
                    await w.DisposeAsync();
                    await w.DisposeAsync();
                }

                // assert throws after dispose
                {
                    var w = MakeAsyncEnumerable();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => ((ITestableAsyncDisposable)w).AssertNotDisposed());
                }

                // this one is weird, so do it manually

                // GetAsyncEnumerator
                {
                    var e = MakeAsyncEnumerable();
                    await e.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => e.GetAsyncEnumerator());
                }

                // MoveNextAsync
                {
                    var e = MakeAsyncEnumerable().GetAsyncEnumerator();
                    await e.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => e.MoveNextAsync());
                }

                // Current
                {
                    var e = MakeAsyncEnumerable().GetAsyncEnumerator();
                    await e.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => e.Current);
                }

                // make a writer that's "good to go"
                static AsyncEnumerable<_IDisposable> MakeAsyncEnumerable()
                {
                    return
                        (AsyncEnumerable<_IDisposable>)
                            Configuration
                                .For<_IDisposable>()
                                .CreateAsyncReader(TextReader.Null)
                                .EnumerateAllAsync();
                }
            }

            // test pipe writer adapter
            static async Task IAsyncDisposable_PipeWriterAdapter()
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

                // WriteAsync
                {
                    var w = MakeWriter();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteAsync(ReadOnlyMemory<char>.Empty, default));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a writer that's "good to go"
                static PipeWriterAdapter MakeWriter()
                {
                    return new PipeWriterAdapter(new Pipe().Writer, Encoding.UTF8, MemoryPool<char>.Shared);
                }
            }

            // test pipe reader adapter
            static async Task IAsyncDisposable_PipeReaderAdapter()
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

                // ReaderAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.ReadAsync(Memory<char>.Empty, default));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static PipeReaderAdapter MakeReader()
                {
                    return new PipeReaderAdapter(new Pipe().Reader, Encoding.UTF8);
                }
            }

            // test async reader adapter
            static async Task IAsyncDisposable_TextReaderAsyncAdapter()
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

                // ReaderAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.ReadAsync(Memory<char>.Empty, default));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static AsyncTextReaderAdapter MakeReader()
                {
                    return new AsyncTextReaderAdapter(TextReader.Null);
                }
            }

            // test async writer adapter
            static async Task IAsyncDisposable_TextWriterAsyncAdapater()
            {
                // double dispose does not error
                {
                    var r = MakeWriter();
                    await r.DisposeAsync();
                    await r.DisposeAsync();
                }

                var testCases = 0;

                // figure out how many _public_ methods need testing
                int expectedTestCases;
                {
                    await using (var r = MakeWriter())
                    {
                        expectedTestCases = GetNumberExpectedDisposableTestCases(r);
                    }
                }

                // WriteAsync
                {
                    var w = MakeWriter();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteAsync(ReadOnlyMemory<char>.Empty, default));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static AsyncTextWriterAdapter MakeWriter()
                {
                    return new AsyncTextWriterAdapter(TextWriter.Null);
                }
            }

            // test async reader
            static async Task IAsyncDisposable_AsyncReaderAsync()
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

                // TryReadWithCommentAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadWithCommentAsync());
                    testCases++;
                }

                // TryReadWithCommentReuseAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => { _IDisposable x = null; r.TryReadWithCommentReuseAsync(ref x); });
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
                static IAsyncReader<_IDisposable> MakeReader()
                {
                    return
                        Configuration.For<_IDisposable>()
                            .CreateAsyncReader(TextReader.Null);
                }
            }

            // test async writer
            static async Task IAsyncDisposable_AsyncWriterAsync()
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
                    Assert.Throws<ObjectDisposedException>(() => w.WriteAsync(default));
                    testCases++;
                }

                // WriteCommentAsync
                {
                    var w = MakeWriter();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteCommentAsync(""));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static IAsyncWriter<_IDisposable> MakeWriter()
                {
                    return
                        Configuration.For<_IDisposable>()
                            .CreateAsyncWriter(TextWriter.Null);
                }
            }

            // test async dynamic reader
            static async Task IAsyncDisposable_AsyncDynamicReaderAsync()
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
                    Assert.Throws<ObjectDisposedException>(() => r.ReadAllAsync(new List<dynamic>()));
                    testCases++;
                }

                // TryReadAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadAsync());
                    testCases++;
                }

                // TryReadWithCommentAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => r.TryReadWithCommentAsync());
                    testCases++;
                }

                // TryReadWithCommentReuseAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => { dynamic x = null; r.TryReadWithCommentReuseAsync(ref x); });
                    testCases++;
                }

                // TryReadWithReuseAsync
                {
                    var r = MakeReader();
                    await r.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => { dynamic x = null; r.TryReadWithReuseAsync(ref x); });
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static IAsyncReader<dynamic> MakeReader()
                {
                    return
                        Configuration.ForDynamic(Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions())
                            .CreateAsyncReader(TextReader.Null);
                }
            }

            // test async dynamic writer
            static async Task IAsyncDisposable_AsyncDynamicWriterAsync()
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

                // WriteCommentAsync
                {
                    var w = MakeWriter();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteCommentAsync(""));
                    testCases++;
                }

                Assert.Equal(expectedTestCases, testCases);

                // make a reader that's "good to go"
                static IAsyncWriter<dynamic> MakeWriter()
                {
                    return
                        Configuration.ForDynamic()
                            .CreateAsyncWriter(TextWriter.Null);
                }
            }
        }

        [Fact]
        public void DynamicRowThrowsAfterDisposed()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    dynamic row1, row2;
                    dynamic c1, c2, c3, c4;
                    using (var reader = makeReader("1,2\r\n3,4"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out row1));
                        Assert.True(csv.TryRead(out row2));

                        c1 = row1[0];
                        c2 = row1[1];

                        Assert.Equal(1, (int)c1);
                        Assert.Equal(2, (int)c2);

                        c3 = row2[0];
                        c4 = row2[1];

                        Assert.Equal(3, (int)c3);
                        Assert.Equal(4, (int)c4);
                    }

                    // the rows throw
                    Assert.Throws<ObjectDisposedException>(() => { var x = row1[0]; });
                    Assert.Throws<ObjectDisposedException>(() => { var x = row1[1]; });
                    Assert.Throws<ObjectDisposedException>(() => { var x = row2[0]; });
                    Assert.Throws<ObjectDisposedException>(() => { var x = row2[1]; });

                    // the cells throw
                    Assert.Throws<ObjectDisposedException>(() => { int x = c1; });
                    Assert.Throws<ObjectDisposedException>(() => { int x = c2; });
                    Assert.Throws<ObjectDisposedException>(() => { int x = c3; });
                    Assert.Throws<ObjectDisposedException>(() => { int x = c4; });
                }
            );
        }

        [Fact]
        public void DynamicCellThrowsAfterRowDisposed()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("1,2\r\n3,4"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out dynamic row));

                        var c1 = row[0];
                        var c2 = row[1];

                        Assert.Equal(1, (int)c1);
                        Assert.Equal(2, (int)c2);

                        row.Dispose();

                        // getting cells throws
                        Assert.Throws<ObjectDisposedException>(() => { var x = row[0]; });
                        Assert.Throws<ObjectDisposedException>(() => { var x = row[1]; });

                        // now cells throw
                        Assert.Throws<ObjectDisposedException>(() => { int x = c1; });
                        Assert.Throws<ObjectDisposedException>(() => { int x = c2; });

                        Assert.True(csv.TryReadWithReuse(ref row));

                        var c3 = row[0];
                        var c4 = row[1];

                        Assert.Equal(3, (int)c3);
                        Assert.Equal(4, (int)c4);

                        // old still throws
                        Assert.Throws<InvalidOperationException>(() => { int x = c1; });
                        Assert.Throws<InvalidOperationException>(() => { int x = c2; });

                        row.Dispose();

                        // getting cells throws
                        Assert.Throws<ObjectDisposedException>(() => { var x = row[0]; });
                        Assert.Throws<ObjectDisposedException>(() => { var x = row[1]; });

                        // all old cells throw
                        Assert.Throws<InvalidOperationException>(() => { int x = c1; });
                        Assert.Throws<InvalidOperationException>(() => { int x = c2; });
                        Assert.Throws<ObjectDisposedException>(() => { int x = c3; });
                        Assert.Throws<ObjectDisposedException>(() => { int x = c4; });
                    }
                }
            );
        }
    }
#pragma warning restore IDE1006
}
