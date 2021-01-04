﻿using System;
using System.Collections.Generic;

namespace Cesil
{
    internal interface IRowConstructor<TRow> : ITestableDisposable
    {
        // just for testing purposes
        IEnumerable<string> Columns { get; }

        bool RowStarted { get; }

        bool TryPreAllocate(in ReadContext ctx, bool checkPrealloc, ref TRow prealloc);

        void SetColumnOrder(Options options, HeadersReader<TRow>.HeaderEnumerator columns);

        void StartRow(in ReadContext ctx);

        void ColumnAvailable(Options options, int rowNumber, int columnNumber, object? context, in ReadOnlySpan<char> data);

        TRow FinishRow();

        IRowConstructor<TRow> Clone(Options options);
    }
}
