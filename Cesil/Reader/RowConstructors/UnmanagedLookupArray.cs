using System;
using System.Buffers;
using System.Runtime.InteropServices;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal struct UnmanagedLookupArray<T>: ITestableDisposable
        where T: unmanaged
    {
        private static readonly unsafe int BYTES_PER_T = sizeof(T);
        private const int BYTES_PER_CHAR = sizeof(char);

        private int _Count;
        public int Count
        {
            get
            {
                AssertNotDisposed(this);

                return _Count;
            }
        }

        private readonly int NumElements;

        private IMemoryOwner<char>? Owner;

        public bool IsDisposed => Owner == null;

        private Span<T> Data
        {
            get
            {
                if (Owner != null)
                {
                    return MemoryMarshal.Cast<char, T>(Owner.Memory.Span).Slice(0, NumElements);
                }

                return default;
            }
        }

        public UnmanagedLookupArray(MemoryPool<char> pool, int elemCount)
        {
            _Count = 0;
            NumElements = elemCount;

            var totalBytesNeeded = (NumElements * BYTES_PER_T);
            var totalCharsNeeded = totalBytesNeeded / BYTES_PER_CHAR;
            if ((totalBytesNeeded % BYTES_PER_CHAR) != 0)
            {
                totalCharsNeeded++;
            }

            Owner = pool.Rent(totalCharsNeeded);

            Owner.Memory.Slice(0, totalCharsNeeded).Span.Clear();
        }

        public void Clear()
        {
            AssertNotDisposed(this);

            Data.Clear();
            _Count = 0;
        }

        public void Add(T item)
        {
            AssertNotDisposed(this);

            Set(_Count, item);
        }

        public void Set(int ix, T item)
        {
            AssertNotDisposed(this);

            var data = Data;

            data[ix] = item;

            _Count = Math.Max(_Count, ix + 1);
        }

        public void Get(int ix, T defaultValue, out T value)
        {
            AssertNotDisposed(this);

            if(ix >= Data.Length)
            {
                value = defaultValue;
                return;
            }

            value = Data[ix];
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
