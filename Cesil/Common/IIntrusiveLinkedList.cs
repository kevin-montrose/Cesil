namespace Cesil
{
    internal interface IIntrusiveLinkedList<T>
       where T : class, IIntrusiveLinkedList<T>
    {
        bool HasNext { get; }
        T Next { get; set; }
        void ClearNext();
        
        bool HasPrevious { get; }
        T Previous { get; set; }
        void ClearPrevious();
    }

    internal static class IIntrusiveLinkedListExtensionMethods
    {
        public static void AddAfter<T>(this T linkedList, T item)
            where T : class, IIntrusiveLinkedList<T>
        {
            item.Previous = linkedList;

            if (linkedList.HasNext)
            {
                item.Next = linkedList.Next;
            }
            else
            {
                item.ClearNext();
            }

            linkedList.Next = item;
        }

        public static void AddHead<T>(this T? linkedList, ref T? head, T item)
            where T : class, IIntrusiveLinkedList<T>
        {
            if (linkedList != null)
            {
                item.Next = linkedList;
                linkedList.Previous = item;
            }
            else
            {
                item.ClearNext();
            }

            head = item;
        }

        public static void Remove<T>(this T? linkedList, ref T? head, T item)
            where T : class, IIntrusiveLinkedList<T>
        {
            if (item.HasPrevious)
            {
                var before = item.Previous;

                if (item.HasNext)
                {
                    before.Next = item.Next;
                }
                else
                {
                    before.ClearNext();
                }
            }
            else
            {
                if (item.HasNext)
                {
                    head = item.Next;
                }
                else
                {
                    head = null;
                }
            }

            if (item.HasNext)
            {
                var after = item.Next;

                if (item.HasPrevious)
                {
                    after.Previous = item.Previous;
                }
                else
                {
                    after.ClearPrevious();
                }
            }

            item.ClearNext();
            item.ClearPrevious();
        }
    }
}
