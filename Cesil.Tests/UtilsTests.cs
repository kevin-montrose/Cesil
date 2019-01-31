using System;
using System.Buffers;
using Xunit;

namespace Cesil.Tests
{
    public class UtilsTests
    {
        [Theory]
        [InlineData("", "a", false)]
        [InlineData("a", "", false)]
        [InlineData("", "", true)]
        [InlineData("aa", "ab", false)]
        [InlineData("aa", "aa", true)]
        [InlineData("aa", "bb", false)]
        [InlineData("aaa", "aab", false)]
        [InlineData("aaa", "aaa", true)]
        [InlineData("aaa", "aba", false)]
        [InlineData("aaaa", "aaab", false)]
        [InlineData("aaaa", "aaaa", true)]
        [InlineData("aaab", "aaaa", false)]
        public void AreEqual(string a, string b, bool expected)
        {
            var aMem = a.AsMemory();
            var bMem = b.AsMemory();

            var res = Utils.AreEqual(aMem, bMem);

            Assert.Equal(expected, res);
        }

        class _FindChar
        {
            public string A { get; set; }
        }

        [Theory]
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

        class _FindNeedsEncode
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
            var config = Configuration.For<_FindNeedsEncode>();
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
            var config = Configuration.For<_FindNeedsEncode>();
            var asMemory = chars.AsMemory();
            var ix = Utils.FindNeedsEncode(asMemory, start, config);

            Assert.Equal(expected, ix);
        }

        sealed class _FindNeedsEncode_Sequence_Segment : ReadOnlySequenceSegment<char>
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

            var config = Configuration.For<_FindNeedsEncode>();
            var ix = Utils.FindNeedsEncode(seq, start, config);

            Assert.Equal(expected, ix);
        }
    }
}