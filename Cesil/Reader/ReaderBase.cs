using System;

namespace Cesil
{
    internal abstract class ReaderBase<T>
    {
        // try and size the buffers so we get a whole page to ourselves
        private const int OVERHEAD_BYTES = 16;
        private const int PAGE_SIZE_BYTES = 4098;
        internal const int DEFAULT_BUFFER_SIZE = (PAGE_SIZE_BYTES / sizeof(char)) - OVERHEAD_BYTES;

        internal readonly BufferWithPushback Buffer;
        internal readonly Partial<T> Partial;

        internal readonly CharacterLookup SharedCharacterLookup;

        internal readonly object? Context;

        internal bool StateMachineInitialized;
        internal ReaderStateMachine StateMachine;

        internal BoundConfigurationBase<T> Configuration { get; }

        internal NonNull<Column[]> Columns;

        internal RowEnding? RowEndings { get; set; }
        internal ReadHeader? ReadHeaders { get; set; }

        internal int RowNumber;

        protected ReaderBase(BoundConfigurationBase<T> config, object? context)
        {
            RowNumber = 0;
            Configuration = config;
            Context = context;

            var bufferSize = config.ReadBufferSizeHint;
            if (bufferSize == 0)
            {
                bufferSize = DEFAULT_BUFFER_SIZE;
            }

            Buffer =
                new BufferWithPushback(
                    config.MemoryPool,
                    bufferSize
                );
            Partial = new Partial<T>(config.MemoryPool);

            SharedCharacterLookup =
                CharacterLookup.MakeCharacterLookup(
                    config.MemoryPool,
                    config.EscapedValueStartAndStop,
                    config.ValueSeparator,
                    config.EscapeValueEscapeChar,
                    config.CommentChar,
                    out _
                );
            StateMachine = new ReaderStateMachine();
        }

        protected internal ReadWithCommentResultType AdvanceWork(int numInBuffer)
        {
            var res = ProcessBuffer(numInBuffer, out var pushBack);
            if (pushBack > 0)
            {
                PreparingToWriteToBuffer();
                Buffer.PushBackFromBuffer(numInBuffer, pushBack);
            }

            return res;
        }

        protected internal ReadWithCommentResult<T> HandleAdvanceResult(ReadWithCommentResultType res, bool returnComments)
        {
            switch (res)
            {
                case ReadWithCommentResultType.HasComment:
                    if (returnComments)
                    {
                        var comment = Partial.PendingAsString(Buffer.Buffer);
                        Partial.ClearValue();
                        Partial.ClearBuffer();
                        return new ReadWithCommentResult<T>(comment);
                    }
                    Partial.ClearValue();
                    Partial.ClearBuffer();
                    return ReadWithCommentResult<T>.Empty;
                case ReadWithCommentResultType.HasValue:
                    var record = GetValueForReturn();
                    return new ReadWithCommentResult<T>(record);
                case ReadWithCommentResultType.NoValue:
                    return ReadWithCommentResult<T>.Empty;

                default:
                    return Throw.InvalidOperationException<ReadWithCommentResult<T>>($"Unexpected {nameof(ReadWithCommentResultType)}: {res}");
            }
        }

