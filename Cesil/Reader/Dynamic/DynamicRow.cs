using System;
using System.Buffers;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace Cesil
{
    internal sealed class DynamicRow : IDynamicMetaObjectProvider, ITestableDisposable
    {
        private const int CHARS_PER_INT = sizeof(int) / sizeof(char);

        internal uint Generation;

        internal int Width;

        public bool IsDisposed => MemoryPool == null;

        private MemoryPool<char> MemoryPool;

        internal IDynamicTypeConverter Converter;
        internal int RowNumber;
        internal string[] Names;

        private int CurrentDataOffset;

        internal IDynamicRowOwner Owner;

        // we store data in here like so:
        //  <front (low address)>
        //    * <index for col #0>
        //    * <index for col #1>
        //    * <index for col #2>
        //    * ...
        //    * <data for col #2> = (length) data
        //    * <data for col #1> = (length) data
        //    * <data for col #0> = (length) data
        //  <back (high address)>
        private IMemoryOwner<char> Data;

        internal void Init(IDynamicRowOwner owner, int rowNumber, int width, IDynamicTypeConverter converter, string[] names, MemoryPool<char> pool)
        {
            if (!IsDisposed)
            {
                Throw.InvalidOperationException("DynamicRow not in an uninitializable state");
            }

            Owner = owner;
            RowNumber = rowNumber;
            Converter = converter;
            MemoryPool = pool;
            Width = width;
            Names = names;
            Generation++;
        }

        public DynamicMetaObject GetMetaObject(Expression exp)
        {
            // explicitly not doing an AssertNotDisposed here
            //   because when this is called is _super_ 
            //   non-obvious... and isn't actually functional
            //   we'll explode elsewhere

            return new DynamicRowMetaObject(this, exp);
        }

        internal void SetValue(int index, ReadOnlySpan<char> text)
        {
            if (Data == null)
            {
                var initialSize = Width * CHARS_PER_INT + CharsToStore(text);

                Data = MemoryPool.Rent(initialSize);
                CurrentDataOffset = Data.Memory.Length;
            }

            StoreDataSpan(text);
            StoreDataIndex(index, CurrentDataOffset);
        }

        internal ReadOnlySpan<char> GetDataSpan(int forCellNumber)
        {
            AssertNotDisposed();

            var dataIx = GetDataIndex(forCellNumber);

            var fromOffset = Data.Memory.Span.Slice(dataIx);
            var asIntSpan = MemoryMarshal.Cast<char, int>(fromOffset);
            var length = asIntSpan[0];
            var fromData = MemoryMarshal.Cast<int, char>(asIntSpan.Slice(1));

            var ret = fromData.Slice(0, length);

            return ret;
        }

        internal object GetIndex(int index)
        {
            AssertNotDisposed();

            if (!TryGetIndex(index, out var ret))
            {
                Throw.ArgumentOutOfRangeException(nameof(index), index, Width);
            }

            return ret;
        }

        internal T GetIndexTyped<T>(int index)
        {
            AssertNotDisposed();

            if (!TryGetIndex(index, out dynamic res))
            {
                return default;
            }

            var ret = (T)res;

            return ret;
        }

        internal object GetValue(string column)
        {
            AssertNotDisposed();

            if (!TryGetValue(column, out var ret))
            {
                Throw.KeyNotFoundException(column);
            }

            return ret;
        }

        internal DynamicCell GetCellAt(int ix)
        {
            AssertNotDisposed();

            var dataIndex = GetDataIndex(ix);
            if (dataIndex == -1)
            {
                return null;
            }

            return new DynamicCell(this, ix);
        }

        internal ReadContext GetReadContext()
        {
            AssertNotDisposed();

            return new ReadContext(RowNumber, 0, null, Owner.Context);
        }

        private bool TryGetIndex(int index, out object result)
        {
            if (index < 0 || index >= Width)
            {
                result = null;
                return false;
            }

            if (!IsSet(index))
            {
                result = null;
                return true;
            }

            result = GetCellAt(index);
            return true;
        }

        private bool TryGetValue(string lookingFor, out object result)
        {
            for (var i = 0; i < Width; i++)
            {
                if (Names?[i]?.Equals(lookingFor) ?? false)
                {
                    if (!IsSet(i))
                    {
                        result = null;
                        return true;
                    }

                    result = GetCellAt(i);
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static int CharsToStore(ReadOnlySpan<char> text)
        => text.Length + CHARS_PER_INT;

        private uint DataOffsetForStorage(int offset)
        {
            // increment by 1 so we can distinguish set and not 0
            var asUint = (uint)offset;
            asUint++;

            return asUint;
        }

        private int StorageToDataOffset(uint stored)
        {
            if (stored == 0)
            {
                return -1;
            }

            var ret = (int)(stored - 1);
            return ret;
        }

        private void StoreDataIndex(int atIndex, int dataIx)
        {
            var dataSpan = Data.Memory.Span;
            var dataUIntSpan = MemoryMarshal.Cast<char, uint>(dataSpan);

            dataUIntSpan[atIndex] = DataOffsetForStorage(dataIx);
        }

        private int GetDataIndex(int atIndex)
        {
            if (Data == null)
            {
                // nothing has been stored
                return -1;
            }

            var dataSpan = Data.Memory.Span;
            var dataUIntSpan = MemoryMarshal.Cast<char, uint>(dataSpan);

            var storageOffset = dataUIntSpan[atIndex];

            return StorageToDataOffset(storageOffset);
        }

        private void ResizeData(int minSize)
        {
            var newData = MemoryPool.Rent(minSize);
            var diff = newData.Memory.Length - Data.Memory.Length;

            // move all the offsets forward by the size change
            var newAsUInt = MemoryMarshal.Cast<char, uint>(newData.Memory.Span);
            for (var i = 0; i < Width; i++)
            {
                var oldOffset = GetDataIndex(i);
                if (oldOffset == -1) continue;

                var newOffset = oldOffset + diff;

                newAsUInt[i] = DataOffsetForStorage(newOffset);
            }

            var newCurrentOffset = CurrentDataOffset + diff;

            // copy old data into the end of the new data
            var oldDataSpan = Data.Memory.Span.Slice(CurrentDataOffset);
            var newDataSpan = newData.Memory.Span.Slice(newCurrentOffset);
            oldDataSpan.CopyTo(newDataSpan);

            // toss the old data
            Data.Dispose();

            // update references
            CurrentDataOffset = newCurrentOffset;
            Data = newData;
        }

        private void StoreDataSpan(ReadOnlySpan<char> data)
        {
            checkSize:
            var desiredInsertionIx = CurrentDataOffset - CharsToStore(data) - 1;
            var dataOffsetStopIx = Width * CHARS_PER_INT;

            if (desiredInsertionIx < dataOffsetStopIx)
            {
                var minSize = dataOffsetStopIx - desiredInsertionIx + Data.Memory.Length;
                ResizeData(minSize);

                goto checkSize;
            }

            var charSpan = Data.Memory.Span.Slice(desiredInsertionIx);
            var intSpan = MemoryMarshal.Cast<char, int>(charSpan);
            intSpan[0] = data.Length;
            var charDestSpan = MemoryMarshal.Cast<int, char>(intSpan.Slice(1));
            data.CopyTo(charDestSpan);

            CurrentDataOffset = desiredInsertionIx;
        }

        internal bool IsSet(int ix)
        => GetDataIndex(ix) != -1;

        internal void AssertGenerationMatch(uint gen)
        {
            if (gen != Generation)
            {
                Throw.ObjectDisposedException(nameof(DynamicRow));
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                CurrentDataOffset = -1;
                Data?.Dispose();
                Data = null;
                MemoryPool = null;
                Names = null;
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(DynamicRow));
            }
        }
    }
}
