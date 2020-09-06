using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed class DynamicWriter :
        SyncWriterBase<dynamic>,
        IDelegateCache
    {
        internal new bool IsFirstRow => ColumnNames == null;

        private NonNull<Comparison<DynamicCellValue>> ColumnNameSorter;

        private EncodedColumnTracker? ColumnNames;

        private readonly object[] DynamicArgumentsBuffer = new object[3];

        private Dictionary<object, Delegate>? DelegateCache;

        private bool HasWrittenComments;

        private IMemoryOwner<DynamicCellValue>? CellBuffer;

        internal DynamicWriter(DynamicBoundConfiguration config, IWriterAdapter inner, object? context) : base(config, inner, context) { }

        bool IDelegateCache.TryGetDelegate<T, V>(T key, [MaybeNullWhen(returnValue: false)] out V del)
        {
            if (DelegateCache == null)
            {
                del = default;
                return false;
            }

            if (DelegateCache.TryGetValue(key, out var untypedDel))
            {
                del = (V)untypedDel;
                return true;
            }

            del = default;
            return false;
        }

        void IDelegateCache.AddDelegate<T, V>(T key, V cached)
        {
            if (DelegateCache == null)
            {
                DelegateCache = new Dictionary<object, Delegate>();
            }

            DelegateCache.Add(key, cached);
        }

        internal override void WriteInner(dynamic row)
        {
            try
            {
                WriteHeadersAndEndRowIfNeeded(row);

                var wholeRowContext = WriteContext.DiscoveringCells(Configuration.Options, RowNumber, Context);

                var options = Configuration.Options;
                var valueSeparator = Configuration.ValueSeparatorMemory.Span;

                var cellValuesMem = Utils.GetCells(Configuration.DynamicMemoryPool, ref CellBuffer, options.TypeDescriber, in wholeRowContext, row as object);

                var columnNamesValue = Utils.NonNullValue(ColumnNames);

                Utils.ForceInOrder(columnNamesValue, ColumnNameSorter, cellValuesMem);
                var cellValuesEnumerableSpan = cellValuesMem.Span;

                var i = 0;
                foreach (var cell in cellValuesEnumerableSpan)
                {
                    var needsSeparator = i != 0;

                    if (needsSeparator)
                    {
                        PlaceAllInStaging(valueSeparator);
                    }

                    ColumnIdentifier ci;
                    if (i < columnNamesValue.Length)
                    {
                        var name = columnNamesValue.GetColumnAt(i);
                        ci = ColumnIdentifier.CreateInner(i, null, name);
                    }
                    else
                    {
                        ci = ColumnIdentifier.Create(i);
                    }

                    var ctx = WriteContext.WritingColumn(Configuration.Options, RowNumber, ci, Context);

                    var formatter = cell.Formatter;
                    var delProvider = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                    var del = delProvider.Guarantee(this);

                    var val = cell.Value as object;
                    if (!del(val, in ctx, Buffer))
                    {
                        Throw.SerializationException<object>($"Could not write column {ci}, formatter {formatter} returned false");
                    }

                    ReadOnlySequence<char> res = default;
                    if (!Buffer.MakeSequence(ref res))
                    {
                        // nothing was written, so just move on
                        goto end;
                    }

                    WriteValue(res);
                    Buffer.Reset();

end:
                    i++;
                }

                RowNumber++;
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow<object>(this, e);
            }
        }

        public override void WriteComment(ReadOnlySpan<char> comment)
        {
            AssertNotDisposed(this);
            AssertNotPoisoned(Configuration);

            try
            {

                var shouldEndRecord = true;
                if (IsFirstRow)
                {
                    if (Configuration.Options.WriteHeader == WriteHeader.Always)
                    {
                        if (!HasWrittenComments)
                        {
                            shouldEndRecord = false;
                        }
                    }
                    else
                    {
                        if (!CheckHeaders(null))
                        {
                            shouldEndRecord = false;
                        }
                    }
                }

                if (shouldEndRecord)
                {
                    EndRecord();
                }

                var options = Configuration.Options;
                var commentCharNullable = options.CommentCharacter;

                if (commentCharNullable == null)
                {
                    Throw.InvalidOperationException<object>($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line");
                    return;
                }

                HasWrittenComments = true;

                var commentChar = commentCharNullable.Value;
                var rowEndingSpan = Configuration.RowEndingMemory.Span;

                var splitIx = Utils.FindNextIx(0, comment, rowEndingSpan);
                if (splitIx == -1)
                {
                    // single segment
                    PlaceCharInStaging(commentChar);
                    if (comment.Length > 0)
                    {
                        PlaceAllInStaging(comment);
                    }
                }
                else
                {
                    // multi segment
                    var prevIx = 0;

                    var isFirstRow = true;
                    while (splitIx != -1)
                    {
                        if (!isFirstRow)
                        {
                            EndRecord();
                        }

                        PlaceCharInStaging(commentChar);
                        var segSpan = comment[prevIx..splitIx];
                        if (segSpan.Length > 0)
                        {
                            PlaceAllInStaging(segSpan);
                        }

                        prevIx = splitIx + rowEndingSpan.Length;
                        splitIx = Utils.FindNextIx(prevIx, comment, rowEndingSpan);

                        isFirstRow = false;
                    }

                    if (prevIx != comment.Length)
                    {
                        if (!isFirstRow)
                        {
                            EndRecord();
                        }

                        PlaceCharInStaging(commentChar);
                        var segSpan = comment[prevIx..];
                        PlaceAllInStaging(segSpan);
                    }
                }
            }
            catch (Exception e)
            {
                Throw.PoisonAndRethrow<object>(this, e);
            }
        }

        private void WriteHeadersAndEndRowIfNeeded(dynamic row)
        {
            var shouldEndRecord = true;

            if (IsFirstRow)
            {
                if (!CheckHeaders(row))
                {
                    shouldEndRecord = false;
                }
            }

            if (shouldEndRecord)
            {
                EndRecord();
            }
        }

        // returns true if it did write out headers,
        //   so we need to end a record before
        //   writing the next one
        private bool CheckHeaders(dynamic? firstRow)
        {
            if (Configuration.Options.WriteHeader == WriteHeader.Never)
            {
                // nothing to write, so bail
                ColumnNames = default(EncodedColumnTracker);
                return false;
            }

            // init columns
            DiscoverColumns(firstRow);

            WriteHeaders();

            return true;
        }

        private void DiscoverColumns(dynamic o)
        {
            var cols = new EncodedColumnTracker();

            var ctx = WriteContext.DiscoveringColumns(Configuration.Options, Context);

            var options = Configuration.Options;

            var cellsMem = Utils.GetCells(Configuration.DynamicMemoryPool, ref CellBuffer, options.TypeDescriber, in ctx, o as object);
            var cells = cellsMem.Span;

            var colIx = 0;
            foreach (var c in cells)
            {
                var colName = c.Name;

                if (colName == null)
                {
                    Throw.InvalidOperationException<object>($"No column name found at index {colIx} when {nameof(Cesil.WriteHeader)} = {options.WriteHeader}");
                    return;
                }

                var encodedColName = colName;

                // encode it, if it needs encoding
                if (NeedsEncode(encodedColName))
                {
                    encodedColName = Utils.Encode(encodedColName, options, Configuration.MemoryPool);
                }

                cols.Add(colName, encodedColName, Configuration.MemoryPool);
            }

            ColumnNames = cols;

            ColumnNameSorter.Value =
                (a, b) =>
                {
                    var columnNamesValue = ColumnNames.Value;

                    int aIx = -1, bIx = -1;
                    for (var i = 0; i < columnNamesValue.Length; i++)
                    {
                        var colName = columnNamesValue.GetColumnAt(i);
                        var eqA = Utils.AreEqual(colName, a.Name.AsMemory());
                        if (eqA)
                        {
                            aIx = i;
                            if (bIx != -1) break;
                        }

                        var eqB = Utils.AreEqual(colName, b.Name.AsMemory());
                        if (eqB)
                        {
                            bIx = i;
                            if (aIx != -1) break;
                        }
                    }

                    return aIx.CompareTo(bIx);
                };
        }

        private void WriteHeaders()
        {
            var valueSeparator = Configuration.ValueSeparatorMemory.Span;

            var columnNamesValue = Utils.NonNullValue(ColumnNames);
            for (var i = 0; i < columnNamesValue.Length; i++)
            {
                if (i != 0)
                {
                    // first value doesn't get a separator
                    PlaceAllInStaging(valueSeparator);
                }
                else
                {
                    // if we're going to write any headers... before we 
                    //   write the first one we need to check if
                    //   we need to end the previous record... which only happens
                    //   if we've written comments _before_ the header
                    if (HasWrittenComments)
                    {
                        EndRecord();
                    }
                }

                var encodedColName = columnNamesValue.GetEncodedColumnAt(i);

                // can colName is always gonna be encoded correctly, because we just discovered them
                //   (ie. they're always correct for this config)
                PlaceAllInStaging(encodedColName.Span);
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                try
                {
                    if (IsFirstRow)
                    {
                        CheckHeaders(null);
                    }

                    if (Configuration.Options.WriteTrailingRowEnding == WriteTrailingRowEnding.Always)
                    {
                        EndRecord();
                    }

                    if (HasStaging)
                    {
                        if (InStaging > 0)
                        {
                            FlushStaging();
                        }

                        Staging.Dispose();
                        Staging = EmptyMemoryOwner.Singleton;
                        StagingMemory = Memory<char>.Empty;
                    }

                    Inner.Dispose();
                    Buffer.Dispose();
                    CellBuffer?.Dispose();
                    ColumnNames?.Dispose();
                }
                catch (Exception e)
                {
                    if (HasStaging)
                    {
                        Staging.Dispose();
                        Staging = EmptyMemoryOwner.Singleton;
                        StagingMemory = Memory<char>.Empty;
                    }

                    Buffer.Dispose();
                    CellBuffer?.Dispose();
                    ColumnNames?.Dispose();

                    Throw.PoisonAndRethrow<object>(this, e);
                }
            }
        }

        public override string ToString()
        => $"{nameof(DynamicWriter)} with {Configuration}";
    }
}
