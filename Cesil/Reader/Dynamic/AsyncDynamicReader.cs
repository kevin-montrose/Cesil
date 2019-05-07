using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class AsyncDynamicReader :
        ReaderBase<dynamic>,
        IAsyncReader<object>,
        IDynamicRowOwner,
        ITestableAsyncDisposable
    {
        public bool IsDisposed => Inner == null;

        private TextReader Inner;

        private string[] ColumnNames;

        public List<DynamicRow> NotifyOnDispose { get; private set; }

        public new object Context => base.Context;

        internal AsyncDynamicReader(TextReader reader, DynamicBoundConfiguration config, object context) : base(config, context)
        {
            Inner = reader;

            if (config.DynamicRowDisposal == DynamicRowDisposal.OnReaderDispose)
            {
                NotifyOnDispose = new List<DynamicRow>();
            }
        }

        public IAsyncEnumerable<dynamic> EnumerateAllAsync()
        {
            AssertNotDisposed();

            return EnumerateAll_Enumerable();

            // todo: convert this into a proper type that won't always await
            async IAsyncEnumerable<dynamic> EnumerateAll_Enumerable()
            {
                ReadResult<dynamic> res;
                while ((res = await TryReadAsync()).HasValue)
                {
                    yield return res.Value;
                }
            }
        }

        public ValueTask<List<dynamic>> ReadAllAsync(CancellationToken cancel = default)
        => ReadAllAsync(new List<dynamic>());

        public ValueTask<List<dynamic>> ReadAllAsync(List<dynamic> into, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            if (into == null)
            {
                Throw.ArgumentNullException(nameof(into));
            }

            while (true)
            {
                var resTask = TryReadAsync(cancel);
                if (resTask.IsCompletedSuccessfully)
                {
                    var res = resTask.Result;
                    if (res.HasValue)
                    {
                        into.Add(res.Value);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    return ReadAllAsync_WaitForRead(this, resTask, into, cancel);
                }
            }

            return new ValueTask<List<dynamic>>(into);

            // wait for a TryReadAsync to finish, then continue reading
            static async ValueTask<List<dynamic>> ReadAllAsync_WaitForRead(AsyncDynamicReader self, ValueTask<ReadResult<dynamic>> toAwait, List<dynamic> into, CancellationToken cancel)
            {
                var res = await toAwait;

                if (res.HasValue)
                {
                    into.Add(res.Value);

                    while (true)
                    {
                        res = await self.TryReadAsync(cancel);
                        if (res.HasValue)
                        {
                            into.Add(res.Value);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                return into;
            }
        }

        public ValueTask<ReadResult<dynamic>> TryReadAsync(CancellationToken cancel = default)
        {
            AssertNotDisposed();

            dynamic row = MakeRow();
            return TryReadWithReuseAsync(ref row);
        }

        public ValueTask<ReadResult<dynamic>> TryReadWithReuseAsync(ref dynamic row, CancellationToken cancel = default)
        {
            AssertNotDisposed();

            if (RowEndings == null)
            {
                var lineEndingsTask = HandleLineEndingsAsync(cancel);
                if (!lineEndingsTask.IsCompletedSuccessfully)
                {
                    // provisionally create row, because we can't pass a ref into the local methods;
                    if (row == null || !(row is DynamicRow))
                    {
                        row = MakeRow();
                    }
                    return TryReadWithReuseAsync_ContinueAfterHandleLineEndings(this, lineEndingsTask, row, cancel);
                }
            }

            if (Columns == null)
            {
                var headersTask = HandleHeadersAsync(cancel);
                if (!headersTask.IsCompletedSuccessfully)
                {
                    // provisionally create row, because we can't pass a ref into the local methods;
                    if (row == null || !(row is DynamicRow))
                    {
                        row = MakeRow();
                    }
                    return TryReadWithReuseAsync_ContinueAfterHandleHeaders(this, headersTask, row, cancel);
                }
            }

            while (true)
            {
                PreparingToWriteToBuffer();
                var availableTask = Buffer.ReadAsync(Inner, cancel);
                if (availableTask.IsCompletedSuccessfully)
                {
                    var available = availableTask.Result;
                    if (available == 0)
                    {
                        EndOfData();

                        if (HasValueToReturn)
                        {
                            row = GetValueForReturn();
                            return new ValueTask<ReadResult<dynamic>>(new ReadResult<dynamic>(row));
                        }

                        // intentionally _not_ modifying record here
                        return new ValueTask<ReadResult<dynamic>>(ReadResult<dynamic>.Empty);
                    }

                    if (!HasValueToReturn)
                    {
                        DynamicRow dynRow;

                        if (row == null || !(row is DynamicRow))
                        {
                            row = dynRow = MakeRow();
                        }
                        else
                        {
                            // clear it, if we're reusing
                            dynRow = (row as DynamicRow);
                            dynRow.Dispose();

                            if (dynRow.Owner != null && dynRow.Owner != this)
                            {
                                dynRow.Owner?.NotifyOnDispose?.Remove(dynRow);
                                NotifyOnDispose?.Add(dynRow);
                            }
                        }

                        dynRow.Init(this, RowNumber, Columns.Length, Configuration.DynamicTypeConverter, ColumnNames, Configuration.MemoryPool);

                        SetValueToPopulate(row);
                    }

                    var res = AdvanceWork(available);
                    if (res)
                    {
                        row = GetValueForReturn();
                        return new ValueTask<ReadResult<dynamic>>(new ReadResult<dynamic>(row));
                    }
                }
                else
                {
                    // provisionally create row, because we can't pass a ref into the local methods;
                    if (row == null || !(row is DynamicRow))
                    {
                        row = MakeRow();
                    }
                    return TryReadWithReuseAsync_ContinueAfterRead(this, availableTask, row, cancel);
                }
            }

            // wait for line endings to be discovered, then continue
            static async ValueTask<ReadResult<dynamic>> TryReadWithReuseAsync_ContinueAfterHandleLineEndings(AsyncDynamicReader self, ValueTask toAwait, dynamic row, CancellationToken cancel)
            {
                await toAwait;

                if (self.Columns == null)
                {
                    await self.HandleHeadersAsync(cancel);
                }

                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    var available = await self.Buffer.ReadAsync(self.Inner, cancel);

                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            row = self.GetValueForReturn();
                            return new ReadResult<dynamic>(row);
                        }

                        // intentionally _not_ modifying record here
                        return ReadResult<dynamic>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        var dynRow = row as DynamicRow;
                        dynRow.Dispose();

                        if (dynRow.Owner != null && dynRow.Owner != self)
                        {
                            dynRow.Owner?.NotifyOnDispose?.Remove(dynRow);
                            self.NotifyOnDispose?.Add(dynRow);
                        }

                        dynRow.Init(self, self.RowNumber, self.Columns.Length, self.Configuration.DynamicTypeConverter, self.ColumnNames, self.Configuration.MemoryPool);

                        self.SetValueToPopulate(row);
                    }

                    var res = self.AdvanceWork(available);
                    if (res)
                    {
                        row = self.GetValueForReturn();
                        return new ReadResult<dynamic>(row);
                    }
                }
            }

            // wait for detection headers to finish, then continue
            static async ValueTask<ReadResult<dynamic>> TryReadWithReuseAsync_ContinueAfterHandleHeaders(AsyncDynamicReader self, ValueTask toAwait, dynamic row, CancellationToken cancel)
            {
                await toAwait;

                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    var available = await self.Buffer.ReadAsync(self.Inner, cancel);

                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            row = self.GetValueForReturn();
                            return new ReadResult<dynamic>(row);
                        }

                        // intentionally _not_ modifying record here
                        return ReadResult<dynamic>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        var dynRow = row as DynamicRow;
                        dynRow.Dispose();

                        if (dynRow.Owner != null && dynRow.Owner != self)
                        {
                            dynRow.Owner?.NotifyOnDispose?.Remove(dynRow);
                            self.NotifyOnDispose?.Add(dynRow);
                        }

                        dynRow.Init(self, self.RowNumber, self.Columns.Length, self.Configuration.DynamicTypeConverter, self.ColumnNames, self.Configuration.MemoryPool);

                        self.SetValueToPopulate(row);
                    }

                    var res = self.AdvanceWork(available);
                    if (res)
                    {
                        row = self.GetValueForReturn();
                        return new ReadResult<dynamic>(row);
                    }
                }
            }

            // wait for an read call to complete, then continue
            static async ValueTask<ReadResult<dynamic>> TryReadWithReuseAsync_ContinueAfterRead(AsyncDynamicReader self, ValueTask<int> toAwait, dynamic row, CancellationToken cancel)
            {
                var available = await toAwait;
                // handle the result of the passed in call
                {
                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            row = self.GetValueForReturn();
                            return new ReadResult<dynamic>(row);
                        }

                        // intentionally _not_ modifying record here
                        return ReadResult<dynamic>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        var dynRow = row as DynamicRow;
                        dynRow.Dispose();

                        if (dynRow.Owner != null && dynRow.Owner != self)
                        {
                            dynRow.Owner?.NotifyOnDispose?.Remove(dynRow);
                            self.NotifyOnDispose?.Add(dynRow);
                        }

                        dynRow.Init(self, self.RowNumber, self.Columns.Length, self.Configuration.DynamicTypeConverter, self.ColumnNames, self.Configuration.MemoryPool);

                        self.SetValueToPopulate(row);
                    }

                    var res = self.AdvanceWork(available);
                    if (res)
                    {
                        row = self.GetValueForReturn();
                        return new ReadResult<dynamic>(row);
                    }
                }

                while (true)
                {
                    self.PreparingToWriteToBuffer();
                    available = await self.Buffer.ReadAsync(self.Inner, cancel);

                    if (available == 0)
                    {
                        self.EndOfData();

                        if (self.HasValueToReturn)
                        {
                            row = self.GetValueForReturn();
                            return new ReadResult<dynamic>(row);
                        }

                        // intentionally _not_ modifying record here
                        return ReadResult<dynamic>.Empty;
                    }

                    if (!self.HasValueToReturn)
                    {
                        var dynRow = row as DynamicRow;
                        dynRow.Dispose();

                        if (dynRow.Owner != null && dynRow.Owner != self)
                        {
                            dynRow.Owner?.NotifyOnDispose?.Remove(dynRow);
                            self.NotifyOnDispose?.Add(dynRow);
                        }

                        dynRow.Init(self, self.RowNumber, self.Columns.Length, self.Configuration.DynamicTypeConverter, self.ColumnNames, self.Configuration.MemoryPool);

                        self.SetValueToPopulate(row);
                    }

                    var res = self.AdvanceWork(available);
                    if (res)
                    {
                        row = self.GetValueForReturn();
                        return new ReadResult<dynamic>(row);
                    }
                }
            }
        }

        private DynamicRow MakeRow()
        {
            var ret = new DynamicRow();
            NotifyOnDispose?.Add(ret);

            return ret;
        }

        private ValueTask HandleHeadersAsync(CancellationToken cancel)
        {
            ReadHeaders = Configuration.ReadHeader;
            TryMakeStateMachine();

            var allowColumnsByName = Configuration.ReadHeader == Cesil.ReadHeaders.Always;

            var reader = new HeadersReader<object>(Configuration, SharedCharacterLookup, Inner, Buffer);
            var disposeReader = true;
            try
            {
                var resTask = reader.ReadAsync(cancel);
                if (resTask.IsCompletedSuccessfully)
                {
                    var res = resTask.Result;

                    var foundHeaders = res.Headers.Count;
                    if (foundHeaders == 0)
                    {
                        Throw.InvalidOperationException("Expected a header row, but found no headers");
                    }

                    Columns = new Column[foundHeaders];
                    if (allowColumnsByName)
                    {
                        ColumnNames = new string[foundHeaders];
                    }

                    using (var e = res.Headers)
                    {
                        var ix = 0;
                        while (e.MoveNext())
                        {
                            var name = allowColumnsByName ? new string(e.Current.Span) : null;
                            if (name != null)
                            {
                                ColumnNames[ix] = name;
                            }
                            var col = new Column(name, Column.MakeDynamicSetter(name, ix), null, false);
                            Columns[ix] = col;

                            ix++;
                        }
                    }

                    Buffer.PushBackFromOutsideBuffer(res.PushBack);

                    return default;
                }
                else
                {
                    disposeReader = false;
                    return HandleHeadersAsync_WaitForRead(this, resTask, allowColumnsByName, reader);
                }
            }
            finally
            {
                if (disposeReader)
                {
                    reader.Dispose();
                }
            }

            // wait for a call to ReadAsync to finish, then continue
            static async ValueTask HandleHeadersAsync_WaitForRead(AsyncDynamicReader self, ValueTask<(HeadersReader<object>.HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> toAwait, bool allowColumnsByName, HeadersReader<object> reader)
            {
                try
                {
                    var res = await toAwait;

                    var foundHeaders = res.Headers.Count;
                    if (foundHeaders == 0)
                    {
                        Throw.InvalidOperationException("Expected a header row, but found no headers");
                    }

                    self.Columns = new Column[foundHeaders];
                    if (allowColumnsByName)
                    {
                        self.ColumnNames = new string[foundHeaders];
                    }

                    using (var e = res.Headers)
                    {
                        var ix = 0;
                        while (e.MoveNext())
                        {
                            var name = allowColumnsByName ? new string(e.Current.Span) : null;
                            if (name != null)
                            {
                                self.ColumnNames[ix] = name;
                            }
                            var col = new Column(name, Column.MakeDynamicSetter(name, ix), null, false);
                            self.Columns[ix] = col;

                            ix++;
                        }
                    }

                    self.Buffer.PushBackFromOutsideBuffer(res.PushBack);
                }
                finally
                {
                    reader.Dispose();
                }
            }
        }

        private ValueTask HandleLineEndingsAsync(CancellationToken cancel)
        {
            if (Configuration.RowEnding != Cesil.RowEndings.Detect)
            {
                RowEndings = Configuration.RowEnding;
                TryMakeStateMachine();
                return default;
            }

            var detector = new RowEndingDetector<object>(Configuration, SharedCharacterLookup, Inner);
            var disposeDetector = true;
            try
            {
                var resTask = detector.DetectAsync(cancel);

                if (resTask.IsCompletedSuccessfully)
                {
                    HandleLineEndingsDetectionResult(resTask.Result);
                    return default;
                }

                disposeDetector = false;
                return HandleLineEndingsAsync_WaitForDetector(this, resTask, detector);
            }
            finally
            {
                if (disposeDetector)
                {
                    detector.Dispose();
                }
            }

            // wait for the call to DetectAsync to complete
            static async ValueTask HandleLineEndingsAsync_WaitForDetector(AsyncDynamicReader self, ValueTask<(RowEndings Ending, Memory<char> PushBack)?> toAwait, RowEndingDetector<object> detector)
            {
                try
                {
                    var res = await toAwait;
                    self.HandleLineEndingsDetectionResult(res);
                }
                finally
                {
                    detector.Dispose();
                }
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(DynamicReader));
            }
        }

        public ValueTask DisposeAsync()
        {
            if (IsDisposed)
            {
                return default;
            }

            // only need to do work if the reader is responsbile for implicitly disposing
            if (NotifyOnDispose != null)
            {
                foreach (var row in NotifyOnDispose)
                {
                    row.Dispose();
                }

                NotifyOnDispose = null;
            }

            if (Inner is IAsyncDisposable iad)
            {
                var disposeTask = iad.DisposeAsync();
                if (disposeTask.IsCompletedSuccessfully)
                {
                    return DisposeAsync_WaitForInnerDispose(this, disposeTask);
                }

                Inner = null;
                return default;
            }
            else
            {
                Inner.Dispose();
                Inner = null;

                return default;
            }

            // wait for Inner's DisposeAsync call to finish, then finish disposing self
            static async ValueTask DisposeAsync_WaitForInnerDispose(AsyncDynamicReader self, ValueTask toAwait)
            {
                await toAwait;

                self.Inner = null;
                return;
            }
        }
    }
}
