using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace Cesil
{
    internal sealed class DynamicRow : IDynamicMetaObjectProvider, ITestableDisposable, IIntrusiveLinkedList<DynamicRow>
    {
        internal sealed class DynamicColumnEnumerator : IEnumerator<ColumnIdentifier>, ITestableDisposable
        {
            internal DynamicRow Row;
            private int NextIndex;
            private uint ExpectedGeneration;

            private ColumnIdentifier _Current;
            public ColumnIdentifier Current
            {
                get
                {
                    AssertNotDisposed();
                    Row?.AssertGenerationMatch(ExpectedGeneration);
                    return _Current;
                }
            }

            object IEnumerator.Current => Current;

            public bool IsDisposed => Row == null;

            internal DynamicColumnEnumerator(DynamicRow row)
            {
                Row = row;
                ExpectedGeneration = row.Generation;

                Reset();
            }

            public bool MoveNext()
            {
                AssertNotDisposed();
                Row?.AssertGenerationMatch(ExpectedGeneration);

                var ix = NextIndex;
                if (ix < Row.Width)
                {
                    var name = Row.Names?[ix];
                    _Current = ColumnIdentifier.Create(ix, name);

                    NextIndex++;
                    return true;
                }

                return false;
            }

            public void Reset()
            {
                AssertNotDisposed();
                Row?.AssertGenerationMatch(ExpectedGeneration);

                NextIndex = 0;
            }

            public void Dispose()
            {
                // generation intentionally not checked

                Row = null;
            }

            public void AssertNotDisposed()
            {
                if (IsDisposed)
                {
                    Throw.ObjectDisposedException(nameof(DynamicColumnEnumerator));
                }
            }

            public override string ToString()
            => $"{nameof(DynamicColumnEnumerator)} backed by {Row}";
        }

        internal sealed class DynamicColumnEnumerable : IReadOnlyList<ColumnIdentifier>
        {
            private readonly DynamicRow Row;

            public int Count => Row.Width;

            public ColumnIdentifier this[int index]
            {
                get
                {
                    Row.AssertNotDisposed();
                    var ix = index;
                    var name = Row.Names?[ix];

                    return ColumnIdentifier.Create(ix, name);
                }
            }

            internal DynamicColumnEnumerable(DynamicRow row)
            {
                Row = row;
            }

            public IEnumerator<ColumnIdentifier> GetEnumerator()
            => new DynamicColumnEnumerator(Row);

            IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

            public override string ToString()
            => $"{nameof(DynamicColumnEnumerable)} backed by {Row}";
        }

        private const int CHARS_PER_INT = sizeof(int) / sizeof(char);

        internal uint Generation;

        private int Width;

        public bool IsDisposed => MemoryPool == null;

        private MemoryPool<char> MemoryPool;

        internal ITypeDescriber Converter;
        internal int RowNumber;
        private string[] Names;

        private int CurrentDataOffset;

        internal IReadOnlyList<ColumnIdentifier> Columns;

        internal IDynamicRowOwner Owner;

        internal object Context;

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

        DynamicRow IIntrusiveLinkedList<DynamicRow>.Next { get; set; }
        DynamicRow IIntrusiveLinkedList<DynamicRow>.Previous { get; set; }

        internal void Init(
            IDynamicRowOwner owner,
            int rowNumber,
            int width,
            object ctx,
            ITypeDescriber converter,
            string[] names,
            MemoryPool<char> pool
        )
        {
            if (!IsDisposed)
            {
                Throw.InvalidOperationException("DynamicRow not in an uninitializable state");
            }

            // keep a single one of these around, but initialize it lazily for consistency
            if (Columns == null)
            {
                Columns = new DynamicColumnEnumerable(this);
            }

            Owner = owner;
            RowNumber = rowNumber;
            Converter = converter;
            MemoryPool = pool;
            Width = width;
            Context = ctx;
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

        internal object GetAt(int index)
        {
            AssertNotDisposed();

            if (!TryGetIndex(index, out var ret))
            {
                Throw.ArgumentOutOfRangeException(nameof(index), index, Width);
            }

            return ret;
        }

        internal object GetByIndex(Index index)
        {
            AssertNotDisposed();

            int actualIndex;
            if (index.IsFromEnd)
            {
                actualIndex = Width - index.Value;
            }
            else
            {
                actualIndex = index.Value;
            }

            if (!TryGetIndex(actualIndex, out var ret))
            {
                Throw.ArgumentOutOfRangeException(nameof(index), index, actualIndex, Width);
            }

            return ret;
        }

        internal T GetAtTyped<T>(in ColumnIdentifier index)
        => (T)(dynamic)GetByIdentifier(in index);

        internal object GetByIdentifier(in ColumnIdentifier index)
        {
            AssertNotDisposed();

            if (index.HasName)
            {
                if (TryGetValue(index.Name, out dynamic res))
                {
                    return res;
                }
            }
            else
            {
                if (TryGetIndex(index.Index, out dynamic res))
                {
                    return res;
                }
            }

            return default;
        }

        internal object GetByName(string column)
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

        internal DynamicRow GetRange(Range range)
        {
            AssertNotDisposed();

            string[] names;

            var startIndex = range.Start;
            var endIndex = range.End;

            int rawStart;
            int rawEnd;

            if (startIndex.IsFromEnd)
            {
                rawStart = Width - startIndex.Value;
            }
            else
            {
                rawStart = startIndex.Value;
            }

            if (endIndex.IsFromEnd)
            {
                rawEnd = Width - endIndex.Value;
            }
            else
            {
                rawEnd = endIndex.Value;
            }

            if (rawStart < 0 || rawStart > Width || rawEnd < 0 || rawEnd > Width)
            {
                Throw.ArgumentOutOfRangeException(nameof(range), range, rawStart, rawEnd, Width);
            }

            if (rawStart > rawEnd)
            {
                Throw.ArgumentException($"Start of range ({rawStart}) greater than end of range ({rawEnd}) in {range}", nameof(range));
            }

            var width = rawEnd - rawStart;

            var newRow = new DynamicRow();
            if (Names != null)
            {
                if (width == 0)
                {
                    names = Array.Empty<string>();
                }
                else
                {
                    names = new string[width];

                    var readFrom = rawStart;
                    for (var writeTo = 0; writeTo < width; writeTo++)
                    {
                        var val = Names[readFrom];
                        names[writeTo] = val;
                        readFrom++;
                    }
                }
            }
            else
            {
                names = null;
            }

            newRow.Init(Owner, RowNumber, width, Context, Converter, names, MemoryPool);

            // todo: it would be _nice_ to avoid a copy here
            //   we might be able to, if we are informed when THIS
            //   row is being disposed
            //
            // as it is now...
            //   we have to copy because otherwise re-using the base row
            //   might change the subset... and might make it invalid, even!
            var copyFrom = rawStart;
            for (var writeTo = 0; writeTo < width; writeTo++)
            {
                var span = GetDataSpan(copyFrom);
                newRow.SetValue(writeTo, span);
                copyFrom++;
            }

            if (Owner != null)
            {
                // by definition, the new row won't be the head, so we can skip the tricks needed for an empty list
                this.AddAfter(newRow);
            }

            return newRow;
        }

        internal ReadContext GetReadContext()
        {
            AssertNotDisposed();

            return ReadContext.ConvertingRow(RowNumber, Owner.Context);
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
                Throw.InvalidOperationException($"Underlying {nameof(DynamicRow)} modified during enumeration, generation mismatch");
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
                Context = null;
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(DynamicRow));
            }
        }

        public override string ToString()
        => $"{nameof(DynamicRow)} {nameof(Generation)}={Generation}, {nameof(Converter)}={Converter}, {nameof(RowNumber)}={RowNumber}";
    }
}
