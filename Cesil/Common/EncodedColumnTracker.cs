using System;
using System.Buffers;
using System.Runtime.InteropServices;

using static Cesil.DisposableHelper;

namespace Cesil
{

    /// <summary>
    /// Helper for tracking column names.  Stores two versions if the 
    ///   column name needed to be encoded, otherwise just stores one.
    ///   
    /// Backed by a Memory(char), low offsets have string data and high offsets
    ///    have pointers into the slab.
    ///    
    /// Data
    /// ----
    ///  (string data 0)
    ///  (string data 1)
    ///  (string data 2)
    ///  ...
    ///  (index of string 2) 
    ///  (index of string 1)
    ///  (index of string 0)
    ///  
    /// We store indexes in reverse order so both data and indexes can grow towards
    ///   the center.
    ///   
    /// If index is positive, the name is only stored once (ie. encoded and unencoded are the same)
    ///   if it is negative then we store encoded first then unencoded.  We also increment indexes by 1 
    ///   (before any negation) so we can distinguish between 0 and -0.
    /// 
    /// The length of the total chunk of string data can be figured out from available memory and the
    ///   offset of the following string data chunk (if any).  If the encoded and unencoded string are the
    ///   same, then the string is just the data.  If they are different, then the unencoded string appears
    ///   first and prefixed by an int with it's length - the encoded string is not prefixed with it's length.
    ///   
    /// All and all, this means that for the common case where column is not encoded this will only
    ///   store (1 int + the char data).
    /// </summary>
    internal struct EncodedColumnTracker : ITestableDisposable
    {
        private const int CHARS_PER_INT = sizeof(int) / sizeof(char);

        private IMemoryOwner<char>? Owner;

        // internal for testing purposes
        internal Memory<char> Memory;

        private int EndOfStringData;

        public bool IsDisposed { get; private set; }

        private int _Length;
        internal int Length
        {
            get
            {
                AssertNotDisposedInternal(this);
                return _Length;
            }
        }

        internal void Add(string colName, string? encodedColName, MemoryPool<char> pool)
        {
            AssertNotDisposedInternal(this);

            var usedSoFar = Length * CHARS_PER_INT + EndOfStringData;

            var needed =
                usedSoFar +
                CHARS_PER_INT +                                     // space for the new index
                (encodedColName != null ? CHARS_PER_INT : 0) +      // space for length of unencoded name (only needed if we're storing two strings)
                colName.Length +                                    // space for the column name
                (encodedColName?.Length ?? 0);                      // include space for the encoded column only if it's present

            GrowIfNeeded(needed, pool);

            var offset = EndOfStringData;

            var memSpan = Memory.Span;

            if (encodedColName == null)
            {
                // just store the string data
                var colSpan = colName.AsSpan();
                colSpan.CopyTo(memSpan[EndOfStringData..]);
                EndOfStringData += colSpan.Length;
            }
            else
            {
                // need to store
                //  - length of colName
                //  - colName data
                //  - encodedColName data

                // get the length in there
                var colNameLengthSpan = MemoryMarshal.Cast<char, int>(memSpan.Slice(EndOfStringData, CHARS_PER_INT));
                colNameLengthSpan[0] = colName.Length;
                EndOfStringData += CHARS_PER_INT;

                // get colName in there
                colName.AsSpan().CopyTo(memSpan[EndOfStringData..]);
                EndOfStringData += colName.Length;

                // get encodedColName in there
                encodedColName.AsSpan().CopyTo(memSpan[EndOfStringData..]);
                EndOfStringData += encodedColName.Length;
            }

            var indexOffset = Memory.Length - (Length + 1) * CHARS_PER_INT;
            var intSpan = MemoryMarshal.Cast<char, int>(memSpan.Slice(indexOffset, CHARS_PER_INT));
            intSpan[0] = ToIndex(offset, encodedColName != null);

            _Length++;
        }

