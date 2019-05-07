using System.Collections.Generic;
using System.IO;

namespace Cesil
{
    internal sealed class DynamicReader :
        ReaderBase<dynamic>,
        IReader<object>,
        IDynamicRowOwner,
        ITestableDisposable
    {
        public bool IsDisposed => Inner == null;

        private TextReader Inner;

        private string[] ColumnNames;

        public List<DynamicRow> NotifyOnDispose { get; private set; }

        public new object Context => base.Context;

        internal DynamicReader(TextReader reader, DynamicBoundConfiguration config, object context): base(config, context)
        {
            Inner = reader;

            if (config.DynamicRowDisposal == DynamicRowDisposal.OnReaderDispose)
            {
                NotifyOnDispose = new List<DynamicRow>();
            }
        }

        public IEnumerable<dynamic> EnumerateAll()
        {
            AssertNotDisposed();

            return EnumerateAll_Enumerable();

            // make the actually enumerable
            IEnumerable<dynamic> EnumerateAll_Enumerable()
            {
                while (TryRead(out var t))
                {
                    yield return t;
                }
            }
        }

        public List<dynamic> ReadAll()
        => ReadAll(new List<dynamic>());

        public List<dynamic> ReadAll(List<dynamic> into)
        {
            AssertNotDisposed();

            if (into == null)
            {
                Throw.ArgumentNullException(nameof(into));
            }

            while (TryRead(out var t))
            {
                into.Add(t);
            }

            return into;
        }

        public bool TryRead(out dynamic row)
        {
            AssertNotDisposed();

            row = MakeRow();
            return TryReadWithReuse(ref row);
        }

        public bool TryReadWithReuse(ref dynamic row)
        {
            AssertNotDisposed();

            if (RowEndings == null)
            {
                HandleLineEndings();
            }

            if (Columns == null)
            {
                HandleHeaders();
            }

            while (true)
            {
                PreparingToWriteToBuffer();
                var available = Buffer.Read(Inner);
                if (available == 0)
                {
                    EndOfData();

                    if (HasValueToReturn)
                    {
                        row = GetValueForReturn();
                        return true;
                    }

                    // intentionally _not_ modifying record here
                    return false;
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
                    return true;
                }
            }
        }

        private DynamicRow MakeRow()
        {
            var ret = new DynamicRow();
            NotifyOnDispose?.Add(ret);

            return ret;
        }

        private void HandleHeaders()
        {
            ReadHeaders = Configuration.ReadHeader;
            TryMakeStateMachine();

            var allowColumnsByName = Configuration.ReadHeader == Cesil.ReadHeaders.Always;

            using (var reader = new HeadersReader<object>(Configuration, SharedCharacterLookup, Inner, Buffer))
            {
                var res = reader.Read();
                var foundHeaders = res.Headers.Count;
                if(foundHeaders == 0)
                {
                    Throw.InvalidOperationException("Expected a header row, but found no headers");
                }

                Columns = new Column[foundHeaders];
                if(allowColumnsByName)
                {
                    ColumnNames = new string[foundHeaders];
                }

                using (var e = res.Headers)
                {
                    var ix = 0;
                    while (e.MoveNext())
                    {
                        var name = allowColumnsByName ? new string(e.Current.Span) : null;
                        if(name != null)
                        {
                            ColumnNames[ix] = name;
                        }
                        var col = new Column(name, Column.MakeDynamicSetter(name, ix), null, false);
                        Columns[ix] = col;

                        ix++;
                    }
                }

                Buffer.PushBackFromOutsideBuffer(res.PushBack);
            }
        }

        private void HandleLineEndings()
        {
            if (Configuration.RowEnding != Cesil.RowEndings.Detect)
            {
                RowEndings = Configuration.RowEnding;
                TryMakeStateMachine();
                return;
            }

            using (var detector = new RowEndingDetector<object>(Configuration, SharedCharacterLookup, Inner))
            {
                var res = detector.Detect();
                HandleLineEndingsDetectionResult(res);
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(DynamicReader));
            }
        }

        public void Dispose()
        {
            if (IsDisposed) return;

            // only need to do work if the reader is responsbile for implicitly disposing
            if (NotifyOnDispose != null)
            {
                foreach (var row in NotifyOnDispose)
                {
                    row.Dispose();
                }

                NotifyOnDispose = null;
            }

            Inner.Dispose();
            Inner = null;
        }
    }
}
