using System;
using System.Collections.Generic;
using System.Linq;

namespace Cesil
{
    internal sealed class DynamicRowConstructor : IRowConstructor<object>
    {
        public IEnumerable<string> Columns => Enumerable.Empty<string>();

        public bool IsDisposed => true;

        internal DynamicRow? PreAlloced;
        internal DynamicRow? CurrentRow;

        public bool RowStarted => CurrentRow != null;

        private int? ExpectedColumnCount;

        internal DynamicRowConstructor() { }

        public IRowConstructor<object> Clone(Options options) => new DynamicRowConstructor();

        public void SetColumnOrder(Options options, HeadersReader<object>.HeaderEnumerator columns)
        {
            ExpectedColumnCount = columns.Count;

            columns.Dispose();
        }

        public bool TryPreAllocate(in ReadContext ctx, bool checkPrealloc, ref object prealloc)
        {
            if (checkPrealloc && prealloc is DynamicRow asObj)
            {
                // dispose!  we're taking it now
                asObj.Dispose();

                // check if the _data_ is disposed
                //   this thing could be semantically disposed by the data actually could be in use
                //   which would be _bad_
                if (asObj.OutstandingUsesOfData == 0)
                {
                    PreAlloced = asObj;

                    return true;
                }
            }

            // we don't have a usable row, make a new one
            prealloc = PreAlloced = new DynamicRow();

            return true;
        }

        public void StartRow(in ReadContext ctx)
        {
            if (PreAlloced == null)
            {
                Throw.ImpossibleException<object>($"Row not pre-allocated, shouldn't be possible");
            }

            CurrentRow = PreAlloced;
            PreAlloced = null;
        }

        public void ColumnAvailable(Options options, int rowNumber, int columnNumber, object? context, in ReadOnlySpan<char> data)
        {
            if (CurrentRow == null)
            {
                Throw.ImpossibleException<object>($"Row not initialized, column unexpected");
                return;
            }

            CurrentRow.SetValue(columnNumber, data);
        }

        public object FinishRow()
        {
            var ret = CurrentRow;

            if (ret == null)
            {
                return Throw.ImpossibleException<object>($"Row not initialized, ending a row unexpected");
            }

            // IF there was a header, make sure the returned row has the appropriate
            //    number of entries
            if (ExpectedColumnCount != null)
            {
                var missing = ExpectedColumnCount.Value - ret.Width;
                if (missing > 0)
                {
                    ret.PadWithNulls(missing);
                }
            }

            CurrentRow = null;

            return ret;
        }

        public void Dispose() { }
    }
}
