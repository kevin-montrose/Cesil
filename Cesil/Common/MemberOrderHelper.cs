using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Cesil
{
    // todo: test

    // helper for getting a list to be in order while it's built up incrementally
    //   with the ordering rules for DefaultTypeDescribers.GetOrder method
    //
    // The basic idea is, we only every append to a List<T> _but_
    //   we can keep a Span<int> in order.
    //
    // We can also optimize for the case where no explicit order is requested,
    //    by not actually bothering to store anything in Indexes.
    //
    // Indexes is as follows:
    //   - (index for item having lowest non-null order)
    //   - (index for item having secondlowest non-null order)
    //   - ...
    //   - (index of first item seen having null order)
    //   - (index of second item seen having null order)
    //   - ...
    //   - (int indexing into Data for item at 1)        // <- note that we store these in reverse order
    //   - (int indexing into Data for item at 0)
    //
    // Optimizations are as follows:
    //   1. If only null orders are provided, we never allocate Indexes and just append directly to Data
    //   2. When adding a null order, we just slap things on the end of the both Indexes and Data performing no search
    //   3. When adding a non-null order, we only search over the chunk of memory that has non-null orders
    internal partial class MemberOrderHelper<T>
        where T : class
    {
        private readonly List<T> Data;

        private int[] IndexesAndOrders;

        private int OffsetOfFirstNullOrder;

        private bool HasNoOrdering;

        private MemberOrderHelper(List<T> data, int[] indexesAndOrders)
        {
            Data = data;
            IndexesAndOrders = indexesAndOrders;
            OffsetOfFirstNullOrder = -1;
            HasNoOrdering = true;

            CurrentIndex = -1;
        }

        internal static MemberOrderHelper<T> Create()
        => new MemberOrderHelper<T>(new List<T>(), Array.Empty<int>());

        private T GetAt(int ix)
        {
            // if we haven't stored anything with an explicit order
            //    yield things in the order they were added
            if (HasNoOrdering)
            {
                return Data[ix];
            }

            // otherwise, we've stored the order in reverse order
            //   from the end
            var distanceFromEnd = ix + 1;

            var dataIx = IndexesAndOrders[^distanceFromEnd];
            return Data[dataIx];
        }

        internal void Add(int? order, T data)
        {
            if (HasNoOrdering)
            {
                if (order == null)
                {
                    // we can optimize the (hopefully common)
                    //    case where there is no explicit ordering

                    Data.Add(data);
                    return;
                }
                else
                {
                    // do the work we have to in order
                    //    to start tracking elements with an actual order
                    ConvertToHavingOrder();
                }
            }

            if (order == null)
            {
                // always goes at the end
                var nullInsertionIx = Data.Count;

                // make some space
                MakeSpaceForInsert(nullInsertionIx);

                // actually store the data (don't bother with the order)
                Insert(nullInsertionIx, nullInsertionIx);
                Data.Add(data);

                // start of nulls was not affected

                return;
            }

            // someone actually specified an order, so we need to search
            var orderVal = order.Value;

            var insertionIx = FindInsertionIndex(orderVal);

            // give us a gap to write into            
            MakeSpaceForInsert(insertionIx);
            
            // actually store the order and data
            InsertWithOrder(insertionIx, orderVal, Data.Count);
            Data.Add(data);

            // and we moved any null's back by one
            OffsetOfFirstNullOrder++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertWithOrder(int insertIx, int order, int dataIndex)
        {
            IndexesAndOrders[insertIx] = order;

            Insert(insertIx, dataIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Insert(int insertIx, int dataIndex)
        {
            var distanceFromEnd = insertIx + 1;
            IndexesAndOrders[^distanceFromEnd] = dataIndex;
        }

        private void ConvertToHavingOrder()
        {
            HasNoOrdering = false;
            OffsetOfFirstNullOrder = 0;

            if (Data.Count == 0)
            {
                return;
            }

            var neededInts = Data.Count * 2;
            var arr = ArrayPool<int>.Shared.Rent(neededInts);

            for (var i = 0; i < Data.Count; i++)
            {
                arr[^(i + 1)] = i;
            }

            IndexesAndOrders = arr;
        }

        private void MakeSpaceForInsert(int insertIx)
        {
            var numElements = Data.Count;

            // make sure we're not full
            var neededSpace = (numElements + 1) * 2;
            if (IndexesAndOrders.Length < neededSpace)
            {
                var oldArr = IndexesAndOrders;
                var oldArrLen = oldArr.Length;
                var newArr = ArrayPool<int>.Shared.Rent(neededSpace);

                if (oldArrLen != 0)
                {
                    if (OffsetOfFirstNullOrder > 0)
                    {
                        Array.Copy(oldArr, 0, newArr, 0, OffsetOfFirstNullOrder);
                    }

                    // numElements will always be > 0 if oldArrLen != 0, so no check is needed
                    Array.Copy(oldArr, oldArrLen - numElements, newArr, newArr.Length - numElements, numElements);

                    ArrayPool<int>.Shared.Return(oldArr);
                }

                IndexesAndOrders = newArr;
            }

            // we're appending, so no need to make any gaps
            //   since just checking for sufficient space gave 
            //   us the gaps we need
            if (insertIx == numElements)
            {
                return;
            }

            var arrLen = IndexesAndOrders.Length;

            // now we need to copy things around to make space
            // so if we had
            // orders: -4, 3, 7, ... indexes: 2, 0, 1
            //
            // and are inserting at 1
            //
            // we need to get
            // orders: -4, <blank>, 3, 7, ... indexes 2, 0, <blank>, 1

            // make the gap at the front
            {
                var frontCopyFromStart = insertIx;
                var frontCopyFromEnd = OffsetOfFirstNullOrder;              // we don't actually need to copy all the indexes, since the once for null orders are always ignored
                var frontCopyLength = frontCopyFromEnd - frontCopyFromStart;
                if (frontCopyLength > 0)
                {
                    var frontCopyToStart = frontCopyFromStart + 1;
                    Array.Copy(IndexesAndOrders, frontCopyFromStart, IndexesAndOrders, frontCopyToStart, frontCopyLength);
                }
            }

            // make the gap at the end
            {
                var endCopyFromStart = arrLen - numElements;
                var endCopyFromEnd = arrLen - insertIx;                 // we always have to copy this whole chunk, since the indexes all matter
                var endCopyLength = endCopyFromEnd - endCopyFromStart;

                if (endCopyLength > 0)
                {
                    var endCopyToStart = endCopyFromStart - 1;
                    Array.Copy(IndexesAndOrders, endCopyFromStart, IndexesAndOrders, endCopyToStart, endCopyLength);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindInsertionIndex(int order)
        {
            var numEntries = OffsetOfFirstNullOrder;

            if (numEntries == 0)
            {
                return 0;
            }

            var ix = Array.BinarySearch(IndexesAndOrders, 0, numEntries, order);

            if (ix >= 0)
            {
                // if positive, that means we had an exact match
                //    so insert the current value AFTER it
                //    so we copy fewer ints around to make
                //    space
                return ix + 1;
            }

            // if negative, then we need to do this to get the index
            //    of the first element greater than order
            // we want to insert at the index of that element
            //    because then we minimize copies
            return Math.Abs(ix) - 1;
        }

        public override string ToString()
        => $"{nameof(MemberOrderHelper<T>)} with {Data.Count} elements";
    }
}
