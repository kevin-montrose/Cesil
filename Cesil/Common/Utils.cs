using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Cesil
{
    internal static class Utils
    {
        // try and size the buffers so we get a whole page to ourselves
        private const int OVERHEAD_BYTES = 16;
        private const int PAGE_SIZE_BYTES = 4098;
        internal const int DEFAULT_BUFFER_SIZE = (PAGE_SIZE_BYTES / sizeof(char)) - OVERHEAD_BYTES;

        private static class LegalFlagEnum<T>
            where T : unmanaged, Enum
        {
            // has all the bits set that are present in 
            internal static readonly byte Mask = CreateMask();
            internal static readonly byte AntiMask = (byte)~CreateMask();

            private static byte CreateMask()
            {
                var values = Enum.GetValues(typeof(T));
                byte ret = 0;
                for (var i = 0; i < values.Length; i++)
                {
                    var o = values.GetValue(i);
                    if (o == null)
                    {
                        return Throw.ImpossibleException<byte>("Shouldn't be possible");
                    }

                    ret |= (byte)o;
                }

                return ret;
            }
        }

        internal static ReadOnlyMemory<char> TrimLeadingWhitespace(ReadOnlyMemory<char> mem)
        {
            var skip = 0;
            var span = mem.Span;
            var len = span.Length;

            while (skip < len)
            {
                var c = span[skip];
                if (!char.IsWhiteSpace(c)) break;

                skip++;
            }

            if (skip == 0) return mem;
            if (skip == len) return ReadOnlyMemory<char>.Empty;

            return mem.Slice(skip);
        }

        internal static ReadOnlyMemory<char> TrimTrailingWhitespace(ReadOnlyMemory<char> mem)
        {
            var span = mem.Span;
            var len = span.Length;
            var start = len - 1;
            var skip = start;

            while (skip >= 0)
            {
                var c = span[skip];
                if (!char.IsWhiteSpace(c)) break;

                skip--;
            }

            if (skip == start) return mem;
            if (skip == -1) return ReadOnlyMemory<char>.Empty;


            return mem.Slice(0, skip + 1);
        }

        internal static bool IsLegalFlagEnum<T>(T e)
            where T : unmanaged, Enum
        {
            byte eAsByte;

            unsafe
            {
                T* ePtr = &e;
                byte* eBytePtr = (byte*)ePtr;

                eAsByte = *eBytePtr;
            }

            if (eAsByte == 0) return true;

            var anySet = (eAsByte & LegalFlagEnum<T>.Mask) != 0;
            var unsetSet = (eAsByte & LegalFlagEnum<T>.AntiMask) != 0;

            return anySet && !unsetSet;
        }

        // check if reading into arg is going to do something "weird" because it's a known immutable collection
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CheckImmutableReadInto<TCollection, TItem>(TCollection arg, string argName)
            where TCollection : ICollection<TItem>
        {
            var isImmutable =
                arg is ImmutableArray<TItem> ||
                arg is ImmutableList<TItem> ||
                arg is ImmutableHashSet<TItem> ||
                arg is ImmutableSortedSet<TItem>;

            if (isImmutable)
            {
                Throw.ArgumentException<object>("Pass a builder to create immutable collections; passed an immutable collection directly, which will not reflect mutations", argName);
                return;
            }
        }

        // Use this when we're validating parameters that the type system
        //   thinks are non-null but we know a USER could subvert
        //
        // Places where the type system thinks we're nullable, don't use this.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CheckArgumentNull<T>(T arg, string argName)
            where T : class
        {
            if (arg == null)
            {
                Throw.ArgumentNullException<object>(argName);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T NonNull<T>(T? toCheck)
            where T : class
        {
            if (toCheck == null)
            {
                return Throw.ImpossibleException<T>("Expected non-null value, but found null");
            }

            return toCheck;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T NonNullValue<T>(T? toCheck)
            where T : struct
        {
            if (toCheck == null)
            {
                return Throw.ImpossibleException<T>("Expected non-null value, but found null");
            }

            return toCheck.Value;
        }

        internal static int FindNextIx(int startAt, ReadOnlyMemory<char> haystack, ReadOnlyMemory<char> needle)
        => FindNextIx(startAt, haystack.Span, needle.Span);

        internal static int FindNextIx(int startAt, ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle)
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

        internal static bool NullReferenceEquality<T>(T? a, T? b)
            where T : class, IEquatable<T>
        {
            var aNull = ReferenceEquals(a, null);
            var bNull = ReferenceEquals(b, null);

            if (aNull && bNull) return true;
            if (aNull || bNull) return false;

#pragma warning disable CES0005 // we've actually checked that a is non-null, but in a way the compiler can't follow
            return a!.Equals(b);
#pragma warning restore CES0005
        }

        internal static IMemoryOwner<T> RentMustIncrease<T>(MemoryPool<T> pool, int newSize, int oldSize)
        {
            int requestSize;
            if (newSize > pool.MaxBufferSize)
            {
                if (oldSize >= pool.MaxBufferSize)
                {
                    return Throw.InvalidOperationException<IMemoryOwner<T>>($"Needed a larger memory segment than could be requested, needed {newSize:N0}; {nameof(MemoryPool<T>.MaxBufferSize)} = {pool.MaxBufferSize:N0}");
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

        internal static bool AreEqual(ReadOnlyMemory<char> a, ReadOnlyMemory<char> b)
        => AreEqual(a.Span, b.Span);

        internal static unsafe bool AreEqual(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            var aLen = a.Length;
            var bLen = b.Length;
            if (aLen != bLen) return false;

            fixed (char* aPin = a)
            fixed (char* bPin = b)
            {
                return AreEqual(aLen, aPin, bPin);
            }
        }

        internal static unsafe bool AreEqual(int length, void* aPtr, void* bPtr)
        {
            if (length == 0) return true;

            var longCount = length / CHARS_PER_LONG;
            var leftOverAfterLong = length % CHARS_PER_LONG;
            var hasLeftOverInt = leftOverAfterLong >= CHARS_PER_INT;
            var leftOverAfterInt = leftOverAfterLong % CHARS_PER_INT;
            var hasLeftOverChar = leftOverAfterInt != 0;

            var aPtrL = (long*)aPtr;
            var bPtrL = (long*)bPtr;

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

        internal static int Find(ReadOnlySpan<char> span, int start, string str)
        {
            if (str.Length == 1)
            {
                return FindChar(span, start, str[0]);
            }

            var c1 = str[0];

            var subset = span.Slice(start);
            var ix = 0;
            while (true)
            {
                var nextIx = FindChar(subset, ix, c1);
                if (nextIx == -1)
                {
                    return -1;
                }

                if (str.Length + nextIx > subset.Length)
                {
                    return -1;
                }

                var tryAgain = false;
                for (var i = 1; i < str.Length; i++)
                {
                    if (str[i] != subset[nextIx + i])
                    {
                        ix = nextIx + i;
                        tryAgain = true;
                        break;
                    }
                }

                if (tryAgain)
                {
                    continue;
                }

                return nextIx + start;
            }
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
                    temp += 0x7FFF_7FFF_7FFF_7FFFUL;
                    temp &= 0x8000_8000_8000_8000UL;
                    temp |= masked;
                    temp |= 0x7FFF_7FFF_7FFF_7FFFUL;
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
                            // todo: figure out how to test this, and implement? (tracking issue: https://github.com/kevin-montrose/Cesil/issues/2)
                            return Throw.NotImplementedException<int>("BigEndian support has not been implemented; see https://github.com/kevin-montrose/Cesil/issues/2");
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
                    temp += 0x7FFF_7FFFU;
                    temp &= 0x8000_8000U;
                    temp |= masked;
                    temp |= 0x7FFF_7FFFU;
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
                            // todo: figure out how to test this, and implement? (tracking issue: https://github.com/kevin-montrose/Cesil/issues/2)
                            return Throw.NotImplementedException<int>("BigEndian support has not been implemented; see https://github.com/kevin-montrose/Cesil/issues/2");
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

        internal static int Find(ReadOnlySequence<char> head, string str)
        {
            if (head.IsSingleSegment)
            {
                return Find(head.First.Span, 0, str);
            }

            var curSegStart = 0;

            foreach (var cur in head)
            {
                var curSegEnd = curSegStart + cur.Length;

                var inSeg = Find(cur.Span, 0, str);
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

        internal static unsafe int FindNeedsEncode<T>(ReadOnlySpan<char> span, int start, BoundConfigurationBase<T> config)
        {
            var subset = span.Slice(start);

            fixed (char* subsetPtr = subset)
            {
                var ret = config.NeedsEncode.ContainsCharRequiringEncoding(subsetPtr, subset.Length);

                if (ret == -1) return -1;

                return ret + start;
            }
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

        internal static string Encode(string rawStr, Options options, MemoryPool<char> pool)
        {
            // assume there's a single character that needs escape, so 2 chars for the start and stop and 1 for the escape
            var defaultSize = rawStr.Length + 2 + 1;

            var escapedValueStartAndStop = options.EscapedValueStartAndEnd;

            if (escapedValueStartAndStop == null)
            {
                return Throw.ImpossibleException<string>("Attempted to encode a string without a configured escape char, shouldn't be possible", options);
            }

            var escapeChar = options.EscapedValueEscapeCharacter;

            if (escapeChar == null)
            {
                return Throw.ImpossibleException<string>("Attempted to encode a string without a configured escape sequence start char, shouldn't be possible", options);
            }

            var raw = rawStr.AsMemory();
            var retOwner = pool.Rent(defaultSize);
            try
            {
                retOwner.Memory.Span[0] = escapedValueStartAndStop.Value;

                var rawIx = 0;
                var destIx = 1;

                while (rawIx < raw.Length)
                {
                    var copyUntil = FindChar(raw, rawIx, escapeChar.Value);
                    if (copyUntil == -1)
                    {
                        var lenToCopy = raw.Length - rawIx;
                        destIx += CopyIntoRet(pool, ref retOwner, raw, rawIx, destIx, lenToCopy);
                        break;
                    }

                    destIx += CopyIntoRet(pool, ref retOwner, raw, rawIx, destIx, copyUntil - rawIx);
                    destIx += AddEscapedChar(pool, ref retOwner, raw.Span[copyUntil], escapeChar.Value, destIx);
                    rawIx = copyUntil + 1;
                }

                var curLen = retOwner.Memory.Length;
                if (destIx == curLen)
                {
                    Resize(pool, ref retOwner, curLen + 1);
                }

                var retSpan = retOwner.Memory.Span;

                retSpan[destIx] = escapedValueStartAndStop.Value;
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

        internal static ExtraColumnTreatment EffectiveColumnTreatmentForStatic<T>(ConcreteBoundConfiguration<T> config)
        {
            var ect = config.Options.ExtraColumnTreatment;

            switch (ect)
            {
                // no difference for static cases
                case ExtraColumnTreatment.Ignore:
                case ExtraColumnTreatment.IncludeDynamic:
                    return ExtraColumnTreatment.Ignore;
                case ExtraColumnTreatment.ThrowException:
                    return ExtraColumnTreatment.ThrowException;
                default:
                    return Throw.ImpossibleException<ExtraColumnTreatment, T>($"Unexpected {nameof(ExtraColumnTreatment)}: {ect}", config);
            }
        }

        internal static Memory<DynamicCellValue> GetCells(MemoryPool<DynamicCellValue> arrPool, ref IMemoryOwner<DynamicCellValue>? buffer, ITypeDescriber describer, in WriteContext context, object rowAsObj)
        {
tryAgain:
            var bufferMem = buffer?.Memory ?? Memory<DynamicCellValue>.Empty;
            var bufferSpan = bufferMem.Span;

            var numCells = describer.GetCellsForDynamicRow(context, rowAsObj, bufferSpan);
            if (numCells > bufferSpan.Length)
            {
                if (buffer != null)
                {
                    buffer.Dispose();
                }
                buffer = arrPool.Rent(numCells);
                goto tryAgain;
            }

            return bufferMem[..numCells];
        }

        internal static void ForceInOrder(
            (string Name, string EncodedName)[] columnNamesValue,
            NonNull<Comparison<DynamicCellValue>> columnNameSorter,
            Memory<DynamicCellValue> raw
        )
        {
            // no headers mean we write whatever we're given
            if (columnNamesValue.Length == 0)
            {
                return;
            }

            var rawSpan = raw.Span;

            var inOrder = true;

            var i = 0;
            foreach (var x in rawSpan)
            {
                if (i == columnNamesValue.Length)
                {
                    Throw.InvalidOperationException<Memory<DynamicCellValue>>("Too many cells returned, could not place in desired order");
                    return;
                }

                var (name, _) = columnNamesValue[i];
                if (!name.Equals(x.Name))
                {
                    inOrder = false;
                    break;
                }

                i++;
            }

            // already in order
            if (inOrder)
            {
                return;
            }

            var comparer = columnNameSorter.Value;

            Sort(rawSpan, comparer);
        }

        // todo: once MemoryExtensions.Sort() lands we can remove all of this (add tracking issue?)
        //       coming as part of .NET 5, as a consequence of https://github.com/dotnet/runtime/issues/19969
        internal static void Sort<T>(Span<T> span, Comparison<T> comparer)
        {
            // crummy quick sort implementation, all of this should get killed

            var len = span.Length;

            if (len <= 1)
            {
                return;
            }

            if (len == 2)
            {
                var a = span[0];
                var b = span[1];

                var res = comparer(a, b);
                if (res > 0)
                {
                    span[0] = b;
                    span[1] = a;
                }

                return;
            }

            // we only ever call this when the span isn't _already_ sorted,
            //    so our sort can be really dumb
            // basically Lomuto (see: https://en.wikipedia.org/wiki/Quicksort#Lomuto_partition_scheme)

            var splitIx = Partition(span, comparer);

            var left = span[..splitIx];
            var right = span[(splitIx + 1)..];

            Sort(left, comparer);
            Sort(right, comparer);

            // re-order subSpan such that items before the returned index are less than the value
            //    at the returned index
            static int Partition(Span<T> subSpan, Comparison<T> comparer)
            {
                var len = subSpan.Length;

                var pivotIx = len - 1;
                var pivotItem = subSpan[pivotIx];

                var i = 0;

                for (var j = 0; j < len; j++)
                {
                    var item = subSpan[j];
                    var res = comparer(item, pivotItem);

                    if (res < 0)
                    {
                        Swap(subSpan, i, j);
                        i++;
                    }
                }

                Swap(subSpan, i, pivotIx);

                return i;
            }

            static void Swap(Span<T> subSpan, int i, int j)
            {
                var oldI = subSpan[i];
                subSpan[i] = subSpan[j];
                subSpan[j] = oldI;
            }
        }
    }
}