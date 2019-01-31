using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Cesil
{
    internal static class Utils
    {
        internal static IMemoryOwner<T> RentMustIncrease<T>(MemoryPool<T> pool, int newSize, int oldSize)
        {
            int requestSize;
            if (newSize > pool.MaxBufferSize)
            {
                if (oldSize >= pool.MaxBufferSize)
                {
                    Throw.InvalidOperation($"Needed a larger memory segment than could be requested, needed {newSize:N0}; {nameof(MemoryPool<T>.MaxBufferSize)} = {pool.MaxBufferSize:N0}");
                }

                requestSize = pool.MaxBufferSize;
            }
            else
            {
                requestSize = newSize;
            }

            return pool.Rent(requestSize);
        }

        private const int CHARS_PER_INT = sizeof(int) / sizeof(char);

        internal static unsafe bool AreEqual(ReadOnlyMemory<char> a, ReadOnlyMemory<char> b)
        {
            if (a.Length != b.Length) return false;

            var intCount = a.Length / CHARS_PER_INT;
            var hasLeftOver = (a.Length % CHARS_PER_INT) != 0;

            using (var aPin = a.Pin())
            using (var bPin = b.Pin())
            {
                var aPtrI = (int*)aPin.Pointer;
                var bPtrI = (int*)bPin.Pointer;

                for (var i = 0; i < intCount; i++)
                {
                    var aI = *aPtrI;
                    var bI = *bPtrI;

                    if (aI != bI)
                    {
                        return false;
                    }

                    aPtrI++;
                    bPtrI++;
                }

                if (hasLeftOver)
                {
                    var aPtrC = (char*)aPtrI;
                    var bPtrC = (char*)bPtrI;

                    var aC = *aPtrC;
                    var bC = *bPtrC;

                    if (aC != bC)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal static int FindChar(ReadOnlyMemory<char> head, int start, char c)
        => FindChar(head.Span, start, c);

        internal static unsafe int FindChar(ReadOnlySpan<char> span, int start, char c)
        {
            var cDoubled = (c << (sizeof(char) * 8)) | c;

            var len = span.Length - start;
            var numInts = len / CHARS_PER_INT;
            var hasLeftOver = (len % CHARS_PER_INT) != 0;

            ref var asRef = ref MemoryMarshal.GetReference(span);

            fixed (char* cPtr = &asRef)
            {
                var cPtrStr = cPtr + start;

                // todo: could be long*?
                var iPtr = (int*)cPtrStr;

                for (var i = 0; i < numInts; i++)
                {
                    var twoChars = *iPtr;

                    // see: https://kevinmontrose.com/2016/04/26/an-optimization-exercise/
                    //      for more on this trick
                    var masked = twoChars ^ cDoubled;
                    var temp = masked & 0x7FFF7FFF;
                    temp = temp + 0x7FFF7FFF;
                    temp = (int)(temp & 0x80008000);
                    temp = temp | masked;
                    temp = temp | 0x7FFF7FFF;
                    temp = ~temp;
                    var hasMatch = temp != 0;

                    if (hasMatch)
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            // little endian, so the rightmost (LS byte) is the logical "first"
                            var c1 = (char)(twoChars);
                            if (c1 == c)
                            {
                                return start + i * 2;
                            }

                            // no need to check second char, by process of elimination
                            return start + i * 2 + 1;
                        }
                        else
                        {
                            // big endian, so the rightmost (LS byte) is the logical "last"
                            var c1 = (char)(twoChars);
                            if (c1 == c)
                            {
                                return start + i * 2 + 1;
                            }

                            // no need to check second char, by process of elimination
                            return start + i * 2;
                        }
                    }

                    iPtr++;
                }

                if (hasLeftOver)
                {
                    var endCPtr = (char*)iPtr;
                    var endC = *endCPtr;

                    if (endC == c)
                    {
                        return start + len - 1;
                    }
                }
            }

            return -1;
        }

        internal static int FindChar(ReadOnlySequence<char> head, int start, char c)
        {
            if (head.IsSingleSegment)
            {
                return FindChar(head.First.Span, start, c);
            }

            var curSegStart = 0;
            var canSearch = false;

            foreach (var cur in head)
            {
                var curSegEnd = curSegStart + cur.Length;

                if (!canSearch)
                {
                    var startInCur = start >= curSegStart && start < curSegEnd;
                    if (startInCur)
                    {
                        canSearch = true;

                        var inFirstLegalSeg = FindChar(cur.Span, start - curSegStart, c);
                        if (inFirstLegalSeg != -1)
                        {
                            return curSegStart + inFirstLegalSeg;
                        }
                    }
                }
                else
                {
                    var inSeg = FindChar(cur.Span, 0, c);
                    if (inSeg != -1)
                    {
                        return curSegStart + inSeg;
                    }
                }

                curSegStart = curSegEnd;
            }

            return -1;
        }

        internal static int FindNeedsEncode<T>(ReadOnlyMemory<char> head, int start, BoundConfiguration<T> config)
            where T : new()
        => FindNeedsEncode(head.Span, start, config);

        internal static int FindNeedsEncode<T>(ReadOnlySpan<char> span, int start, BoundConfiguration<T> config)
            where T : new()
        {
            if(config.CommentChar == null)
            {
                return FindNeedsEncodeNoComment(span, start, config);
            }

            return FindNeedsEncodeWithComment(span, start, config);
        }

        private static unsafe int FindNeedsEncodeNoComment<T>(ReadOnlySpan<char> span, int start, BoundConfiguration<T> config)
            where T:new()
        {
            var sepChar = config.ValueSeparator;
            var escapeValueChar = config.EscapedValueStartAndStop;
            var escapeChar = config.EscapeValueEscapeChar;

            // allocate and initalize with \r and \n
            short* probMap = stackalloc short[PROBABILITY_MAP_SIZE];
            probMap[0] = 9216;
            AddCharacterToProbMap(probMap, sepChar);
            AddCharacterToProbMap(probMap, escapeValueChar);
            AddCharacterToProbMap(probMap, escapeChar);

            fixed (char* charPtr = span.Slice(start))
            {
                char* charPtrMut = charPtr;

                var len = span.Length - start;
                var ix = ProbablyContains(probMap, ref charPtrMut, len);
                if (ix == -1)
                {
                    return -1;
                }

                for (var i = ix; i < len; i++)
                {
                    var c = *charPtrMut;
                    if (c == sepChar || c == '\r' || c == '\n' || c == escapeValueChar || c == escapeChar)
                    {
                        return i + start;
                    }

                    charPtrMut++;
                }
            }

            return -1;
        }

        private static unsafe int FindNeedsEncodeWithComment<T>(ReadOnlySpan<char> span, int start, BoundConfiguration<T> config)
            where T : new()
        {
            var sepChar = config.ValueSeparator;
            var escapeValueChar = config.EscapedValueStartAndStop;
            var escapeChar = config.EscapeValueEscapeChar;
            var commentChar = config.CommentChar.Value;

            // allocate and initalize with \r and \n
            short* probMap = stackalloc short[PROBABILITY_MAP_SIZE];
            probMap[0] = 9216;
            AddCharacterToProbMap(probMap, sepChar);
            AddCharacterToProbMap(probMap, escapeValueChar);
            AddCharacterToProbMap(probMap, escapeChar);
            AddCharacterToProbMap(probMap, commentChar);

            fixed (char* charPtr = span.Slice(start))
            {
                char* charPtrMut = charPtr;

                var len = span.Length - start;
                var ix = ProbablyContains(probMap, ref charPtrMut, len);
                if(ix == -1)
                {
                    return -1;
                }

                for (var i = ix; i < len; i++)
                {
                    var c = *charPtrMut;
                    if (c == sepChar || c == '\r' || c == '\n' || c == escapeValueChar || c == escapeChar || c == commentChar)
                    {
                        return i + start;
                    }

                    charPtrMut++;
                }
            }

            return -1;
        }

        internal static int FindNeedsEncode<T>(ReadOnlySequence<char> head, int start, BoundConfiguration<T> config)
            where T : new()
        {
            if (head.IsSingleSegment)
            {
                return FindNeedsEncode(head.First.Span, start, config);
            }

            var curSegStart = 0;
            var canSearch = false;

            foreach (var cur in head)
            {
                var curSegEnd = curSegStart + cur.Length;

                if (!canSearch)
                {
                    var startInCur = start >= curSegStart && start < curSegEnd;
                    if (startInCur)
                    {
                        canSearch = true;

                        var inFirstLegalSeg = FindNeedsEncode(cur.Span, start - curSegStart, config);
                        if (inFirstLegalSeg != -1)
                        {
                            return curSegStart + inFirstLegalSeg;
                        }
                    }
                }
                else
                {
                    var inSeg = FindNeedsEncode(cur.Span, 0, config);
                    if (inSeg != -1)
                    {
                        return curSegStart + inSeg;
                    }
                }

                curSegStart = curSegEnd;
            }

            return -1;
        }

        // inspired by https://github.com/bbowyersmyth/coreclr/blob/d59b674ee9cd6d092073f9d8d321f935a757e53d/src/classlibnative/bcltype/stringnative.cpp
        private const int PROBABILITY_MAP_SIZE = 16;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int ProbablyContains(short* probMap, ref char* strPtr, int len)
        {
            for(var i = 0; i < len; i++)
            {
                var c = *strPtr;
                var b = (byte)c;

                // 0 to 15
                var ln = (byte)(b & 0x00_00_00_FF);
                var hn = (byte)(b >> 4);

                // based on the low half
                {
                    var mask = (short)(1 << ln);

                    var inMap = (probMap[hn] & mask) != 0;
                    if (!inMap)
                    {
                        strPtr++;
                        continue;
                    }
                }

                return i;
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void AddCharacterToProbMap(short* map, char c)
        {
            var b = (byte)c;

            var ln = (byte)(b & 0x00_00_00_FF);
            var hn = (byte)(b >> 4);

            var mask = (short)(1 << ln);
            map[hn] |= mask;
        }
    }
}