        internal ReadOnlyMemory<char> GetColumnAt(int index)
        {
            AssertNotDisposedInternal(this);

            if (index < 0 || index >= Length)
            {
                Throw.ArgumentOutOfRangeException(nameof(index), index, Length);
            }

            var memSpan = Memory.Span;

            CalculateStringDetails(EndOfStringData, Length, index, memSpan, out var stringDataStart, out var stringDataLength, out var hasEncodedName);

            if (hasEncodedName)
            {
                var stringDataSpan = memSpan.Slice(stringDataStart, stringDataLength);

                var intDataSpan = MemoryMarshal.Cast<char, int>(stringDataSpan[..CHARS_PER_INT]);
                var colNameLength = intDataSpan[0];

                return Memory.Slice(stringDataStart + CHARS_PER_INT, colNameLength);
            }
            else
            {
                return Memory.Slice(stringDataStart, stringDataLength);
            }
        }

        internal ReadOnlyMemory<char> GetEncodedColumnAt(int index)
        {
            AssertNotDisposedInternal(this);

            if (index < 0 || index >= Length)
            {
                Throw.ArgumentOutOfRangeException(nameof(index), index, Length);
            }

            var memSpan = Memory.Span;

            CalculateStringDetails(EndOfStringData, Length, index, memSpan, out var stringDataStart, out var stringDataLength, out var hasEncodedName);

            if (hasEncodedName)
            {
                var stringDataSpan = memSpan.Slice(stringDataStart, stringDataLength);

                var intDataSpan = MemoryMarshal.Cast<char, int>(stringDataSpan[..CHARS_PER_INT]);
                var colNameLength = intDataSpan[0];

                var encodedLength = stringDataLength - CHARS_PER_INT - colNameLength;

                return Memory.Slice(stringDataStart + CHARS_PER_INT + colNameLength, encodedLength);
            }
            else
            {
                return Memory.Slice(stringDataStart, stringDataLength);
            }
        }

        private static void CalculateStringDetails(int endOfStringData, int length, int index, ReadOnlySpan<char> memSpan, out int stringDataStart, out int stringDataLength, out bool hasEncodedName)
        {
            var indexRaw = GetRawIndex(index, memSpan);
            FromIndex(indexRaw, out stringDataStart, out hasEncodedName);

            int startOfNextString;
            if (index == (length - 1))
            {
                startOfNextString = endOfStringData;
            }
            else
            {
                var nextIndexRaw = GetRawIndex(index + 1, memSpan);
                FromIndex(nextIndexRaw, out startOfNextString, out _);
            }

            stringDataLength = startOfNextString - stringDataStart;
        }

        private static int GetRawIndex(int index, ReadOnlySpan<char> memSpan)
        {
            var indexOffset = (index + 1) * CHARS_PER_INT;
            var startOffset = memSpan.Length - indexOffset;
            var intSpan = MemoryMarshal.Cast<char, int>(memSpan.Slice(startOffset, CHARS_PER_INT));
            var indexRaw = intSpan[0];

            return indexRaw;
        }

        private static int ToIndex(int stringDataStart, bool hasEncodedName)
        {
            var ret = stringDataStart + 1;
            if (hasEncodedName)
            {
                ret = -ret;
            }

            return ret;
        }

        private static void FromIndex(int index, out int stringDataStart, out bool hasEncodedName)
        {
            hasEncodedName = index < 0;
            stringDataStart = Math.Abs(index) - 1;
        }

        private void GrowIfNeeded(int neededSize, MemoryPool<char> pool)
        {
            if (Owner == null)
            {
                Owner = pool.Rent(neededSize);
                Memory = Owner.Memory;

                return;
            }

            if (Memory.Length >= neededSize)
            {
                return;
            }

            var newOwner = pool.Rent(neededSize);
            var newMem = newOwner.Memory;
            var newSpan = newMem.Span;

            var oldSpan = Memory.Span;

            // copy the string data to the front of the new memory
            oldSpan[..EndOfStringData].CopyTo(newSpan[..EndOfStringData]);

            // copy the indexes to the end of the new memory
            var offsetLength = Length * CHARS_PER_INT;
            oldSpan[(^offsetLength)..].CopyTo(newSpan[(^offsetLength)..]);

            // swap over
            Owner.Dispose();
            Owner = newOwner;
            Memory = newMem;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Owner?.Dispose();

                Owner = null;
                Memory = default;
            }
        }
    }
}
