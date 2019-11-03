using System;

namespace Cesil
{
    internal sealed class DynamicReader :
        SyncReaderBase<dynamic>,
        IDynamicRowOwner
    {
        private NonNull<string[]> ColumnNames;

        private DynamicRow? NotifyOnDisposeHead;
        public IIntrusiveLinkedList<DynamicRow>? NotifyOnDispose => NotifyOnDisposeHead;

        public new object? Context => base.Context;

        internal DynamicReader(IReaderAdapter reader, DynamicBoundConfiguration config, object? context) : base(reader, config, context) { }

        internal override void HandleRowEndingsAndHeaders()
        {
            if (RowEndings == null)
            {
                HandleLineEndings();
            }

            if (ReadHeaders == null)
            {
                HandleHeaders();
            }
        }

        internal override ReadWithCommentResult<dynamic> TryReadInner(bool returnComments, bool pinAcquired, ref dynamic row)
        {
            ReaderStateMachine.PinHandle handle = default;

            if (!pinAcquired)
            {
                handle = StateMachine.Pin();
            }

            using (handle)
            {

                while (true)
                {
                    PreparingToWriteToBuffer();
                    var available = Buffer.Read(Inner);
                    if (available == 0)
                    {
                        var endRes = EndOfData();

                        return HandleAdvanceResult(endRes, returnComments);
                    }

                    if (!Partial.HasPending)
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
                            dynRow = Utils.NonNull(row as DynamicRow);
                            dynRow.Dispose();

                            if (dynRow.Owner.HasValue && dynRow.Owner.Value != this)
                            {
                                dynRow.Owner.Value.Remove(dynRow);
                                if (Configuration.DynamicRowDisposal == DynamicRowDisposal.OnReaderDispose)
                                {
                                    NotifyOnDisposeHead.AddHead(ref NotifyOnDisposeHead, dynRow);
                                }
                            }
                        }

                        dynRow.Init(this, RowNumber, Columns.Value.Length, Context, Configuration.TypeDescriber.Value, ColumnNames, Configuration.MemoryPool);

                        SetValueToPopulate(row);
                    }

                    var res = AdvanceWork(available);
                    var possibleReturn = HandleAdvanceResult(res, returnComments);
                    if (possibleReturn.ResultType != ReadWithCommentResultType.NoValue)
                    {
                        return possibleReturn;
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
           
            var allowColumnsByName = Configuration.ReadHeader == Cesil.ReadHeader.Always;

            var headerConfig =
                new DynamicBoundConfiguration(
                    Configuration.TypeDescriber.Value,
                    Configuration.ValueSeparator,
                    Configuration.EscapedValueStartAndStop,
                    Configuration.EscapeValueEscapeChar,
                    RowEndings!.Value,
                    Configuration.ReadHeader,
                    Configuration.WriteHeader,
                    Configuration.WriteTrailingNewLine,
                    Configuration.MemoryPool,
                    Configuration.CommentChar,
                    Configuration.WriteBufferSizeHint,
                    Configuration.ReadBufferSizeHint,
                    Configuration.DynamicRowDisposal
                );

            using (var reader = new HeadersReader<object>(StateMachine, headerConfig, SharedCharacterLookup, Inner, Buffer))
            {
                var res = reader.Read();
                var foundHeaders = res.Headers.Count;
                if (foundHeaders == 0)
                {
                    // rare, but possible if the file is empty or all comments or something like that
                    Columns.Value = Array.Empty<Column>();
                    ColumnNames.Value = Array.Empty<string>();
                }
                else
                {
                    var columnsValue = new Column[foundHeaders];
                    Columns.Value = columnsValue;

                    string[] columnNamesValue = Array.Empty<string>();
                    if (allowColumnsByName)
                    {
                        columnNamesValue = new string[foundHeaders];
                        ColumnNames.Value = columnNamesValue;
                    }

                    using (var e = res.Headers)
                    {
                        var ix = 0;
                        while (e.MoveNext())
                        {
                            var name = allowColumnsByName ? new string(e.Current.Span) : null;
                            if (name != null)
                            {
                                columnNamesValue[ix] = name;
                            }
                            var col = new Column(name, ColumnSetter.CreateDynamic(name, ix), null, false);
                            columnsValue[ix] = col;

                            ix++;
                        }
                    }
                }

                Buffer.PushBackFromOutsideBuffer(res.PushBack);
            }

            TryMakeStateMachine();
        }

        private void HandleLineEndings()
        {
            if (Configuration.RowEnding != Cesil.RowEnding.Detect)
            {
                RowEndings = Configuration.RowEnding;
                TryMakeStateMachine();
                return;
            }

            using (var detector = new RowEndingDetector<object>(StateMachine, Configuration, SharedCharacterLookup, Inner))
            {
                var res = detector.Detect();
                HandleLineEndingsDetectionResult(res);
            }
        }

        public override void Dispose()
        {
            if (IsDisposed) return;

            // only need to do work if the reader is responsbile for implicitly disposing
            while (NotifyOnDisposeHead != null)
            {
                NotifyOnDisposeHead.Dispose();
                NotifyOnDisposeHead.Remove(ref NotifyOnDisposeHead, NotifyOnDisposeHead);
            }

            Inner.Dispose();
            Buffer.Dispose();
            Partial.Dispose();
            StateMachine.Dispose();
            SharedCharacterLookup.Dispose();

            IsDisposed = true;
        }

        public override string ToString()
        {
            return $"{nameof(DynamicReader)} with {Configuration}";
        }
    }
}
