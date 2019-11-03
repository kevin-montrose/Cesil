using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
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
        private sealed class _FakeOwner : IDynamicRowOwner
        {
            public object Context { get; set; }

            public IIntrusiveLinkedList<DynamicRow> NotifyOnDispose { get; set; }

            public void Remove(DynamicRow row) { }
        }

        private class _IDisposable
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
                else if (t == typeof(Enumerator<>))
                {
                    IDisposable_Enumerator();
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
                else if(t == typeof(TextReaderAdapter))
                {
                    IDisposable_TextReaderAdapter();
                }
                else if(t == typeof(BufferWriterCharAdapter))
                {
                    IDisposable_BufferWriterAdapter();
                }
                else if (t == typeof(ReadOnlyCharSequenceAdapter))
                {
                    IDisposable_ReadOnlySequenceAdapter();
                }
                else if(t == typeof(ReaderStateMachine.RePin))
                {
                    // intentionally NOT testing this one, it's a useful hack
                    //   to use using for this one but it's not a traditional
                    //   disposable
                }
                else if(t == typeof(DynamicRowMemberNameEnumerator))
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
                else
                {
                    throw new XunitException($"No test configured for .Dispose() on {t.Name}");
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
                BufferWriterByteAdapter MakeAdapter()
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
                ReadOnlyByteSequenceAdapter MakeAdapter()
                {
                    return new ReadOnlyByteSequenceAdapter(default, Encoding.UTF8);
                }
            }

            // test for DynamicRowMemberNameEnumerator
            void IDisposable_DynamicRowMemberNameEnumerator()
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
                DynamicRowMemberNameEnumerator MakeEnumerator()
                {
                    var row = new DynamicRow();
                    var owner = new _FakeOwner();
                    var cols = new NonNull<string[]>();
                    cols.Value = new[] { "foo" };
                    row.Init(owner, 1, 0, null, TypeDescribers.Default, cols, MemoryPool<char>.Shared);

                    return new DynamicRowMemberNameEnumerator(row);
                }
            }

            // test for ReadOnlySequenceAdapter
            void IDisposable_ReadOnlySequenceAdapter()
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
                ReadOnlyCharSequenceAdapter MakeReader()
                {
                    return new ReadOnlyCharSequenceAdapter(ReadOnlySequence<char>.Empty);
                }
            }

            // test for BufferWriterAdapter
            void IDisposable_BufferWriterAdapter()
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
                BufferWriterCharAdapter MakeWriter()
                {
                    return new BufferWriterCharAdapter(new CharWriter(new Pipe().Writer));
                }
            }

            // test for TextReaderAdapter
            void IDisposable_TextReaderAdapter()
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
                TextReaderAdapter MakeReader()
                {
                    return new TextReaderAdapter(TextReader.Null);
                }
            }

            // test for TextWriterAdapter
            void IDisposable_TextWriterAdapter()
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
                TextWriterAdapter MakeWriter()
                {
                    return new TextWriterAdapter(TextWriter.Null);
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
                    var ret = new ReaderStateMachine();
                    ret.Initialize(
                        CharacterLookup.MakeCharacterLookup(MemoryPool<char>.Shared, 'a', 'b', 'c', 'd', out _),
                        'a',
                        'b',
                        RowEnding.CarriageReturnLineFeed,
                        ReadHeader.Always,
                        false
                    );
                    return ret;
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


                // WriteComment
                {
                    var w = MakeWriter();
                    w.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteComment("foo"));
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
                    return
                        new BufferWithPushback(
                            MemoryPool<char>.Shared,
                            64
                        );
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
                        new ReaderStateMachine(),
                        (ConcreteBoundConfiguration<_IDisposable>)Configuration.For<_IDisposable>(),
                        CharacterLookup.MakeCharacterLookup(MemoryPool<char>.Shared, 'a', 'b', 'c', 'd', out _),
                        new TextReaderAdapter(TextReader.Null)
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
                    var config = (ConcreteBoundConfiguration<_IDisposable>)Configuration.For<_IDisposable>();

                    return
                        new HeadersReader<_IDisposable>(
                            new ReaderStateMachine(),
                            config,
                            CharacterLookup.MakeCharacterLookup(MemoryPool<char>.Shared, 'a', 'b', 'c', 'd', out _),
                            new TextReaderAdapter(TextReader.Null),
                            new BufferWithPushback(
                                MemoryPool<char>.Shared,
                                64
                            )
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
                CharacterLookup MakeLookup()
                {
                    return CharacterLookup.MakeCharacterLookup(MemoryPool<char>.Shared, 'a', 'b', 'c', 'd', out _);
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

            // test for DynamicReader
            void IDisposable_DynamicReader()
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
                IReader<dynamic> MakeReader()
                {
                    return
                        Configuration.ForDynamic(Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions())
                            .CreateReader(new StringReader(""));
                }
            }

            // test for DynamicRow
            void IDisposable_DynamicRow()
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
                DynamicRow MakeRow()
                {
                    return new DynamicRow();
                }
            }

            // test for DynamicRowEnumerator
            void IDisposable_DynamicRowEnumerator()
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
                IEnumerator<string> MakeDynamicRowEnumerator()
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
            void IDisposable_Enumerator()
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
                IEnumerator<_IDisposable> MakeEnumerator()
                {
                    var config = Configuration.For<_IDisposable>();

                    var r = config.CreateReader(new StringReader("a\r\nb\r\nc"));

                    return r.EnumerateAll().GetEnumerator();
                }
            }

            // test for DynamicWriter
            void IDisposable_DynamicWriter()
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
                IWriter<dynamic> MakeWriter()
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
            void IDisposable_DynamicColumnEnumerator()
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
                IEnumerator<ColumnIdentifier> MakeEnumerator()
                {
                    var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                    var config = Configuration.ForDynamic(opts);

                    var r = config.CreateReader(new StringReader("a,b,c"));

                    var row = r.ReadAll().Single();

                    var dynRow = row as DynamicRow;

                    var e = dynRow.Columns.Value.GetEnumerator();

                    return e;
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
                if (t == typeof(AsyncReader<>))
                {
                    await IAsyncDisposable_AsyncReaderAsync();
                }
                else if (t == typeof(AsyncWriter<>))
                {
                    await IAsyncDisposable_AsyncWriterAsync();
                }
                else if (t == typeof(AsyncEnumerator<>))
                {
                    await IAsyncDisposable_AsyncEnumeratorAsync();
                }
                else if (t == typeof(AsyncDynamicReader))
                {
                    await IAsyncDisposable_AsyncDynamicReaderAsync();
                }
                else if (t == typeof(AsyncDynamicWriter))
                {
                    await IAsyncDisposable_AsyncDynamicWriterAsync();
                }
                else if(t == typeof(AsyncTextWriterAdapter))
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
                else
                {
                    throw new XunitException($"No test configured for .DisposeAsync() on {t.Name}");
                }
            }

            // test pipe writer adapter
            async Task IAsyncDisposable_PipeWriterAdapter()
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
                PipeWriterAdapter MakeWriter()
                {
                    return new PipeWriterAdapter(new Pipe().Writer, Encoding.UTF8, MemoryPool<char>.Shared);
                }
            }

            // test pipe reader adapter
            async Task IAsyncDisposable_PipeReaderAdapter()
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
                PipeReaderAdapter MakeReader()
                {
                    return new PipeReaderAdapter(new Pipe().Reader, Encoding.UTF8);
                }
            }

            // test async reader adapter
            async Task IAsyncDisposable_TextReaderAsyncAdapter()
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
                AsyncTextReaderAdapter MakeReader()
                {
                    return new AsyncTextReaderAdapter(TextReader.Null);
                }
            }

            // test async writer adapter
            async Task IAsyncDisposable_TextWriterAsyncAdapater()
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
                AsyncTextWriterAdapter MakeWriter()
                {
                    return new AsyncTextWriterAdapter(TextWriter.Null);
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

                // WriteCommentAsync
                {
                    var w = MakeWriter();
                    await w.DisposeAsync();
                    Assert.Throws<ObjectDisposedException>(() => w.WriteCommentAsync(""));
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

            // test async dynamic reader
            async Task IAsyncDisposable_AsyncDynamicReaderAsync()
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
                IAsyncReader<dynamic> MakeReader()
                {
                    return
                        Configuration.ForDynamic(Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions())
                            .CreateAsyncReader(TextReader.Null);
                }
            }

            // test async dynamic writer
            async Task IAsyncDisposable_AsyncDynamicWriterAsync()
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
                IAsyncWriter<dynamic> MakeWriter()
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
