using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

using static Cesil.DisposableHelper;

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
                    AssertNotDisposed(this);
                    Row?.AssertGenerationMatch(ExpectedGeneration);
                    return _Current;
                }
            }

            object IEnumerator.Current => Current;

            public bool IsDisposed { get; private set; }

            internal DynamicColumnEnumerator(DynamicRow row)
            {
                Row = row;
                ExpectedGeneration = row.Generation;

                Reset();
            }

            public bool MoveNext()
            {
                AssertNotDisposed(this);
                Row.AssertGenerationMatch(ExpectedGeneration);

                var ix = NextIndex;
                if (ix < Row.Width)
                {
                    if (Row.Names.HasValue)
                    {
                        var name = Row.Names.Value[ix];
                        _Current = ColumnIdentifier.CreateInner(ix, name);
                    }
                    else
                    {
                        _Current = ColumnIdentifier.Create(ix);
                    }

                    NextIndex++;
                    return true;
                }

                return false;
            }

            public void Reset()
            {
                AssertNotDisposed(this);
                Row?.AssertGenerationMatch(ExpectedGeneration);

                NextIndex = 0;
            }

            public void Dispose()
            {
                // generation intentionally not checked

                IsDisposed = true;
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
                    AssertNotDisposed(Row);

                    string? colName = null;

                    var ix = index;
                    if (Row.Names.HasValue)
                    {
                        var names = Row.Names.Value;
                        if (index < names.Length)
                        {
                            colName = names[ix];
                        }
                    }

                    return ColumnIdentifier.CreateInner(ix, colName);
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

        public bool IsDisposed { get; private set; }

        private NonNull<MemoryPool<char>> MemoryPool;

        internal NonNull<ITypeDescriber> Converter;

        internal int RowNumber;

        private NonNull<string[]> Names;

        private int CurrentDataOffset;

        internal NonNull<IReadOnlyList<ColumnIdentifier>> Columns;

        internal NonNull<IDynamicRowOwner> Owner;

        internal object? Context;

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
        private NonNull<IMemoryOwner<char>> Data;

        private NonNull<DynamicRow> _Next;
        ref NonNull<DynamicRow> IIntrusiveLinkedList<DynamicRow>.Next => ref _Next;

        private NonNull<DynamicRow> _Previous;
        ref NonNull<DynamicRow> IIntrusiveLinkedList<DynamicRow>.Previous => ref _Previous;

        internal DynamicRow()
        {
            IsDisposed = true;
        }

        internal void Init(
            IDynamicRowOwner owner,
            int rowNumber,
            object? ctx,
            ITypeDescriber converter,
            NonNull<string[]> names,
            MemoryPool<char> pool
        )
        {
            if (!IsDisposed)
            {
                Throw.InvalidOperationException<object>("DynamicRow not in an uninitialized state");
            }

            // keep a single one of these around, but initialize it lazily for consistency
            if (!Columns.HasValue)
            {
                Columns.Value = new DynamicColumnEnumerable(this);
            }

            Owner.Value = owner;
            RowNumber = rowNumber;
            Converter.Value = converter;
            MemoryPool.Value = pool;
            Width = 0;
            Context = ctx;
            Names = names;
            Generation++;

            IsDisposed = false;
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
            if (!Data.HasValue)
            {
                var initialSize = Width * CHARS_PER_INT + CharsToStore(text);

                var dataValue = MemoryPool.Value.Rent(initialSize);
                Data.Value = dataValue;
                CurrentDataOffset = dataValue.Memory.Length;
            }

            Width = Math.Max(Width, index + 1);

            StoreDataSpan(text);
            StoreDataIndex(index, CurrentDataOffset);
        }

        internal ReadOnlySpan<char> GetDataSpan(int forCellNumber)
        {
            AssertNotDisposed(this);

            var dataIx = GetDataIndex(forCellNumber);

            var fromOffset = Data.Value.Memory.Span.Slice(dataIx);
            var asIntSpan = MemoryMarshal.Cast<char, int>(fromOffset);
            var length = asIntSpan[0];
            var fromData = MemoryMarshal.Cast<int, char>(asIntSpan.Slice(1));

            var ret = fromData.Slice(0, length);

            return ret;
        }

        internal object? GetAt(int index)
        {
            AssertNotDisposed(this);

            if (!TryGetIndex(index, out var ret))
            {
                return Throw.ArgumentOutOfRangeException<object>(nameof(index), index, Width);
            }

            return ret;
        }

        internal object? GetByIndex(Index index)
        {
            AssertNotDisposed(this);

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
                return Throw.ArgumentOutOfRangeException<object>(nameof(index), index, actualIndex, Width);
            }

            return ret;
        }

        internal T GetAtTyped<T>(in ColumnIdentifier index)
        {
            dynamic? toCast = GetByIdentifier(in index);

            return (T)toCast!;
        }

        internal object? GetByIdentifier(in ColumnIdentifier index)
        {
            AssertNotDisposed(this);

            if (index.HasName)
            {
                if (TryGetValue(index.Name, out var res))
                {
                    return res;
                }
            }
            else
            {
                if (TryGetIndex(index.Index, out var res))
                {
                    return res;
                }
            }

            return default;
        }

        internal object? GetByName(string column)
        {
            AssertNotDisposed(this);

            if (!TryGetValue(column, out var ret))
            {
                return Throw.KeyNotFoundException<object>(column);
            }

            return ret;
        }

        internal DynamicCell? GetCellAt(int ix)
        {
            AssertNotDisposed(this);

            var dataIndex = GetDataIndex(ix);
            if (dataIndex == -1)
            {
                return null;
            }

            return new DynamicCell(this, ix);
        }

        internal DynamicRow GetRange(Range range)
        {
            AssertNotDisposed(this);

            string[]? names;

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
                return Throw.ArgumentOutOfRangeException<DynamicRow>(nameof(range), range, rawStart, rawEnd, Width);
            }

            if (rawStart > rawEnd)
            {
                return Throw.ArgumentException<DynamicRow>($"Start of range ({rawStart}) greater than end of range ({rawEnd}) in {range}", nameof(range));
            }

            var width = rawEnd - rawStart;

            var newRow = new DynamicRow();
            if (Names.HasValue)
            {
                if (width == 0)
                {
                    names = Array.Empty<string>();
                }
                else
                {
                    var namesValue = Names.Value;

                    names = new string[width];

                    var readFrom = rawStart;
                    for (var writeTo = 0; writeTo < width; writeTo++)
                    {
                        var val = namesValue[readFrom];
                        names[writeTo] = val;
                        readFrom++;
                    }
                }
            }
            else
            {
                names = null;
            }

            var namesNonNull = new NonNull<string[]>();
            namesNonNull.SetAllowNull(names);

            newRow.Init(Owner.Value, RowNumber, Context, Converter.Value, namesNonNull, MemoryPool.Value);

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

            if (Owner.HasValue)
            {
                // by definition, the new row won't be the head, so we can skip the tricks needed for an empty list
                this.AddAfter(newRow);
            }

            return newRow;
        }

        internal ReadContext GetReadContext()
        {
            AssertNotDisposed(this);

            var owner = Owner.Value;

            return ReadContext.ConvertingRow(owner.Options, RowNumber, owner.Context);
        }

        private bool TryGetIndex(int index, out object? result)
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

        private bool TryGetValue(string lookingFor, out object? result)
        {
            var namesHasVal = Names.HasValue;
            var namesVal = namesHasVal ? Names.Value : Array.Empty<string>();

            for (var i = 0; i < Width; i++)
            {
                if (namesHasVal && (namesVal[i]?.Equals(lookingFor) ?? false))
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
            var dataSpan = Data.Value.Memory.Span;
            var dataUIntSpan = MemoryMarshal.Cast<char, uint>(dataSpan);

            dataUIntSpan[atIndex] = DataOffsetForStorage(dataIx);
        }

        private int GetDataIndex(int atIndex)
        {
            if (!Data.HasValue)
            {
                // nothing has been stored
                return -1;
            }

            var dataSpan = Data.Value.Memory.Span;
            var dataUIntSpan = MemoryMarshal.Cast<char, uint>(dataSpan);

            var storageOffset = dataUIntSpan[atIndex];

            return StorageToDataOffset(storageOffset);
        }

        private void ResizeData(int minSize)
        {
            var dataValue = Data.Value;

            var newData = MemoryPool.Value.Rent(minSize);
            var diff = newData.Memory.Length - dataValue.Memory.Length;

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
            var oldDataSpan = dataValue.Memory.Span.Slice(CurrentDataOffset);
            var newDataSpan = newData.Memory.Span.Slice(newCurrentOffset);
            oldDataSpan.CopyTo(newDataSpan);

            // toss the old data
            dataValue.Dispose();

            // update references
            CurrentDataOffset = newCurrentOffset;
            Data.Value = newData;
        }

        private void StoreDataSpan(ReadOnlySpan<char> data)
        {
checkSize:
            var dataValue = Data.Value;

            var desiredInsertionIx = CurrentDataOffset - CharsToStore(data) - 1;
            var dataOffsetStopIx = Width * CHARS_PER_INT;

            if (desiredInsertionIx < dataOffsetStopIx)
            {
                var minSize = dataOffsetStopIx - desiredInsertionIx + dataValue.Memory.Length;
                ResizeData(minSize);

                goto checkSize;
            }

            var charSpan = dataValue.Memory.Span.Slice(desiredInsertionIx);
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
                Throw.InvalidOperationException<object>($"Underlying {nameof(DynamicRow)} modified during enumeration, generation mismatch");
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                CurrentDataOffset = -1;
                if (Data.HasValue)
                {
                    Data.Value.Dispose();
                }

                Context = null;

                Data.Clear();
                Names.Clear();
                MemoryPool.Clear();

                // important, not clearing Owner, _Next, or Previous here ; doing so will break ownership and disposal management

                IsDisposed = true;
            }
        }

        public override string ToString()
        => $"{nameof(DynamicRow)} {nameof(Generation)}={Generation}, {nameof(Converter)}={Converter}, {nameof(RowNumber)}={RowNumber}";
    }
}
