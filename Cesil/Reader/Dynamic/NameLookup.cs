using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using static Cesil.DisposableHelper;

namespace Cesil
{
    /// <summary>
    /// This is basically an adaptive radix tree, laid out in memory.
    /// 
    /// For some number of strings, each unique prefix is 
    ///   found, and then sorted, and then stored in a Memory(char).
    /// 
    /// After each prefix an offset is stored, which identifes the start
    ///   of the branch to take.
    /// 
    /// So if you have prefixes: foo, fizz, bar, bazz
    /// 
    /// Unique prefixes are: f, ba
    /// Sorted they are: ba, f
    /// 
    /// In memory we store (as chars)
    /// 1,             2,          b,     a,   X             1,          f      X
    /// ^              ^           ^      ^    ^             ^           ^      ^
    /// # prefixes - 1 |           |      |    |             |           |      |
    ///                len prefix  |      |    |             |           |      |
    ///                            char   char |             |           |      |
    ///                                        offset branch |           |      |
    ///                                                      len prefix  char   offset branch
    ///                                                  
    /// If the offset is zero or positive, then we've hit a leaf and it's a value.
    /// If the offset is negative, than we jump ahead the absolute value of the offset
    ///   to get to the next branch.
    ///   
    /// We store # of prefixes - 1, since there will always be at least one prefix.
    /// </summary>
    internal struct NameLookup : ITestableDisposable
    {
        // low enough we're unlikely to get there from a bug, but not so low that we'll overflow from a bug either
        private const long EXPLICITLY_DISPOSED_REFERENCE_COUNT = -1_000_000;

        private const int NUM_PREFIX_OFFSET = 0;

        // internal for testing purposes
        internal readonly IMemoryOwner<char> MemoryOwner;
        internal readonly ReadOnlyMemory<char> Memory;

        private long ReferenceCount;

        public bool IsDisposed => ReferenceCount <= 0;

        public NameLookup(IMemoryOwner<char> owner, ReadOnlyMemory<char> memory)
        {
            MemoryOwner = owner;
            Memory = memory;
            ReferenceCount = 1;
        }

        internal void AddReference()
        {
            AssertNotDisposedInternal(this);

            // has to be interlocked because RemoveReference is interlocked
            Interlocked.Increment(ref ReferenceCount);
        }

        internal void RemoveReference()
        {
            AssertNotDisposedInternal(this);

            // has to be interlocked because we can't assume disposal will happen on the same thread as creation
            var res = Interlocked.Decrement(ref ReferenceCount);

            // if this was the last reference, let go of the MemoryOwner
            if (res == 0)
            {
                DisposeInner();
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                ReferenceCount = EXPLICITLY_DISPOSED_REFERENCE_COUNT;
                DisposeInner();
            }
        }

        private void DisposeInner()
        {
            MemoryOwner.Dispose();
        }

