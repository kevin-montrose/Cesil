using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicRow : IDynamicMetaObjectProvider, ITestableDisposable, IIntrusiveLinkedList<DynamicRow>
    {
        internal sealed class DynamicColumnEnumerator : IEnumerator<ColumnIdentifier>, ITestableDisposable
        {
            internal DynamicRow Row;
            private int NextIndex;

            private readonly uint ExpectedGeneration;
            private readonly int? Offset;
            private readonly int? Length;

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

            [ExcludeFromCoverage("Trivial, and covered by IEnumerator<T>.Current")]
            object IEnumerator.Current => Current;

            public bool IsDisposed { get; private set; }

            internal DynamicColumnEnumerator(DynamicRow row, int? offset, int? length)
            {
                Row = row;
                ExpectedGeneration = row.Generation;

                Offset = offset;
                Length = length;

                Reset();
            }

            public bool MoveNext()
            {
                AssertNotDisposed(this);
                Row.AssertGenerationMatch(ExpectedGeneration);

                var trueWidth = Length ?? Row.Width;

                var ix = NextIndex;
                if (ix < trueWidth)
                {
                    var trueIx = ix;
                    trueIx += Offset ?? 0;

                    if (Row.HasNames)
                    {
                        var name = Row.Names[trueIx];
                        _Current = ColumnIdentifier.CreateInner(ix, name, null);
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
                    // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

                    string? colName = null;

                    var ix = index;
                    if (Row.HasNames)
                    {
                        var names = Row.Names;
                        if (index < names.Length)
                        {
                            colName = names[ix];
                        }
                    }

                    return ColumnIdentifier.CreateInner(ix, colName, null);
                }
            }

            internal DynamicColumnEnumerable(DynamicRow row)
            {
                Row = row;
            }

            public IEnumerator<ColumnIdentifier> GetEnumerator()
            => new DynamicColumnEnumerator(Row, null, null);

            [ExcludeFromCoverage("Trivial, and covered by IEnumerable<T>.GetEnumerator()")]
            IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

            public override string ToString()
            => $"{nameof(DynamicColumnEnumerable)} backed by {Row}";
        }

        private const int CHARS_PER_INT = sizeof(int) / sizeof(char);

        internal uint Generation;

        internal int Width;

        internal volatile int OutstandingUsesOfData;

        public bool IsDisposed { get; private set; }

        private MemoryPool<char> MemoryPool;

        internal ITypeDescriber Converter;

        internal int RowNumber;

        private bool HasNames;
        private string[] Names;
        private int NamesIndexOffset;
        private NameLookup NameLookup;

        private int CurrentDataOffset;

        internal IReadOnlyList<ColumnIdentifier> Columns;

        internal bool HasOwner;
        internal IDynamicRowOwner Owner;

        internal object? Context;

        private bool HasData;
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
        private Memory<char> DataMemory;

        private NonNull<DynamicRow> _Next;
        ref NonNull<DynamicRow> IIntrusiveLinkedList<DynamicRow>.Next => ref _Next;

        private NonNull<DynamicRow> _Previous;
        ref NonNull<DynamicRow> IIntrusiveLinkedList<DynamicRow>.Previous => ref _Previous;

        internal DynamicRow()
        {
            IsDisposed = true;
            OutstandingUsesOfData = 0;

            // we only keep one of these around for the lifetime of this row
            Columns = new DynamicColumnEnumerable(this);

            // won't ever actually be used, just avoid the null
            MemoryPool = MemoryPool<char>.Shared;

            // likewise, will be inited before we touch them
            HasNames = false;
            Names = Array.Empty<string>();
            NamesIndexOffset = 0;

            // again, no data
            HasData = false;
            Data = EmptyMemoryOwner.Singleton;
            DataMemory = Memory<char>.Empty;

            // nada until we touch it
            HasOwner = false;
            Owner = EmptyDynamicRowOwner.Singleton;

            // doesn't matter, it'll get overwritten
            Converter = TypeDescribers.Default;
        }

        internal void Init(
            IDynamicRowOwner owner,
            int rowNumber,
            object? ctx,
            ITypeDescriber converter,
            bool hasNames,
            string[]? names,
            int namesIndexOffset,
            MemoryPool<char> pool
        )
        {
            if (!IsDisposed)
            {
                Throw.InvalidOperationException<object>($"{nameof(DynamicRow)} not in an uninitialized state");
            }

            if (OutstandingUsesOfData != 0)
            {
                Throw.InvalidOperationException<object>($"{nameof(DynamicRow)} data is not in an uninitialized state");
            }

            HasOwner = true;
            Owner = owner;
            RowNumber = rowNumber;
            Converter = converter;
            MemoryPool = pool;
            Width = 0;
            Context = ctx;
            HasNames = hasNames;
            if (HasNames)
            {
                Names = Utils.NonNull(names);
                NamesIndexOffset = namesIndexOffset;
                NameLookup = owner.AcquireNameLookup();
            }
            else
            {
                Names = Array.Empty<string>();
                NamesIndexOffset = 0;
                NameLookup = default;
            }
            Generation++;

            Interlocked.Exchange(ref OutstandingUsesOfData, 1);
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
            AssertNotDisposedInternal(this);

            InitializeDataIfNeeded(Width * CHARS_PER_INT + CharsToStore(text));

            Width = Math.Max(Width, index + 1);

            StoreDataSpan(text);
            StoreDataIndex(index, CurrentDataOffset);
        }

        internal void SetNull(int index)
        {
            AssertNotDisposedInternal(this);

            InitializeDataIfNeeded((index + 1) * CHARS_PER_INT);

            Width = Math.Max(Width, index + 1);

            StoreDataSpan(ReadOnlySpan<char>.Empty);
            StoreDataIndex(index, -1);
        }

        private void InitializeDataIfNeeded(int initialSize)
        {
            if (!HasData)
            {
                InitializeData(initialSize);
            }
        }

        private void InitializeData(int initialSize)
        {
            var dataValue = MemoryPool.Rent(initialSize);
            HasData = true;
            Data = dataValue;
            DataMemory = Data.Memory;
            CurrentDataOffset = DataMemory.Length;
        }

        internal void PadWithNulls(int trailingCount)
        {
            AssertNotDisposedInternal(this);

            if (!HasData)
            {
                InitializeData(trailingCount * CHARS_PER_INT);
            }
            else
            {
                var startOfNulls = Width * CHARS_PER_INT;
                var neededSpace = trailingCount * CHARS_PER_INT;

                var endOfNulls = startOfNulls + neededSpace;

checkSize:
                if (CurrentDataOffset < endOfNulls)
                {
                    var minSize = Data.Memory.Length + (endOfNulls - CurrentDataOffset);
                    ResizeData(minSize);

                    goto checkSize;
                }
            }

            for (var i = 0; i < trailingCount; i++)
            {
                StoreDataIndex(Width + i, -1);
            }

            Width += trailingCount;
        }

        internal bool TryGetDataSpan(int forCellNumber, out ReadOnlySpan<char> span)
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            var dataIx = GetDataIndex(forCellNumber);
            if (dataIx == -1)
            {
                span = default;
                return false;
            }

            var fromOffset = DataMemory.Span.Slice(dataIx);
            var asIntSpan = MemoryMarshal.Cast<char, int>(fromOffset);
            var length = asIntSpan[0];
            var fromData = MemoryMarshal.Cast<int, char>(asIntSpan.Slice(1));

            var ret = fromData.Slice(0, length);

            span = ret;
            return true;
        }

        internal object? GetAt(int index, ITestableDisposable dependsOn, int? offset, int? length)
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            if (!TryGetIndex(index, out var ret, dependsOn, offset, length))
            {
                return Throw.ArgumentOutOfRangeException<object>(nameof(index), index, length ?? Width);
            }

            return ret;
        }

        internal object? GetByIndex(Index index, ITestableDisposable dependsOn, int? offset, int? length)
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            int actualIndex;
            if (index.IsFromEnd)
            {
                actualIndex = (length ?? Width) - index.Value;
            }
            else
            {
                actualIndex = index.Value;
            }

            if (!TryGetIndex(actualIndex, out var ret, dependsOn, offset, length))
            {
                return Throw.ArgumentOutOfRangeException<object>(nameof(index), index, actualIndex, Width);
            }

            return ret;
        }

        internal T GetAtTyped<T>(in ColumnIdentifier index, ITestableDisposable dependsOn, int? offset, int? length)
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            dynamic? toCast = GetByIdentifier(in index, dependsOn, offset, length);

