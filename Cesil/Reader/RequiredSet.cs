using System;
using System.Buffers;

namespace Cesil
{
    internal struct RequiredSet : ITestableDisposable
    {
        private const int CHARS_PER_INT = sizeof(int) / sizeof(char);
        private const int CHARS_PER_LONG = sizeof(long) / sizeof(char);

        private const int BITS_PER_CHAR = sizeof(char) * 8;
        private const int BITS_PER_LONG = sizeof(long) * 8;
        private const int BITS_PER_INT = sizeof(int) * 8;

        private IMemoryOwner<char>? Owner;

        private readonly int LongsInLength;
        private readonly bool HasIntInLength;
        private readonly bool HasCharInLength;

        private readonly Memory<char> Required;
        private readonly Memory<char> Set;

        public bool IsDisposed => Owner == null;

        internal RequiredSet(MemoryPool<char> pool, int numMembers)
        {
            var neededChars = (numMembers / BITS_PER_CHAR);
            if ((numMembers % BITS_PER_CHAR > 0))
            {
                neededChars++;
            }

            var space = neededChars * 2;

            Owner = pool.Rent(space);

            Owner.Memory.Span.Slice(0, space).Clear();

            Required = Owner.Memory.Slice(0, neededChars);
            Set = Owner.Memory.Slice(neededChars, neededChars);

            LongsInLength = neededChars / CHARS_PER_LONG;

            var leftOver = neededChars % CHARS_PER_LONG;

            if (leftOver >= CHARS_PER_INT)
            {
                HasIntInLength = true;
                leftOver -= CHARS_PER_INT;
            }
            else
            {
                HasIntInLength = false;
            }

            HasCharInLength = leftOver != 0;
        }

        internal void ClearRequired()
        {
            if (IsDisposed) return;

            if (Owner != null)
            {
                Owner.Memory.Span.Clear();
            }
        }

        internal void SetIsRequired(int ix)
        {
            if (IsDisposed) return;

            SetImpl(ix, Required);
        }

        internal void MarkSet(int ix)
        {
            if (IsDisposed) return;

            SetImpl(ix, Set);
        }

        internal unsafe bool CheckRequiredAndClear(out int firstMissingRequired)
        {
            if (IsDisposed)
            {
                firstMissingRequired = -1;
                return true;
            }

            // todo: is there a way to test that this won't read out of bounds?
            fixed (char* requiredCharPtrConst = Required.Span)
            fixed (char* setCharPtrConst = Set.Span)
            {
                long* requiredLongPtr = (long*)requiredCharPtrConst;
                long* setLongPtr = (long*)setCharPtrConst;

                for (var i = 0; i < LongsInLength; i++)
                {
                    var longDiff = (*requiredLongPtr) ^ (*setLongPtr);

                    if (longDiff != 0)
                    {
                        // it's fine to be slow, this is a "we're going to throw an exception"-case

                        var cur = (ulong)longDiff;
                        var missingIx = 0;

                        while ((cur & 0x1) == 0)
                        {
                            missingIx++;
                            cur = cur >> 1;
                        }

                        Set.Span.Clear();

                        firstMissingRequired = i * BITS_PER_LONG + missingIx;
                        return false;
                    }

                    requiredLongPtr++;
                    setLongPtr++;
                }

                int* requiredIntPtr = (int*)requiredLongPtr;
                int* setIntPtr = (int*)setLongPtr;

                if (HasIntInLength)
                {
                    var intDiff = (*requiredIntPtr) ^ (*setIntPtr);

                    if (intDiff != 0)
                    {
                        // it's fine to be slow, this is a "we're going to throw an exception"-case

                        var cur = (uint)intDiff;
                        var missingIx = 0;

                        while ((cur & 0x1) == 0)
                        {
                            missingIx++;
                            cur = cur >> 1;
                        }

                        Set.Span.Clear();

                        firstMissingRequired = LongsInLength * BITS_PER_LONG + missingIx;
                        return false;
                    }

                    requiredIntPtr++;
                    setIntPtr++;
                }

                char* requiredCharPtr = (char*)requiredIntPtr;
                char* setCharPtr = (char*)setIntPtr;

                if (HasCharInLength)
                {
                    var charDiff = (char)((*requiredCharPtr) ^ (*setCharPtr));

                    if (charDiff != 0)
                    {
                        var cur = (uint)charDiff;
                        var missingIx = 0;

                        while ((cur & 0x1) == 0)
                        {
                            missingIx++;
                            cur = cur >> 1;
                        }

                        Set.Span.Clear();

                        firstMissingRequired = LongsInLength * BITS_PER_LONG + (HasIntInLength ? BITS_PER_INT : 0) + missingIx;
                        return false;
                    }
                }
            }

            Set.Span.Clear();

            firstMissingRequired = -1;
            return true;
        }

        private static void SetImpl(int ix, Memory<char> inMemory)
        {
            var charIx = ix / BITS_PER_CHAR;
            var bitIx = ix % BITS_PER_CHAR;

            var span = inMemory.Span;

            var mask = (char)(1 << bitIx);

            span[charIx] |= mask;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                Owner?.Dispose();
                Owner = null;
            }
        }
    }
}
