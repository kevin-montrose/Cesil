using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static Cesil.DisposableHelper;

namespace Cesil
{
    // contains bits necessary for ordering 
    //  some names and values during creation
    //  without allocating on the heap
    internal partial struct NameLookup
    {
        /// <summary>
        /// In order string-int key values
        /// 
        /// Data is stored like:
        ///  - (index of string) (value for string)
        ///  - (inded of string) (value for string)
        ///  - ...
        ///  - (length of string)
        ///  - (string data)
        ///  - (value for string)
        ///  - (length of string)
        ///  - (string data)
        ///  - (value for string)
        /// 
        /// So, it looks like:
        ///  - index of string 0
        ///  - index of string 1
        ///  - index of string 2
        ///  - ...
        ///  - (length and string and value data)
        ///  - (length and string and value data)
        ///  - (length and string and value data)
        /// 
        /// The indexes at the front are stored sorted, but everything else isn't.  That means
        ///   that whenever a value is inserted, the indexes at the front may move around
        ///   but once written none of the string data is ever moved.
        /// 
        /// Strings are referred to by absolute indexes.  Offsets would be more efficient
        ///   if we resized the array, but since we pre-allocate the correct size
        ///   it's just extra math.
        /// 
        /// The variable length data being at the end and _not_ being in order
        ///   let's the ordering algorithm swap values just by moving fixed size
        ///   chunks around, never having to move string data.
        /// </summary>
        internal struct OrdererNames : ITestableDisposable
        {
            private const int CHARS_PER_INT = sizeof(int) / sizeof(char);

            private IMemoryOwner<char> Owner;

            internal readonly ReadOnlyMemory<char> Memory;
            internal readonly int Count;

            public bool IsDisposed => Owner == EmptyMemoryOwner.Singleton;

            internal (ReadOnlyMemory<char> Name, int Index) this[int index]
            {
                get
                {
                    AssertNotDisposedInternal(this);

                    if (index < 0 || index >= Count)
                    {
                        return Throw.ArgumentOutOfRangeException<(ReadOnlyMemory<char>, int)>(nameof(index), index, Count);
                    }

                    var memSpan = Memory.Span;

                    var entryOffset = index * CHARS_PER_INT;
                    var entrySliceSpan = memSpan.Slice(entryOffset, CHARS_PER_INT);
                    var entryIntSpan = MemoryMarshal.Cast<char, int>(entrySliceSpan);
                    var stringIndex = entryIntSpan[0];

                    var stringLengthSliceSpan = memSpan.Slice(stringIndex, CHARS_PER_INT);
                    var stringLength = MemoryMarshal.Cast<char, int>(stringLengthSliceSpan)[0];

                    var stringMem = Memory.Slice(stringIndex + CHARS_PER_INT, stringLength);

                    var valueSliceSpan = memSpan.Slice(stringIndex + CHARS_PER_INT + stringLength, CHARS_PER_INT);
                    var valueIntSpan = MemoryMarshal.Cast<char, int>(valueSliceSpan);
                    var valueIndex = valueIntSpan[0];

                    return (stringMem, valueIndex);
                }
            }

            internal OrdererNames(int count, IMemoryOwner<char> owner)
            {
                Count = count;
                Owner = owner;
                Memory = owner.Memory;
            }

