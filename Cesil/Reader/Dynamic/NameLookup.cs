using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Cesil.DisposableHelper;

namespace Cesil
{
    /// <summary>
    /// This has two modes: 
    ///  1. An adaptive radix tree
    ///  2. A sorted array search with binary search
    ///  
    /// The radix tree is faster and more compact, but can fail if names are reeeeaaaallly long.
    /// 
    /// Binary search can handle names that are quite long, but does many more comparisons and 
    ///   is less compact.
    /// 
    /// ===
    /// 
    /// The adaptix radix tree
    /// ----
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
    /// 
    /// ===
    /// 
    /// The binary search
    /// ----
    /// 
    /// Strings are sorted, then a count of strings (as an int), then pairs of indexes and values.
    ///   The indexes are absolute into the memory.
    ///   The values are stored plain.
    ///   The actual strings are in reverse order at the end of the memory.
    /// 
    /// For the strings:
    /// 
    ///  - abc = 2
    ///  - defg = 1
    ///  - hijkl = 0
    ///  
    /// we end up with:
    ///  - count 3
    /// 
    /// 
    ///  0: 0000, 0003,     // 3 strings (each int takes 2 chars)
    ///  2: 0000, 0022,     // index of abc 
    ///  4: 0000, 0002,     // value for abc
    ///  6: 0000, 0018,     // index to defg
    ///  8: 0000, 0001,     // value for defg
    /// 10: 0000, 0014,     // index to hijkl
    /// 12: 0000, 0000,     // value for hijkl
    /// 14: h, i, j, k, l,  // string hijkl
    /// 18: d, e, f, g,     // string defg
    /// 22: a, b, c         // string abc
    /// 
    /// The length of each string can be determined from the difference between each index, with a special case for the LAST string
    ///   whose length can be calculated from the end of the memory.
    /// </summary>
    internal partial struct NameLookup : ITestableDisposable
    {
        // internal for testing purposes
        internal enum Algorithm : byte
        {
            None = 0,

            AdaptiveRadixTrie = 1,
            BinarySearch = 2
        }

        internal static readonly NameLookup Empty = new NameLookup(Algorithm.None, EmptyMemoryOwner.Singleton, ReadOnlyMemory<char>.Empty);

        private const int NUM_PREFIX_OFFSET = 0;
        private const char ZERO_PREFIX_COUNT = (char)ushort.MaxValue; // this represents 0, which is actually 0 - 1; we'll immediately increment it before use
        private const int CHARS_PER_INT = sizeof(int) / sizeof(char);

        // internal for testing purposes
        internal readonly IMemoryOwner<char> MemoryOwner;
        internal readonly ReadOnlyMemory<char> Memory;
        internal readonly Algorithm Mode;

        private bool _IsDisposed;
        public bool IsDisposed => _IsDisposed;

        internal NameLookup(Algorithm mode, IMemoryOwner<char> owner, ReadOnlyMemory<char> memory)
        {
            MemoryOwner = owner;
            Memory = memory;
            Mode = mode;
            _IsDisposed = false;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                _IsDisposed = true;
                MemoryOwner.Dispose();
            }
        }

        internal readonly bool TryLookup(string key, out int value)
        {
            switch (Mode)
            {
                case Algorithm.AdaptiveRadixTrie: return TryLookupAdaptiveRadixTrie(key, out value);
                case Algorithm.BinarySearch: return TryLookupBinarySearch(key, out value);
                default:
                    value = 0;
                    Throw.ImpossibleException($"Unexpected {nameof(Algorithm)}: {Mode}");
                    return default;
            }
        }

