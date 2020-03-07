﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Cesil
{
    internal sealed class DynamicRowConstructor : IRowConstructor<object>
    {
        public IEnumerable<string> Columns => Enumerable.Empty<string>();

        public bool IsDisposed => true;

        private DynamicRow? PreAlloced;
        private DynamicRow? CurrentRow;

        public bool RowStarted => CurrentRow != null;

        internal DynamicRowConstructor() { }

        public IRowConstructor<object> Clone() => new DynamicRowConstructor();

        public void SetColumnOrder(HeadersReader<object>.HeaderEnumerator columns)
        {
            // took ownership, have to dispose
            columns.Dispose();
        }

        public bool TryPreAllocate(in ReadContext ctx, ref object prealloc)
        {
            var asObj = prealloc as DynamicRow;
            if (asObj != null)
            {
                PreAlloced = asObj;
                asObj.Dispose();
            }
            else
            {
                prealloc = PreAlloced = new DynamicRow();
            }

            return true;
        }

        public void StartRow(in ReadContext ctx)
        {
            if (PreAlloced == null)
            {
                Throw.Exception<object>($"Row not pre-allocated, shouldn't be possible");
            }

            CurrentRow = PreAlloced;
            PreAlloced = null;
        }

        public void ColumnAvailable(Options options, int rowNumber, int columnNumber, object? context, in ReadOnlySpan<char> data)
        {
            if (CurrentRow == null)
            {
                Throw.Exception<object>($"Row not initialized, column unexpected");
                return;
            }

            CurrentRow.SetValue(columnNumber, data);
        }

        public object FinishRow()
        {
            var ret = CurrentRow;

            if (ret == null)
            {
                return Throw.Exception<object>($"Row not initialized, ending a row unexpected");
            }

            CurrentRow = null;

            return ret;
        }

        public void Dispose() { }
    }
}