using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cesil.Tests
{
    public class EncodedColumnTrackerTests
    {
        [Fact]
        public void Basic()
        {
            // build the thing up
            using var tracker = new EncodedColumnTracker();
            tracker.Add("fizz", "buzz", MemoryPool<char>.Shared);
            tracker.Add("foo", null, MemoryPool<char>.Shared);
            tracker.Add("no", "yes", MemoryPool<char>.Shared);
            tracker.Add("world", null, MemoryPool<char>.Shared);
            tracker.Add("abcd", "efghijk", MemoryPool<char>.Shared);

            // check that the bytes are what we expect
            {
                var mem = tracker.Memory.ToArray();

                var expectedStart =
                    new char[]
                    {
                    // 0
                    (char)4, (char)0,                   // length of "fizz" (little endian)
                    'f', 'i', 'z', 'z',                 // "fizz"
                    'b', 'u', 'z', 'z',                 // "buzz"
                    // 10
                    'f', 'o', 'o',                      // "foo"
                    // 13
                    (char)2, (char)0,                   // length of "no" (little endian)
                    'n', 'o',                           // "no"
                    'y', 'e', 's',                      // "yes"
                    // 20
                    'w', 'o', 'r', 'l', 'd',            // "world"
                    // 25
                    (char)4, (char)0,                   // length of "abcd" (little endian)
                    'a', 'b', 'c', 'd',                 // "abcd"
                    'e', 'f', 'g', 'h', 'i', 'j', 'k'   // "efghijk"
                    };

                var expectedEnd =
                    new char[]
                    {
                        (char)0xFFE6, (char)0xFFFF,                 // -1 * (index of "abcd" + 1) = -26 (little endian)
                        (char)0x0015, (char)0,                      // index of "world" + 1 = 21 (little endian)
                        (char)0xFFF2, (char)0xFFFF,                 // -1 * (index of "no" + 1) = -14 (little endian)
                        (char)0x000B, (char)0,                      // index of "foo" + 1 = 11 (little endian)
                        (char)0xFFFF, (char)0xFFFF                  // -1 * (index of "fizz" + 1) = -1 (little endian)
                    };

                Assert.Equal(expectedStart, mem.Take(expectedStart.Length));
                Assert.Equal(expectedEnd, mem.Skip(mem.Count() - expectedEnd.Length));
            }

            // check expected values come out
            Assert.Equal(5, tracker.Length);

            var a1 = tracker.GetColumnAt(0);
            var a2 = tracker.GetEncodedColumnAt(0);
            Assert.True(Utils.AreEqual("fizz".AsMemory(), a1));
            Assert.True(Utils.AreEqual("buzz".AsMemory(), a2));

            var b1 = tracker.GetColumnAt(1);
            var b2 = tracker.GetEncodedColumnAt(1);
            Assert.True(Utils.AreEqual("foo".AsMemory(), b1));
            Assert.True(Utils.AreEqual("foo".AsMemory(), b2));

            var c1 = tracker.GetColumnAt(2);
            var c2 = tracker.GetEncodedColumnAt(2);
            Assert.True(Utils.AreEqual("no".AsMemory(), c1));
            Assert.True(Utils.AreEqual("yes".AsMemory(), c2));

            var d1 = tracker.GetColumnAt(3);
            var d2 = tracker.GetEncodedColumnAt(3);
            Assert.True(Utils.AreEqual("world".AsMemory(), d1));
            Assert.True(Utils.AreEqual("world".AsMemory(), d2));

            var e1 = tracker.GetColumnAt(4);
            var e2 = tracker.GetEncodedColumnAt(4);
            Assert.True(Utils.AreEqual("abcd".AsMemory(), e1));
            Assert.True(Utils.AreEqual("efghijk".AsMemory(), e2));
        }

        [Fact]
        public void Simple()
        {
            var TEST_DATA =
                new[]
                {
                    // test 1
                    new[]
                    {
                        new [] {"hello" }
                    },
                    // test 2
                    new[]
                    {
                        new [] {"hello", "buzz" }
                    },
                    // test 3
                    new []
                    {
                        new [] {"hello"},
                        new [] {"hello", "world" },
                        new [] {"fizz", "buzz"},
                        new [] {"nope"}
                    }
                };

            foreach (var columns in TEST_DATA)
            {
                var tracker = new EncodedColumnTracker();
                for (var i = 0; i < columns.Length; i++)
                {
                    var col = columns[i];

                    Assert.NotNull(col);
                    Assert.True(col.Length >= 1 && col.Length <= 2);

                    tracker.Add(col[0], col.ElementAtOrDefault(1), MemoryPool<char>.Shared);

                    for (var j = 0; j <= i; j++)
                    {
                        var testCol = columns[j];
                        var colName = tracker.GetColumnAt(j);
                        var encodedName = tracker.GetEncodedColumnAt(j);

                        var colNameShouldMatch = testCol[0];
                        var encodedNameShouldMatch = testCol.Length == 1 ? testCol[0] : testCol[1];


                        Assert.True(Utils.AreEqual(colNameShouldMatch.AsMemory(), colName));
                        Assert.True(Utils.AreEqual(encodedNameShouldMatch.AsMemory(), encodedName));
                    }
                }
                tracker.Dispose();
            }
        }

        [Fact]
        public void Big()
        {
            var r = new Random(2020_09_06);
            for (var i = 0; i < 10_000; i++)
            {
                using var tracker = new EncodedColumnTracker();
                var expected = new List<(ReadOnlyMemory<char> Column, ReadOnlyMemory<char> Encoded)>();

                var num = r.Next(50) + 1;
                for (var j = 0; j < num; j++)
                {
                    var col = RandomString(r);
                    var hasEncoded = r.Next(2) == 1;
                    var encoded = hasEncoded ? RandomString(r) : null;

                    tracker.Add(col, encoded, MemoryPool<char>.Shared);
                    expected.Add((col.AsMemory(), (encoded ?? col).AsMemory()));

                    for (var k = 0; k <= j; k++)
                    {
                        var (expected1, expected2) = expected[k];
                        var actual1 = tracker.GetColumnAt(k);
                        var actual2 = tracker.GetEncodedColumnAt(k);

                        Assert.True(Utils.AreEqual(expected1, actual1));
                        Assert.True(Utils.AreEqual(expected2, actual2));
                    }
                }
            }

            static string RandomString(Random r)
            {
                var len = r.Next(25);
                var chars = new char[len];
                for (var i = 0; i < len; i++)
                {
                    var c1 = r.Next(char.MaxValue + 1);
                    chars[i] = (char)c1;
                }

                return new string(chars);
            }
        }
    }
}
