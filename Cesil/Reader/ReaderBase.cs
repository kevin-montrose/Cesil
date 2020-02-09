using System;

namespace Cesil
{
    internal abstract class ReaderBase<T> : PoisonableBase
    {
        internal readonly IRowConstructor<T> RowBuilder;
        internal readonly BufferWithPushback Buffer;
        internal readonly Partial Partial;

        internal readonly CharacterLookup SharedCharacterLookup;

        internal readonly object? Context;

        internal bool StateMachineInitialized;
        internal ReaderStateMachine StateMachine;

        internal BoundConfigurationBase<T> Configuration { get; }

        internal RowEnding? RowEndings { get; set; }
        internal ReadHeader? ReadHeaders { get; set; }

        internal int RowNumber;

        protected ReaderBase(BoundConfigurationBase<T> config, object? context, IRowConstructor<T> rowBuilder)
        {
            RowNumber = 0;
            Configuration = config;
            Context = context;

            var options = config.Options;

            var bufferSize = options.ReadBufferSizeHint;
            if (bufferSize == 0)
            {
                bufferSize = Utils.DEFAULT_BUFFER_SIZE;
            }

            var memPool = options.MemoryPool;

            Buffer =
                new BufferWithPushback(
                    memPool,
                    bufferSize
                );
            Partial = new Partial(memPool);

            SharedCharacterLookup = CharacterLookup.MakeCharacterLookup(options, out _);
            StateMachine = new ReaderStateMachine();
            RowBuilder = rowBuilder;
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
                        Partial.ClearBuffer();
                        return new ReadWithCommentResult<T>(comment);
                    }
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

                    case ReaderStateMachine.AdvanceResult.Finished_Unescaped_Value:
                        PushPendingCharactersToValue(false);
                        break;
                    case ReaderStateMachine.AdvanceResult.Finished_Escaped_Value:
                        PushPendingCharactersToValue(true);
                        break;

                    case ReaderStateMachine.AdvanceResult.Finished_LastValueUnescaped_Record:
                        if (Partial.PendingCharsCount > 0)
                        {
                            PushPendingCharactersToValue(false);
                        }

                        unprocessedCharacters = bufferLen - i - 1;
                        return ReadWithCommentResultType.HasValue;
                    case ReaderStateMachine.AdvanceResult.Finished_LastValueEscaped_Record:
                        if (Partial.PendingCharsCount > 0)
                        {
                            PushPendingCharactersToValue(true);
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

                case ReaderStateMachine.AdvanceResult.Finished_Unescaped_Value:
                    PushPendingCharactersToValue(false);
                    return ReadWithCommentResultType.HasValue;
                case ReaderStateMachine.AdvanceResult.Finished_Escaped_Value:
                    PushPendingCharactersToValue(true);
                    return ReadWithCommentResultType.HasValue;

                case ReaderStateMachine.AdvanceResult.Finished_LastValueUnescaped_Record:
                    if (Partial.PendingCharsCount > 0)
                    {
                        PushPendingCharactersToValue(false);
                    }
                    return ReadWithCommentResultType.HasValue;
                case ReaderStateMachine.AdvanceResult.Finished_LastValueEscaped_Record:
                    if (Partial.PendingCharsCount > 0)
                    {
                        PushPendingCharactersToValue(true);
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

        protected internal void PreparingToWriteToBuffer()
        {
            Partial.BufferToBeReused(Buffer.Buffer.Span);
        }

        protected internal T GetValueForReturn()
        {
            var ret = RowBuilder.FinishRow();

            RowNumber++;

            return ret;
        }

        internal void TryPreAllocateRow(ref T val)
        {
            var ctx = ReadContext.ReadingRow(Configuration.Options, RowNumber, Context);

            RowBuilder.TryPreAllocate(in ctx, ref val);
        }

        protected void StartRow()
        {
            var ctx = ReadContext.ReadingRow(Configuration.Options, RowNumber, Context);
            RowBuilder.StartRow(ctx);
            Partial.ResetColumn(false);
        }

        private void PushPendingCharactersToValue(bool wasEscaped)
        {
            var dataSpan = Partial.PendingAsMemory(Buffer.Buffer);

            var whitespace = Configuration.Options.WhitespaceTreatment;

            // The state machine will skip leading values outside of values, so we only need to do any trimming IN the values
            //
            // Technically we could probably have the state machine skip leading inside too...
            // todo: do that ^^^
            var needsLeadingTrim = whitespace.HasFlag(WhitespaceTreatments.TrimLeadingInValues);
            if (needsLeadingTrim)
            {
                dataSpan = Utils.TrimLeadingWhitespace(dataSpan);
            }

            // We need to trim trailing IN values if requested, and we need to trim trailing after values
            //   if requested AND the value wasn't escaped.
            //
            // Trimming trailing requires look ahead, which would greatly complicate the state machine
            //   (technically making it not a state machine) so this will have to do.
            var needsTrailingTrim =
                whitespace.HasFlag(WhitespaceTreatments.TrimTrailingInValues) ||
                (whitespace.HasFlag(WhitespaceTreatments.TrimAfterValues) && !wasEscaped);

            if (needsTrailingTrim)
            {
                dataSpan = Utils.TrimTrailingWhitespace(dataSpan);
            }

            RowBuilder.ColumnAvailable(Configuration.Options, RowNumber, Partial.CurrentColumnIndex, Context, dataSpan.Span);

            Partial.ClearBufferAndAdvanceColumnIndex();
        }

        protected void HandleHeadersReaderResult((HeadersReader<T>.HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack) headers)
        {
            if (!headers.IsHeader)
            {
                if (Configuration.Options.ReadHeader == ReadHeader.Always)
                {
                    Throw.InvalidOperationException<object>("First row of input was not a row of headers");
                }
            }

            // what are we _actually_ doing?
            this.ReadHeaders = headers.IsHeader ? ReadHeader.Always : ReadHeader.Never;
            TryMakeStateMachine();

            if (this.ReadHeaders == ReadHeader.Always)
            {
                RowBuilder.SetColumnOrder(headers.Headers);
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

            var options = Configuration.Options;

            var escapeStart = options.EscapedValueStartAndEnd;
            var escape = options.EscapedValueEscapeCharacter;

            StateMachine.Initialize(
                    SharedCharacterLookup,
                    escapeStart,
                    escape,
                    RowEndings.Value,
                    ReadHeaders.Value,
                    options.CommentCharacter != null,
                    options.WhitespaceTreatment.HasFlag(WhitespaceTreatments.TrimBeforeValues),
                    options.WhitespaceTreatment.HasFlag(WhitespaceTreatments.TrimAfterValues)
                );
        }
    }
}