        public unsafe bool TryLookup(string key, out int value)
        {
            AssertNotDisposedInternal(this);

            fixed (char* keyPtrConst = key)
            {
                // as the search continues, keyPtr will have the remaining
                //  parts of the key
                char* keyPtr = keyPtrConst;
                int keyLen = key.Length;

                var trieSpan = Memory.Span;

                fixed(char* triePtrConst = trieSpan)
                {
                    // likewise, triePtr will always be pointing at the _start_
                    //   of the prefix group
                    char* triePtr = triePtrConst;

                    // starting point for processing a single prefix group
                    //   as we descend the trie we'll come back here
                    processPrefixGroup:

                    // this can read 1 past the end of keyPtr (if keyPtr == "")
                    //   but this is fine because key is always a string AND
                    //   .NET strings are always null terminated (with a zero
                    //   char not just a zero byte)
                    var firstKeyChar = *keyPtr;
                    var numPrefixes = FromPrefixCount(*triePtr);
                    // advance past the prefix count
                    triePtr++;
                    for (var i = 0; i < numPrefixes; i++)
                    {
                        var prefixLen = FromPrefixLength(*triePtr);
                        // move past the len, we're either pointing at the first
                        //   letter of the prefix OR the value/offset slot (if 
                        //   prefixLen == 0)
                        triePtr++;

                        // the key being empty will only ever happen when i == 0
                        //   and indicates that we need to either accept the current
                        //   value (if prefixLen == 0, that is the prefix is "")
                        //   or bail
                        if (keyLen == 0)
                        {
                            if (prefixLen == 0)
                            {
                                // offset is already point at value, since prefixLen == 0
                                value = FromValue(*triePtr);
                                return true;
                            }

                            value = -1;
                            return false;
                        }

                        // terminal empty node, and keySpan is not empty
                        if (prefixLen == 0)
                        {
                            // did not find key, skip the value and continue;
                            triePtr++;
                            continue;
                        }

                        var firstPrefixChar = *triePtr;

                        // we've gone far enough that we're not going to find a prefix
                        //   that matches key (since this prefix occurs after key
                        //   lexicographically), bail
                        if (firstKeyChar < firstPrefixChar)
                        {
                            value = -1;
                            return false;
                        }
                        else if (firstKeyChar > firstPrefixChar)
                        {
                            // key may be found after the current prefix (which occurs
                            //   before key lexiocographically), skip it
                            triePtr += prefixLen;
                            triePtr++;    // skip the offset or value slot
                            continue;
                        }
                        else
                        {
                            // key needs to match prefix, at least up to prefix length

                            if (keyLen < prefixLen)
                            {
                                // key overlaps prefix, but the actual key value isn't in the trie, bail
                                //
                                // if key were in the trie, then the prefix would either match or
                                //   overlap the key (that is, there'd be 0 or more key chars
                                //   to process).
                                // taking this branch means that some value that is key + "<some other chars>"
                                //   IS in the trie.
                                value = -1;
                                return false;
                            }

                            // we've already checked the first char, but need to check the rest of the prefix
                            if (!Utils.AreEqual(prefixLen - 1, keyPtr + 1, triePtr + 1))
                            {
                                // key starts with the same char as prefix, but isn't actually equal to the prefix
                                //   which can only happen if key doesn't appear in the trie (if it did, the prefix
                                //   would be split after the last common character).

                                value = -1;
                                return false;
                            }

                            // we're now pointing at the value / offset slot
                            triePtr += prefixLen;

                            // we've handled prefixLen number of chars in the key now
                            var remainingKeyPtr = keyPtr + prefixLen;
                            var remainingKeyLen = keyLen - prefixLen;

                            // figure out if the current prefix is the terminal
                            //   part of a key, or if there's more work to be done.
                            //
                            // if there is more work to do, then we'll find an offset
                            //   to the next prefix group to process.
                            var valueOrOffset = *triePtr;
                            var isOffset = IsOffset(valueOrOffset);

                            if (isOffset)
                            {
                                // jump to the group pointed to by offset

                                var toNextGroupOffset = FromOffset(valueOrOffset);

                                var nextGroupPtr = triePtr + toNextGroupOffset;

                                // trim the parts of the key we've dealt with off
                                keyPtr = remainingKeyPtr;
                                keyLen = remainingKeyLen;

                                //  move the whole triePtr forward to the next group
                                triePtr = nextGroupPtr;

                                // start over at the new prefix group
                                goto processPrefixGroup;
                            }
                            else
                            {
                                // if we've found a value in the trie, we can take it
                                //   only if key is fully consumed
                                // otherwise, we know the key is not in the trie
                                if (remainingKeyLen == 0)
                                {
                                    value = FromValue(valueOrOffset);
                                    return true;
                                }

                                value = -1;
                                return false;
                            }
                        }
                    }
                }
            }

            // enumerated all the prefixes in this group, and key is still after them
            //   lexicographically so we're never going to find it
            value = -1;
            return false;
        }