#pragma warning disable CES0005 // T is generic, so we can't annotate it (could be a class or struct) but we want dynamic to try and convert regardless
            return (T)toCast!;
#pragma warning restore CES0005
        }

        internal object? GetByIdentifier(in ColumnIdentifier index, ITestableDisposable dependsOn, int? offset, int? length)
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            if (index.HasName && HasNames)
            {
                if (TryGetValue(index.Name, out var res, dependsOn, offset, length))
                {
                    return res;
                }

                return Throw.KeyNotFoundException<object>(index.Name);
            }
            else
            {
                if (TryGetIndex(index.Index, out var res, dependsOn, offset, length))
                {
                    return res;
                }

                return Throw.ArgumentOutOfRangeException<object>(nameof(index), index.Index, length ?? Width);
            }
        }

        internal object? GetByName(string column, ITestableDisposable dependsOn, int? offset, int? length)
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            if (!TryGetValue(column, out var ret, dependsOn, offset, length))
            {
                return Throw.KeyNotFoundException<object>(column);
            }

            return ret;
        }

        internal DynamicCell? GetCellAt(ITestableDisposable dependsOn, int ix)
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            var dataIndex = GetDataIndex(ix);
            if (dataIndex == -1)
            {
                return null;
            }

            return new DynamicCell(this, dependsOn, ix);
        }

        internal DynamicRowRange GetRange(Range range, int? offset, int? length)
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            var actualStart = offset ?? 0;
            var actualWidth = length ?? Width;
            var actualEnd = actualStart + actualWidth;

            var startIndex = range.Start;
            var endIndex = range.End;

            int rawStart;
            int rawEnd;

            if (startIndex.IsFromEnd)
            {
                rawStart = actualEnd - startIndex.Value;
            }
            else
            {
                rawStart = actualStart + startIndex.Value;
            }

            if (endIndex.IsFromEnd)
            {
                rawEnd = actualEnd - endIndex.Value;
            }
            else
            {
                rawEnd = actualStart + endIndex.Value;
            }

            if (rawStart < actualStart || rawStart > actualEnd || rawEnd < actualStart || rawEnd > actualEnd)
            {
                return Throw.ArgumentOutOfRangeException<DynamicRowRange>(nameof(range), range, actualStart, actualEnd, actualWidth);
            }

            if (rawStart > rawEnd)
            {
                return Throw.ArgumentException<DynamicRowRange>($"Start of range ({rawStart}) greater than end of range ({rawEnd}) in {range}", nameof(range));
            }

            var width = rawEnd - rawStart;

            // don't actually release this row until all the ranges are also gone
            Interlocked.Increment(ref OutstandingUsesOfData);

            return new DynamicRowRange(this, rawStart, width);
        }

        internal ReadContext GetReadContext(int? offset, int? length)
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            var owner = Owner;

            return ReadContext.ConvertingRow(owner.Options, RowNumber, owner.Context);
        }

        private bool TryGetIndex(int index, out object? result, ITestableDisposable dependsOn, int? offset, int? length)
        {
            if (offset.HasValue && length.HasValue && index >= length.Value)
            {
                result = null;
                return false;
            }

            var actualStart = offset ?? 0;

            var actualIndex = index + actualStart;

            if (actualIndex < actualStart || actualIndex >= Width)
            {
                result = null;
                return false;
            }

            if (!IsSet(actualIndex))
            {
                result = null;
                return true;
            }

            result = GetCellAt(dependsOn, actualIndex);
            return true;
        }

        private bool TryGetValue(string lookingFor, out object? result, ITestableDisposable dependsOn, int? offset, int? length)
        {
            if (HasNames)
            {
                if (NameLookup.TryLookup(lookingFor, out var index))
                {
                    // only need to check that we can actually find it, the true offset is fine for fetching
                    if (offset.HasValue && length.HasValue)
                    {
                        if (index < offset.Value || index >= (offset.Value + length.Value))
                        {
                            result = null;
                            return false;
                        }
                    }

                    // we might be in a row fetched by GetRange
                    //   in which case _our_ index is a bit different from
                    //   the outer index
                    var adjuestedIndex = index + NamesIndexOffset;

                    // trying to access a column that isn't part of this subset?
                    if (adjuestedIndex < 0 || adjuestedIndex >= Width)
                    {
                        result = null;
                        return false;
                    }

                    if (!IsSet(adjuestedIndex))
                    {
                        result = null;
                        return true;
                    }

                    result = GetCellAt(dependsOn, adjuestedIndex);
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static int CharsToStore(ReadOnlySpan<char> text)
        => text.Length + CHARS_PER_INT;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint DataOffsetForStorage(int offset)
        {
            // increment by 1 so we can distinguish set and not 0
            var asUint = (uint)offset;
            asUint++;

            return asUint;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var dataSpan = DataMemory.Span;
            var dataUIntSpan = MemoryMarshal.Cast<char, uint>(dataSpan);

            dataUIntSpan[atIndex] = DataOffsetForStorage(dataIx);
        }

        // internal for testing purposes
        internal int GetDataIndex(int atIndex)
        {
            if (!HasData)
            {
                // nothing has been stored
                return -1;
            }

            if (atIndex >= Width)
            {
                // past the end
                return -1;
            }

            var dataSpan = DataMemory.Span;
            var dataUIntSpan = MemoryMarshal.Cast<char, uint>(dataSpan);

            var storageOffset = dataUIntSpan[atIndex];

            return StorageToDataOffset(storageOffset);
        }

        private void ResizeData(int minSize)
        {
            var dataValue = Data;

            var newData = MemoryPool.Rent(minSize);
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
            HasData = true;
            Data = newData;
            DataMemory = Data.Memory;
        }

        private void StoreDataSpan(ReadOnlySpan<char> data)
        {
checkSize:
            var dataValue = Data;

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
        {
            // not checking disposal here as it could be accessed, post visible disposal, via a DynamicRowRange

            return GetDataIndex(ix) != -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AssertGenerationMatch(uint gen)
        {
            if (gen != Generation)
            {
                Throw.InvalidOperationException<object>($"Underlying {nameof(DynamicRow)} modified during enumeration, generation mismatch");
            }
        }

        internal bool TryDataDispose(bool force = false)
        {
            if (CurrentDataOffset == -1)
            {
                return true;
            }

            var ret = Interlocked.Decrement(ref OutstandingUsesOfData);
            if (ret == 0 || force)
            {
                IsDisposed = true;

                CurrentDataOffset = -1;
                if (HasData)
                {
                    Data.Dispose();
                }

                Context = null;

                HasData = false;
                Data = EmptyMemoryOwner.Singleton;
                DataMemory = Memory<char>.Empty;
                HasNames = false;
                Names = Array.Empty<string>();
                MemoryPool = MemoryPool<char>.Shared;

                // if we never acquired one this is a no-op
                Owner.ReleaseNameLookup();

                // important, not clearing Owner, _Next, or Previous here ; doing so will break ownership and disposal management

                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                TryDataDispose();
            }
        }

        public override string ToString()
        => $"{nameof(DynamicRow)} {nameof(Generation)}={Generation}, {nameof(Converter)}={Converter}, {nameof(RowNumber)}={RowNumber}";
    }
}
