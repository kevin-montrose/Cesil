namespace Cesil
{
    internal interface IIntrusiveLinkedList<T>
       where T : class, IIntrusiveLinkedList<T>
    {
        ref NonNull<T> Next { get; }

        ref NonNull<T> Previous { get; }
    }

    internal static class IIntrusiveLinkedListExtensionMethods
    {
        internal static void AddAfter<T>(this T linkedList, T item)
            where T : class, IIntrusiveLinkedList<T>
        {
            item.Previous.Value = linkedList;

            ref var linkedListNext = ref linkedList.Next;
            ref var itemNext = ref item.Next;

            if (linkedListNext.HasValue)
            {
                itemNext.Value = linkedListNext.Value;
            }
            else
            {
                itemNext.Clear();
            }

            linkedListNext.Value = item;
        }

        internal static void AddHead<T>(this T? linkedList, ref T? head, T item)
            where T : class, IIntrusiveLinkedList<T>
        {
            ref var itemNext = ref item.Next;

            if (linkedList != null)
            {
                itemNext.Value = linkedList;
                linkedList.Previous.Value = item;
            }
            else
            {
                itemNext.Clear();
            }

            head = item;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Necessary to make this an extension method, which is good for ergonomics")]
        internal static void Remove<T>(this T? linkedList, ref T? head, T item)
            where T : class, IIntrusiveLinkedList<T>
        {
            ref var itemPrevious = ref item.Previous;
            ref var itemNext = ref item.Next;

            if (itemPrevious.HasValue)
            {
                var before = itemPrevious.Value;

                ref var beforeNext = ref before.Next;

                if (itemNext.HasValue)
                {
                    beforeNext.Value = itemNext.Value;
                }
                else
                {
                    beforeNext.Clear();
                }
            }
            else
            {
                if (itemNext.HasValue)
                {
                    head = itemNext.Value;
                }
                else
                {
                    head = null;
                }
            }

            if (itemNext.HasValue)
            {
                var after = itemNext.Value;
                ref var afterPrevious = ref after.Previous;

                if (item.Previous.HasValue)
                {
                    afterPrevious.Value = itemPrevious.Value;
                }
                else
                {
                    afterPrevious.Clear();
                }
            }

            itemNext.Clear();
            itemPrevious.Clear();
        }
    }
}