        public static NameLookup Create(List<string> names, MemoryPool<char> memoryPool)
        {
            if (names.Count > ushort.MaxValue)
            {
                return Throw.ArgumentException<NameLookup>($"{nameof(NameLookup)} can only at most {ushort.MaxValue}", nameof(names));
            }

            // sort 'em, so we can more easily find common prefixes
            var inOrder = names.Select((n, ix) => (Name: n, Index: ix)).OrderBy(t => t.Name, StringComparer.Ordinal).ToList();

            var sortedNames = new List<ReadOnlyMemory<char>>();
            var sortedNamesValues = new List<ushort>();

            foreach (var t in inOrder)
            {
                sortedNames.Add(t.Name.AsMemory());
                sortedNamesValues.Add((ushort)t.Index);
            }

            Debug.WriteLineIf(
                LogConstants.NAME_LOOKUP,
                $"{nameof(Create)}: ordered names ({string.Join(", ", sortedNames.Select(x => '"' + new string(x.Span) + '"'))})"
            );

            var neededMemory = CalculateNeededMemory(sortedNames, 0, sortedNames.Count - 1, 0);

            var memOwner = memoryPool.Rent(neededMemory);
            var mem = memOwner.Memory.Slice(0, neededMemory);

            var span = mem.Span;

            StorePrefixGroups(0, sortedNames, 0, sortedNames.Count - 1, sortedNamesValues, span, 0);

            return new NameLookup(memOwner, mem);

            // pushes a level of prefix groups into groupStartSpan
            static int StorePrefixGroups(
                // for debugging purposes
                int depth,
                // rather than allocate explicit subsets, 
                //   we only work on the part of names between
                //   [firstNamesIx, lastNamesIx] on each call
                // this works because names is sorted, so
                //   we always process contiguous chunks
                List<ReadOnlyMemory<char>> names,
                int firstNamesIx,
                int lastNamesIx,
                // values is boxed by the same indexes
                //    as names
                List<ushort> values,
                // groupStartSpan is the origin point for writing
                //   in this particular call.
                // it is advanced prior to each recursion, so
                //   that "the current prefix group" always starts
                //   at groupStartSpan[0]
                Span<char> groupStartSpan,
                // rather than re-allocate names with prefixes removed
                //   we just ignore a certain number of characters on each
                //   recursion.
                // this works because the path to the current prefix group
                //   is shared (foobar and football both start with "foo") 
                //   and thus always the same length
                int ignoreCharCount
            )
            {
                // one past, since we're going to write to the count repeatedly and directly
                var curOffset = NUM_PREFIX_OFFSET + 1;

                // fill out the prefix groups
                //
                // we stash the _end_ of the prefix groups (because we can infer
                //   the start) in the <value> / <offset> slot since it's unneeded.
                //
                // if names is (foo, foobar, hello, world) then at the end of this loop we'll have
                //              ^    ^       ^      ^
                //              0    1       2      3
                //
                // 2,                   // number of prefixes - 1
                // 3, f, o, o, 1        // group 0 for prefix "foo", 1 is the end of the group
                // 1, h, 2              // group 1 for prefix "h", 2 is the end of the group
                // 1, w, 3              // group 2 for prefix "w", 3 is the end of the group
                {
                    var startOfPrefixGroup = firstNamesIx;
                    while (startOfPrefixGroup <= lastNamesIx)
                    {
                        var name = names[startOfPrefixGroup].Span;

                        // increase the count of prefixes in this group
                        if (startOfPrefixGroup == firstNamesIx)
                        {
                            // first time, just store 1
                            groupStartSpan[NUM_PREFIX_OFFSET] = ToPrefixCount(1);
                        }
                        else
                        {
                            // afterwards, increment
                            groupStartSpan[NUM_PREFIX_OFFSET]++;
                        }

                        // find the length of common prefix for name
                        var prefixLen = CommonPrefixLength(names, lastNamesIx, startOfPrefixGroup, ignoreCharCount, out int lastIndexOfPrefixGroup);

                        // store the length of this prefix
                        groupStartSpan[curOffset] = ToPrefixLength(prefixLen);
                        curOffset++;

                        Debug.WriteLineIf(
                            LogConstants.NAME_LOOKUP,
                            $"{nameof(StorePrefixGroups)}: depth={depth}, start={startOfPrefixGroup}, prefix=\"{new string(name.Slice(0, prefixLen))}\""
                        );

                        // store the actual prefix characters
                        var prefixCharsToCopy = name.Slice(ignoreCharCount, prefixLen);
                        prefixCharsToCopy.CopyTo(groupStartSpan.Slice(curOffset));
                        curOffset += prefixLen;

                        // stash the end of the group for the next loop
                        groupStartSpan[curOffset] = ToEndOfPrefixGroup(lastIndexOfPrefixGroup);
                        curOffset++;

                        startOfPrefixGroup = lastIndexOfPrefixGroup + 1;
                    }
                }

                // now curOffset points to right after the group of prefixes, so we can go fill in offsets
                {
                    var numPrefixesInGroup = FromPrefixCount(groupStartSpan[NUM_PREFIX_OFFSET]);
                    var groupPtr = NUM_PREFIX_OFFSET + 1;
                    var startOfPrefixGroup = firstNamesIx;
                    for (var i = 0; i < numPrefixesInGroup; i++)
                    {
                        var prefixLen = FromPrefixLength(groupStartSpan[groupPtr]);

                        // skip the length
                        groupPtr++;

                        // skip the prefix text
                        groupPtr += prefixLen;
                        // now groupPtr is pointing to the <offset> or <value> cell for this prefix

                        var nextGroupStartSpan = groupStartSpan.Slice(curOffset);

                        var newFirstNamesIx = startOfPrefixGroup;
                        var newLastNamesIx = FromEndOfPrefixGroup(groupStartSpan[groupPtr]);
                        var size = newLastNamesIx - newFirstNamesIx + 1;

                        Debug.WriteLineIf(LogConstants.NAME_LOOKUP, $"Indexes: depth={depth}, start:{newFirstNamesIx}, last:{newLastNamesIx}");

                        Debug.Assert(
                            newFirstNamesIx <= newLastNamesIx,
                            $"Indexes into group are non-sensical; {nameof(newFirstNamesIx)} ({newFirstNamesIx}) > {nameof(newLastNamesIx)} ({newLastNamesIx})"
                        );

                        if (size > 1)
                        {
                            // store the jump to the next chunk of free memory as the offset
                            var offset = curOffset - groupPtr;
                            groupStartSpan[groupPtr] = ToOffset(offset);
                            groupPtr++;

                            // store the next prefix groups into the buffer, and then note that we've advanced that far
                            //   into the span
                            var newIgnoreCharCount = ignoreCharCount + prefixLen;
                            var sizeOfNextPrefixGroup = StorePrefixGroups(depth + 1, names, newFirstNamesIx, newLastNamesIx, values, nextGroupStartSpan, newIgnoreCharCount);
                            curOffset += sizeOfNextPrefixGroup;
                        }
                        else
                        {
                            // if there's only one prefix in the group, it's a leaf
                            //   so rather than actually store a 1 entry group
                            //   overload the space we'd store the offset
                            //   to instead store the final value.
                            var value = values[newFirstNamesIx];
                            groupStartSpan[groupPtr] = ToValue(value);
                            groupPtr++;
                        }

                        // move onto the next group, which will always immediately follow this one
                        startOfPrefixGroup = newLastNamesIx + 1;
                    }
                }

                return curOffset;
            }
        }

