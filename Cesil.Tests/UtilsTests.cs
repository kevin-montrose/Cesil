using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

            foreach (var f in files)
            {
                foreach (var m in members)
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
        public void NonNullValue()
        {
            Assert.Equal(1, Utils.NonNullValue((int?)1));

            Assert.Throws<ImpossibleException>(() => Utils.NonNullValue(default(int?)));
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
            Assert.Throws<InvalidOperationException>(() => t.GetMethodNonNull("Foo", BindingFlags.Static, null, new[] { typeof(string).GetTypeInfo() }, null));
            Assert.Throws<InvalidOperationException>(() => t.GetPropertyNonNull("Foo", BindingFlags.Static));

            var c = typeof(_ReflectionHelpers).GetTypeInfo();
            var p = c.GetPropertyNonNull("Foo", BindingFlags.Public | BindingFlags.Instance);
            Assert.Throws<InvalidOperationException>(() => p.GetGetMethodNonNull());

            // generic tuple types
            Assert.True(typeof(Tuple<,,,,,,,>).GetTypeInfo().IsBigTuple());
            Assert.True(typeof(ValueTuple<,,,,,,,>).GetTypeInfo().IsBigValueTuple());
        }

        [Fact]
        public void WeirdReflectionCases()
        {
            // fields with no declaring type
            {
                var name = $"{nameof(Cesil)}.{nameof(UtilsTests)}.{nameof(WeirdReflectionCases)}.Fields";
                var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
                var modBuilder = asmBuilder.DefineDynamicModule("Module");
                var fieldBuilder = modBuilder.DefineInitializedData("WeirdField", new byte[4] { 1, 2, 3, 4 }, FieldAttributes.Static | FieldAttributes.Public);
                modBuilder.CreateGlobalFunctions();

                var field = modBuilder.GetField(fieldBuilder.Name, BindingFlags.Static | BindingFlags.Public);

                var exc = Assert.Throws<InvalidOperationException>(() => field.DeclaringTypeNonNull());
                Assert.StartsWith("Could not find declaring type for ", exc.Message);
            }

            // method with no declaring type
            {
                var name = $"{nameof(Cesil)}.{nameof(UtilsTests)}.{nameof(WeirdReflectionCases)}.Methods";
                var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
                var modBuilder = asmBuilder.DefineDynamicModule("Module");
                var mtdBuilder = modBuilder.DefineGlobalMethod("WeirdMethod", MethodAttributes.Static | MethodAttributes.Public, null, null);
                var ilGen = mtdBuilder.GetILGenerator();
                ilGen.Emit(OpCodes.Ret);

                modBuilder.CreateGlobalFunctions();

                var mtd = modBuilder.GetMethod(mtdBuilder.Name);

                var exc = Assert.Throws<InvalidOperationException>(() => mtd.DeclaringTypeNonNull());
                Assert.StartsWith("Could not find declaring type for ", exc.Message);
            }

            // constructor with no declaring type
            {
                var name = $"{nameof(Cesil)}.{nameof(UtilsTests)}.{nameof(WeirdReflectionCases)}.Constructors";
                var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
                var modBuilder = asmBuilder.DefineDynamicModule("Module");

                var globalTypeBuilder = GetModuleTypeBuilder(modBuilder);

                // now we can make a static constructor on this stupid fake type
                var consBuilder = globalTypeBuilder.DefineConstructor(MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.Standard, null);
                var ilGenerator = consBuilder.GetILGenerator();
                ilGenerator.Emit(OpCodes.Ret);

                // actually emit things...
                modBuilder.CreateGlobalFunctions();

                var cons = Assert.IsAssignableFrom<ConstructorInfo>(consBuilder);

                var exc = Assert.Throws<InvalidOperationException>(() => cons.DeclaringTypeNonNull());
                Assert.StartsWith("Could not find declaring type for ", exc.Message);
            }

            // TypeBuilder makes a type but returns null
            {
                var name = $"{nameof(Cesil)}.{nameof(UtilsTests)}.{nameof(WeirdReflectionCases)}.TypeBuilder";
                var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
                var modBuilder = asmBuilder.DefineDynamicModule("Module");

                var globalTypeBuilder = GetModuleTypeBuilder(modBuilder);

                var exc = Assert.Throws<InvalidOperationException>(() => globalTypeBuilder.CreateTypeNonNull());
                Assert.Equal("Created type was null", exc.Message);
            }

            // get the TypeBuilder for the hidden <Module> type
            static TypeBuilder GetModuleTypeBuilder(ModuleBuilder modBuilder)
            {
                // blruuuuuugh, getting this out properly is a huge pain... so just reflect it out
                var moduleDataField = modBuilder.GetType().GetField("_moduleData", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(moduleDataField);

                var moduleData = moduleDataField.GetValue(modBuilder);

                var globalTypeBuilderField = moduleData.GetType().GetField("_globalTypeBuilder", BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(globalTypeBuilderField);

                var globalTypeBuilderObj = globalTypeBuilderField.GetValue(moduleData);

                var globalTypeBuilder = Assert.IsAssignableFrom<TypeBuilder>(globalTypeBuilderObj);

                return globalTypeBuilder;
            }
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
                if (typeof(T) == typeof(char))
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

        private class _DeterminingNullability
        {
            // fields, with annotations
#nullable enable
            public string NonNullField = "";
            public string? NullField = null;

            public List<string> GenericNonNullField = new List<string>();
            public List<string>? GenericNullField = null;

            public ImmutableArray<string> GenericValueNonNullField = ImmutableArray<string>.Empty;
            public ImmutableArray<string>? GenericValueNullField = null;

            public int NonNullValueField = 0;
            public int? NullValueField = null;
#nullable disable

            // fields, without annotations
            public string OblivousField = null;
            public List<string> ObliviousGenericField = null;
            public ImmutableArray<string> ObliviousGenericValueField = ImmutableArray<string>.Empty;
            public ImmutableArray<string>? ObliviousGenericNullableValueField = null;
            public int ObliviousValueField = 0;
            public int? ObliviousNullValueField = null;

            // properties, with annotations
#nullable enable
            public string NonNullProp { get; } = "";
            public string? NullProp { get; } = null;

            public List<string> GenericNonNullProp { get; } = new List<string>();
            public List<string>? GenericNullProp { get; } = null;

            public ImmutableArray<string> GenericValueNonNullProp { get; } = ImmutableArray<string>.Empty;
            public ImmutableArray<string>? GenericValueNullProp { get; } = null;

            public int NonNullValueProp { get; } = 0;
            public int? NullValueProp { get; } = null;
#nullable disable

            // properties, without annotations
            public string OblivousProp { get; } = null;
            public List<string> ObliviousGenericProp { get; } = null;
            public ImmutableArray<string> ObliviousGenericValueProp { get; } = ImmutableArray<string>.Empty;
            public ImmutableArray<string>? ObliviousGenericNullableValueProp { get; } = null;
            public int ObliviousValueProp { get; } = 0;
            public int? ObliviousNullValueProp { get; } = null;
        }

#nullable enable
        private static void _DetermineNullability_Enabled_Method(
            string refNonNull,
            string? refNull,
            List<string> genRefNonNull,
            List<string>? genRefNull,
            ImmutableArray<string> genStructNonNull,
            ImmutableArray<string>? genStructNull,
            int valNonNull,
            int? valNull,

            ref string byRef_refNonNull,
            ref string? byRef_refNull,
            ref List<string> byRef_genRefNonNull,
            ref List<string>? byRef_genRefNull,
            ref ImmutableArray<string> byRef_genStructNonNull,
            ref ImmutableArray<string>? byRef_genStructNull,
            ref int byRef_valNonNull,
            ref int? byRef_valNull
        )
        { }
#nullable disable

        private static void _DetermineNullability_Oblivious_Method(
            string refNull,
            List<string> genRefNull,
            ImmutableArray<string> genStructNonNull,
            ImmutableArray<string>? genStructNull,
            int valNonNull,
            int? valNull,

            ref string byRef_refNull,
            ref List<string> byRef_genRefNull,
            ref ImmutableArray<string> byRef_genStructNonNull,
            ref ImmutableArray<string>? byRef_genStructNull,
            ref int byRef_valNonNull,
            ref int? byRef_valNull
        )
        { }

        [Fact]
        public void DeterminingNullability()
        {
            // field tests

            Assert.Equal(NullHandling.AllowNull, default(FieldInfo).DetermineNullability());

            Assert.Equal(NullHandling.ForbidNull, Field(nameof(_DeterminingNullability.NonNullField)));
            Assert.Equal(NullHandling.AllowNull, Field(nameof(_DeterminingNullability.NullField)));

            Assert.Equal(NullHandling.ForbidNull, Field(nameof(_DeterminingNullability.GenericNonNullField)));
            Assert.Equal(NullHandling.AllowNull, Field(nameof(_DeterminingNullability.GenericNullField)));

            Assert.Equal(NullHandling.CannotBeNull, Field(nameof(_DeterminingNullability.GenericValueNonNullField)));
            Assert.Equal(NullHandling.AllowNull, Field(nameof(_DeterminingNullability.GenericValueNullField)));

            Assert.Equal(NullHandling.CannotBeNull, Field(nameof(_DeterminingNullability.NonNullValueField)));
            Assert.Equal(NullHandling.AllowNull, Field(nameof(_DeterminingNullability.NullValueField)));

            Assert.Equal(NullHandling.AllowNull, Field(nameof(_DeterminingNullability.OblivousField)));
            Assert.Equal(NullHandling.AllowNull, Field(nameof(_DeterminingNullability.ObliviousGenericField)));
            Assert.Equal(NullHandling.CannotBeNull, Field(nameof(_DeterminingNullability.ObliviousGenericValueField)));
            Assert.Equal(NullHandling.AllowNull, Field(nameof(_DeterminingNullability.ObliviousGenericNullableValueField)));
            Assert.Equal(NullHandling.CannotBeNull, Field(nameof(_DeterminingNullability.ObliviousValueField)));
            Assert.Equal(NullHandling.AllowNull, Field(nameof(_DeterminingNullability.ObliviousNullValueField)));

            // prop tests

            Assert.Equal(NullHandling.AllowNull, default(PropertyInfo).DetermineNullability());

            Assert.Equal(NullHandling.ForbidNull, Property(nameof(_DeterminingNullability.NonNullProp)));
            Assert.Equal(NullHandling.AllowNull, Property(nameof(_DeterminingNullability.NullProp)));

            Assert.Equal(NullHandling.ForbidNull, Property(nameof(_DeterminingNullability.GenericNonNullProp)));
            Assert.Equal(NullHandling.AllowNull, Property(nameof(_DeterminingNullability.GenericNullProp)));

            Assert.Equal(NullHandling.CannotBeNull, Property(nameof(_DeterminingNullability.GenericValueNonNullProp)));
            Assert.Equal(NullHandling.AllowNull, Property(nameof(_DeterminingNullability.GenericValueNullProp)));

            Assert.Equal(NullHandling.CannotBeNull, Property(nameof(_DeterminingNullability.NonNullValueProp)));
            Assert.Equal(NullHandling.AllowNull, Property(nameof(_DeterminingNullability.NullValueProp)));

            Assert.Equal(NullHandling.AllowNull, Property(nameof(_DeterminingNullability.OblivousProp)));
            Assert.Equal(NullHandling.AllowNull, Property(nameof(_DeterminingNullability.ObliviousGenericProp)));
            Assert.Equal(NullHandling.CannotBeNull, Property(nameof(_DeterminingNullability.ObliviousGenericValueProp)));
            Assert.Equal(NullHandling.AllowNull, Property(nameof(_DeterminingNullability.ObliviousGenericNullableValueProp)));
            Assert.Equal(NullHandling.CannotBeNull, Property(nameof(_DeterminingNullability.ObliviousValueProp)));
            Assert.Equal(NullHandling.AllowNull, Property(nameof(_DeterminingNullability.ObliviousNullValueProp)));

            // argument tests (enabled)

            Assert.Equal(NullHandling.AllowNull, default(ParameterInfo).DetermineNullability());

            Assert.Equal(NullHandling.ForbidNull, Argument(nameof(_DetermineNullability_Enabled_Method), "refNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Enabled_Method), "refNull"));

            Assert.Equal(NullHandling.ForbidNull, Argument(nameof(_DetermineNullability_Enabled_Method), "genRefNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Enabled_Method), "genRefNull"));

            Assert.Equal(NullHandling.CannotBeNull, Argument(nameof(_DetermineNullability_Enabled_Method), "genStructNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Enabled_Method), "genStructNull"));

            Assert.Equal(NullHandling.CannotBeNull, Argument(nameof(_DetermineNullability_Enabled_Method), "valNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Enabled_Method), "valNull"));

            Assert.Equal(NullHandling.ForbidNull, Argument(nameof(_DetermineNullability_Enabled_Method), "byRef_refNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Enabled_Method), "byRef_refNull"));

            Assert.Equal(NullHandling.ForbidNull, Argument(nameof(_DetermineNullability_Enabled_Method), "byRef_genRefNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Enabled_Method), "byRef_genRefNull"));

            Assert.Equal(NullHandling.CannotBeNull, Argument(nameof(_DetermineNullability_Enabled_Method), "byRef_genStructNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Enabled_Method), "byRef_genStructNull"));

            Assert.Equal(NullHandling.CannotBeNull, Argument(nameof(_DetermineNullability_Enabled_Method), "byRef_valNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Enabled_Method), "byRef_valNull"));

            // argument tests (disabled)

            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "refNull"));

            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "genRefNull"));

            Assert.Equal(NullHandling.CannotBeNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "genStructNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "genStructNull"));

            Assert.Equal(NullHandling.CannotBeNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "valNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "valNull"));

            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "byRef_refNull"));

            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "byRef_genRefNull"));

            Assert.Equal(NullHandling.CannotBeNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "byRef_genStructNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "byRef_genStructNull"));

            Assert.Equal(NullHandling.CannotBeNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "byRef_valNonNull"));
            Assert.Equal(NullHandling.AllowNull, Argument(nameof(_DetermineNullability_Oblivious_Method), "byRef_valNull"));

            static NullHandling Field(string name)
            {
                var field = typeof(_DeterminingNullability).GetField(name);

                Assert.NotNull(field);

                return field.DetermineNullability();
            }

            static NullHandling Property(string name)
            {
                var prop = typeof(_DeterminingNullability).GetProperty(name);

                Assert.NotNull(prop);

                return prop.DetermineNullability();
            }

            static NullHandling Argument(string mtdName, string argName)
            {
                var mtd = typeof(UtilsTests).GetMethod(mtdName, BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(mtd);

                var arg = mtd.GetParameters().Single(p => p.Name == argName);

                return arg.DetermineNullability();
            }
        }

        [Theory]
        [InlineData(1, "foobar", "ob", 2)]
        [InlineData(1, "foobar", "are", -1)]
        [InlineData(0, "foofozz", "oz", 4)]
        public void FindNextIx(int startAt, string haystack, string needle, int expected)
        {
            var haystackSpan = haystack.AsSpan();
            var needleSpan = needle.AsSpan();

            var res = Utils.FindNextIx(startAt, haystackSpan, needleSpan);
            Assert.Equal(expected, res);
        }

        [Fact]
        public void Find_Sequence()
        {
            // empty
            {
                var res = Utils.Find(ReadOnlySequence<char>.Empty, "foobar");
                Assert.Equal(-1, res);
            }

            // single segment
            {
                var mem = "foobar".AsMemory();
                var seq = new ReadOnlySequence<char>(mem);

                Assert.True(seq.IsSingleSegment);

                var res = Utils.Find(seq, "ba");
                Assert.Equal(3, res);
            }

            // multi segment
            {
                var head = new _FindNeedsEncode_Sequence_Segment("foo", 0);
                var tail = head;
                var next1 = new _FindNeedsEncode_Sequence_Segment("bar", (int)(tail.RunningIndex + tail.Memory.Length));
                tail.SetNext(next1);
                tail = next1;
                var next2 = new _FindNeedsEncode_Sequence_Segment("fizz", (int)(tail.RunningIndex + tail.Memory.Length));
                tail.SetNext(next2);
                tail = next2;

                var seq = new ReadOnlySequence<char>(head, 0, tail, "fizz".Length);

                Assert.False(seq.IsSingleSegment);

                // found, within single span
                var res1 = Utils.Find(seq, "ba");
                Assert.Equal(3, res1);

                // found, straddles span
                var res2 = Utils.Find(seq, "ob");
                Assert.Equal(2, res2);

                // not found (partial match)
                var res3 = Utils.Find(seq, "re");
                Assert.Equal(-1, res3);

                // not found (no match)
                var res4 = Utils.Find(seq, "xz");
                Assert.Equal(-1, res4);

                // found, completely covers span
                var res5 = Utils.Find(seq, "bar");
                Assert.Equal(3, res5);

                // found, stradles multiple spans
                var res6 = Utils.Find(seq, "obarf");
                Assert.Equal(2, res6);

                // found, stradles multiple spans (starts in non-first span)
                var res7 = Utils.Find(seq, "arfi");
                Assert.Equal(4, res7);

                // not found, partial stradles multiple spans
                var res8 = Utils.Find(seq, "arfix");
                Assert.Equal(-1, res8);

                // not found, partial stradles multiple spans and ends sequence
                var res9 = Utils.Find(seq, "arfizzy");
                Assert.Equal(-1, res9);
            }
        }

        [Theory]
        // reference types, anything is legal!
        [InlineData(typeof(object), (byte)NullHandling.ForbidNull, false)]
        [InlineData(typeof(object), (byte)NullHandling.AllowNull, false)]
        // value types, can't be null
        [InlineData(typeof(int), (byte)NullHandling.AllowNull, true)]
        [InlineData(typeof(int), (byte)NullHandling.ForbidNull, false)]
        // nullable value types can be anything
        [InlineData(typeof(int?), (byte)NullHandling.AllowNull, false)]
        [InlineData(typeof(int?), (byte)NullHandling.ForbidNull, false)]
        public void ValidateNullHandling(Type forType, byte updatedByte, bool throws)
        {
            var updated = (NullHandling)updatedByte;

            Action toRun = () => Utils.ValidateNullHandling(forType.GetTypeInfo(), updated);

            if (throws)
            {
                Assert.Throws<InvalidOperationException>(toRun);
            }
            else
            {
                toRun();
            }
        }

        // what happens when chaining things _as parameters_ (stuff passed to setters, formatters, and so on)
        [Theory]
        [InlineData((byte)NullHandling.AllowNull, (byte)NullHandling.AllowNull, (byte)NullHandling.AllowNull)]      // if they all take null, null is OK
        [InlineData((byte)NullHandling.AllowNull, (byte)NullHandling.ForbidNull, (byte)NullHandling.ForbidNull)]    // if either forbid null, null isn't OK
        [InlineData((byte)NullHandling.ForbidNull, (byte)NullHandling.AllowNull, (byte)NullHandling.ForbidNull)]
        // CannotBeNull isn't possible
        public void CommonInputNullHandling(byte firstByte, byte secondByte, byte expectedByte)
        {
            var first = (NullHandling)firstByte;
            var second = (NullHandling)secondByte;
            var expected = (NullHandling)expectedByte;

            var res = Utils.CommonInputNullHandling(first, second);
            Assert.Equal(expected, res);
        }

        [Theory]
        [InlineData(typeof(string), false, true)]
        [InlineData(typeof(int), false, false)]
        [InlineData(typeof(int?), false, true)]
        [InlineData(typeof(string), true, true)]
        [InlineData(typeof(int), true, false)]
        [InlineData(typeof(int?), true, true)]
        public void AllowsNullLikeValue(Type forType, bool byRef, bool expected)
        {
            var toCheck = forType;
            if (byRef)
            {
                toCheck = toCheck.MakeByRefType();
            }

            var res = toCheck.GetTypeInfo().AllowsNullLikeValue();
            Assert.Equal(expected, res);
        }

        [Theory]
        [InlineData(typeof(int), false)]
        [InlineData(typeof(ReadHeader), false)]
        [InlineData(typeof(WhitespaceTreatments), true)]
        public void IsFlagsEnum(Type forType, bool expected)
        {
            var res = forType.GetTypeInfo().IsFlagsEnum();
            Assert.Equal(expected, res);
        }

        private enum _EnumToULong_Byte : byte { A = 0, B = byte.MaxValue }
        private enum _EnumToULong_SByte : sbyte { A = 0, B = sbyte.MinValue }
        private enum _EnumToULong_Short : short { A = 0, B = short.MinValue }
        private enum _EnumToULong_UShort : ushort { A = 0, B = ushort.MaxValue }
        private enum _EnumToULong_Int : int { A = 0, B = int.MinValue }
        private enum _EnumToULong_UInt : uint { A = 0, B = uint.MaxValue }
        private enum _EnumToULong_Long : long { A = 0, B = long.MinValue }
        private enum _EnumToULong_ULong : ulong { A = 0, B = ulong.MaxValue }

        [Fact]
        public void EnumToULong()
        {
            // byte
            Check(_EnumToULong_Byte.A, (byte)_EnumToULong_Byte.A);
            Check(_EnumToULong_Byte.B, (byte)_EnumToULong_Byte.B);
            Check((_EnumToULong_Byte)100, 100);

            // sbyte
            Check(_EnumToULong_SByte.A, (ulong)(sbyte)_EnumToULong_SByte.A);
            Check(_EnumToULong_SByte.B, unchecked((ulong)(sbyte)_EnumToULong_SByte.B));
            Check((_EnumToULong_SByte)(-100), unchecked((ulong)-100));

            // short
            Check(_EnumToULong_Short.A, (ulong)(short)_EnumToULong_Short.A);
            Check(_EnumToULong_Short.B, unchecked((ulong)(short)_EnumToULong_Short.B));
            Check((_EnumToULong_Short)100, (ulong)(short)100);

            // ushort
            Check(_EnumToULong_UShort.A, (ushort)_EnumToULong_UShort.A);
            Check(_EnumToULong_UShort.B, (ushort)_EnumToULong_UShort.B);
            Check((_EnumToULong_UShort)100, 100);

            // int
            Check(_EnumToULong_Int.A, (int)_EnumToULong_Int.A);
            Check(_EnumToULong_Int.B, unchecked((ulong)(int)_EnumToULong_Int.B));
            Check((_EnumToULong_Int)100, 100);

            // uint
            Check(_EnumToULong_UInt.A, (uint)_EnumToULong_UInt.A);
            Check(_EnumToULong_UInt.B, (uint)_EnumToULong_UInt.B);
            Check((_EnumToULong_UInt)100, 100);

            // long
            Check(_EnumToULong_Long.A, (long)_EnumToULong_Long.A);
            Check(_EnumToULong_Long.B, unchecked((ulong)(long)_EnumToULong_Long.B));
            Check((_EnumToULong_Long)(-100), unchecked((ulong)-100));

            // ulong
            Check(_EnumToULong_ULong.A, (ulong)_EnumToULong_ULong.A);
            Check(_EnumToULong_ULong.B, (ulong)_EnumToULong_ULong.B);
            Check((_EnumToULong_ULong)100, 100);

            // test that Utils produces the right value
            static void Check<T>(T val, ulong expected)
                where T : struct, Enum
            {
                var res = Utils.EnumToULong<T>(val);
                Assert.Equal(expected, res);
            }
        }

        [Flags]
        private enum _TryFormatFlagsEnum : ulong { A = 1, B = 2, C = 4, D = 8, E = 1UL << 63 };
        [Flags]
        private enum _TryFormatFlagsEnum_WithZero : ulong { Z = 0, A = 1, B = 2, C = 4, D = 8, E = 1UL << 63 };

        [Fact]
        public void TryFormatFlagsEnum()
        {
            var namesTryFormatFlagsEnum = Enum.GetNames(typeof(_TryFormatFlagsEnum));
            var valuesTryFormatFlagsEnum = Enum.GetValues(typeof(_TryFormatFlagsEnum)).Cast<ulong>().ToArray();

            var namesTryFormatFlagsEnumWithZero = Enum.GetNames(typeof(_TryFormatFlagsEnum_WithZero));
            var valuesTryFormatFlagsEnumWithZero = Enum.GetValues(typeof(_TryFormatFlagsEnum_WithZero)).Cast<ulong>().ToArray();

            // no zero
            Test(_TryFormatFlagsEnum.A, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, true);
            Test(_TryFormatFlagsEnum.A | _TryFormatFlagsEnum.B, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, true);
            Test(_TryFormatFlagsEnum.B | _TryFormatFlagsEnum.E, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, true);
            Test(_TryFormatFlagsEnum.A | _TryFormatFlagsEnum.B | _TryFormatFlagsEnum.C | _TryFormatFlagsEnum.D | _TryFormatFlagsEnum.E, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, true);
            Test((_TryFormatFlagsEnum)17, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, false);
            Test((_TryFormatFlagsEnum)0, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, false);

            // with zero
            Test(_TryFormatFlagsEnum_WithZero.A, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);
            Test(_TryFormatFlagsEnum_WithZero.A | _TryFormatFlagsEnum_WithZero.B, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);
            Test(_TryFormatFlagsEnum_WithZero.B | _TryFormatFlagsEnum_WithZero.E, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);
            Test(_TryFormatFlagsEnum_WithZero.A | _TryFormatFlagsEnum_WithZero.B | _TryFormatFlagsEnum_WithZero.C | _TryFormatFlagsEnum_WithZero.D | _TryFormatFlagsEnum_WithZero.E, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);
            Test((_TryFormatFlagsEnum_WithZero)17, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, false);
            Test(_TryFormatFlagsEnum_WithZero.Z, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);

            // DRY up the test a bit
            static void Test<T>(T enumValue, string[] names, ulong[] values, bool expectedBool)
                where T : struct, Enum
            {
                var expectedText = enumValue.ToString();
                Span<char> span = default;

tryAgain:
                var res = DefaultTypeFormatters.DefaultEnumTypeFormatter<T>.FormatFlagsEnumImpl(enumValue, names, values, span);
                if (res == 0)
                {
                    // malformed!
                    Assert.False(expectedBool);
                }
                else if (res > 0)
                {
                    // it fit!

                    var actualText = new string(span);
                    Assert.Equal(expectedText, actualText);
                }
                else
                {
                    // didn't fit, did it ask for the right size?
                    var neededLength = -res;
                    Assert.Equal(expectedText.Length, neededLength);

                    // make the span slightly bigger!
                    span = new char[span.Length + 1].AsSpan();
                    goto tryAgain;
                }
            }
        }

        [Fact]
        public void ULongToEnum()
        {
            // byte
            Check(_EnumToULong_Byte.A, (byte)_EnumToULong_Byte.A);
            Check(_EnumToULong_Byte.B, (byte)_EnumToULong_Byte.B);
            Check((_EnumToULong_Byte)100, 100);

            // sbyte
            Check(_EnumToULong_SByte.A, (ulong)(sbyte)_EnumToULong_SByte.A);
            Check(_EnumToULong_SByte.B, unchecked((ulong)(sbyte)_EnumToULong_SByte.B));
            Check((_EnumToULong_SByte)(-100), unchecked((ulong)-100));

            // short
            Check(_EnumToULong_Short.A, (ulong)(short)_EnumToULong_Short.A);
            Check(_EnumToULong_Short.B, unchecked((ulong)(short)_EnumToULong_Short.B));
            Check((_EnumToULong_Short)100, (ulong)(short)100);

            // ushort
            Check(_EnumToULong_UShort.A, (ushort)_EnumToULong_UShort.A);
            Check(_EnumToULong_UShort.B, (ushort)_EnumToULong_UShort.B);
            Check((_EnumToULong_UShort)100, 100);

            // int
            Check(_EnumToULong_Int.A, (int)_EnumToULong_Int.A);
            Check(_EnumToULong_Int.B, unchecked((ulong)(int)_EnumToULong_Int.B));
            Check((_EnumToULong_Int)100, 100);

            // uint
            Check(_EnumToULong_UInt.A, (uint)_EnumToULong_UInt.A);
            Check(_EnumToULong_UInt.B, (uint)_EnumToULong_UInt.B);
            Check((_EnumToULong_UInt)100, 100);

            // long
            Check(_EnumToULong_Long.A, (long)_EnumToULong_Long.A);
            Check(_EnumToULong_Long.B, unchecked((ulong)(long)_EnumToULong_Long.B));
            Check((_EnumToULong_Long)(-100), unchecked((ulong)-100));

            // ulong
            Check(_EnumToULong_ULong.A, (ulong)_EnumToULong_ULong.A);
            Check(_EnumToULong_ULong.B, (ulong)_EnumToULong_ULong.B);
            Check((_EnumToULong_ULong)100, 100);

            // test that Utils produces the right value
            static void Check<T>(T expected, ulong rawValue)
                where T : struct, Enum
            {
                var res = Utils.ULongToEnum<T>(rawValue);
                Assert.Equal(expected, res);
            }
        }

        [Fact]
        public void TryParseFlagsEnum()
        {
            var namesTryFormatFlagsEnum = Enum.GetNames(typeof(_TryFormatFlagsEnum));
            var valuesTryFormatFlagsEnum = Enum.GetValues(typeof(_TryFormatFlagsEnum)).Cast<ulong>().ToArray();

            var namesTryFormatFlagsEnumWithZero = Enum.GetNames(typeof(_TryFormatFlagsEnum_WithZero));
            var valuesTryFormatFlagsEnumWithZero = Enum.GetValues(typeof(_TryFormatFlagsEnum_WithZero)).Cast<ulong>().ToArray();

            // no zero
            Test(_TryFormatFlagsEnum.A, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, true);
            Test(_TryFormatFlagsEnum.A | _TryFormatFlagsEnum.B, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, true);
            Test(_TryFormatFlagsEnum.B | _TryFormatFlagsEnum.E, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, true);
            Test(_TryFormatFlagsEnum.A | _TryFormatFlagsEnum.B | _TryFormatFlagsEnum.C | _TryFormatFlagsEnum.D | _TryFormatFlagsEnum.E, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, true);
            Test((_TryFormatFlagsEnum)17, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, false);
            Test((_TryFormatFlagsEnum)0, namesTryFormatFlagsEnum, valuesTryFormatFlagsEnum, false);

            // with zero
            Test(_TryFormatFlagsEnum_WithZero.A, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);
            Test(_TryFormatFlagsEnum_WithZero.A | _TryFormatFlagsEnum_WithZero.B, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);
            Test(_TryFormatFlagsEnum_WithZero.B | _TryFormatFlagsEnum_WithZero.E, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);
            Test(_TryFormatFlagsEnum_WithZero.A | _TryFormatFlagsEnum_WithZero.B | _TryFormatFlagsEnum_WithZero.C | _TryFormatFlagsEnum_WithZero.D | _TryFormatFlagsEnum_WithZero.E, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);
            Test((_TryFormatFlagsEnum_WithZero)17, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, false);
            Test(_TryFormatFlagsEnum_WithZero.Z, namesTryFormatFlagsEnumWithZero, valuesTryFormatFlagsEnumWithZero, true);

            // DRY up the test a bit
            static void Test<T>(T enumValue, string[] names, ulong[] values, bool expectedBool)
                where T : struct, Enum
            {
                {
                    var valueText = enumValue.ToString();

                    var resBool = Utils.TryParseFlagsEnum<T>(valueText.AsSpan(), names, values, out var resValue);
                    if (!resBool)
                    {
                        // malformed!
                        Assert.False(expectedBool);
                    }
                    else
                    {
                        // valid!
                        Assert.True(expectedBool);
                        Assert.Equal(enumValue, resValue);
                    }
                }

                // lower case also needs to work
                {
                    var valueText = enumValue.ToString().ToLowerInvariant();

                    var resBool = Utils.TryParseFlagsEnum<T>(valueText.AsSpan(), names, values, out var resValue);
                    if (!resBool)
                    {
                        // malformed!
                        Assert.False(expectedBool);
                    }
                    else
                    {
                        // valid!
                        Assert.True(expectedBool);
                        Assert.Equal(enumValue, resValue);
                    }
                }

                // upper case also needs to work
                {
                    var valueText = enumValue.ToString().ToUpperInvariant();

                    var resBool = Utils.TryParseFlagsEnum<T>(valueText.AsSpan(), names, values, out var resValue);
                    if (!resBool)
                    {
                        // malformed!
                        Assert.False(expectedBool);
                    }
                    else
                    {
                        // valid!
                        Assert.True(expectedBool);
                        Assert.Equal(enumValue, resValue);
                    }
                }
            }
        }
    }
}