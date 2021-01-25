using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    internal delegate void StartRowDelegate(in ReadContext ctx);
    internal delegate bool TryPreAllocateDelegate<TRow>(in ReadContext ctx, bool checkPrealloc, ref TRow prealloc);
    internal delegate bool GeneratedColumnAvailableDelegate(ReadOnlySpan<char> data, in ReadContext ctx);
    internal delegate ref Memory<int> GetColumnMapDelegate();

    internal sealed class AheadOfTimeRowConstructor<TRow> : IRowConstructor<TRow>
    {
        private readonly TypeInfo GeneratedRowConstructorType;

        private readonly StartRowDelegate _StartRow;
        private readonly TryPreAllocateDelegate<TRow> _TryPreAllocate;
        private readonly GeneratedColumnAvailableDelegate _ColumnAvailable;
        private readonly Func<TRow> _FinishRow;

        private readonly GetColumnMapDelegate _CurrentColumnMap;
        private IMemoryOwner<int>? _CurrentColumnMap_Memory;

        private readonly Func<ImmutableArray<string>> _Columns;
        public IEnumerable<string> Columns => _Columns();

        private readonly Func<bool> _RowStarted;
        public bool RowStarted => _RowStarted();

        public bool IsDisposed { get; private set; }

        internal AheadOfTimeRowConstructor(
            TypeInfo generatedRowConstructorType,
            StartRowDelegate startRow,
            TryPreAllocateDelegate<TRow> tryPreAllocate,
            GeneratedColumnAvailableDelegate columnAvailable,
            Func<TRow> finishRow,
            GetColumnMapDelegate currentColumnMap,
            Func<ImmutableArray<string>> columns,
            Func<bool> rowStarted
        )
        {
            GeneratedRowConstructorType = generatedRowConstructorType;
            _StartRow = startRow;
            _TryPreAllocate = tryPreAllocate;
            _ColumnAvailable = columnAvailable;
            _FinishRow = finishRow;
            _CurrentColumnMap = currentColumnMap;
            _Columns = columns;
            _RowStarted = rowStarted;
        }

        internal static AheadOfTimeRowConstructor<TRow> Create(TypeInfo generatedRowConstructorType)
        {
            var rowType = typeof(TRow).GetTypeInfo();

            var cons = generatedRowConstructorType.GetConstructorNonNull(Array.Empty<TypeInfo>());

            var generatedRowConstructor = cons.Invoke(Array.Empty<object>());

            var columns = generatedRowConstructorType.GetPropertyNonNull("__ColumnNames", PublicStatic);
            var columnsGet = Utils.NonNull(columns.GetMethod);
            var columnsDel = (Func<ImmutableArray<string>>)Delegate.CreateDelegate(Types.FuncOfImmutableArrayOfString, columnsGet);


            var rowStarted = generatedRowConstructorType.GetPropertyNonNull("__RowStarted", PublicInstance);
            var rowStartedGet = Utils.NonNull(rowStarted.GetMethod);
            var rowStartedDel = (Func<bool>)Delegate.CreateDelegate(Types.FuncOfBool, generatedRowConstructor, rowStartedGet);

            var startRowMtd = generatedRowConstructorType.GetMethodNonNull("StartRow", PublicInstance);
            var startRowDel = (StartRowDelegate)Delegate.CreateDelegate(Types.StartRowDelegate, generatedRowConstructor, startRowMtd);

            var tryPreAllocateMtd = generatedRowConstructorType.GetMethodNonNull("TryPreAllocate", PublicInstance);
            var tryPreAllocateDelOfTRow = Types.TryPreAllocateDelegateOfT.MakeGenericType(rowType);
            var tryPreAllocateDel = (TryPreAllocateDelegate<TRow>)Delegate.CreateDelegate(tryPreAllocateDelOfTRow, generatedRowConstructor, tryPreAllocateMtd);

            var columnAvailableMtd = generatedRowConstructorType.GetMethodNonNull("__ColumnAvailable", PublicInstance);
            var columnAvailableDel = (GeneratedColumnAvailableDelegate)Delegate.CreateDelegate(Types.GeneratedColumnAvailableDelegate, generatedRowConstructor, columnAvailableMtd);

            var finishRowMtd = generatedRowConstructorType.GetMethodNonNull("FinishRow", PublicInstance);
            var funcOfTRow = Types.FuncOfT.MakeGenericType(rowType);
            var finishRowDel = (Func<TRow>)Delegate.CreateDelegate(funcOfTRow, generatedRowConstructor, finishRowMtd);

            var currentColumnMap = generatedRowConstructorType.GetPropertyNonNull("__CurrentColumnMap", PublicInstance);
            var currentColumnMapGet = Utils.NonNull(currentColumnMap.GetMethod);
            var currentColumnMapDel = (GetColumnMapDelegate)Delegate.CreateDelegate(Types.GetColumnMapDelegate, generatedRowConstructor, currentColumnMapGet);

            return new AheadOfTimeRowConstructor<TRow>(generatedRowConstructorType, startRowDel, tryPreAllocateDel, columnAvailableDel, finishRowDel, currentColumnMapDel, columnsDel, rowStartedDel);
        }

        public IRowConstructor<TRow> Clone(Options options)
        {
            var ret = Create(GeneratedRowConstructorType);

            // setup the defaul memory map
            var alloc = options.MemoryPoolProvider.GetMemoryPool<int>();

            var cols = ret._Columns();
            ref var mapMem = ref ret._CurrentColumnMap();

            var mapOwner = alloc.Rent(cols.Length);
            mapMem = mapOwner.Memory[0..cols.Length];

            var mapSpan = mapMem.Span;
            for (var i = 0; i < mapSpan.Length; i++)
            {
                mapSpan[i] = i;
            }

            ret._CurrentColumnMap_Memory = mapOwner;

            return ret;
        }

        public void StartRow(in ReadContext ctx)
        => _StartRow(in ctx);

        public bool TryPreAllocate(in ReadContext ctx, bool checkPrealloc, ref TRow prealloc)
        => _TryPreAllocate(in ctx, checkPrealloc, ref prealloc);

        public void ColumnAvailable(Options options, int rowNumber, int columnNumber, object? context, in ReadOnlySpan<char> data)
        {
            ref var mapMem = ref _CurrentColumnMap();
            var colNames = _Columns();

            var mapSpan = mapMem.Span;

            if (columnNumber >= mapSpan.Length)
            {
                // it'll be ignored anyway, so ignoring here is fine
                return;
            }

            var originalColumnNumber = mapSpan[columnNumber];

            if (originalColumnNumber == -1)
            {
                // column not actually mapped
                return;
            }

            var name = colNames[originalColumnNumber];

            var ctx = ReadContext.ReadingColumn(options, rowNumber, ColumnIdentifier.CreateInner(columnNumber, name, null), context);

            _ColumnAvailable(data, in ctx);
        }

        public TRow FinishRow()
        => _FinishRow();

        public void SetColumnOrder(Options options, HeadersReader<TRow>.HeaderEnumerator columns)
        {
            var cols = _Columns();
            ref var mapMem = ref _CurrentColumnMap();

            var colsInHeader = columns.Count;

            if (mapMem.Length > colsInHeader)
            {
                mapMem = mapMem[..colsInHeader];
            }
            else if (mapMem.Length < colsInHeader)
            {
                _CurrentColumnMap_Memory?.Dispose();

                var pool = options.MemoryPoolProvider.GetMemoryPool<int>();
                var newMemOwner = pool.Rent(colsInHeader);

                _CurrentColumnMap_Memory = newMemOwner;
                mapMem = newMemOwner.Memory[..colsInHeader];
            }

            var mapSpan = mapMem.Span;

            using (columns)
            {
                var ix = 0;

                while (columns.MoveNext())
                {
                    var columnName = columns.Current.Span;

                    int? foundIx = null;

                    for (var setterIx = 0; setterIx < cols.Length; setterIx++)
                    {
                        var setterName = cols[setterIx].AsSpan();

                        if (Utils.AreEqual(columnName, setterName))
                        {
                            foundIx = setterIx;
                            break;
                        }
                    }

                    if (foundIx == null)
                    {
                        mapSpan[ix] = -1;
                    }
                    else
                    {
                        mapSpan[ix] = foundIx.Value;
                    }

                    ix++;
                }
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                ref var mapMem = ref _CurrentColumnMap();

                mapMem = Memory<int>.Empty;
                _CurrentColumnMap_Memory?.Dispose();
                _CurrentColumnMap_Memory = null;
            }
        }
    }
}
