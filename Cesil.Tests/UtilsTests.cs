﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cesil.Tests
{
    public class UtilsTests
    {
        [Fact]
        public void Sort()
        {
            // empty
            {
                var items = Span<int>.Empty;
                Utils.Sort(items, (a, b) => a.CompareTo(b));

                Assert.True(items.IsEmpty);
            }

            // single element
            {
                var items = new[] { 1 }.AsSpan();
                Utils.Sort(items, (a, b) => a.CompareTo(b));

                Assert.Collection(
                    items.ToArray(),
                    a => Assert.Equal(1, a)
                );
            }

            // two element
            {
                var items1 = new[] { 1, 2 }.AsSpan();
                var items2 = new[] { 2, 1 }.AsSpan();

                Utils.Sort(items1, (a, b) => a.CompareTo(b));
                Utils.Sort(items2, (a, b) => a.CompareTo(b));

                Assert.Collection(
                    items1.ToArray(),
                    a => Assert.Equal(1, a),
                    a => Assert.Equal(2, a)
                );

                Assert.Collection(
                    items2.ToArray(),
                    a => Assert.Equal(1, a),
                    a => Assert.Equal(2, a)
                );
            }

            // three element
            {
                var items1 = new[] { 1, 2, 3 }.AsSpan();
                var items2 = new[] { 1, 3, 2 }.AsSpan();
                var items3 = new[] { 2, 1, 3 }.AsSpan();
                var items4 = new[] { 2, 3, 1 }.AsSpan();
                var items5 = new[] { 3, 1, 2 }.AsSpan();
                var items6 = new[] { 3, 2, 1 }.AsSpan();

                Utils.Sort(items1, (a, b) => a.CompareTo(b));
                Utils.Sort(items2, (a, b) => a.CompareTo(b));
                Utils.Sort(items3, (a, b) => a.CompareTo(b));
                Utils.Sort(items4, (a, b) => a.CompareTo(b));
                Utils.Sort(items5, (a, b) => a.CompareTo(b));
                Utils.Sort(items6, (a, b) => a.CompareTo(b));

                Assert.Collection(
                    items1.ToArray(),
                    a => Assert.Equal(1, a),
                    a => Assert.Equal(2, a),
                    a => Assert.Equal(3, a)
                );

                Assert.Collection(
                    items2.ToArray(),
                    a => Assert.Equal(1, a),
                    a => Assert.Equal(2, a),
                    a => Assert.Equal(3, a)
                );

                Assert.Collection(
                    items3.ToArray(),
                    a => Assert.Equal(1, a),
                    a => Assert.Equal(2, a),
                    a => Assert.Equal(3, a)
                );

                Assert.Collection(
                    items4.ToArray(),
                    a => Assert.Equal(1, a),
                    a => Assert.Equal(2, a),
                    a => Assert.Equal(3, a)
                );

                Assert.Collection(
                    items5.ToArray(),
                    a => Assert.Equal(1, a),
                    a => Assert.Equal(2, a),
                    a => Assert.Equal(3, a)
                );

                Assert.Collection(
                    items6.ToArray(),
                    a => Assert.Equal(1, a),
                    a => Assert.Equal(2, a),
                    a => Assert.Equal(3, a)
                );
            }

            // lots of random data
            {
                var rand = new Random(2020_06_21);
                for (var i = 0; i < 1_000; i++)
                {
                    var len = rand.Next(1_000);
                    var items = new List<int>();
                    for (var j = 0; j < len; j++)
                    {
                        items.Add(rand.Next());
                    }

                    var span = items.ToArray().AsSpan();
                    Utils.Sort(span, (a, b) => a.CompareTo(b));

                    // make sure all the elements appear
                    var spanArr = span.ToArray();
                    for (var j = 0; j < items.Count; j++)
                    {
                        var subItem = items[j];
                        var countInItems = items.Count(x => x == subItem);
                        var countInSpan = spanArr.Count(x => x == subItem);

                        Assert.Equal(countInItems, countInSpan);
                    }

                    if (items.Count > 0)
                    {
                        // check in order
                        var prevItem = span[0];
                        for (var j = 1; j < span.Length; j++)
                        {
                            var curItem = span[j];
                            var inOrder = prevItem <= curItem;

                            Assert.True(inOrder);
                        }
                    }
                    else
                    {
                        Assert.True(span.IsEmpty);
                    }
                }
            }
        }

        [Fact]
        public void EmptyMemoryOwnerIsEmpty()
        {
            var m = EmptyMemoryOwner.Singleton;
            Assert.True(m.Memory.IsEmpty);
        }

        private sealed class _WeirdImpossibleExceptions
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void WeirdImpossibleExceptions()
        {
            // for lack of a better place to test these, just do it

            var concreteConfig = Configuration.For<_WeirdImpossibleExceptions>();
            var concreteExc = ImpossibleException.Create("testing", "foo", "bar", 123, concreteConfig);
            Assert.Equal("The impossible has happened!\r\ntesting\r\nFile: foo\r\nMember: bar\r\nLine: 123\r\nPlease report this to https://github.com/kevin-montrose/Cesil/issues/new\r\nBound to Cesil.Tests.UtilsTests+_WeirdImpossibleExceptions\r\nConcrete binding\r\nWith options: Options with CommentCharacter=, DynamicRowDisposal=OnReaderDispose, EscapedValueEscapeCharacter=\", EscapedValueStartAndEnd=\", MemoryPoolProvider=DefaultMemoryPoolProvider Shared Instance, ReadBufferSizeHint=0, ReadHeader=Detect, RowEnding=CarriageReturnLineFeed, TypeDescriber=DefaultTypeDescriber Shared Instance, ValueSeparator=,, WriteBufferSizeHint=, WriteHeader=Always, WriteTrailingRowEnding=Never, WhitespaceTreatment=Preserve, ExtraColumnTreatment=Ignore", concreteExc.Message);

            var dynConfig = Configuration.ForDynamic();
            var dynExc = ImpossibleException.Create("testing", "foo", "bar", 123, dynConfig);

            Assert.Equal("The impossible has happened!\r\ntesting\r\nFile: foo\r\nMember: bar\r\nLine: 123\r\nPlease report this to https://github.com/kevin-montrose/Cesil/issues/new\r\nBound to System.Object\r\nDynamic binding\r\nWith options: Options with CommentCharacter=, DynamicRowDisposal=OnReaderDispose, EscapedValueEscapeCharacter=\", EscapedValueStartAndEnd=\", MemoryPoolProvider=DefaultMemoryPoolProvider Shared Instance, ReadBufferSizeHint=0, ReadHeader=Always, RowEnding=CarriageReturnLineFeed, TypeDescriber=DefaultTypeDescriber Shared Instance, ValueSeparator=,, WriteBufferSizeHint=, WriteHeader=Always, WriteTrailingRowEnding=Never, WhitespaceTreatment=Preserve, ExtraColumnTreatment=IncludeDynamic", dynExc.Message);

            Assert.Throws<ImpossibleException>(() => Throw.ImpossibleException<object>("wat"));
            Assert.Throws<ImpossibleException>(() => Throw.ImpossibleException<object>("wat", Options.Default));
            Assert.Throws<ImpossibleException>(() => Throw.ImpossibleException<object, _WeirdImpossibleExceptions>("wat", concreteConfig));


            var files = new[] { "SomeFile.cs", null };
            var members = new[] { "SomeMember", null };

            foreach(var f in files)
            {
                foreach(var m in members)
                {
                    Assert.Throws<ImpossibleException>(() => Throw.ImpossibleException<object>("wat", f, m));
                    Assert.Throws<ImpossibleException>(() => Throw.ImpossibleException<object>("wat", Options.Default, f, m));
                    Assert.Throws<ImpossibleException>(() => Throw.ImpossibleException<object, _WeirdImpossibleExceptions>("wat", concreteConfig, f, m));
                }
            }
        }

        [Fact]
        public void EmptyDynamicRowOwnerMembersThrow()
        {
            var e = EmptyDynamicRowOwner.Singleton;
            Assert.Throws<ImpossibleException>(() => e.AcquireNameLookup());
            Assert.Throws<ImpossibleException>(() => e.Context);
            Assert.Throws<ImpossibleException>(() => e.Options);
            Assert.Throws<ImpossibleException>(() => e.ReleaseNameLookup());
            Assert.Throws<ImpossibleException>(() => e.Remove(new DynamicRow()));

            var dc = (IDelegateCache)e;
            Assert.Throws<ImpossibleException>(() => dc.TryGetDelegate<string, Action>("hello", out _));
            Assert.Throws<ImpossibleException>(() => dc.AddDelegate<string, Action>("hello", () => { }));
        }

        [Fact]
        public void NonNull()
        {
            Assert.NotNull(Utils.NonNull("foo"));

            Assert.Throws<ImpossibleException>(() => Utils.NonNull(default(string)));
        }

        [Fact]
        public void CheckImmutableReadInto()
        {
            // arrays
            {
                var arr = ImmutableArray.Create("foo");
                var arrBuilder = ImmutableArray.CreateBuilder<string>();
                Assert.Throws<ArgumentException>(() => Utils.CheckImmutableReadInto<ImmutableArray<string>, string>(arr, "foo"));
                Utils.CheckImmutableReadInto<ImmutableArray<string>.Builder, string>(arrBuilder, "foo");
            }

            // list
            {
                var list = ImmutableList.Create("foo");
                var listBuilder = ImmutableList.CreateBuilder<string>();
                Assert.Throws<ArgumentException>(() => Utils.CheckImmutableReadInto<ImmutableList<string>, string>(list, "foo"));
                Utils.CheckImmutableReadInto<ImmutableList<string>.Builder, string>(listBuilder, "foo");
            }

            // hashset
            {
                var set = ImmutableHashSet.Create("foo");
                var setBuilder = ImmutableHashSet.CreateBuilder<string>();
                Assert.Throws<ArgumentException>(() => Utils.CheckImmutableReadInto<ImmutableHashSet<string>, string>(set, "foo"));
                Utils.CheckImmutableReadInto<ImmutableHashSet<string>.Builder, string>(setBuilder, "foo");
            }

            // sortedSet
            {
                var set = ImmutableSortedSet.Create("foo");
                var setBuilder = ImmutableSortedSet.CreateBuilder<string>();
                Assert.Throws<ArgumentException>(() => Utils.CheckImmutableReadInto<ImmutableSortedSet<string>, string>(set, "foo"));
                Utils.CheckImmutableReadInto<ImmutableSortedSet<string>.Builder, string>(setBuilder, "foo");
            }
        }

        private sealed class _RentMustIncrease : MemoryPool<char>
        {
            private sealed class MemoryOwner : IMemoryOwner<char>
            {
                public Memory<char> Memory { get; }

                public MemoryOwner(int size)
                {
                    Memory = new char[size].AsMemory();
                }

                public void Dispose() { }
            }

            private readonly int DefaultSize;
            public override int MaxBufferSize { get; }

            public _RentMustIncrease(int defaultSize, int maxSize)
            {
                DefaultSize = defaultSize;
                MaxBufferSize = maxSize;
            }

            protected override void Dispose(bool disposing) { }

            public override IMemoryOwner<char> Rent(int minBufferSize = -1)
            {
                int size;

                if (minBufferSize == -1)
                {
                    size = DefaultSize;
                }
                else
                {
                    size = minBufferSize;
                }

                if (size > MaxBufferSize)
                {
                    size = MaxBufferSize;
                }

                return new MemoryOwner(size);
            }
        }

        [Fact]
        public void RentMustIncrease()
        {
            var pool = new _RentMustIncrease(50, 1000);

            Utils.RentMustIncrease(pool, 0, 10);

            Assert.Throws<InvalidOperationException>(() => Utils.RentMustIncrease(pool, 1200, 1000));
        }

        private sealed class _ReflectionHelpers
        {
            public string Foo { set { } }
        }

        [Fact]
        public void ReflectionHelpers()
        {
            var t = typeof(string).GetTypeInfo();
            var o = typeof(object).GetTypeInfo();

            Assert.Throws<InvalidOperationException>(() => t.GetConstructorNonNull(new[] { o }));
            Assert.Throws<InvalidOperationException>(() => t.GetConstructorNonNull(BindingFlags.Public, null, new[] { o }, null));
            Assert.Throws<InvalidOperationException>(() => t.GetElementTypeNonNull());
            Assert.Throws<InvalidOperationException>(() => t.GetFieldNonNull("Foo", BindingFlags.Static));
            Assert.Throws<InvalidOperationException>(() => t.GetMethodNonNull("Foo", BindingFlags.Static));
            Assert.Throws<InvalidOperationException>(() => t.GetMethodNonNull("Foo"));
            Assert.Throws<InvalidOperationException>(() => t.GetMethodNonNull("Foo", BindingFlags.Static, null, Array.Empty<TypeInfo>(), null));
            Assert.Throws<InvalidOperationException>(() => t.GetPropertyNonNull("Foo", BindingFlags.Static));

            var c = typeof(_ReflectionHelpers).GetTypeInfo();
            var p = c.GetPropertyNonNull("Foo", BindingFlags.Public | BindingFlags.Instance);
            Assert.Throws<InvalidOperationException>(() => p.GetGetMethodNonNull());

            // generic tuple types
            Assert.True(typeof(Tuple<,,,,,,,>).GetTypeInfo().IsBigTuple());
            Assert.True(typeof(ValueTuple<,,,,,,,>).GetTypeInfo().IsBigValueTuple());
        }

        [Fact]
        public void CharacterLookupWhitespace()
        {
            foreach (var c in CharacterLookup.WhitespaceCharacters)
            {
                Assert.True(char.IsWhiteSpace(c));
            }

            for (int i = char.MinValue; i <= char.MaxValue; i++)
            {
                var c = (char)i;
                if (!char.IsWhiteSpace(c)) continue;

                var ix = Array.IndexOf(CharacterLookup.WhitespaceCharacters, c);
                Assert.NotEqual(-1, ix);
            }
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("abc", "abc")]
        [InlineData("abc ", "abc ")]
        [InlineData(" ", "")]
        [InlineData(" a", "a")]
        [InlineData(" a ", "a ")]
        public void TrimLeadingWhitespace(string input, string expected)
        {
            var inputMem = input.AsMemory();
            var trimmedMem = Utils.TrimLeadingWhitespace(inputMem);
            var trimmedStr = new string(trimmedMem.Span);

            Assert.Equal(expected, trimmedStr);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("abc", "abc")]
        [InlineData("abc ", "abc")]
        [InlineData(" ", "")]
        [InlineData("a ", "a")]
        [InlineData(" a ", " a")]
        public void TrimTrailingWhitespace(string input, string expected)
        {
            var inputMem = input.AsMemory();
            var trimmedMem = Utils.TrimTrailingWhitespace(inputMem);
            var trimmedStr = new string(trimmedMem.Span);

            Assert.Equal(expected, trimmedStr);
        }

        private class _Encode
        {
            public string Foo { get; set; }
        }

        private sealed class _Encode_MemoryPoolProvider : IMemoryPoolProvider
        {
            public MemoryPool<T> GetMemoryPool<T>()
            {
                if(typeof(T) == typeof(char))
                {
                    return (MemoryPool<T>)(object)new _Encode_MemoryPool();
                }

                return MemoryPool<T>.Shared;
            }
        }

        private sealed class _Encode_MemoryPool : MemoryPool<char>
        {
            private sealed class Owner : IMemoryOwner<char>
            {
                public Memory<char> Memory { get; }

                public Owner(int size)
                {
                    Memory = new char[size].AsMemory();
                }

                public void Dispose() { }
            }

            public _Encode_MemoryPool() { }

            public override int MaxBufferSize => throw new NotImplementedException();

            public override IMemoryOwner<char> Rent(int minBufferSize = -1)
            {
                int size;

                if (minBufferSize == -1)
                {
                    size = 1;
                }
                else
                {
                    size = minBufferSize;
                }

                return new Owner(size);
            }

            protected override void Dispose(bool disposing) { }
        }

        [Theory]
        [InlineData("\"", "\"\"\"\"")]
        [InlineData(" \"\"\"\"\"\"\"\" ", "\" \"\"\"\"\"\"\"\"\"\"\"\"\"\"\"\" \"")]
        public void Encode(string input, string expected)
        {
            // defaults
            {
                var res = Utils.Encode(input, Options.Default, MemoryPool<char>.Shared);

                Assert.Equal(expected, res);
            }

            // exact buffer

            {
                var opts = Options.CreateBuilder(Options.Default).WithMemoryPoolProvider(new _Encode_MemoryPoolProvider()).ToOptions();

                var res = Utils.Encode(input, opts, new _Encode_MemoryPool());

                Assert.Equal(expected, res);
            }
        }

        [Fact]
        public void EncodeErrors()
        {
            // no escapes at all
            var opts1 = Options.CreateBuilder(Options.Default).WithEscapedValueEscapeCharacter(null).WithEscapedValueStartAndEnd(null).ToOptions();

            Assert.Throws<ImpossibleException>(() => Utils.Encode("", opts1, MemoryPool<char>.Shared));

            // no escape char
            var opts2 = Options.CreateBuilder(opts1).WithEscapedValueEscapeCharacter(null).ToOptions();

            Assert.Throws<ImpossibleException>(() => Utils.Encode("", opts2, MemoryPool<char>.Shared));
        }


        [Theory]
        // 0 chars
        [InlineData("", "", true)]

        // 0 vs 1 chars
        [InlineData("", "a", false)]

        // 1 char
        [InlineData("a", "b", false)]

        // 1 vs 2 chars
        [InlineData("a", "ab", false)]

        // 2 chars (1 int)
        [InlineData("aa", "ab", false)]
        [InlineData("aa", "aa", true)]
        [InlineData("aa", "bb", false)]

        // 2 vs 3 chars 
        [InlineData("aa", "aab", false)]

        // 3 chars (1 int, 1 char)
        [InlineData("aaa", "aab", false)]
        [InlineData("aaa", "aaa", true)]
        [InlineData("aaa", "aba", false)]

        // 3 vs 4 chars
        [InlineData("aaa", "aaab", false)]

        // 4 chars (1 long)
        [InlineData("aaaa", "aaab", false)]
        [InlineData("aaaa", "aaaa", true)]
        [InlineData("aaab", "aaaa", false)]

        // 4 vs 5 chars
        [InlineData("aaaa", "aaaab", false)]

        // 5 chars (1 long, 1 char)
        [InlineData("aaaaa", "aaaaa", true)]
        [InlineData("aaaaa", "aaaab", false)]
        [InlineData("aaaab", "aaaaa", false)]

        // 5 vs 6 chars
        [InlineData("aaaaa", "aaaaab", false)]

        // 6 chars (1 long, 1 int)
        [InlineData("aaaaaa", "aaaaaa", true)]
        [InlineData("aaaaaa", "aaaaab", false)]
        [InlineData("aaaaab", "aaaaaa", false)]

        // 6 vs 7 chars
        [InlineData("aaaaaa", "aaaaaab", false)]

        // 7 chars (1 long, 1 int, 1 char)
        [InlineData("aaaaaaa", "aaaaaaa", true)]
        [InlineData("aaaaaaa", "aaaaaab", false)]
        [InlineData("aaaaaab", "aaaaaaa", false)]
        public void AreEqual(string a, string b, bool expected)
        {
            var aMem = a.AsMemory();
            var bMem = b.AsMemory();

            var res = Utils.AreEqual(aMem, bMem);

            Assert.Equal(expected, res);
        }

        private class _FindChar
        {
            public string A { get; set; }
        }

        [Theory]
        // empty
        [InlineData("", 0, 'd', -1)]

        // 1 char
        [InlineData("d", 0, 'd', 0)]
        [InlineData("c", 0, 'd', -1)]

        // 2 chars (1 int)
        [InlineData("de", 0, 'e', 1)]
        [InlineData("dc", 0, 'a', -1)]

        // 3 chars (1 int, 1 char)
        [InlineData("def", 0, 'f', 2)]
        [InlineData("def", 0, 'a', -1)]

        // 4 chars (1 long)
        [InlineData("defg", 0, 'e', 1)]
        [InlineData("defg", 0, 'h', -1)]

        // 5 chars (1 long, 1 char)
        [InlineData("defgh", 0, 'd', 0)]
        [InlineData("defgh", 0, 'i', -1)]

        // 6 chars (1 long, 1 int)
        [InlineData("defghi", 0, 'i', 5)]
        [InlineData("defghi", 0, 'a', -1)]

        // 7 chars (1 long, 1 int, 1 char)
        [InlineData("defghij", 0, 'e', 1)]
        [InlineData("defghij", 0, 'a', -1)]

        [InlineData("abc", 0, 'd', -1)]
        [InlineData("ab\"c", 0, '"', 2)]
        [InlineData("ab\"c", 1, '"', 2)]
        [InlineData("ab\"c", 3, '"', -1)]
        [InlineData("\nabc", 0, '\n', 0)]
        [InlineData("\nabc", 1, '\n', -1)]
        [InlineData("abc\r", 0, '\r', 3)]
        [InlineData("abc\r", 2, '\r', 3)]
        [InlineData("abc\r", 4, '\r', -1)]
        public void FindChar_Span(string chars, int start, char c, int expected)
        {
            var asSpan = chars.AsSpan();
            var ix = Utils.FindChar(asSpan, start, c);

            Assert.Equal(expected, ix);
        }

        [Theory]
        // empty
        [InlineData("", 0, 'd', -1)]

        // 1 char
        [InlineData("d", 0, 'd', 0)]
        [InlineData("c", 0, 'd', -1)]

        // 2 chars (1 int)
        [InlineData("de", 0, 'e', 1)]
        [InlineData("dc", 0, 'a', -1)]

        // 3 chars (1 int, 1 char)
        [InlineData("def", 0, 'f', 2)]
        [InlineData("def", 0, 'a', -1)]

        // 4 chars (1 long)
        [InlineData("defg", 0, 'e', 1)]
        [InlineData("defg", 0, 'h', -1)]

        // 5 chars (1 long, 1 char)
        [InlineData("defgh", 0, 'd', 0)]
        [InlineData("defgh", 0, 'i', -1)]

        // 6 chars (1 long, 1 int)
        [InlineData("defghi", 0, 'i', 5)]
        [InlineData("defghi", 0, 'a', -1)]

        // 7 chars (1 long, 1 int, 1 char)
        [InlineData("defghij", 0, 'e', 1)]
        [InlineData("defghij", 0, 'a', -1)]

        [InlineData("abc", 0, 'd', -1)]
        [InlineData("ab\"c", 0, '"', 2)]
        [InlineData("ab\"c", 1, '"', 2)]
        [InlineData("ab\"c", 3, '"', -1)]
        [InlineData("\nabc", 0, '\n', 0)]
        [InlineData("\nabc", 1, '\n', -1)]
        [InlineData("abc\r", 0, '\r', 3)]
        [InlineData("abc\r", 2, '\r', 3)]
        [InlineData("abc\r", 4, '\r', -1)]
        public void FindChar_Memory(string chars, int start, char c, int expected)
        {
            var asSpan = chars.AsMemory();
            var ix = Utils.FindChar(asSpan, start, c);

            Assert.Equal(expected, ix);
        }

        [Theory]
        // single-segment
        [InlineData(new[] { "abc" }, 1, 'c', 2)]

        // multi-segment
        [InlineData(new[] { "ab\n", "cdef" }, 0, '\n', 2)]
        [InlineData(new[] { "ab\n", "cdef" }, 1, '\n', 2)]
        [InlineData(new[] { "ab\n", "cdef" }, 4, '\n', -1)]
        [InlineData(new[] { "ab", "cdef" }, 0, 'h', -1)]
        [InlineData(new[] { "ab", "cdef" }, 1, 'h', -1)]
        [InlineData(new[] { "ab", "cdef" }, 6, 'h', -1)]
        [InlineData(new[] { "ab", "\"def" }, 0, '"', 2)]
        [InlineData(new[] { "ab", "\"def" }, 2, '"', 2)]
        [InlineData(new[] { "ab", "\"def" }, 4, '"', -1)]
        [InlineData(new[] { "\rab", "def" }, 0, '\r', 0)]
        [InlineData(new[] { "\rab", "def" }, 3, '\r', -1)]
        [InlineData(new[] { "ab", "def\n" }, 0, '\n', 5)]
        [InlineData(new[] { "ab", "def\n" }, 4, '\n', 5)]
        [InlineData(new[] { "ab", "def\n" }, 6, '\n', -1)]
        public void FindChar_Sequence(string[] seqs, int start, char c, int expected)
        {
            var head = new _FindNeedsEncode_Sequence_Segment(seqs[0], 0);
            var tail = head;
            for (var i = 1; i < seqs.Length; i++)
            {
                var next = new _FindNeedsEncode_Sequence_Segment(seqs[i], (int)(tail.RunningIndex + tail.Memory.Length));
                tail.SetNext(next);
                tail = next;
            }

            var seq = new ReadOnlySequence<char>(head, 0, tail, seqs[seqs.Length - 1].Length);

            var ix = Utils.FindChar(seq, start, c);

            Assert.Equal(expected, ix);
        }

        private class _FindNeedsEncode
        {
            public string A { get; set; }
        }

        [Theory]
        [InlineData("abc", 0, -1)]
        [InlineData("ab\"c", 0, 2)]
        [InlineData("ab\"c", 1, 2)]
        [InlineData("ab\"c", 3, -1)]
        [InlineData("\nabc", 0, 0)]
        [InlineData("\nabc", 1, -1)]
        [InlineData("abc\r", 0, 3)]
        [InlineData("abc\r", 2, 3)]
        [InlineData("abc\r", 4, -1)]
        public void FindNeedsEncode_Span(string chars, int start, int expected)
        {
            var config = (ConcreteBoundConfiguration<_FindNeedsEncode>)Configuration.For<_FindNeedsEncode>();
            var asSpan = chars.AsSpan();
            var ix = Utils.FindNeedsEncode(asSpan, start, config);

            Assert.Equal(expected, ix);
        }

        [Theory]
        [InlineData("abc", 0, -1)]
        [InlineData("ab\"c", 0, 2)]
        [InlineData("ab\"c", 1, 2)]
        [InlineData("ab\"c", 3, -1)]
        [InlineData("\nabc", 0, 0)]
        [InlineData("\nabc", 1, -1)]
        [InlineData("abc\r", 0, 3)]
        [InlineData("abc\r", 2, 3)]
        [InlineData("abc\r", 4, -1)]
        public void FindNeedsEncode_Memory(string chars, int start, int expected)
        {
            var config = (ConcreteBoundConfiguration<_FindNeedsEncode>)Configuration.For<_FindNeedsEncode>();
            var asMemory = chars.AsMemory();
            var ix = Utils.FindNeedsEncode(asMemory, start, config);

            Assert.Equal(expected, ix);
        }

        private sealed class _FindNeedsEncode_Sequence_Segment : ReadOnlySequenceSegment<char>
        {
            private readonly string Inner;

            public _FindNeedsEncode_Sequence_Segment(string str, int startsAt)
            {
                Inner = str;

                Memory = str.AsMemory();
                RunningIndex = startsAt;
            }

            public void SetNext(ReadOnlySequenceSegment<char> next)
            {
                Next = next;
            }
        }

        [Theory]
        // single-segment
        [InlineData(new[] { "ab\ncdef" }, 0, 2)]

        // multi-segment
        [InlineData(new[] { "ab\n", "cdef" }, 0, 2)]
        [InlineData(new[] { "ab\n", "cdef" }, 1, 2)]
        [InlineData(new[] { "ab\n", "cdef" }, 4, -1)]
        [InlineData(new[] { "ab", "cdef" }, 0, -1)]
        [InlineData(new[] { "ab", "cdef" }, 1, -1)]
        [InlineData(new[] { "ab", "cdef" }, 6, -1)]
        [InlineData(new[] { "ab", "\"def" }, 0, 2)]
        [InlineData(new[] { "ab", "\"def" }, 2, 2)]
        [InlineData(new[] { "ab", "\"def" }, 4, -1)]
        [InlineData(new[] { "\rab", "def" }, 0, 0)]
        [InlineData(new[] { "\rab", "def" }, 3, -1)]
        [InlineData(new[] { "ab", "def\n" }, 0, 5)]
        [InlineData(new[] { "ab", "def\n" }, 4, 5)]
        [InlineData(new[] { "ab", "def\n" }, 6, -1)]
        public void FindNeedsEncode_Sequence(string[] seqs, int start, int expected)
        {
            var head = new _FindNeedsEncode_Sequence_Segment(seqs[0], 0);
            var tail = head;
            for (var i = 1; i < seqs.Length; i++)
            {
                var next = new _FindNeedsEncode_Sequence_Segment(seqs[i], (int)(tail.RunningIndex + tail.Memory.Length));
                tail.SetNext(next);
                tail = next;
            }

            var seq = new ReadOnlySequence<char>(head, 0, tail, seqs[seqs.Length - 1].Length);

            var config = (ConcreteBoundConfiguration<_FindNeedsEncode>)Configuration.For<_FindNeedsEncode>();
            var ix = Utils.FindNeedsEncode(seq, start, config);

            Assert.Equal(expected, ix);
        }

        [Theory]

        [InlineData("hello", "world", -1)]

        [InlineData(",ello", ",", 0)]
        [InlineData("h,llo", ",", 1)]
        [InlineData("he,lo", ",", 2)]
        [InlineData("hel,o", ",", 3)]
        [InlineData("hell,", ",", 4)]

        [InlineData("hello", "#*", -1)]
        [InlineData("#ello", "#*", -1)]
        [InlineData("*ello", "#*", -1)]
        [InlineData("#*llo", "#*", 0)]
        [InlineData("h#*lo", "#*", 1)]
        [InlineData("he#*o", "#*", 2)]
        [InlineData("hel#*", "#*", 3)]
        [InlineData("hell#", "#*", -1)]

        [InlineData("##*lo", "#*", 1)]
        [InlineData("#e#*o", "#*", 2)]
        [InlineData("#el#*", "#*", 3)]
        [InlineData("#ell#", "#*", -1)]
        public void Find(string txt, string needle, int expected)
        {
            var ix = Utils.Find(txt, 0, needle);
            Assert.Equal(expected, ix);
        }
    }
}