        // index is stored as is, just assuming >= 0
        //    since it's an index into a list
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static char ToEndOfPrefixGroup(int index)
        {
            // index will always be positive
            Debug.Assert(index >= 0, $"Index out of range: {index}");

            ushort asUShort;
            checked
            {
                asUShort = (ushort)index;
            }

            return (char)asUShort;
        }

        // undoes ToEndOfPrefixGroup
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FromEndOfPrefixGroup(char c)
        {
            ushort asUShort = c;

            return asUShort;
        }

        // prefix counts are stored as (char)(count-1)
        //   since we always expect a > 0 number of prefixes
        //   in a group
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static char ToPrefixCount(int count)
        {
            // offset will always be >= 1
            Debug.Assert(count > 0, $"PrefixCount out of range: {count}");

            ushort asUShort;
            checked
            {
                asUShort = (ushort)(count - 1);
            }

            return (char)asUShort;
        }

        // undoes ToPrefixCount
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FromPrefixCount(char c)
        {
            ushort asShort = c;
            var asInt = asShort;
            asInt++;

            return asInt;
        }

        // offsets are stored as (char)(-offset)
        // this lets us distinguish between values 
        //   (which are always >= 0) and offsets
        //   (which will always be < 0).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static char ToOffset(int offset)
        {
            // offset will always be >= 1
            Debug.Assert(offset >= 1, $"Offset out of range: {offset}");

            short asShort;
            checked
            {
                asShort = (short)-offset;
            }

            return (char)asShort;
        }

        // undoes ToOffset
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FromOffset(char c)
        {
            short asShort = (short)c;

            Debug.Assert(asShort < 0, $"Unexpected offset value, wasn't negative: {asShort}");

            return -asShort;
        }

        // distinguises between offsets adn values
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsOffset(char c)
        {
            var asShort = (short)c;

            return asShort < 0;
        }

        // prefix lengths are stored as (char)len
        //   and always interpreted as unsigned
        // nothing fancy, except we always expect
        //   prefix lengths to be >= 0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static char ToPrefixLength(int len)
        {
            Debug.Assert(len >= 0, $"Length out of range: {len}");

            ushort asUShort;
            checked
            {
                asUShort = (ushort)len;
            }

            return (char)asUShort;
        }

        // undoes ToPrefixLength
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FromPrefixLength(char c)
        {
            ushort asUShort = c;

            return asUShort;
        }

        // values are stored as (char)value
        //   and always interpreted as unsigned
        // nothing fancy, except we always expect
        //   values to be >=0 since they're indexes
        //   into a List
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static char ToValue(int value)
        {
            // value will always be >= 0
            Debug.Assert(value >= 0, $"Value out of range: {value}");

            ushort asUShort;
            checked
            {
                asUShort = (ushort)value;
            }

            return (char)asUShort;
        }

