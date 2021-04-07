using System.Collections.Generic;

namespace Cesil
{
    internal partial class MemberOrderHelper<T> : ICollection<T>
    {
        int ICollection<T>.Count => Data.Count;

        bool ICollection<T>.IsReadOnly => true;

        void ICollection<T>.Add(T item)
        => Throw.NotSupportedException(nameof(MemberOrderHelper<T>), nameof(Add));

        void ICollection<T>.Clear()
        => Throw.NotSupportedException(nameof(MemberOrderHelper<T>), nameof(ICollection<T>.Clear));

        bool ICollection<T>.Contains(T item)
        => Data.Contains(item);

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            Utils.CheckArgumentNull(array, nameof(array));
            
            if (arrayIndex < 0)
            {
                Throw.ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, 0, array.Length);
            }

            if (arrayIndex + Data.Count > array.Length)
            {
                Throw.ArgumentException(nameof(arrayIndex), $"Collection contains {Data.Count} elements, which will not fit in array of Length {array.Length} starting at index {arrayIndex}");
            }

            // not looking to optimize this, because we don't really care about
            //     this interface... but need it for LINQ to play ball
            for (var i = 0; i < Data.Count; i++)
            {
                array[i + arrayIndex] = GetAt(i);
            }
        }

        bool ICollection<T>.Remove(T item)
        {
            Throw.NotSupportedException(nameof(MemberOrderHelper<T>), nameof(ICollection<T>.Remove));
            return default;
        }
    }
}
