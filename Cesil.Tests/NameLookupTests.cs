using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cesil.Tests
{
    public class NameLookupTests
    {
        [Fact]
        public void CommonPrefixLength()
        {
            // one value
            {
                var vals = new List<string> { "bar" }.Select(b => b.AsMemory()).ToList();

                var take = NameLookup.CommonPrefixLength(vals, vals.Count - 1, 0, 0, out var endOfGroupIx);
                Assert.Equal(3, take);
                Assert.Equal(0, endOfGroupIx);
            }

            // actual values
            {
                var vals = new List<string> { "bar", "buzz", "foo", "head", "heap", "hello" }.Select(b => b.AsMemory()).ToList();

                Assert.True(vals.Select(_ => new string(_.Span)).SequenceEqual(vals.Select(_ => new string(_.Span)).OrderBy(_ => _)));

                var fooIx = 2;
                Assert.True(Utils.AreEqual("foo".AsMemory(), vals[fooIx]));
                var barIx = 0;
                Assert.True(Utils.AreEqual("bar".AsMemory(), vals[barIx]));
                var headIx = 3;
                Assert.True(Utils.AreEqual("head".AsMemory(), vals[headIx]));

                var takeFromFoo = NameLookup.CommonPrefixLength(vals, vals.Count - 1, fooIx, 0, out var endOfFooIx);
                Assert.Equal(3, takeFromFoo);
                Assert.Equal(fooIx, endOfFooIx);

                var takeFromBar = NameLookup.CommonPrefixLength(vals, vals.Count - 1, barIx, 0, out var endOfBarIx);
                Assert.Equal(1, takeFromBar);
                Assert.Equal(barIx + 1, endOfBarIx);

                var takeFromHead = NameLookup.CommonPrefixLength(vals, vals.Count - 1, headIx, 0, out var endOfHeadIx);
                Assert.Equal(2, takeFromHead);
                Assert.Equal(headIx + 2, endOfHeadIx);
            }
        }

        [Fact]
        public void CalculateNeededMemory()
        {
            // just one string
            // should be 
            //   0, 3, b, a, r, -1 == 3 + "bar".Length = 6
            {
                var vals = new List<string> { "bar" }.Select(b => b.AsMemory()).ToList();

                var mem = NameLookup.CalculateNeededMemory(vals, 0, vals.Count - 1, 0);
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

                var g32Mem = NameLookup.CalculateNeededMemory(groupAt32, 0, groupAt32.Count - 1, 0);
                Assert.Equal(7, g32Mem);

                var g23Mem = NameLookup.CalculateNeededMemory(groupAt23, 0, groupAt23.Count - 1, 0);
                Assert.Equal(9, g23Mem - g32Mem);

                var g13Mem = NameLookup.CalculateNeededMemory(groupAt13, 0, groupAt13.Count - 1, 0);
                Assert.Equal(10, g13Mem);

                var rootMem = NameLookup.CalculateNeededMemory(rootVals, 0, rootVals.Count - 1, 0);
                Assert.Equal(39, rootMem);
            }
        }

        [Fact]
        public void Create()
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
                    Assert.NotNull(lookup.MemoryOwner);
                    Assert.Equal(6, lookup.Memory.Length);

                    var arr = lookup.Memory.ToArray();
                    Assert.True(
                        arr.SequenceEqual(
                            new char[]
                            {
                            NameLookup.ToPrefixCount(1),
                            NameLookup.ToPrefixLength(3),
                            'b',
                            'a',
                            'r',
                            NameLookup.ToValue(0)
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

                    Assert.NotNull(lookup.MemoryOwner);

                    var arr = lookup.Memory.ToArray();

                    var shouldMatch =
                        new char[]
                        { 
                        // -- root --
                        NameLookup.ToPrefixCount(1),
                        NameLookup.ToPrefixLength(1),
                        'b',
                        NameLookup.ToOffset(1),
                            
                        // -- branch from prefix@1 --
                        NameLookup.ToPrefixCount(2),
                        NameLookup.ToPrefixLength(2),
                        'a',
                        'r',
                        NameLookup.ToValue(0),
                        NameLookup.ToPrefixLength(3),
                        'u',
                        'z',
                        'z',
                        NameLookup.ToValue(1)
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
                    Assert.NotNull(lookup.MemoryOwner);
                    Assert.Equal(39, lookup.Memory.Length);

                    var arr = lookup.Memory.ToArray();
                    Assert.True(
                        arr.SequenceEqual(
                            new char[]
                            {
                            // -- root ---
                            NameLookup.ToPrefixCount(3),
                            NameLookup.ToPrefixLength(1),
                            'b',
                            NameLookup.ToOffset(10),
                            NameLookup.ToPrefixLength(3),
                            'f', 'o', 'o',
                            NameLookup.ToValue(2),
                            NameLookup.ToPrefixLength(2),
                            'h', 'e',
                            NameLookup.ToOffset(11),

                            // -- branch from prefix @1 --
                            NameLookup.ToPrefixCount(2),
                            NameLookup.ToPrefixLength(2),
                            'a', 'r',
                            NameLookup.ToValue(0),
                            NameLookup.ToPrefixLength(3),
                            'u', 'z', 'z',
                            NameLookup.ToValue(1),

                            // -- branch from prefix @9 --
                            NameLookup.ToPrefixCount(2),
                            NameLookup.ToPrefixLength(1),
                            'a',
                            NameLookup.ToOffset(6),
                            NameLookup.ToPrefixLength(3),
                            'l', 'l', 'o',
                            NameLookup.ToValue(5),


                            // -- branch from prefix @24 --
                            NameLookup.ToPrefixCount(2),
                            NameLookup.ToPrefixLength(1),
                            'd',
                            NameLookup.ToValue(3),
                            NameLookup.ToPrefixLength(1),
                            'p',
                            NameLookup.ToValue(4)
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
                        Assert.NotNull(lookup.MemoryOwner);
                        var arr = lookup.Memory.ToArray();

                        Assert.Equal(58, arr.Length);

                        var shouldMatch =
                            new char[]
                                {
                                // --root--
                                NameLookup.ToPrefixCount(2),    // root, has 2 prefixes "f" and "he"
                                NameLookup.ToPrefixLength(1),   // <length = 1>, "f", <offset = 8 - 3 = 5>
                                'f',
                                NameLookup.ToOffset(5),
                                NameLookup.ToPrefixLength(2),   // <length = 2>, "he", <offset = 26 - 7 = 19>
                                'h', 'e',
                                NameLookup.ToOffset(19),
                                
                                // -- branch from prefix@1 --
                                NameLookup.ToPrefixCount(2),    // from "f", has 2 prefixes: "oo", "izz"
                                NameLookup.ToPrefixLength(3),   // <length = 3>, "izz", <offset = 18 - 13 = 5>
                                'i', 'z', 'z',
                                NameLookup.ToOffset(5),
                                NameLookup.ToPrefixLength(2),   // <length = 2>, "oo", <value = ?>
                                'o', 'o',
                                NameLookup.ToValue(fooIx),

                                // -- branch from prefix@14 --
                                NameLookup.ToPrefixCount(2),    // from "fizz", has 2 prefixes: "", "ing"
                                NameLookup.ToPrefixLength(0),   // <length = 0>, "", <value = ?>
                                NameLookup.ToValue(fizzIx),
                                NameLookup.ToPrefixLength(3),   // <length = 3>, "ing", <value = ?>
                                'i', 'n', 'g',
                                NameLookup.ToValue(fizzingIx),
                                
                                // -- branch from prefix@4 --
                                NameLookup.ToPrefixCount(2),    // from "he", has 3 prefixes: "", "a"
                                NameLookup.ToPrefixLength(0),   // <length = 0>, "", <value = ?>
                                NameLookup.ToValue(heIx),
                                NameLookup.ToPrefixLength(1),   // <length = 1>, "a", <offset = 32-31 = 1>
                                'a',
                                NameLookup.ToOffset(1),
                                
                                // -- branch from prefix@29 --
                                NameLookup.ToPrefixCount(2),    // from "hea", has 2 prefixes: "p", "t"
                                NameLookup.ToPrefixLength(1),   // <length = 1>, "p", <offset = 39 - 35 = 4>
                                'p',
                                NameLookup.ToOffset(4),
                                NameLookup.ToPrefixLength(1),   // <length = 1>, "t", <value = ?>
                                't',
                                NameLookup.ToValue(heatIx),
                                
                                // -- branch from prefix@33 --
                                NameLookup.ToPrefixCount(3),    // from "heap", has 3 prefixes: "", "er", "ing"
                                NameLookup.ToPrefixLength(0),   // <length = 0>, "", <value = ?>
                                NameLookup.ToValue(heapIx),
                                NameLookup.ToPrefixLength(2),   // <length = 2>, "er", <value = ?>
                                'e', 'r',
                                NameLookup.ToValue(heaperIx),
                                NameLookup.ToPrefixLength(3),   // <length = 3>, "ing", <offset = 51 - 50 = 1>
                                'i', 'n', 'g',
                                NameLookup.ToOffset(1),
                                
                                // -- branch from prefix@46
                                NameLookup.ToPrefixCount(2),    // from "heaping", has 2 prefixes: "", "ly"
                                NameLookup.ToPrefixLength(0),   // <length = 0>, "", <value = ?>
                                NameLookup.ToValue(heapingIx),
                                NameLookup.ToPrefixLength(2),   // <length = 2>, "ly", <value = ?>
                                'l', 'y',
                                NameLookup.ToValue(heapinglyIx)
                                };

                        Assert.True(arr.SequenceEqual(shouldMatch));
                    }
                }
            }
        }

        [Fact]
        public void Lookup()
        {
            // single key
            {
                var vals = new List<string> { "bar" };

                using (var lookup = NameLookup.Create(vals, MemoryPool<char>.Shared))
                {
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
