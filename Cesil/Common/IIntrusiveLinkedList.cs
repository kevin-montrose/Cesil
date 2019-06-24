namespace Cesil
{
    internal interface IIntrusiveLinkedList<T>
       where T : class, IIntrusiveLinkedList<T>
    {
        T Next { get; set; }
        T Previous { get; set; }
    }

    internal static class IIntrusiveLinkedListExtensionMethods
    {
        public static void AddAfter<T>(this T linkedList, T item)
            where T : class, IIntrusiveLinkedList<T>
        {
            item.Previous = linkedList;
            item.Next = linkedList.Next;

            linkedList.Next = item;
        }

        public static void AddHead<T>(this T linkedList, ref T head, T item)
            where T : class, IIntrusiveLinkedList<T>
        {
            item.Next = linkedList;
            if (linkedList != null)
            {
                linkedList.Previous = item;
            }

            head = item;
        }

        public static void Remove<T>(this T linkedList, ref T head, T item)
            where T : class, IIntrusiveLinkedList<T>
        {
            var before = item.Previous;
            var after = item.Next;

            if (before != null)
            {
                // item is not the head of the list
                before.Next = after;
            }
            else
            {
                // item is the head of the list
                head = after;
            }

            if (after != null)
            {
                after.Previous = before;
            }

            item.Next = item.Previous = null;
        }
    }
}