        private ReadWithCommentResultType ProcessBuffer(int bufferLen, out int unprocessedCharacters)
        {
            var buffSpan = Buffer.Buffer.Span;

            ReaderStateMachine.AdvanceResult? inBatchableResult = null;
            var consistentResultSince = -1;

            for (var i = 0; i < bufferLen; i++)
            {
                var c = buffSpan[i];
                var res = StateMachine.Advance(c);

                var state = StateMachine.CurrentState;

                // try and batch skips and appends
                //   to save time on copying AND on 
                //   basically pointless method calls
                if (inBatchableResult != null)
                {
                    if (res == inBatchableResult)
                    {
                        continue;
                    }
                    else
                    {
                        switch (inBatchableResult.Value)
                        {
                            case ReaderStateMachine.AdvanceResult.Skip_Character:

                                // there's no distinction between skipping several characters and skipping one
                                //    so this doesn't need the length
                                Partial.SkipCharacter();
                                break;
                            case ReaderStateMachine.AdvanceResult.Append_Character:
                                var length = i - consistentResultSince;

                                Partial.AppendCharacters(buffSpan, consistentResultSince, length);
                                break;
                            default:
                                unprocessedCharacters = default;
                                return Throw.Exception<ReadWithCommentResultType>($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {inBatchableResult.Value}");
                        }

                        inBatchableResult = null;
                        consistentResultSince = -1;

                        // fall through into the switch to handle the current character
                    }
                }

                // inBatchableResult is always null here
                //   because if it's NOT null we either continue (if res == inBatchableResult),
                //   thereby not hitting this point, or set it to null (if res != inBatchableResult)
                // this means we don't need to handle the inBatchableResult != null cases in
                //   the following switch

                switch (res)
                {
                    case ReaderStateMachine.AdvanceResult.Skip_Character:
                        inBatchableResult = ReaderStateMachine.AdvanceResult.Skip_Character;
                        consistentResultSince = i;
                        continue;

                    case ReaderStateMachine.AdvanceResult.Append_Character:
                        inBatchableResult = ReaderStateMachine.AdvanceResult.Append_Character;
                        consistentResultSince = i;
                        continue;
                        
                    case ReaderStateMachine.AdvanceResult.Append_CarriageReturnAndCurrentCharacter:
                        Partial.AppendCarriageReturn(buffSpan);
                        Partial.AppendCharacters(buffSpan, i, 1);
                        break;

                    // cannot reach ReaderStateMachine.AdvanceResult.Append_CarriageReturnAndEndComment, because that only happens
                    //   when the data ENDs

                    case ReaderStateMachine.AdvanceResult.Finished_Value:
                        PushPendingCharactersToValue();
                        break;

                    case ReaderStateMachine.AdvanceResult.Finished_Record:
                        if (Partial.PendingCharsCount > 0)
                        {
                            PushPendingCharactersToValue();
                        }

                        unprocessedCharacters = bufferLen - i - 1;
                        return ReadWithCommentResultType.HasValue;

                    case ReaderStateMachine.AdvanceResult.Finished_Comment:
                        unprocessedCharacters = bufferLen - i - 1;
                        return ReadWithCommentResultType.HasComment;

                    default:
                        HandleUncommonAdvanceResults(res, c);
                        break;
                }
            }

            // handle any batch that was still pending
            if (inBatchableResult != null)
            {
                switch (inBatchableResult.Value)
                {
                    case ReaderStateMachine.AdvanceResult.Skip_Character:
                        // there's no distinction between skipping several characters and skipping one
                        //    so this doesn't need the length
                        Partial.SkipCharacter();
                        break;

                    case ReaderStateMachine.AdvanceResult.Append_Character:
                        // we read all the up to the end, so length needs to include the last character
                        var length = bufferLen - consistentResultSince;

                        Partial.AppendCharacters(buffSpan, consistentResultSince, length);
                        break;

                    default:
                        unprocessedCharacters = default;
                        return Throw.Exception<ReadWithCommentResultType>($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {inBatchableResult.Value}");
                }
            }

            unprocessedCharacters = 0;
            return ReadWithCommentResultType.NoValue;
        }

        protected internal ReadWithCommentResultType EndOfData()
        {
            var res = StateMachine.EndOfData();

            switch (res)
            {
                case ReaderStateMachine.AdvanceResult.Skip_Character:
                    // nothing to be done!
                    return ReadWithCommentResultType.NoValue;

                case ReaderStateMachine.AdvanceResult.Append_CarriageReturnAndCurrentCharacter:
                case ReaderStateMachine.AdvanceResult.Append_Character:
                    return Throw.Exception<ReadWithCommentResultType>($"Attempted to append end of data with {nameof(ReaderStateMachine.Advance)} = {res}");

                case ReaderStateMachine.AdvanceResult.Append_CarriageReturnAndEndComment:
                    Partial.AppendCarriageReturn(ReadOnlySpan<char>.Empty);
                    return ReadWithCommentResultType.HasComment;

                case ReaderStateMachine.AdvanceResult.Finished_Value:
                    PushPendingCharactersToValue();
                    return ReadWithCommentResultType.HasValue;

                case ReaderStateMachine.AdvanceResult.Finished_Record:
                    if (Partial.PendingCharsCount > 0)
                    {
                        PushPendingCharactersToValue();
                    }
                    return ReadWithCommentResultType.HasValue;
                
                case ReaderStateMachine.AdvanceResult.Finished_Comment:
                    return ReadWithCommentResultType.HasComment;

                default:
                    return HandleUncommonAdvanceResults(res, null);
            }
        }

        private ReadWithCommentResultType HandleUncommonAdvanceResults(ReaderStateMachine.AdvanceResult res, char? lastRead)
        {
            string c;
            if (lastRead.HasValue)
            {
                c = lastRead.Value.ToString();
            }
            else
            {
                c = "<end of data>";
            }

            switch (res)
            {
                case ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecord:
                    return Throw.InvalidOperationException<ReadWithCommentResultType>($"Encountered '{c}' when expecting end of record");
                case ReaderStateMachine.AdvanceResult.Exception_InvalidState:
                    return Throw.InvalidOperationException<ReadWithCommentResultType>($"Internal state machine is in an invalid state due to a previous error");
                case ReaderStateMachine.AdvanceResult.Exception_StartEscapeInValue:
                    return Throw.InvalidOperationException<ReadWithCommentResultType>($"Encountered '{c}', starting an escaped value, when already in a value");
                case ReaderStateMachine.AdvanceResult.Exception_UnexpectedCharacterInEscapeSequence:
                    return Throw.InvalidOperationException<ReadWithCommentResultType>($"Encountered '{c}' in an escape sequence, which is invalid");
                
                case ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecordOrValue:
                    return Throw.InvalidOperationException<ReadWithCommentResultType>($"Encountered '{c}' when expecting the end of a record or value");

                case ReaderStateMachine.AdvanceResult.Exception_UnexpectedEnd:
                    return Throw.InvalidOperationException<ReadWithCommentResultType>($"Data ended unexpectedly");

                // this is CRAZY unlikely, but indicates that the TransitionMatrix used was incorrect
                case ReaderStateMachine.AdvanceResult.Exception_UnexpectedLineEnding:
                    return Throw.Exception<ReadWithCommentResultType>($"Unexpected {nameof(Cesil.RowEnding)} value encountered");

                // likewise, CRAZY unlikely
                case ReaderStateMachine.AdvanceResult.Exception_UnexpectedState:
                    return Throw.Exception<ReadWithCommentResultType>($"Unexpected state value entered");

                default:
                    return Throw.Exception<ReadWithCommentResultType>($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {res}");
            }
        }

        protected internal T GetValueForReturn()
        {
            var columnsValue = Columns.Value;

            for (var i = Partial.CurrentColumnIndex; i < columnsValue.Length; i++)
            {
                var col = columnsValue[i];
                if (col.IsRequired)
                {
                    return Throw.SerializationException<T>($"Column [{col.Name}] is required, but was not found in row");
                }
            }

            var ret = Partial.Value;
            Partial.ClearValue();

            RowNumber++;

            return ret;
        }

        protected void SetValueToPopulate(T val)
        {
            Partial.SetValueAndResetColumn(val);
        }

        protected internal void PreparingToWriteToBuffer()
        {
            Partial.BufferToBeReused(Buffer.Buffer.Span);
        }

        private void PushPendingCharactersToValue()
        {
            var columnsValue = Columns.Value;
            if (Partial.CurrentColumnIndex >= columnsValue.Length)
            {
                Throw.InvalidOperationException<object>($"Unexpected column (Index={Partial.CurrentColumnIndex})");
            }

            var dataSpan = Partial.PendingAsMemory(Buffer.Buffer);

            var colIx = Partial.CurrentColumnIndex;
            var column = columnsValue[colIx];

            if (column.IsRequired && dataSpan.Length == 0)
            {
                Throw.SerializationException<object>($"Column [{column.Name}] is required, but was not found in row");
            }

            var ctx = ReadContext.ReadingColumn(RowNumber, ColumnIdentifier.Create(colIx, column.Name), Context);

            if (!column.Set.Value(dataSpan.Span, in ctx, Partial.Value))
            {
                Throw.SerializationException<object>($"Could not assign value \"{Partial.PendingAsString(Buffer.Buffer)}\" to column \"{column.Name}\" (Index={Partial.CurrentColumnIndex})");
            }

            Partial.ClearBufferAndAdvanceColumnIndex();
        }

        protected void HandleHeadersReaderResult((HeadersReader<T>.HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack) headers)
        {
            if (!headers.IsHeader)
            {
                if (Configuration.ReadHeader == Cesil.ReadHeader.Always)
                {
                    Throw.InvalidOperationException<object>("First row of input was not a row of headers");
                }
            }

            // what are we _actually_ doing?
            this.ReadHeaders = headers.IsHeader ? Cesil.ReadHeader.Always : Cesil.ReadHeader.Never;
            TryMakeStateMachine();

            if (this.ReadHeaders == Cesil.ReadHeader.Always)
            {
                var columnsInDiscoveredOrder = new Column[headers.Headers.Count];
                foreach (var col in Configuration.DeserializeColumns)
                {
                    var isRequired = col.IsRequired;
                    var found = false;

                    using (var e = headers.Headers)
                    {
                        var i = 0;
                        while (e.MoveNext())
                        {
                            var header = e.Current;
                            var colNameMem = col.Name.Value.AsMemory();
                            if (Utils.AreEqual(colNameMem, header))
                            {
                                columnsInDiscoveredOrder[i] = col;
                                found = true;
                                break;
                            }

                            i++;
                        }
                    }

                    if (isRequired && !found)
                    {
                        Throw.SerializationException<object>($"Column [{col.Name}] is required, but was not found in the header");
                    }
                }

                for (var i = 0; i < columnsInDiscoveredOrder.Length; i++)
                {
                    if (columnsInDiscoveredOrder[i] == null)
                    {
                        columnsInDiscoveredOrder[i] = Column.Ignored;
                    }
                }

                Columns.Value = columnsInDiscoveredOrder;
            }
            else
            {
                Columns.Value = Configuration.DeserializeColumns;
            }

            Buffer.PushBackFromOutsideBuffer(headers.PushBack);
        }

        protected void HandleLineEndingsDetectionResult((RowEnding Ending, Memory<char> PushBack)? res)
        {
            if (res == null)
            {
                Throw.InvalidOperationException<object>($"Unable to automatically detect row endings");
                return;
            }

            RowEndings = res.Value.Ending;
            TryMakeStateMachine();

            Buffer.PushBackFromOutsideBuffer(res.Value.PushBack);
        }

        internal void TryMakeStateMachine()
        {
            if (StateMachineInitialized) return;

            if (RowEndings == null || ReadHeaders == null) return;

            StateMachineInitialized = true;
            StateMachine.Initialize(
                    SharedCharacterLookup,
                    Configuration.EscapedValueStartAndStop,
                    Configuration.EscapeValueEscapeChar,
                    RowEndings.Value,
                    ReadHeaders.Value,
                    Configuration.CommentChar.HasValue
                );
        }
    }
}