            internal static OrdererNames Create(string[] names, MemoryPool<char> pool)
            {
                // it's REALLY easy to spend a ton of time copying
                //      data around, so just pre-freaking allocate
                var neededChars = 0;
                foreach (var item in names)
                {
                    neededChars += CHARS_PER_INT;   // offset
                    neededChars += CHARS_PER_INT;   // value
                    neededChars += CHARS_PER_INT;   // string length
                    neededChars += item.Length;     // chars
                }

                var memOwner = pool.Rent(neededChars);
                var span = memOwner.Memory.Span;

                if (span.Length < neededChars)
                {
                    return Throw.InvalidOperationException<OrdererNames>($"Could not order dynamic member names, names could not fit in memory acquired from MemoryPool: {pool}");
                }

                var lastOffsetIx = 0;
                var lastStringIx = span.Length;

                var ix = 0;
                foreach (var name in names)
                {
                    var nameSpan = name.AsSpan();

                    // insert (length) + string.Length chars
                    var insertStringAt = lastStringIx - name.Length;
                    insertStringAt -= 2 * CHARS_PER_INT;

                    var newLastOffsetIx = lastOffsetIx + CHARS_PER_INT;

                    // copy string
                    MemoryMarshal.Cast<char, int>(span.Slice(insertStringAt, CHARS_PER_INT))[0] = name.Length;
                    nameSpan.CopyTo(span.Slice(insertStringAt + CHARS_PER_INT, name.Length));
                    // copy value
                    MemoryMarshal.Cast<char, int>(span.Slice(insertStringAt + CHARS_PER_INT + name.Length, CHARS_PER_INT))[0] = ix;
                    lastStringIx = insertStringAt;

                    // binary search to find where to insert
                    var insertEntryAtIx = FindInsertionIx(span, ix, nameSpan);

                    // copy entries forward to make space
                    if (insertEntryAtIx != ix)
                    {
                        var entriesToCopy = ix - insertEntryAtIx;

                        var startOfEntriesToCopy = insertEntryAtIx * CHARS_PER_INT;
                        var charsToCopy = entriesToCopy * CHARS_PER_INT;

                        var toCopy = span.Slice(startOfEntriesToCopy, charsToCopy);

                        var startOfCopyTo = startOfEntriesToCopy + CHARS_PER_INT;
                        var copyTo = span.Slice(startOfCopyTo, charsToCopy);

                        toCopy.CopyTo(copyTo);
                    }

                    // record entry
                    var entrySpan = MemoryMarshal.Cast<char, int>(span.Slice(insertEntryAtIx * CHARS_PER_INT, CHARS_PER_INT));
                    entrySpan[0] = insertStringAt;
                    lastOffsetIx = newLastOffsetIx;

                    ix++;
                }

                return new OrdererNames(ix, memOwner);
            }

            // internal for testing purposes
            internal static int FindInsertionIx(ReadOnlySpan<char> span, int numEntries, ReadOnlySpan<char> nameSpan)
            {
                if (numEntries == 0)
                {
                    return 0;
                }

                var startOfWindowIx = 0;
                var endOfWindowIx = numEntries - 1;
                var curPivotIx = numEntries / 2;

                while (true)
                {
                    var str = GetStringAtIndex(span, curPivotIx);

                    // at end of loop
                    //    cmpRes < 0 iff str < name
                    //    cmpRes > 0 iff str > name
                    //    cmpRes = 0 iff str == name UP TO THEIR COMMON LENGTH
                    var cmpRes = 0;
                    var cmpLen = Math.Min(str.Length, nameSpan.Length);
                    for (var i = 0; i < cmpLen; i++)
                    {
                        var cStr = str[i];
                        var cName = nameSpan[i];

                        cmpRes = cName - cStr;
                        if (cmpRes != 0)
                        {
                            break;
                        }
                    }

                    // after this, cmp = 0 iff str == name in absolute terms
                    if (cmpRes == 0)
                    {
                        cmpRes = nameSpan.Length - str.Length;
                    }

                    // adjust the window to look at
                    if (cmpRes < 0)
                    {
                        // move towards the start of the window
                        endOfWindowIx = curPivotIx - 1;
                    }
                    else if (cmpRes > 0)
                    {
                        // move towards the end of the window
                        startOfWindowIx = curPivotIx + 1;
                    }
                    else
                    {
                        // exact match found, that's not allowed
                        return Throw.InvalidOperationException<int>($"Two or more members with same name ({new string(nameSpan)}) encountered");
                    }

                    // have we exhausted our search?
                    //   if so, the correct insertion point is whatever
                    //   we just looked at if nameSpan < str
                    //   and one after it if nameSpan > str
                    // equivalently
                    //    return curPivotIx iff cmpRes < 0
                    //    return curPivotIx + 1 iff cmpRes > 0
                    if (startOfWindowIx > endOfWindowIx)
                    {
                        var ret = curPivotIx;
                        if (cmpRes > 0)
                        {
                            ret++;
                        }

                        return ret;
                    }

                    // pick a new pivot point
                    //   note that you have to be really careful about this calculation
                    //   or you have an overflow
                    // don't mess with the math
                    var entriesInWindow = endOfWindowIx - startOfWindowIx + 1;
                    curPivotIx = startOfWindowIx + entriesInWindow / 2;
                }
            }

            private static ReadOnlySpan<char> GetStringAtIndex(ReadOnlySpan<char> span, int ix)
            {
                var entryIx = ix * CHARS_PER_INT;
                var index = MemoryMarshal.Cast<char, int>(span.Slice(entryIx, CHARS_PER_INT))[0];

                var strLen = MemoryMarshal.Cast<char, int>(span.Slice(index, CHARS_PER_INT))[0];

                var ret = span.Slice(index + CHARS_PER_INT, strLen);

                return ret;
            }

            public void Dispose()
            {
                if (!IsDisposed)
                {
                    Owner.Dispose();
                    Owner = EmptyMemoryOwner.Singleton;
                }
            }
        }

    }
}
