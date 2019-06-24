using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Cesil
{
    internal static class Utils
    {
        // won't return empty entries
        internal static ReadOnlySequence<char> Split(ReadOnlyMemory<char> str, ReadOnlyMemory<char> with)
        {
            var strSpan = str.Span;
            var withSpan = with.Span;

            var ix = FindNextIx(0, strSpan, withSpan);

            if (ix == -1)
            {
                return new ReadOnlySequence<char>(str);
            }

            ReadOnlyCharSegment head = null;
            ReadOnlyCharSegment tail = null;

            var lastIx = 0;
            while (ix != -1)
            {
                var len = ix - lastIx;
                if (len > 0)
                {
                    var subset = str.Slice(lastIx, ix - lastIx);
                    if (head == null)
                    {
                        head = new ReadOnlyCharSegment(subset, len);
                    }
                    else
                    {
                        if (tail == null)
                        {
                            tail = head.Append(subset, len);
                        }
                        else
                        {
                            tail = tail.Append(subset, len);
                        }
                    }
                }

                lastIx = ix + with.Length;
                ix = FindNextIx(lastIx, strSpan, withSpan);
            }

            if (lastIx != str.Length)
            {
                var len = str.Length - lastIx;
                var end = str.Slice(lastIx);
                if (head == null)
                {
                    head = new ReadOnlyCharSegment(end, len);

                }
                else
                {
                    if (tail == null)
                    {
                        tail = head.Append(end, len);
                    }
                    else
                    {
                        tail = tail.Append(end, len);
                    }
                }
            }

            if (head == null)
            {
                return ReadOnlySequence<char>.Empty;
            }

            if (tail == null)
            {
                return new ReadOnlySequence<char>(head.Memory);
            }

            var ret = new ReadOnlySequence<char>(head, 0, tail, tail.Memory.Length);

            return ret;

            // actually figure out the next end
            static int FindNextIx(int startAt, ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle)
            {
                var c = needle[0];
                var lookupFrom = startAt;

tryAgain:

                var ix = FindChar(haystack, lookupFrom, c);
                if (ix == -1) return -1;

                for (var i = 1; i < needle.Length; i++)
                {
                    var readIx = ix + i;
                    if (readIx == haystack.Length)
                    {
                        // past the end
                        return -1;
                    }

                    var shouldMatch = needle[i];
                    var actuallyIs = haystack[readIx];
                    if (shouldMatch != actuallyIs)
                    {
                        lookupFrom = readIx;
                        goto tryAgain;
                    }
                }

                // actually all matched, hooray!
                return ix;
            }
        }

        internal static bool NullReferenceEquality<T>(T a, T b)
            where T : class, IEquatable<T>
        {
            var aNull = ReferenceEquals(a, null);
            var bNull = ReferenceEquals(b, null);

            if (aNull && bNull) return true;
            if (aNull || bNull) return false;

            return a.Equals(b);
        }

        internal static IMemoryOwner<T> RentMustIncrease<T>(MemoryPool<T> pool, int newSize, int oldSize)
        {
            int requestSize;
            if (newSize > pool.MaxBufferSize)
            {
                if (oldSize >= pool.MaxBufferSize)
                {
                    Throw.InvalidOperationException($"Needed a larger memory segment than could be requested, needed {newSize:N0}; {nameof(MemoryPool<T>.MaxBufferSize)} = {pool.MaxBufferSize:N0}");
                }

                requestSize = pool.MaxBufferSize;
            }
            else
            {
                requestSize = newSize;
            }

            return pool.Rent(requestSize);
        }

        private const int CHARS_PER_LONG = sizeof(long) / sizeof(char);
        private const int CHARS_PER_INT = sizeof(int) / sizeof(char);

        internal static unsafe bool AreEqual(ReadOnlyMemory<char> a, ReadOnlyMemory<char> b)
        {
            if (a.Length != b.Length) return false;

            var longCount = a.Length / CHARS_PER_LONG;
            var leftOverAfterLong = a.Length % CHARS_PER_LONG;
            var hasLeftOverInt = leftOverAfterLong >= CHARS_PER_INT;
            var leftOverAfterInt = leftOverAfterLong % CHARS_PER_INT;
            var hasLeftOverChar = leftOverAfterInt != 0;

            using (var aPin = a.Pin())
            using (var bPin = b.Pin())
            {
                var aPtrL = (long*)aPin.Pointer;
                var bPtrL = (long*)bPin.Pointer;

                for (var i = 0; i < longCount; i++)
                {
                    var aL = *aPtrL;
                    var bL = *bPtrL;

                    if (aL != bL)
                    {
                        return false;
                    }

                    aPtrL++;
                    bPtrL++;
                }

                var aPtrI = (int*)aPtrL;
                var bPtrI = (int*)bPtrL;

                if (hasLeftOverInt)
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

                if (hasLeftOverChar)
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

        internal static int FindChar(ReadOnlySpan<char> span, int start, char c)
        {
            var subset = span.Slice(start);
            var ret = FindChar(subset, c);
            if (ret == -1) return -1;

            return ret + start;
        }

        private static unsafe int FindChar(ReadOnlySpan<char> span, char c)
        {
            var cQuad =
                (((ulong)c) << (sizeof(char) * 8 * 3)) |
                (((ulong)c) << (sizeof(char) * 8 * 2)) |
                (((ulong)c) << (sizeof(char) * 8 * 1)) |
                (((ulong)c) << (sizeof(char) * 8 * 0));

            var len = span.Length;
            var longCount = len / CHARS_PER_LONG;
            var leftOverAfterLong = span.Length % CHARS_PER_LONG;
            var hasLeftOverInt = leftOverAfterLong >= CHARS_PER_INT;
            var leftOverAfterInt = leftOverAfterLong % CHARS_PER_INT;
            var hasLeftOverChar = leftOverAfterInt != 0;

            fixed (char* cPtr = span)
            {
                var cPtrStr = cPtr;

                var lPtr = (ulong*)cPtrStr;

                for (var i = 0; i < longCount; i++)
                {
                    var fourChars = *lPtr;

                    // see: https://kevinmontrose.com/2016/04/26/an-optimization-exercise/
                    //      for more on this trick
                    var masked = fourChars ^ cQuad;
                    var temp = masked & 0x7FFF_7FFF_7FFF_7FFFUL;
                    temp = temp + 0x7FFF_7FFF_7FFF_7FFFUL;
                    temp = temp & 0x8000_8000_8000_8000UL;
                    temp = temp | masked;
                    temp = temp | 0x7FFF_7FFF_7FFF_7FFFUL;
                    temp = ~temp;
                    var hasMatch = temp != 0;

                    if (hasMatch)
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            // little endian, so the rightmost (LS byte) is the logical "first"
                            var c1 = (char)(fourChars >> (sizeof(char) * 8 * 0));
                            if (c1 == c)
                            {
                                return i * CHARS_PER_LONG;
                            }

                            var c2 = (char)(fourChars >> (sizeof(char) * 8 * 1));
                            if (c2 == c)
                            {
                                return i * CHARS_PER_LONG + 1;
                            }

                            var c3 = (char)(fourChars >> (sizeof(char) * 8 * 2));
                            if (c3 == c)
                            {
                                return i * CHARS_PER_LONG + 2;
                            }

                            // no need to check last char, by process of elimination
                            return i * CHARS_PER_LONG + 3;
                        }
                        else
                        {
                            // todo: figure out how to test this, and implement?
                            throw new NotImplementedException();
                        }
                    }

                    lPtr++;
                }

                var iPtr = (uint*)lPtr;

                if (hasLeftOverInt)
                {
                    var cDouble = (uint)cQuad;
                    var twoChars = *iPtr;

                    // see: https://kevinmontrose.com/2016/04/26/an-optimization-exercise/
                    //      for more on this trick
                    var masked = twoChars ^ cDouble;
                    var temp = masked & 0x7FFF_7FFFU;
                    temp = temp + 0x7FFF_7FFFU;
                    temp = temp & 0x8000_8000U;
                    temp = temp | masked;
                    temp = temp | 0x7FFF_7FFFU;
                    temp = ~temp;
                    var hasMatch = temp != 0;

                    if (hasMatch)
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            // little endian, so the rightmost (LS byte) is the logical "first"
                            var c1 = (char)(twoChars >> (sizeof(char) * 8 * 0));
                            if (c1 == c)
                            {
                                return longCount * CHARS_PER_LONG;
                            }

                            // no need to check last char, by process of elimination
                            return longCount * CHARS_PER_LONG + 1;
                        }
                        else
                        {
                            // todo: figure out how to test this, and implement?
                            throw new NotImplementedException();
                        }
                    }

                    iPtr++;
                }

                if (hasLeftOverChar)
                {
                    var endCPtr = (char*)iPtr;
                    var endC = *endCPtr;

                    if (endC == c)
                    {
                        return len - 1;
                    }
                }
            }

            return -1;
        }

        internal static int FindChar(ReadOnlySequence<char> head, int start, char c)
        {
            int ret;

            if (head.IsSingleSegment)
            {
                ret = FindChar(head.First.Span.Slice(start), c);
            }
            else
            {
                ret = FindChar(head.Slice(start), c);
            }

            if (ret == -1) return -1;

            return ret + start;
        }

        private static int FindChar(ReadOnlySequence<char> head, char c)
        {
            if (head.IsSingleSegment)
            {
                return FindChar(head.First.Span, c);
            }

            var curSegStart = 0;

            foreach (var cur in head)
            {
                var curSegEnd = curSegStart + cur.Length;

                var inSeg = FindChar(cur.Span, c);
                if (inSeg != -1)
                {
                    return curSegStart + inSeg;
                }

                curSegStart = curSegEnd;
            }

            return -1;
        }

        internal static int FindNeedsEncode<T>(ReadOnlyMemory<char> head, int start, BoundConfigurationBase<T> config)
        => FindNeedsEncode(head.Span, start, config);

        internal static int FindNeedsEncode<T>(ReadOnlySpan<char> span, int start, BoundConfigurationBase<T> config)
        {
            var subset = span.Slice(start);
            int ret;

            if (config.CommentChar == null)
            {
                ret = FindNeedsEncodeNoComment(subset, config);
            }
            else
            {
                ret = FindNeedsEncodeWithComment(subset, config);
            }

            if (ret == -1) return -1;

            return ret + start;
        }

        private static unsafe int FindNeedsEncodeNoComment<T>(ReadOnlySpan<char> span, BoundConfigurationBase<T> config)
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

            fixed (char* charPtr = span)
            {
                char* charPtrMut = charPtr;

                var len = span.Length;
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
                        return i;
                    }

                    charPtrMut++;
                }
            }

            return -1;
        }

        private static unsafe int FindNeedsEncodeWithComment<T>(ReadOnlySpan<char> span, BoundConfigurationBase<T> config)
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

            fixed (char* charPtr = span)
            {
                char* charPtrMut = charPtr;

                var len = span.Length;
                var ix = ProbablyContains(probMap, ref charPtrMut, len);
                if (ix == -1)
                {
                    return -1;
                }

                for (var i = ix; i < len; i++)
                {
                    var c = *charPtrMut;
                    if (c == sepChar || c == '\r' || c == '\n' || c == escapeValueChar || c == escapeChar || c == commentChar)
                    {
                        return i;
                    }

                    charPtrMut++;
                }
            }

            return -1;
        }

        internal static int FindNeedsEncode<T>(ReadOnlySequence<char> head, int start, BoundConfigurationBase<T> config)
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

        public static string Encode<T>(string rawStr, BoundConfigurationBase<T> config)
        {
            // assume there's a single character that needs escape, so 2 chars for the start and stop and 1 for the escape
            var defaultSize = rawStr.Length + 2 + 1;

            var pool = config.MemoryPool;
            var escapeChar = config.EscapeValueEscapeChar;

            var raw = rawStr.AsMemory();
            var retOwner = config.MemoryPool.Rent(defaultSize);
            try
            {
                retOwner.Memory.Span[0] = config.EscapedValueStartAndStop;

                var rawIx = 0;
                var destIx = 1;

                while (rawIx < raw.Length)
                {
                    var copyUntil = FindChar(raw, rawIx, escapeChar);
                    if (copyUntil == -1)
                    {
                        var lenToCopy = raw.Length - rawIx;
                        destIx += CopyIntoRet(pool, ref retOwner, raw, rawIx, destIx, lenToCopy);
                        break;
                    }

                    destIx += CopyIntoRet(pool, ref retOwner, raw, rawIx, destIx, copyUntil - rawIx);
                    destIx += AddEscapedChar(pool, ref retOwner, raw.Span[copyUntil], escapeChar, destIx);
                    rawIx = copyUntil + 1;
                }

                var curLen = retOwner.Memory.Length;
                if (destIx == curLen)
                {
                    Resize(pool, ref retOwner, curLen + 1);
                }

                var retSpan = retOwner.Memory.Span;

                retSpan[destIx] = config.EscapedValueStartAndStop;
                destIx++;

                var retStr = new string(retSpan.Slice(0, destIx));

                return retStr;
            }
            finally
            {
                retOwner.Dispose();
            }

            // blit string bits into destOwner, resizing if necessary
            // returns the number of characters copied
            static int CopyIntoRet(MemoryPool<char> pool, ref IMemoryOwner<char> destOwner, ReadOnlyMemory<char> raw, int copyFrom, int copyTo, int copyLength)
            {
                var neededLength = copyTo + copyLength;
                if (neededLength > destOwner.Memory.Length)
                {
                    // + 1 'cause we need space for the trailing escape end character
                    Resize(pool, ref destOwner, neededLength + 1);
                }

                var dest = destOwner.Memory;

                raw.Slice(copyFrom, copyLength).CopyTo(dest.Slice(copyTo));

                return copyLength;
            }

            // encode the needed char into destOwner, resizing if necessary
            // returns the number of characters written
            static int AddEscapedChar(MemoryPool<char> pool, ref IMemoryOwner<char> destOwner, char needsEscape, char escapeChar, int copyTo)
            {
                var neededLength = copyTo + 2;
                if (neededLength > destOwner.Memory.Length)
                {
                    // + 1 'cause we need space for the trailing escape end character
                    Resize(pool, ref destOwner, neededLength + 1);
                }

                var dest = destOwner.Memory.Span;
                dest[copyTo] = escapeChar;
                dest[copyTo + 1] = needsEscape;

                return 2;
            }

            // update oldOwner to be a rented memory with the given size,
            //   copies all the old values over
            static void Resize(MemoryPool<char> pool, ref IMemoryOwner<char> oldOwner, int newLength)
            {
                var newOwner = pool.Rent(newLength);
                oldOwner.Memory.CopyTo(newOwner.Memory);

                oldOwner.Dispose();

                oldOwner = newOwner;
            }
        }

        // inspired by https://github.com/bbowyersmyth/coreclr/blob/d59b674ee9cd6d092073f9d8d321f935a757e53d/src/classlibnative/bcltype/stringnative.cpp
        private const int PROBABILITY_MAP_SIZE = 16;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int ProbablyContains(short* probMap, ref char* strPtr, int len)
        {
            for (var i = 0; i < len; i++)
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