using System;
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

        private DynamicRow NotifyOnDisposeHead;
        public IIntrusiveLinkedList<DynamicRow> NotifyOnDispose => NotifyOnDisposeHead;

        public new object Context => base.Context;

        internal DynamicReader(TextReader reader, DynamicBoundConfiguration config, object context) : base(config, context)
        {
            Inner = reader;
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

            var res = TryReadInner(false, ref row);
            if (res.ResultType == ReadWithCommentResultType.HasValue)
            {
                row = res.Value;
                return true;
            }

            // intentionally not clearing record here
            return false;
        }

        public ReadWithCommentResult<dynamic> TryReadWithComment()
        {
            AssertNotDisposed();

            dynamic record = null;
            return TryReadWithCommentReuse(ref record);
        }

        public ReadWithCommentResult<dynamic> TryReadWithCommentReuse(ref dynamic row)
        {
            AssertNotDisposed();

            return TryReadInner(true, ref row);
        }

        private ReadWithCommentResult<dynamic> TryReadInner(bool returnComments, ref dynamic row)
        {
            if (RowEndings == null)
            {
                HandleLineEndings();
            }

            if (ReadHeaders == null)
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
                        return new ReadWithCommentResult<dynamic>(row);
                    }

                    if (HasCommentToReturn)
                    {
                        HasCommentToReturn = false;
                        if (returnComments)
                        {
                            var comment = Partial.PendingAsString(Buffer.Buffer);
                            return new ReadWithCommentResult<dynamic>(comment);
                        }
                    }

                    // intentionally _not_ modifying record here
                    return ReadWithCommentResult<dynamic>.Empty;
                }

                if (!HasValueToReturn)
                {
                    DynamicRow dynRow;

                    var rowAsObj = row as object;

                    if (rowAsObj == null || !(row is DynamicRow))
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
                            dynRow.Owner.Remove(dynRow);
                            if (Configuration.DynamicRowDisposal == DynamicRowDisposal.OnReaderDispose)
                            {
                                NotifyOnDisposeHead.AddHead(ref NotifyOnDisposeHead, dynRow);
                            }
                        }
                    }

                    dynRow.Init(this, RowNumber, Columns.Length, Context, Configuration.TypeDescriber, ColumnNames, Configuration.MemoryPool);

                    SetValueToPopulate(row);
                }

                var res = AdvanceWork(available);
                if (res == ReadWithCommentResultType.HasValue)
                {
                    row = GetValueForReturn();
                    return new ReadWithCommentResult<dynamic>(row);
                }
                if (res == ReadWithCommentResultType.HasComment)
                {
                    HasCommentToReturn = false;

                    if (returnComments)
                    {
                        // only actually allocate for the comment if it's been asked for

                        var comment = Partial.PendingAsString(Buffer.Buffer);
                        Partial.ClearValue();
                        Partial.ClearBuffer();
                        return new ReadWithCommentResult<dynamic>(comment);
                    }
                    else
                    {
                        Partial.ClearValue();
                        Partial.ClearBuffer();
                    }
                }
            }
        }

        private DynamicRow MakeRow()
        {
            var ret = new DynamicRow();
            if (Configuration.DynamicRowDisposal == DynamicRowDisposal.OnReaderDispose)
            {
                NotifyOnDisposeHead.AddHead(ref NotifyOnDisposeHead, ret);
            }

            return ret;
        }

        public void Remove(DynamicRow row)
        {
            NotifyOnDisposeHead.Remove(ref NotifyOnDisposeHead, row);
        }

        private void HandleHeaders()
        {
            ReadHeaders = Configuration.ReadHeader;
            TryMakeStateMachine();

            var allowColumnsByName = Configuration.ReadHeader == Cesil.ReadHeaders.Always;

            var headerConfig =
                new DynamicBoundConfiguration(
                    Configuration.TypeDescriber,
                    Configuration.ValueSeparator,
                    Configuration.EscapedValueStartAndStop,
                    Configuration.EscapeValueEscapeChar,
                    RowEndings.Value,
                    Configuration.ReadHeader,
                    Configuration.WriteHeader,
                    Configuration.WriteTrailingNewLine,
                    Configuration.MemoryPool,
                    Configuration.CommentChar,
                    Configuration.WriteBufferSizeHint,
                    Configuration.ReadBufferSizeHint,
                    Configuration.DynamicRowDisposal
                );

            using (var reader = new HeadersReader<object>(headerConfig, SharedCharacterLookup, Inner, Buffer))
            {
                var res = reader.Read();
                var foundHeaders = res.Headers.Count;
                if (foundHeaders == 0)
                {
                    // rare, but possible if the file is empty or all comments or something like that
                    Columns = Array.Empty<Column>();
                    ColumnNames = Array.Empty<string>();
                }
                else
                {
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
            while (NotifyOnDispose != null)
            {
                NotifyOnDisposeHead.Dispose();
                NotifyOnDisposeHead.Remove(ref NotifyOnDisposeHead, NotifyOnDisposeHead);
            }

            Inner.Dispose();
            Inner = null;
        }

        public override string ToString()
        {
            return $"{nameof(DynamicReader)} with {Configuration}";
        }
    }
}
