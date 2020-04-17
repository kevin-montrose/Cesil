using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Cesil.Tests
{
    public class NameLookupTests
    {
        private sealed class _FailIfMemoryNotAvailable : MemoryPool<char>
        {
            private sealed class Owner: IMemoryOwner<char>
            {
                public Memory<char> Memory { get; }

                internal Owner(char[] mem)
                {
                    Memory = mem.AsMemory();
                }

                public void Dispose() { }
            }

            public override int MaxBufferSize => 100;

            public override IMemoryOwner<char> Rent(int minBufferSize = -1)
            => new Owner(new char[MaxBufferSize]);

            protected override void Dispose(bool disposing) { }
        }

        [Fact]
        public void FailIfMemoryNotAvailable()
        {
            var mem = new _FailIfMemoryNotAvailable();

            var keys = Enumerable.Range(0, ushort.MaxValue + 2).Select(i => i.ToString()).ToList();

            Assert.Throws<InvalidOperationException>(() => NameLookup.Create(keys, mem));
        }

        [Fact]
        public void FallbackToBinarySearch()
        {
            // too many keys
            {
                var keys = Enumerable.Range(0, ushort.MaxValue + 2).Select(i => i.ToString()).ToList();

                using (var lookup = NameLookup.Create(keys, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.BinarySearch, lookup.Mode);

                    for (var i = 0; i <= ushort.MaxValue + 1; i++)
                    {
                        var key = i.ToString();

                        Assert.True(lookup.TryLookup(key, out var val));
                        Assert.Equal(i, val);
                    }

                    var badKey = (ushort.MaxValue + 2).ToString();
                    Assert.False(lookup.TryLookup(badKey, out _));
                }
            }

            // single keys / prefixes too big
            //
            // this tests ToPrefixLength() failing
            {
                var keys =
                    new[]
                    {
                        string.Join("", Enumerable.Repeat('a', 1024*64)),
                        string.Join("", Enumerable.Repeat('b', 1024*64))
                    };

                using (var lookup = NameLookup.Create(keys, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.BinarySearch, lookup.Mode);

                    Assert.True(lookup.TryLookup(keys[0], out var val0));
                    Assert.Equal(0, val0);

                    Assert.True(lookup.TryLookup(keys[1], out var val1));
                    Assert.Equal(1, val1);

                    Assert.False(lookup.TryLookup("c", out _));
                }
            }

            // offset too far
            //
            // this is a tricky one to think about
            //
            // basically, if there needs to be a prefix
            //   group such that storing all the other options
            //   takes up enough space the the difference between
            //   the first prefix option and the _next_ one
            //   can't fit in a ushort
            //
            // this tests ToOffset() failing
            {
                // the root group will have two entires, 
                //   one for UniqueKey, and one of the CommonPrefix
                //   which keeps this "simple"
                const string UniqueKey = "cd";
                const string CommonPrefix = "ab";
                const int InfixCount = 1000;
                const int InfixLength = 100;

                var keys = new List<string>();

                keys.Add(UniqueKey);

                // these a 
                var uniqueInfixes = Enumerable.Range(0, InfixCount).Select(x => string.Join("", Enumerable.Repeat((char)x, InfixLength))).ToArray();

                foreach (var infix in uniqueInfixes)
                {
                    // we now create two keys with the infix, so the
                    //    infix doesn't get the tail character attached
                    var key1 = CommonPrefix + infix + (char)InfixCount;
                    var key2 = CommonPrefix + infix + (char)(InfixCount + 1);

                    keys.Add(key1);
                    keys.Add(key2);
                }

                // with these keys, the second level after the CommonPrefix will have
                //   InfixCount entries, each entry will be InfixLength + 2 (one for 
                //   prefix length, and one for the offset to the next group) chars long,
                //   making the offset from the FIRST entry = (InfixCount - 1) * (InfixLength +2)
                //   (minus one because the offset doesn't have to go past the first entry)
                //   and so long as that is > short.MaxValue we can't fit that in the trie.

                using (var lookup = NameLookup.Create(keys, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.BinarySearch, lookup.Mode);

                    for (var i = 0; i < keys.Count; i++)
                    {
                        var key = keys[i];
                        Assert.True(lookup.TryLookup(key, out var val));
                        Assert.Equal(i, val);
                    }

                    Assert.False(lookup.TryLookup("d", out _));
                }
            }
        }

        // Binary Search tests
        [Fact]
        public void Create_BinarySearch()
        {
            // todo: this test assumes little endian

            // one value
            {
                var vals = OrderValues(new[] { "bar" });

                Assert.True(NameLookup.TryCreateBinarySearch(vals, MemoryPool<char>.Shared, out var owner, out var mem));
                var chars = mem.ToArray();
                owner.Dispose();

                // should be (remember, ints are little endian here)
                // 00: 01, 00         -- count = 1
                // 02: 06, 00         -- index of bar       
                // 04: 00, 00         -- value of bar = 0
                // 06: b, a, r        -- text of bar

                Assert.True(
                    chars.SequenceEqual(
                        new[]
                        {
                            '\x0001', '\0',
                            '\x0006', '\0',
                            '\0', '\0',
                            'b', 'a', 'r'
                        }
                    )
                );
            }

            // two values
            {
                var vals = OrderValues(new[] { "bar", "buzz" });

                Assert.True(NameLookup.TryCreateBinarySearch(vals, MemoryPool<char>.Shared, out var owner, out var mem));
                var chars = mem.ToArray();
                owner.Dispose();

                // should be (remember, ints are little endian here)
                // 00: 02, 00         -- count = 2
                // 02: 14, 00         -- index of bar
                // 04: 00, 00         -- value of bar = 0
                // 06: 10, 00         -- index of buzz
                // 08: 01, 00         -- value of buzz = 1
                // 10: b, u, z, z     -- text of buzz
                // 14: b, a, r        -- text of bar

                Assert.True(
                    chars.SequenceEqual(
                        new[]
                        {
                            '\x0002','\0',
                            '\x000E','\0',
                            '\0', '\0',
                            '\x000A','\0',
                            '\x0001','\0',
                            'b', 'u', 'z', 'z',
                            'b', 'a', 'r'
                        }
                    )
                );
            }

            // actual values
            {
                var vals = OrderValues(new[] { "bar", "buzz", "foo", "head", "heap", "hello" });

                Assert.True(NameLookup.TryCreateBinarySearch(vals, MemoryPool<char>.Shared, out var owner, out var mem));
                var chars = mem.ToArray();
                owner.Dispose();

                // should be (remember, ints are little endian here)
                // 00: 06, 00         -- count = 6
                // 02: 46, 00         -- index of bar
                // 04: 00, 00         -- value of bar = 0
                // 06: 42, 00         -- index of buzz
                // 08: 01, 00         -- value of buzz = 1
                // 10: 39, 00         -- index of foo
                // 12: 02, 00         -- value of foo = 2
                // 14: 35, 00         -- index of head
                // 16: 03, 00         -- value of head = 3
                // 18: 31, 00         -- index of heap
                // 20: 04, 00         -- value of heap = 4
                // 22: 26, 00         -- index of hello
                // 24: 05, 00         -- value of hello = 5
                // 26: h, e, l, l, o  -- text of hello
                // 31: h, e, a, p     -- text of heap
                // 35: h, e, a, d     -- text of head
                // 39: f, o, o        -- text of foo
                // 42: b, u, z, z     -- text of buzz
                // 46: b, a, r        -- text of bar

                Assert.True(
                    chars.SequenceEqual(
                        new[]
                        {
                            '\x0006','\0',
                            (char)46,'\0',
                            '\0', '\0',
                            (char)42,'\0',
                            '\x0001','\0',
                            (char)39, '\0',
                            '\x0002', '\0',
                            (char)35, '\0',
                            '\x0003', '\0',
                            (char)31, '\0',
                            '\x0004', '\0',
                            (char)26, '\0',
                            '\x0005', '\0',
                            'h', 'e', 'l', 'l', 'o',
                            'h', 'e', 'a', 'p',
                            'h', 'e', 'a', 'd',
                            'f', 'o', 'o',
                            'b', 'u', 'z', 'z',
                            'b', 'a', 'r'
                        }
                    )
                );
            }

            // some longer values, and try a bunch of orders
            {
                var rand = new Random(2020_04_14);
                var valsMaster = new List<string> { "fizz", "fizzing", "foo", "he", "heap", "heaper", "heaping", "heapingly", "heat" };

                for (var i = 0; i < 10; i++)
                {
                    // get em in a random order for test purposes
                    var vals =
                        valsMaster
                            .Select(v => (Value: v, Order: rand.Next()))
                            .OrderBy(t => t.Order)
                            .Select((t, ix) => (Name: t.Value, Index: ix))
                            .ToList();

                    var valsOrdered = vals.OrderBy(x => x.Name, StringComparer.Ordinal);

                    var fizzIx = vals.Single(s => s.Name == "fizz").Index;
                    Assert.NotEqual(-1, fizzIx);
                    var fizzingIx = vals.Single(s => s.Name == "fizzing").Index;
                    Assert.NotEqual(-1, fizzingIx);
                    var fooIx = vals.Single(s => s.Name == "foo").Index;
                    Assert.NotEqual(-1, fooIx);
                    var heIx = vals.Single(s => s.Name == "he").Index;
                    Assert.NotEqual(-1, heIx);
                    var heapIx = vals.Single(s => s.Name == "heap").Index;
                    Assert.NotEqual(-1, heapIx);
                    var heaperIx = vals.Single(s => s.Name == "heaper").Index;
                    Assert.NotEqual(-1, heaperIx);
                    var heapingIx = vals.Single(s => s.Name == "heaping").Index;
                    Assert.NotEqual(-1, heapingIx);
                    var heapinglyIx = vals.Single(s => s.Name == "heapingly").Index;
                    Assert.NotEqual(-1, heapinglyIx);
                    var heatIx = vals.Single(s => s.Name == "heat").Index;
                    Assert.NotEqual(-1, heatIx);

                    Assert.True(NameLookup.TryCreateBinarySearch(valsOrdered, MemoryPool<char>.Shared, out var owner, out var mem));
                    var chars = mem.ToArray();
                    owner.Dispose();

                    var asSpan = chars.AsSpan();
                    var asIntSpan = MemoryMarshal.Cast<char, int>(asSpan);

                    // validate count and values
                    Assert.Equal(valsOrdered.Count(), asIntSpan[0]);

                    var fizzStart = asIntSpan[1];
                    Assert.Equal(fizzIx, asIntSpan[2]);

                    var fizzingStart = asIntSpan[3];
                    Assert.Equal(fizzingIx, asIntSpan[4]);

                    var fooStart = asIntSpan[5];
                    Assert.Equal(fooIx, asIntSpan[6]);

                    var heStart = asIntSpan[7];
                    Assert.Equal(heIx, asIntSpan[8]);

                    var heapStart = asIntSpan[9];
                    Assert.Equal(heapIx, asIntSpan[10]);

                    var heaperStart = asIntSpan[11];
                    Assert.Equal(heaperIx, asIntSpan[12]);

                    var heapingStart = asIntSpan[13];
                    Assert.Equal(heapingIx, asIntSpan[14]);

                    var heapinglyStart = asIntSpan[15];
                    Assert.Equal(heapinglyIx, asIntSpan[16]);

                    var heatStart = asIntSpan[17];
                    Assert.Equal(heatIx, asIntSpan[18]);

                    // strings are stored in reverse order
                    Assert.True(fizzStart > fizzingStart);
                    Assert.True(fizzingStart > fooStart);
                    Assert.True(fooStart > heStart);
                    Assert.True(heStart > heapStart);
                    Assert.True(heapStart > heaperStart);
                    Assert.True(heaperStart > heapingStart);
                    Assert.True(heapingStart > heapinglyStart);
                    Assert.True(heapinglyStart > heatStart);

                    // strings are where expected
                    var fizzLen = asSpan.Length - fizzStart;
                    var fizzSpan = asSpan.Slice(fizzStart, fizzLen);
                    Assert.True(Equal(fizzSpan, "fizz".AsSpan()));

                    var fizzingLen = fizzStart - fizzingStart;
                    var fizzingSpan = asSpan.Slice(fizzingStart, fizzingLen);
                    Assert.True(Equal(fizzingSpan, "fizzing".AsSpan()));

                    var fooLen = fizzingStart - fooStart;
                    var fooSpan = asSpan.Slice(fooStart, fooLen);
                    Assert.True(Equal(fooSpan, "foo".AsSpan()));

                    var heLen = fooStart - heStart;
                    var heSpan = asSpan.Slice(heStart, heLen);
                    Assert.True(Equal(heSpan, "he".AsSpan()));

                    var heapLen = heStart - heapStart;
                    var heapSpan = asSpan.Slice(heapStart, heapLen);
                    Assert.True(Equal(heapSpan, "heap".AsSpan()));

                    var heaperLen = heapStart - heaperStart;
                    var heaperSpan = asSpan.Slice(heaperStart, heaperLen);
                    Assert.True(Equal(heaperSpan, "heaper".AsSpan()));

                    var heapingLen = heaperStart - heapingStart;
                    var heapingSpan = asSpan.Slice(heapingStart, heapingLen);
                    Assert.True(Equal(heapingSpan, "heaping".AsSpan()));

                    var heapinglyLen = heapingStart - heapinglyStart;
                    var heapinglySpan = asSpan.Slice(heapinglyStart, heapinglyLen);
                    Assert.True(Equal(heapinglySpan, "heapingly".AsSpan()));

                    var heatLen = heapinglyStart - heatStart;
                    var heatSpan = asSpan.Slice(heatStart, heatLen);
                    Assert.True(Equal(heatSpan, "heat".AsSpan()));
                }
            }

            // put stuff in the correct order for the create call
            static IOrderedEnumerable<(string Name, int Index)> OrderValues(IEnumerable<string> raw)
            => raw.Select((r, ix) => (Name: r, Index: ix)).OrderBy(r => r.Name, StringComparer.Ordinal);

            static unsafe bool Equal(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
            {
                fixed (char* aPtr = a)
                fixed (char* bPtr = b)
                {
                    return a.Length == b.Length && Utils.AreEqual(a.Length, aPtr, bPtr);
                }
            }
        }

        [Fact]
        public void Lookup_BinarySearch()
        {
            // single key
            {
                var vals = new List<string> { "bar" };

                using (var lookup = Create(vals))
                {
                    Assert.Equal(NameLookup.Algorithm.BinarySearch, lookup.Mode);
                    Assert.True(lookup.TryLookup("bar", out var barVal));
                    Assert.Equal(0, barVal);

                    Assert.False(lookup.TryLookup("", out var emptyVal));
                    Assert.Equal(-1, emptyVal);

                    Assert.False(lookup.TryLookup("b", out var bVal));
                    Assert.Equal(-1, bVal);

                    Assert.False(lookup.TryLookup("ba", out var baVal));
                    Assert.Equal(-1, baVal);

                    Assert.False(lookup.TryLookup("bars", out var barsVal));
                    Assert.Equal(-1, barsVal);

                    Assert.False(lookup.TryLookup("a", out var aVal));
                    Assert.Equal(-1, aVal);

                    Assert.False(lookup.TryLookup("aar", out var aarVal));
                    Assert.Equal(-1, aarVal);

                    Assert.False(lookup.TryLookup("c", out var cVal));
                    Assert.Equal(-1, cVal);

                    Assert.False(lookup.TryLookup("car", out var carVal));
                    Assert.Equal(-1, carVal);
                }
            }

            // two strings
            {
                var vals = new List<string> { "bar", "buzz" };

                using (var lookup = Create(vals))
                {
                    Assert.Equal(NameLookup.Algorithm.BinarySearch, lookup.Mode);
                    Assert.True(lookup.TryLookup("bar", out var barVal));
                    Assert.Equal(0, barVal);

                    Assert.True(lookup.TryLookup("buzz", out var buzzVal));
                    Assert.Equal(1, buzzVal);

                    Assert.False(lookup.TryLookup("", out var emptyVal));
                    Assert.Equal(-1, emptyVal);

                    Assert.False(lookup.TryLookup("b", out var bVal));
                    Assert.Equal(-1, bVal);

                    Assert.False(lookup.TryLookup("ba", out var baVal));
                    Assert.Equal(-1, baVal);

                    Assert.False(lookup.TryLookup("bars", out var barsVal));
                    Assert.Equal(-1, barsVal);

                    Assert.False(lookup.TryLookup("a", out var aVal));
                    Assert.Equal(-1, aVal);

                    Assert.False(lookup.TryLookup("aar", out var aarVal));
                    Assert.Equal(-1, aarVal);

                    Assert.False(lookup.TryLookup("c", out var cVal));
                    Assert.Equal(-1, cVal);

                    Assert.False(lookup.TryLookup("car", out var carVal));
                    Assert.Equal(-1, carVal);

                    Assert.False(lookup.TryLookup("buzzy", out var buzzyVal));
                    Assert.Equal(-1, buzzyVal);
                }
            }

            // proper values
            {
                var vals = new List<string> { "bar", "buzz", "foo", "head", "heap", "hello" };

                using (var lookup = Create(vals))
                {
                    Assert.Equal(NameLookup.Algorithm.BinarySearch, lookup.Mode);
                    Assert.False(lookup.TryLookup("", out var emptyVal));
                    Assert.Equal(-1, emptyVal);

                    for (var i = 0; i < vals.Count; i++)
                    {
                        var val = vals[i];
                        Assert.True(lookup.TryLookup(val, out var valVal));
                        Assert.Equal(i, valVal);

                        var missingHead = val.Substring(1);
                        if (!vals.Contains(missingHead))
                        {
                            Assert.False(lookup.TryLookup(missingHead, out var missingVal));
                            Assert.Equal(-1, missingVal);
                        }

                        var missingTail = val.Substring(0, val.Length - 1);
                        if (!vals.Contains(missingTail))
                        {
                            Assert.False(lookup.TryLookup(missingTail, out var missingVal));
                            Assert.Equal(-1, missingVal);
                        }

                        var missingCenter = val.Substring(0, 1) + val.Substring(2);
                        if (!vals.Contains(missingCenter))
                        {
                            Assert.False(lookup.TryLookup(missingCenter, out var missingVal));
                            Assert.Equal(-1, missingVal);
                        }

                        var extraHead = 'a' + val;
                        if (!vals.Contains(extraHead))
                        {
                            Assert.False(lookup.TryLookup(extraHead, out var extraVal));
                            Assert.Equal(-1, extraVal);
                        }

                        var extraTail = val + 'a';
                        if (!vals.Contains(extraTail))
                        {
                            Assert.False(lookup.TryLookup(extraTail, out var extraVal));
                            Assert.Equal(-1, extraVal);
                        }

                        var extraCenter = val.Substring(0, 1) + val[1] + val.Substring(1);
                        if (!vals.Contains(extraCenter))
                        {
                            Assert.False(lookup.TryLookup(extraCenter, out var extraVal));
                            Assert.Equal(-1, extraVal);
                        }
                    }
                }
            }

            // tricky values
            {
                var rand = new Random(2020_03_22);
                var valsMaster = new List<string> { "fizz", "fizzing", "foo", "he", "heap", "heaper", "heaping", "heapingly", "heat" };

                for (var i = 0; i < 10; i++)
                {
                    // get em in a random order for test purposes
                    var vals = valsMaster.Select(v => (Value: v, Order: rand.Next())).OrderBy(t => t.Order).Select(t => t.Value).ToList();

                    using (var lookup = Create(vals))
                    {
                        Assert.Equal(NameLookup.Algorithm.BinarySearch, lookup.Mode);
                        Assert.False(lookup.TryLookup("", out var emptyVal));
                        Assert.Equal(-1, emptyVal);

                        for (var j = 0; j < vals.Count; j++)
                        {
                            var val = vals[j];
                            Assert.True(lookup.TryLookup(val, out var valVal));
                            Assert.Equal(j, valVal);

                            var missingHead = val.Substring(1);
                            if (!vals.Contains(missingHead))
                            {
                                Assert.False(lookup.TryLookup(missingHead, out var missingVal));
                                Assert.Equal(-1, missingVal);
                            }

                            var missingTail = val.Substring(0, val.Length - 1);
                            if (!vals.Contains(missingTail))
                            {
                                Assert.False(lookup.TryLookup(missingTail, out var missingVal));
                                Assert.Equal(-1, missingVal);
                            }

                            var missingCenter = val.Substring(0, 1) + val.Substring(2);
                            if (!vals.Contains(missingCenter))
                            {
                                Assert.False(lookup.TryLookup(missingCenter, out var missingVal));
                                Assert.Equal(-1, missingVal);
                            }

                            var extraHead = 'a' + val;
                            if (!vals.Contains(extraHead))
                            {
                                Assert.False(lookup.TryLookup(extraHead, out var extraVal));
                                Assert.Equal(-1, extraVal);
                            }

                            var extraTail = val + 'a';
                            if (!vals.Contains(extraTail))
                            {
                                Assert.False(lookup.TryLookup(extraTail, out var extraVal));
                                Assert.Equal(-1, extraVal);
                            }

                            var extraCenter = val.Substring(0, 1) + val[1] + val.Substring(1);
                            if (!vals.Contains(extraCenter))
                            {
                                Assert.False(lookup.TryLookup(extraCenter, out var extraVal));
                                Assert.Equal(-1, extraVal);
                            }
                        }
                    }
                }
            }

            // benchmark values
            {
                var vals = new List<string> { "NullableGuid", "NullableInt", "NullableChar", "NullableSByte", "NullableDateTime", "NullableFloat", "DateTime", "Long", "Float", "ULong", "NullableUInt", "NullableShort", "Byte", "Enum", "NullableDecimal", "ShallowRows", "DeepRows", "Decimal", "NullableByte", "NullableUShort", "Char", "DateTimeOffset", "Int", "NullableULong", "SByte", "Short", "NullableLong", "NullableDouble", "UShort", "Double", "FlagsEnum", "Uri", "String", "NullableEnum", "NullableDateTimeOffset", "UInt", "NullableFlagsEnum", "Guid" };
                using (var lookup = Create(vals))
                {
                    Assert.Equal(NameLookup.Algorithm.BinarySearch, lookup.Mode);
                    for (var i = 0; i < vals.Count; i++)
                    {
                        var val = vals[i];
                        Assert.True(lookup.TryLookup(val, out var ix));
                        Assert.Equal(i, ix);
                    }
                }
            }

            static NameLookup Create(List<string> values)
            {
                var vals = values.Select((t, ix) => (Name: t, Index: ix)).OrderBy(o => o.Name, StringComparer.Ordinal);

                Assert.True(NameLookup.TryCreateBinarySearch(vals, MemoryPool<char>.Shared, out var owner, out var mem));

                return new NameLookup(NameLookup.Algorithm.BinarySearch, owner, mem);
            }
        }

        // Trie tests

        [Fact]
        public void MaxSizePrefixGroup_Trie()
        {
            // bugs can easily creep in
            //   when certain values get up near 
            //   your max values.
            //
            // this tests that we can handle having
            //   a prefix group with all values [0, short.MaxValue]
            //   in it

            var keys = Enumerable.Range(0, short.MaxValue + 1).Select(x => "" + (char)x).ToList();

            using (var lookup = NameLookup.Create(keys, MemoryPool<char>.Shared))
            {
                Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);

                for (var i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    Assert.True(lookup.TryLookup(key, out var val));
                    Assert.Equal(i, val);
                }

                Assert.False(lookup.TryLookup("ab", out _));
            }
        }

        [Fact]
        public void CommonPrefixLength_Trie()
        {
            // one value
            {
                var vals = new List<string> { "bar" }.Select(b => b.AsMemory()).ToList();

                var take = NameLookup.CommonPrefixLengthAdaptivePrefixTrie(vals, (ushort)(vals.Count - 1), 0, 0, out var endOfGroupIx);
                Assert.Equal(3, take);
                Assert.Equal(0, endOfGroupIx);
            }

            // actual values
            {
                var vals = new List<string> { "bar", "buzz", "foo", "head", "heap", "hello" }.Select(b => b.AsMemory()).ToList();

                Assert.True(vals.Select(_ => new string(_.Span)).SequenceEqual(vals.Select(_ => new string(_.Span)).OrderBy(_ => _)));

                ushort fooIx = 2;
                Assert.True(Utils.AreEqual("foo".AsMemory(), vals[fooIx]));
                ushort barIx = 0;
                Assert.True(Utils.AreEqual("bar".AsMemory(), vals[barIx]));
                ushort headIx = 3;
                Assert.True(Utils.AreEqual("head".AsMemory(), vals[headIx]));

                var takeFromFoo = NameLookup.CommonPrefixLengthAdaptivePrefixTrie(vals, (ushort)(vals.Count - 1), fooIx, 0, out var endOfFooIx);
                Assert.Equal(3, takeFromFoo);
                Assert.Equal(fooIx, endOfFooIx);

                var takeFromBar = NameLookup.CommonPrefixLengthAdaptivePrefixTrie(vals, (ushort)(vals.Count - 1), barIx, 0, out var endOfBarIx);
                Assert.Equal(1, takeFromBar);
                Assert.Equal(barIx + 1, endOfBarIx);

                var takeFromHead = NameLookup.CommonPrefixLengthAdaptivePrefixTrie(vals, (ushort)(vals.Count - 1), headIx, 0, out var endOfHeadIx);
                Assert.Equal(2, takeFromHead);
                Assert.Equal(headIx + 2, endOfHeadIx);
            }
        }

        [Fact]
        public void CalculateNeededMemory_Trie()
        {
            // just one string
            // should be 
            //   0, 3, b, a, r, -1 == 3 + "bar".Length = 6
            {
                var vals = new List<string> { "bar" }.Select(b => b.AsMemory()).ToList();

                var mem = NameLookup.CalculateNeededMemoryAdaptivePrefixTrie(vals, 0, (ushort)(vals.Count - 1), 0);
                Assert.Equal(6, mem);
            }

            // actual values
            // should be
            //
            // --root--
            // 0: 2,                        // head, has 3 prefixes (remember, subtract 1 from prefix count)
            // 1: 1, b, <offset>            // first prefix is "b"
            // 4: 3, f, o, o, <value>       // second prefix is "foo"
            // 9: 2, h, e, <offset>         // third prefix is "he"
            //
            // -- branch from prefix @1 --
            // 13: 1                        // after "b", has 2 prefixes (remember, subtract 1 from prefix count)
            // 14: 2, a, r, <value>         // first prefix is "bar"
            // 18: 3, u, z, z, <value>      // second prefix is "buzz"
            //
            // -- branch from prefix @9 --
            // 23: 1                        // after "he", has 2 prefixes (remember, subtract 1 from prefix count)
            // 24: 1, a, <offset>           // first prefix is "hea"
            // 26: 3, l, l, o, <value>      // second prefix is "hello"
            //
            // -- branch from prefix @24 --
            // 32: 1                        // after "hea", has 2 prefixes (remember, subtract 1 from prefix count)
            // 33: 1, d, <value>            // first prefix is "head"
            // 36: 1, p, <value>            // second prefix is "heap"
            //
            // final length is 39 chars
            {
                var rootVals = new List<string> { "bar", "buzz", "foo", "head", "heap", "hello" }.Select(b => b.AsMemory()).ToList();
                var groupAt13 = new List<string> { "ar", "uzz" }.Select(b => b.AsMemory()).ToList();                                    // after taking b
                var groupAt23 = new List<string> { "ad", "ap", "llo" }.Select(b => b.AsMemory()).ToList();                              // after taking he
                var groupAt32 = new List<string> { "d", "p" }.Select(b => b.AsMemory()).ToList();                                       // after taking hea

                Assert.True(rootVals.Select(_ => new string(_.Span)).SequenceEqual(rootVals.Select(_ => new string(_.Span)).OrderBy(_ => _)));
                Assert.True(groupAt13.Select(_ => new string(_.Span)).SequenceEqual(groupAt13.Select(_ => new string(_.Span)).OrderBy(_ => _)));
                Assert.True(groupAt23.Select(_ => new string(_.Span)).SequenceEqual(groupAt23.Select(_ => new string(_.Span)).OrderBy(_ => _)));
                Assert.True(groupAt32.Select(_ => new string(_.Span)).SequenceEqual(groupAt32.Select(_ => new string(_.Span)).OrderBy(_ => _)));

                var g32Mem = NameLookup.CalculateNeededMemoryAdaptivePrefixTrie(groupAt32, 0, (ushort)(groupAt32.Count - 1), 0);
                Assert.Equal(7, g32Mem);

                var g23Mem = NameLookup.CalculateNeededMemoryAdaptivePrefixTrie(groupAt23, 0, (ushort)(groupAt23.Count - 1), 0);
                Assert.Equal(9, g23Mem - g32Mem);

                var g13Mem = NameLookup.CalculateNeededMemoryAdaptivePrefixTrie(groupAt13, 0, (ushort)(groupAt13.Count - 1), 0);
                Assert.Equal(10, g13Mem);

                var rootMem = NameLookup.CalculateNeededMemoryAdaptivePrefixTrie(rootVals, 0, (ushort)(rootVals.Count - 1), 0);
                Assert.Equal(39, rootMem);
            }
        }

        private static char ToPrefixCount(int val)
        {
            var res = ToPrefixCountAssert(val, out var ret);
            Assert.True(res);
            return ret;
        }

        private static bool ToPrefixCountAssert(int count, out char asChar)
        {
            var val = count - 1;
            if (val < 0 || val > ushort.MaxValue)
            {
                asChar = '\0';
                return false;
            }

            ushort asUShort = checked((ushort)val);

            asChar = (char)asUShort;
            return true;
        }

        private static char ToPrefixLength(int val)
        {
            var res = NameLookup.ToPrefixLength(val, out var ret);
            Assert.True(res);
            return ret;
        }

        private static char ToValue(int val)
        {
            var asUShort = checked((ushort)val);

            return NameLookup.ToValue(asUShort);
        }

        private static char ToOffset(int val)
        {
            var res = NameLookup.ToOffset(val, out var ret);
            Assert.True(res);
            return ret;
        }

        [Fact]
        public void Create_Trie()
        {
            // just one string: "bar"
            // should be 
            // 
            // -- root --
            // 0: 0,                // 1 prefix          
            // 1: 3, b, a, r, 0     // <length = 3>, "bar", <value = 0>
            // 
            // len = 6
            {
                var vals = new List<string> { "bar" };

                using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);

                    Assert.NotNull(lookup.MemoryOwner);
                    Assert.Equal(6, lookup.Memory.Length);

                    var arr = lookup.Memory.ToArray();
                    Assert.True(
                        arr.SequenceEqual(
                            new char[]
                            {
                            ToPrefixCount(1),
                            ToPrefixLength(3),
                            'b',
                            'a',
                            'r',
                            ToValue(0)
                            }
                        )
                    );
                }
            }

            // two strings, single character prefix: "bar", "buzz"
            // should be
            //
            // -- root --
            // 0: 0,                        // 1 prefix: "b"
            // 1: 1, b, -1                  // <length = 1>, "b", <offset = 1>
            //
            // -- branch from prefix@1 --
            // 4: 1,                        // 2 prefixes: "ar", "uzz"
            // 5: 2, a, r, 0                // <length = 2>, "ar", <value = 0>
            // 9: 3, u, z, z, 1             // <length = 3>, "uzz", <value = 1>
            //
            // len = 14
            {
                var vals = new List<string> { "bar", "buzz" };

                using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);
                    Assert.NotNull(lookup.MemoryOwner);

                    var arr = lookup.Memory.ToArray();

                    var shouldMatch =
                        new char[]
                        { 
                        // -- root --
                        ToPrefixCount(1),
                        ToPrefixLength(1),
                        'b',
                        ToOffset(1),
                            
                        // -- branch from prefix@1 --
                        ToPrefixCount(2),
                        ToPrefixLength(2),
                        'a',
                        'r',
                        ToValue(0),
                        ToPrefixLength(3),
                        'u',
                        'z',
                        'z',
                        ToValue(1)
                        };

                    Assert.Equal(14, arr.Length);
                    Assert.True(arr.SequenceEqual(shouldMatch));
                }
            }

            // actual values: "bar", "buzz", "foo", "head", "heap", "hello"
            // should be
            //
            // --root--
            // 0: 2,                        // head, has 3 prefixes (remember, subtract 1 from prefix count)
            // 1: 1, b, -10                 // <length = 1>, "b", <offset = 13 - 3 = 10>
            // 4: 3, f, o, o, 2             // <length = 3>, "foo", <value = 2>
            // 9: 2, h, e, -11              // <length = 2>, "he", <offset = 23 - 12 = 11>
            //
            // -- branch from prefix @1 --
            // 13: 1                        // after "b", has 2 prefixes (remember, subtract 1 from prefix count)
            // 14: 2, a, r, 0               // <length = 2>, "ar", <value = 0>
            // 18: 3, u, z, z, 1            // <length = 3>, "uzz", <value = 1>
            //
            // -- branch from prefix @9 --
            // 23: 1                        // after "he", has 2 prefixes (remember, subtract 1 from prefix count)
            // 24: 1, a, -6                 // <length = 1>, "a", <offset = 32 - 26 = 6>
            // 26: 3, l, l, o, 5            // <length = 3>, "llo", <value = 5>
            //
            // -- branch from prefix @24 --
            // 32: 1                        // after "hea", has 2 prefixes (remember, subtract 1 from prefix count)
            // 33: 1, d, 3                  // <length = 1>, "d", <value = 3>
            // 36: 1, p, 4                  // <length = 1>, "p", <value = 4>
            //
            // final length is 39 chars
            {
                var vals = new List<string> { "bar", "buzz", "foo", "head", "heap", "hello" };

                using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);
                    Assert.NotNull(lookup.MemoryOwner);
                    Assert.Equal(39, lookup.Memory.Length);

                    var arr = lookup.Memory.ToArray();
                    Assert.True(
                        arr.SequenceEqual(
                            new char[]
                            {
                            // -- root ---
                            ToPrefixCount(3),
                            ToPrefixLength(1),
                            'b',
                            ToOffset(10),
                            ToPrefixLength(3),
                            'f', 'o', 'o',
                            ToValue(2),
                            ToPrefixLength(2),
                            'h', 'e',
                            ToOffset(11),

                            // -- branch from prefix @1 --
                            ToPrefixCount(2),
                            ToPrefixLength(2),
                            'a', 'r',
                            ToValue(0),
                            ToPrefixLength(3),
                            'u', 'z', 'z',
                            ToValue(1),

                            // -- branch from prefix @9 --
                            ToPrefixCount(2),
                            ToPrefixLength(1),
                            'a',
                            ToOffset(6),
                            ToPrefixLength(3),
                            'l', 'l', 'o',
                            ToValue(5),


                            // -- branch from prefix @24 --
                            ToPrefixCount(2),
                            ToPrefixLength(1),
                            'd',
                            ToValue(3),
                            ToPrefixLength(1),
                            'p',
                            ToValue(4)
                            }
                        )
                    );
                }
            }

            // tricky values: "fizz", "fizzing", "foo", "he", "heap", "heaper", "heaping", "heapingly", "heat"
            //   ^ we'll randomize the order, so <value> will be different each time
            // should be
            //
            // --root--
            // 0 : 1                        // root, has 2 prefixes "f" and "he"
            // 1 : 1, f, -5                 // <length = 1>, "f", <offset = 8 - 3 = 5>
            // 4 : 2, h, e, -19             // <length = 2>, "he", <offset = 26 - 7 = 19>
            //
            // -- branch from prefix@1 --
            // 8 : 1                        // from "f", has 2 prefixes: "oo", "izz"
            // 9 : 3, i, z, z, -5           // <length = 3>, "izz", <offset = 18 - 13 = 5>
            // 14: 2, o, o, <value>         // <length = 2>, "oo", <value = ?>
            //
            // -- branch from prefix@14 --
            // 18: 1                        // from "fizz", has 2 prefixes: "", "ing"
            // 19: 0, <value>               // <length = 0>, "", <value = ?>
            // 21: 3, i, n, g, <value>      // <length = 3>, "ing", <value = ?>
            //
            // -- branch from prefix@4 --
            // 26: 1                        // from "he", has 3 prefixes: "", "a"
            // 27: 0, <value>               // <length = 0>, "", <value = ?>
            // 29: 1, a, -1                 // <length = 1>, "a", <offset = 32 - 31 = 1>
            //
            // -- branch from prefix@29 --
            // 32: 1                        // from "hea", has 2 prefixes: "p", "t"
            // 33: 1, p, -4                 // <length = 1>, "p", <offset = 39 - 35 = 4>
            // 36: 1, t, <value>            // <length = 1>, "t", <value = ?>
            //
            // -- branch from prefix@33 --
            // 39: 2                        // from "heap", has 3 prefixes: "", "er", "ing"
            // 40: 0, <value>               // <length = 0>, "", <value = ?>
            // 42: 2, e, r, <value>         // <length = 2>, "er", <value = ?>
            // 46: 3, i, n, g, -1           // <length = 3>, "ing", <offset = 51 - 50 = 1>
            //
            // -- branch from prefix@46
            // 51: 1                        // from "heaping", has 2 prefixes: "", "ly"
            // 52: 0, <value>               // <length = 0>, "", <value = ?>
            // 54: 2, l, y, <value>         // <length = 2>, "ly", <value = ?>
            //
            // length = 58
            {
                var rand = new Random(2020_03_21);
                var valsMaster = new List<string> { "fizz", "fizzing", "foo", "he", "heap", "heaper", "heaping", "heapingly", "heat" };

                for (var i = 0; i < 10; i++)
                {
                    // get em in a random order for test purposes
                    var vals = valsMaster.Select(v => (Value: v, Order: rand.Next())).OrderBy(t => t.Order).Select(t => t.Value).ToList();

                    var fooIx = vals.IndexOf("foo");
                    Assert.NotEqual(-1, fooIx);
                    var fizzIx = vals.IndexOf("fizz");
                    Assert.NotEqual(-1, fizzIx);
                    var fizzingIx = vals.IndexOf("fizzing");
                    Assert.NotEqual(-1, fizzingIx);
                    var heIx = vals.IndexOf("he");
                    Assert.NotEqual(-1, heIx);
                    var heatIx = vals.IndexOf("heat");
                    Assert.NotEqual(-1, heatIx);
                    var heapIx = vals.IndexOf("heap");
                    Assert.NotEqual(-1, heapIx);
                    var heaperIx = vals.IndexOf("heaper");
                    Assert.NotEqual(-1, heaperIx);
                    var heapingIx = vals.IndexOf("heaping");
                    Assert.NotEqual(-1, heapingIx);
                    var heapinglyIx = vals.IndexOf("heapingly");
                    Assert.NotEqual(-1, heapinglyIx);

                    using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                    {
                        Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);
                        Assert.NotNull(lookup.MemoryOwner);
                        var arr = lookup.Memory.ToArray();

                        Assert.Equal(58, arr.Length);

                        var shouldMatch =
                            new char[]
                                {
                                // --root--
                                ToPrefixCount(2),    // root, has 2 prefixes "f" and "he"
                                ToPrefixLength(1),   // <length = 1>, "f", <offset = 8 - 3 = 5>
                                'f',
                                ToOffset(5),
                                ToPrefixLength(2),   // <length = 2>, "he", <offset = 26 - 7 = 19>
                                'h', 'e',
                                ToOffset(19),
                                
                                // -- branch from prefix@1 --
                                ToPrefixCount(2),    // from "f", has 2 prefixes: "oo", "izz"
                                ToPrefixLength(3),   // <length = 3>, "izz", <offset = 18 - 13 = 5>
                                'i', 'z', 'z',
                                ToOffset(5),
                                ToPrefixLength(2),   // <length = 2>, "oo", <value = ?>
                                'o', 'o',
                                ToValue(fooIx),

                                // -- branch from prefix@14 --
                                ToPrefixCount(2),    // from "fizz", has 2 prefixes: "", "ing"
                                ToPrefixLength(0),   // <length = 0>, "", <value = ?>
                                ToValue(fizzIx),
                                ToPrefixLength(3),   // <length = 3>, "ing", <value = ?>
                                'i', 'n', 'g',
                                ToValue(fizzingIx),
                                
                                // -- branch from prefix@4 --
                                ToPrefixCount(2),    // from "he", has 3 prefixes: "", "a"
                                ToPrefixLength(0),   // <length = 0>, "", <value = ?>
                                ToValue(heIx),
                                ToPrefixLength(1),   // <length = 1>, "a", <offset = 32-31 = 1>
                                'a',
                                ToOffset(1),
                                
                                // -- branch from prefix@29 --
                                ToPrefixCount(2),    // from "hea", has 2 prefixes: "p", "t"
                                ToPrefixLength(1),   // <length = 1>, "p", <offset = 39 - 35 = 4>
                                'p',
                                ToOffset(4),
                                ToPrefixLength(1),   // <length = 1>, "t", <value = ?>
                                't',
                                ToValue(heatIx),
                                
                                // -- branch from prefix@33 --
                                ToPrefixCount(3),    // from "heap", has 3 prefixes: "", "er", "ing"
                                ToPrefixLength(0),   // <length = 0>, "", <value = ?>
                                ToValue(heapIx),
                                ToPrefixLength(2),   // <length = 2>, "er", <value = ?>
                                'e', 'r',
                                ToValue(heaperIx),
                                ToPrefixLength(3),   // <length = 3>, "ing", <offset = 51 - 50 = 1>
                                'i', 'n', 'g',
                                ToOffset(1),
                                
                                // -- branch from prefix@46
                                ToPrefixCount(2),    // from "heaping", has 2 prefixes: "", "ly"
                                ToPrefixLength(0),   // <length = 0>, "", <value = ?>
                                ToValue(heapingIx),
                                ToPrefixLength(2),   // <length = 2>, "ly", <value = ?>
                                'l', 'y',
                                ToValue(heapinglyIx)
                                };

                        Assert.True(arr.SequenceEqual(shouldMatch));
                    }
                }
            }
        }

        [Fact]
        public void Lookup_Trie()
        {
            // single key
            {
                var vals = new List<string> { "bar" };

                using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);
                    Assert.True(lookup.TryLookup("bar", out var barVal));
                    Assert.Equal(0, barVal);

                    Assert.False(lookup.TryLookup("", out var emptyVal));
                    Assert.Equal(-1, emptyVal);

                    Assert.False(lookup.TryLookup("b", out var bVal));
                    Assert.Equal(-1, bVal);

                    Assert.False(lookup.TryLookup("ba", out var baVal));
                    Assert.Equal(-1, baVal);

                    Assert.False(lookup.TryLookup("bars", out var barsVal));
                    Assert.Equal(-1, barsVal);

                    Assert.False(lookup.TryLookup("a", out var aVal));
                    Assert.Equal(-1, aVal);

                    Assert.False(lookup.TryLookup("aar", out var aarVal));
                    Assert.Equal(-1, aarVal);

                    Assert.False(lookup.TryLookup("c", out var cVal));
                    Assert.Equal(-1, cVal);

                    Assert.False(lookup.TryLookup("car", out var carVal));
                    Assert.Equal(-1, carVal);
                }
            }

            // two strings
            {
                var vals = new List<string> { "bar", "buzz" };

                using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);
                    Assert.True(lookup.TryLookup("bar", out var barVal));
                    Assert.Equal(0, barVal);

                    Assert.True(lookup.TryLookup("buzz", out var buzzVal));
                    Assert.Equal(1, buzzVal);

                    Assert.False(lookup.TryLookup("", out var emptyVal));
                    Assert.Equal(-1, emptyVal);

                    Assert.False(lookup.TryLookup("b", out var bVal));
                    Assert.Equal(-1, bVal);

                    Assert.False(lookup.TryLookup("ba", out var baVal));
                    Assert.Equal(-1, baVal);

                    Assert.False(lookup.TryLookup("bars", out var barsVal));
                    Assert.Equal(-1, barsVal);

                    Assert.False(lookup.TryLookup("a", out var aVal));
                    Assert.Equal(-1, aVal);

                    Assert.False(lookup.TryLookup("aar", out var aarVal));
                    Assert.Equal(-1, aarVal);

                    Assert.False(lookup.TryLookup("c", out var cVal));
                    Assert.Equal(-1, cVal);

                    Assert.False(lookup.TryLookup("car", out var carVal));
                    Assert.Equal(-1, carVal);

                    Assert.False(lookup.TryLookup("buzzy", out var buzzyVal));
                    Assert.Equal(-1, buzzyVal);
                }
            }

            // proper values
            {
                var vals = new List<string> { "bar", "buzz", "foo", "head", "heap", "hello" };

                using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);
                    Assert.False(lookup.TryLookup("", out var emptyVal));
                    Assert.Equal(-1, emptyVal);

                    for (var i = 0; i < vals.Count; i++)
                    {
                        var val = vals[i];
                        Assert.True(lookup.TryLookup(val, out var valVal));
                        Assert.Equal(i, valVal);

                        var missingHead = val.Substring(1);
                        if (!vals.Contains(missingHead))
                        {
                            Assert.False(lookup.TryLookup(missingHead, out var missingVal));
                            Assert.Equal(-1, missingVal);
                        }

                        var missingTail = val.Substring(0, val.Length - 1);
                        if (!vals.Contains(missingTail))
                        {
                            Assert.False(lookup.TryLookup(missingTail, out var missingVal));
                            Assert.Equal(-1, missingVal);
                        }

                        var missingCenter = val.Substring(0, 1) + val.Substring(2);
                        if (!vals.Contains(missingCenter))
                        {
                            Assert.False(lookup.TryLookup(missingCenter, out var missingVal));
                            Assert.Equal(-1, missingVal);
                        }

                        var extraHead = 'a' + val;
                        if (!vals.Contains(extraHead))
                        {
                            Assert.False(lookup.TryLookup(extraHead, out var extraVal));
                            Assert.Equal(-1, extraVal);
                        }

                        var extraTail = val + 'a';
                        if (!vals.Contains(extraTail))
                        {
                            Assert.False(lookup.TryLookup(extraTail, out var extraVal));
                            Assert.Equal(-1, extraVal);
                        }

                        var extraCenter = val.Substring(0, 1) + val[1] + val.Substring(1);
                        if (!vals.Contains(extraCenter))
                        {
                            Assert.False(lookup.TryLookup(extraCenter, out var extraVal));
                            Assert.Equal(-1, extraVal);
                        }
                    }
                }
            }

            // tricky values
            {
                var rand = new Random(2020_03_22);
                var valsMaster = new List<string> { "fizz", "fizzing", "foo", "he", "heap", "heaper", "heaping", "heapingly", "heat" };

                for (var i = 0; i < 10; i++)
                {
                    // get em in a random order for test purposes
                    var vals = valsMaster.Select(v => (Value: v, Order: rand.Next())).OrderBy(t => t.Order).Select(t => t.Value).ToList();

                    using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                    {
                        Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);
                        Assert.False(lookup.TryLookup("", out var emptyVal));
                        Assert.Equal(-1, emptyVal);

                        for (var j = 0; j < vals.Count; j++)
                        {
                            var val = vals[j];
                            Assert.True(lookup.TryLookup(val, out var valVal));
                            Assert.Equal(j, valVal);

                            var missingHead = val.Substring(1);
                            if (!vals.Contains(missingHead))
                            {
                                Assert.False(lookup.TryLookup(missingHead, out var missingVal));
                                Assert.Equal(-1, missingVal);
                            }

                            var missingTail = val.Substring(0, val.Length - 1);
                            if (!vals.Contains(missingTail))
                            {
                                Assert.False(lookup.TryLookup(missingTail, out var missingVal));
                                Assert.Equal(-1, missingVal);
                            }

                            var missingCenter = val.Substring(0, 1) + val.Substring(2);
                            if (!vals.Contains(missingCenter))
                            {
                                Assert.False(lookup.TryLookup(missingCenter, out var missingVal));
                                Assert.Equal(-1, missingVal);
                            }

                            var extraHead = 'a' + val;
                            if (!vals.Contains(extraHead))
                            {
                                Assert.False(lookup.TryLookup(extraHead, out var extraVal));
                                Assert.Equal(-1, extraVal);
                            }

                            var extraTail = val + 'a';
                            if (!vals.Contains(extraTail))
                            {
                                Assert.False(lookup.TryLookup(extraTail, out var extraVal));
                                Assert.Equal(-1, extraVal);
                            }

                            var extraCenter = val.Substring(0, 1) + val[1] + val.Substring(1);
                            if (!vals.Contains(extraCenter))
                            {
                                Assert.False(lookup.TryLookup(extraCenter, out var extraVal));
                                Assert.Equal(-1, extraVal);
                            }
                        }
                    }
                }
            }

            // benchmark values
            {
                var vals = new List<string> { "NullableGuid", "NullableInt", "NullableChar", "NullableSByte", "NullableDateTime", "NullableFloat", "DateTime", "Long", "Float", "ULong", "NullableUInt", "NullableShort", "Byte", "Enum", "NullableDecimal", "ShallowRows", "DeepRows", "Decimal", "NullableByte", "NullableUShort", "Char", "DateTimeOffset", "Int", "NullableULong", "SByte", "Short", "NullableLong", "NullableDouble", "UShort", "Double", "FlagsEnum", "Uri", "String", "NullableEnum", "NullableDateTimeOffset", "UInt", "NullableFlagsEnum", "Guid" };
                using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                {
                    Assert.Equal(NameLookup.Algorithm.AdaptiveRadixTrie, lookup.Mode);
                    for (var i = 0; i < vals.Count; i++)
                    {
                        var val = vals[i];
                        Assert.True(lookup.TryLookup(val, out var ix));
                        Assert.Equal(i, ix);
                    }
                }
            }
        }
    }
}