        private readonly unsafe bool TryLookupAdaptiveRadixTrie(string key, out int value)
        {
            AssertNotDisposedInternal(this);

            fixed (char* keyPtrConst = key)
            {
                // as the search continues, keyPtr will have the remaining
                //  parts of the key
                char* keyPtr = keyPtrConst;
                int keyLen = key.Length;

                var trieSpan = Memory.Span;

                fixed (char* triePtrConst = trieSpan)
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

        private readonly unsafe bool TryLookupBinarySearch(string key, out int value)
        {
            AssertNotDisposedInternal(this);

            var keyLength = key.Length;
            fixed (char* keyConstPtr = key)
            {
                var treeSpan = Memory.Span;
                var treeLength = treeSpan.Length;
                fixed (char* constTreePtr = treeSpan)
                {
                    int* intPtr = (int*)constTreePtr;

                    // read how many names we have to check, in total
                    var count = *intPtr;

                    var startOfPairs = intPtr + 1;

                    var startOfNames = startOfPairs + count * 2;      // times 2 because we have the index and the value as pairs

                    // setup pointers to the "active window" of values to search over
                    var startOfWindow = startOfPairs;
                    var endOfWindow = startOfNames - 2;

                    while (startOfWindow <= endOfWindow)
                    {
                        // this calculation is a bit long winded, but I'm fairly certain
                        //   it doesn't fall afoul of the "every binary search ever is 
                        //   broken"-problem
                        var intsInWindow = endOfWindow - startOfWindow + 2; // we need to add 2 because if start == end we still have 1 pair (2 ints)
                        var pairsInWindow = intsInWindow / 2;

                        var pivot = pairsInWindow / 2;

                        var offsetFromStartOfGroup = pivot * 2; // times 2 because we have the index and the value as pairs

                        // read the pair we're currently considering
                        var toCheck = startOfWindow + offsetFromStartOfGroup;
                        var toCheckIx = *toCheck;
                        var toCheckValue = *(toCheck + 1);

                        // now we need to determine the length of the string
                        //   at `toCheckIx`
                        int toCheckLength;

                        // if we have no previous value, then the string
                        //    is going to terminate at the end of memory
                        if (toCheck == startOfPairs)
                        {
                            toCheckLength = treeLength - toCheckIx;
                        }
                        else
                        {
                            // otherwise, we need to look BACK one pair
                            //   this will give us the index of the FOLLOWING
                            //   string in the sequence
                            var beforeToCheck = toCheck - 2;

                            var beforeToCheckIx = *beforeToCheck;
                            toCheckLength = beforeToCheckIx - toCheckIx;
                        }

                        // get a ptr to the string value
                        var startOfNamePtr = constTreePtr + toCheckIx;

                        var commonLength = Math.Min(keyLength, toCheckLength);
                        int cmpRes = 0;
                        for (var i = 0; i < commonLength; i++)
                        {
                            var keyChar = keyConstPtr[i];
                            var nameChar = *startOfNamePtr;

                            cmpRes = keyChar - nameChar;
                            if (cmpRes != 0)
                            {
                                break;
                            }

                            startOfNamePtr++;
                        }

                        // now, if cmpRes < 0 then key < name
                        //      if cmpRes > 0 then key > name
                        //      if cmpRes == 0 then key == name UP TO commonLength

                        if (cmpRes == 0)
                        {
                            // now we have to compare the lengths
                            cmpRes = keyLength - toCheckLength;
                        }

                        // now, if cmpRes < 0 then key < name
                        //      if cmpRes > 0 then key > name
                        //      if cmpRes == 0 then key == name

                        if (cmpRes == 0)
                        {
                            value = toCheckValue;
                            return true;
                        }
                        else if (cmpRes < 0)
                        {
                            // we need to move back towards the origin
                            //    so startOfWindow remains the same and
                            //    endOfWindow needs to move to before toCheck
                            endOfWindow = toCheck - 2;  // skip back one because we've checked it
                        }
                        else
                        {
                            // we need to move away from the origin
                            //    so startOfWindow advances to after pivot
                            //    but endOfWindow remains teh same
                            startOfWindow = toCheck + 2;    // skip forward one because we've checked it
                        }
                    }

                    // we've fully exhausted the window, so we're not going to find key
                    value = -1;
                    return false;
                }
            }
        }

        internal static NameLookup Create(string[] names, MemoryPool<char> memoryPool)
        {
            using var ordered = OrdererNames.Create(names, memoryPool);

            return CreateInner(ordered, memoryPool);
        }

        // internal for testing purposes
        internal static NameLookup CreateInner(OrdererNames ordered, MemoryPool<char> memoryPool)
        {
            if (TryCreateAdaptiveRadixTrie(ordered, memoryPool, out var trieOwner, out var trieMem))
            {
                return new NameLookup(Algorithm.AdaptiveRadixTrie, trieOwner, trieMem);
            }

            if (TryCreateBinarySearch(ordered, memoryPool, out var binaryTreeOwner, out var binaryTreeMem))
            {
                return new NameLookup(Algorithm.BinarySearch, binaryTreeOwner, binaryTreeMem);
            }

            Throw.InvalidOperationException($"Could create a lookup for dynamic member names, names could not fit in memory acquired from MemoryPool: {memoryPool}");
            return default;
        }

        // internal for testing purposes
        internal static bool TryCreateBinarySearch(OrdererNames inOrder, MemoryPool<char> memoryPool, out IMemoryOwner<char> memOwner, out ReadOnlyMemory<char> mem)
        {
            var neededChars = CHARS_PER_INT;    // start at 1 int, because we need a count

            for (var i = 0; i < inOrder.Count; i++)
            {
                var (tMem, _) = inOrder[i];

                neededChars += CHARS_PER_INT;   // 1 for the index into the string
                neededChars += CHARS_PER_INT;   // 1 for the value
                neededChars += tMem.Length;     // then the string itself
            }

            var writeableMemOwner = memoryPool.Rent(neededChars);
            var writeableMem = writeableMemOwner.Memory;
            if (writeableMem.Length < neededChars)
            {
                writeableMemOwner.Dispose();

                memOwner = EmptyMemoryOwner.Singleton;
                mem = ReadOnlyMemory<char>.Empty;
                return false;
            }

            writeableMem = writeableMem[0..neededChars];
            var charSpan = writeableMem.Span;
            var intSpan = MemoryMarshal.Cast<char, int>(charSpan);

            var frontPtr = 0;
            var backPtr = charSpan.Length;

            // store the count of names
            intSpan[frontPtr] = inOrder.Count;
            frontPtr++;

            for (var i = 0; i < inOrder.Count; i++)
            {
                var (name, value) = inOrder[i];

                // copy the string to the furthest unused chunk of charSpan
                var startOfNameIx = backPtr - name.Length;
                name.Span.CopyTo(charSpan[startOfNameIx..]);

                // write the index of the string, and the value that goes with it
                //   to the next two open slots of intSpan
                var indexAddr = frontPtr;
                var valueAddr = frontPtr + 1;
                intSpan[indexAddr] = startOfNameIx;
                intSpan[valueAddr] = value;

                // update the pointers
                frontPtr = valueAddr + 1;
                backPtr = startOfNameIx;
            }

            memOwner = writeableMemOwner;
            mem = writeableMem;
            return true;
        }

        internal static bool TryCreateAdaptiveRadixTrie(OrdererNames inOrder, MemoryPool<char> memoryPool, out IMemoryOwner<char> memOwner, out ReadOnlyMemory<char> mem)
        {
            // check to see if any of the values are too large
            for (var i = 0; i < inOrder.Count; i++)
            {
                var (_, index) = inOrder[i];
                if (index > ushort.MaxValue)
                {
                    memOwner = EmptyMemoryOwner.Singleton;
                    mem = ReadOnlyMemory<char>.Empty;
                    return false;
                }
            }

            // is the total count of keys too large?
            if (inOrder.Count > ushort.MaxValue)
            {
                memOwner = EmptyMemoryOwner.Singleton;
                mem = ReadOnlyMemory<char>.Empty;
                return false;
            }

            LogHelper.NameLookup_OrderedNames(inOrder);

            ushort startIx = 0;
            ushort lastIx = (ushort)(inOrder.Count - 1);    // we know this will fix because we check the site of sortedNames above

            var neededMemory = CalculateNeededMemoryAdaptivePrefixTrie(inOrder, startIx, lastIx, 0);

            var writeableMemOwner = memoryPool.Rent(neededMemory);
            var writeableMem = writeableMemOwner.Memory;
            if (writeableMem.Length < neededMemory)
            {
                writeableMemOwner.Dispose();

                memOwner = EmptyMemoryOwner.Singleton;
                mem = ReadOnlyMemory<char>.Empty;
                return false;
            }

            writeableMem = writeableMem[0..neededMemory];

            var span = writeableMem.Span;

            if (!StorePrefixGroups(0, inOrder, startIx, lastIx, span, 0, out _))
            {
                writeableMemOwner.Dispose();
                memOwner = EmptyMemoryOwner.Singleton;
                mem = ReadOnlyMemory<char>.Empty;

                return false;
            }

            memOwner = writeableMemOwner;
            mem = writeableMem;
            return true;

            // pushes a level of prefix groups into groupStartSpan
            static bool StorePrefixGroups(
                // for debugging purposes
                int depth,
                // rather than allocate explicit subsets, 
                //   we only work on the part of names between
                //   [firstNamesIx, lastNamesIx] on each call
                // this works because names is sorted, so
                //   we always process contiguous chunks
                OrdererNames names,
                ushort firstNamesIx,
                ushort lastNamesIx,
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
                int ignoreCharCount,
                out int curOffset
            )
            {
                // initialize our prefix count to -1
                //   we'll immediately increment it in the while
                groupStartSpan[NUM_PREFIX_OFFSET] = ZERO_PREFIX_COUNT;

                // one past, since we're going to write to the count repeatedly and directly
                curOffset = NUM_PREFIX_OFFSET + 1;

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
                        var name = names[startOfPrefixGroup].Name.Span;

                        // increment the number of prefixes we've stored
                        //
                        // we pre-initialize this to 0 so there isn't a 
                        //    branch here
                        //
                        // note that the maximum iteration count is
                        //   (if value in names gets an entry) equal
                        //   to (lastNamesIx - firstNamesIx) which
                        //   will always fit in a char
                        groupStartSpan[NUM_PREFIX_OFFSET]++;

                        // find the length of common prefix for name
                        var prefixLen = CommonPrefixLengthAdaptivePrefixTrie(names, lastNamesIx, startOfPrefixGroup, ignoreCharCount, out var lastIndexOfPrefixGroup);

                        // store the length of this prefix
                        if (!ToPrefixLength(prefixLen, out var prefixLenChar))
                        {
                            curOffset = -1;
                            return false;
                        }
                        groupStartSpan[curOffset] = prefixLenChar;
                        curOffset++;

                        LogHelper.NameLookup_StorePrefixGroups(depth, startOfPrefixGroup, name, prefixLen);

                        // store the actual prefix characters
                        var prefixCharsToCopy = name.Slice(ignoreCharCount, prefixLen);
                        prefixCharsToCopy.CopyTo(groupStartSpan.Slice(curOffset));
                        curOffset += prefixLen;

                        // stash the end of the group for the next loop
                        var indexChar = ToEndOfPrefixGroup(lastIndexOfPrefixGroup);
                        groupStartSpan[curOffset] = indexChar;
                        curOffset++;

                        startOfPrefixGroup = (ushort)(lastIndexOfPrefixGroup + 1);
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

                        LogHelper.NameLookup_Indexes(depth, newFirstNamesIx, newLastNamesIx);

                        if (size > 1)
                        {
                            // store the jump to the next chunk of free memory as the offset
                            var offset = curOffset - groupPtr;
                            if (!ToOffset(offset, out var offsetChar))
                            {
                                curOffset = -1;
                                return false;
                            }
                            groupStartSpan[groupPtr] = offsetChar;
                            groupPtr++;

                            // store the next prefix groups into the buffer, and then note that we've advanced that far
                            //   into the span
                            var newIgnoreCharCount = ignoreCharCount + prefixLen;
                            if (!StorePrefixGroups(depth + 1, names, newFirstNamesIx, newLastNamesIx, nextGroupStartSpan, newIgnoreCharCount, out var sizeOfNextPrefixGroup))
                            {
                                curOffset = -1;
                                return false;
                            }
                            curOffset += sizeOfNextPrefixGroup;
                        }
                        else
                        {
                            // if there's only one prefix in the group, it's a leaf
                            //   so rather than actually store a 1 entry group
                            //   overload the space we'd store the offset
                            //   to instead store the final value.
                            var value = (ushort)names[newFirstNamesIx].Index;
                            var valueChar = ToValue(value);
                            groupStartSpan[groupPtr] = valueChar;
                            groupPtr++;
                        }

                        // move onto the next group, which will always immediately follow this one
                        startOfPrefixGroup = (ushort)(newLastNamesIx + 1);
                    }
                }

                return true;
            }
        }