        // undoes ToValue
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FromValue(char c)
        {
            ushort asUShort = c;

            return asUShort;
        }

        // figure out how much total memory we'll need to store the whole tree
        // internal for testing purposes
        internal static int CalculateNeededMemory(
            // rather than allocate explicit subsets, 
            //   we only work on the part of names between
            //   [firstNamesIx, lastNamesIx] on each call
            // this works because names is sorted, so
            //   we always process contiguous chunks
            List<ReadOnlyMemory<char>> names,
            int firstNamesIx,
            int lastNamesIx,
            // rather than re-allocate names with prefixes removed
            //   we just ignore a certain number of characters on each
            //   recursion.
            // this works because the path to the current prefix group
            //   is shared (foobar and football both start with "foo") 
            //   and thus always the same length
            int ignoreCharCount
        )
        {
            var neededMemory = 1;   // number of prefixes, takes up a ushort

            var startOfPrefixGroup = firstNamesIx;
            while (startOfPrefixGroup <= lastNamesIx)
            {
                // find the length of common prefix for name
                var prefixLen = CommonPrefixLength(names, lastNamesIx, startOfPrefixGroup, ignoreCharCount, out int lastIndexOfPrefixGroup);

                // calculate the subset of names we need to recurse on
                var newFirstNamesIx = startOfPrefixGroup;
                var newLastNamesIx = lastIndexOfPrefixGroup;
                var newIgnoreCharCount = ignoreCharCount + prefixLen;
                var size = newLastNamesIx - newFirstNamesIx + 1;

                // each group is <prefix length>, <prefix chars>, <offset>
                neededMemory += 1 + prefixLen + 1;

                // if we only have one value, we're at a leaf and we'll store 
                //   the value in <offset> - but if we have more than one
                //   offset is going to point to another group
                if (size > 1)
                {
                    // figure out how much we'll need from the prefixes too
                    var subgroupNeeded = CalculateNeededMemory(names, newFirstNamesIx, newLastNamesIx, newIgnoreCharCount);
                    neededMemory += subgroupNeeded;
                }

                startOfPrefixGroup = lastIndexOfPrefixGroup + 1;
            }

            return neededMemory;
        }

        // figure out how many characters are in the prefix group (and how big that group is) for name
        internal static int CommonPrefixLength(
            // rather than allocate explicit subsets, 
            //   we only work on the part of names between
            //   on each call.  other methods use [first, last]
            //   indexes, but this method only cares about last
            //   since it's starting wherever nameIx (which is
            //   >= first).
            // this works because names is sorted, so
            //   we always process contiguous chunks
            List<ReadOnlyMemory<char>> names,
            int lastNamesIx,
            int nameIx,
            // rather than re-allocate names with prefixes removed
            //   we just ignore a certain number of characters on each
            //   recursion.
            // this works because the path to the current prefix group
            //   is shared (foobar and football both start with "foo") 
            //   and thus always the same length
            int ignoreCharCount,

            out int lastIndexInPrefixGroup
        )
        {
            lastIndexInPrefixGroup = nameIx;

            var name = names[nameIx].Span;

            name = name.Slice(ignoreCharCount);

            // special case, if there's an EXACT match on a subset of one of names then we'll have an empty entry
            //   ie. if names has "foo" & "foobar", one group will be "" & "bar"
            if (name.IsEmpty) return 0;

            var firstChar = name[0];
            var commonPrefixLength = name.Length;
            for (var j = nameIx + 1; j <= lastNamesIx; j++)
            {
                var otherName = names[j].Span.Slice(ignoreCharCount);
                if (otherName[0] != firstChar)
                {
                    break;
                }

                // figure out how many, if any, chars are in common from the start of name and otherName
                int inCommonChars;
                for (inCommonChars = 1; inCommonChars < Math.Min(commonPrefixLength, otherName.Length); inCommonChars++)
                {
                    var c1 = name[inCommonChars];
                    var c2 = otherName[inCommonChars];

                    if (c1 != c2) break;
                }

                // if the answer is 0, we've gone too far
                var newCommonPrefixLength = Math.Min(commonPrefixLength, inCommonChars);
                if (newCommonPrefixLength == 0)
                {
                    break;
                }

                // if there are any, keep going forward to see how many
                //   strings we can cover with a single prefix
                commonPrefixLength = newCommonPrefixLength;
                lastIndexInPrefixGroup = j;
            }

            return commonPrefixLength;
        }
    }
}