        // index is stored as is
        //    since it's an index into a list
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static char ToEndOfPrefixGroup(ushort index)
        => (char)index;

        // undoes ToEndOfPrefixGroup
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort FromEndOfPrefixGroup(char c)
        => (ushort)c;

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
        internal static bool ToOffset(int offset, out char asChar)
        {
            var val = -offset;
            if (val < short.MinValue || val > short.MaxValue)
            {
                asChar = '\0';
                return false;
            }

            short asShort = checked((short)val);

            asChar = (char)asShort;
            return true;
        }

        // undoes ToOffset
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FromOffset(char c)
        {
            short asShort = (short)c;

            return -asShort;
        }

        // distinguises between offsets and values
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsOffset(char c)
        {
            var asShort = (short)c;

            return asShort < 0;
        }

        // prefix lengths are stored as (char)len
        //   and always interpreted as unsigned
        // nothing fancy, except we always expect
        //   prefix lengths to be >= 0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ToPrefixLength(int len, out char asChar)
        {
            if (len < 0 || len > ushort.MaxValue)
            {
                asChar = '\0';
                return false;
            }

            ushort asUShort = checked((ushort)len);

            asChar = (char)asUShort;
            return true;
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
        internal static char ToValue(ushort value)
        => (char)value;

        // undoes ToValue
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort FromValue(char c)
        => (ushort)c;

        // figure out how much total memory we'll need to store the whole tree
        // internal for testing purposes
        internal static int CalculateNeededMemoryAdaptivePrefixTrie(
            // rather than allocate explicit subsets, 
            //   we only work on the part of names between
            //   [firstNamesIx, lastNamesIx] on each call
            // this works because names is sorted, so
            //   we always process contiguous chunks
            OrdererNames names,
            ushort firstNamesIx,
            ushort lastNamesIx,
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
                var prefixLen = CommonPrefixLengthAdaptivePrefixTrie(names, lastNamesIx, startOfPrefixGroup, ignoreCharCount, out var lastIndexOfPrefixGroup);

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
                    var subgroupNeeded = CalculateNeededMemoryAdaptivePrefixTrie(names, newFirstNamesIx, newLastNamesIx, newIgnoreCharCount);
                    neededMemory += subgroupNeeded;
                }

                startOfPrefixGroup = (ushort)(lastIndexOfPrefixGroup + 1);
            }

            return neededMemory;
        }

        // figure out how many characters are in the prefix group (and how big that group is) for name
        internal static int CommonPrefixLengthAdaptivePrefixTrie(
            // rather than allocate explicit subsets, 
            //   we only work on the part of names between
            //   on each call.  other methods use [first, last]
            //   indexes, but this method only cares about last
            //   since it's starting wherever nameIx (which is
            //   >= first).
            // this works because names is sorted, so
            //   we always process contiguous chunks
            OrdererNames names,
            ushort lastNamesIx,
            ushort nameIx,
            // rather than re-allocate names with prefixes removed
            //   we just ignore a certain number of characters on each
            //   recursion.
            // this works because the path to the current prefix group
            //   is shared (foobar and football both start with "foo") 
            //   and thus always the same length
            int ignoreCharCount,

            out ushort lastIndexInPrefixGroup
        )
        {
            lastIndexInPrefixGroup = nameIx;

            var name = names[nameIx].Name.Span;

            name = name.Slice(ignoreCharCount);

            // special case, if there's an EXACT match on a subset of one of names then we'll have an empty entry
            //   ie. if names has "foo" & "foobar", one group will be "" & "bar"
            if (name.IsEmpty) return 0;

            var firstChar = name[0];
            var commonPrefixLength = name.Length;

            for (ushort j = (ushort)(nameIx + 1); j <= lastNamesIx; j++)
            {
                var otherName = names[j].Name.Span.Slice(ignoreCharCount);
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
