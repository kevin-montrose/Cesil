using System;
using System.Diagnostics.CodeAnalysis;

namespace Cesil
{
    internal abstract class ReaderBase<T> : PoisonableBase
    {
        internal readonly IRowConstructor<T> RowBuilder;
        internal readonly BufferWithPushback Buffer;
        internal readonly Partial Partial;

        internal readonly CharacterLookup SharedCharacterLookup;

        internal readonly object? Context;

        internal int ColumnCount;

        internal bool StateMachineInitialized;
        internal ReaderStateMachine StateMachine;

        internal BoundConfigurationBase<T> Configuration { get; }

        internal ReadRowEnding? RowEndings { get; set; }
        internal ReadHeader? ReadHeaders { get; set; }

        private readonly ExtraColumnTreatment ExtraColumnTreatment;

        internal int RowNumber;

        protected ReaderBase(BoundConfigurationBase<T> config, object? context, IRowConstructor<T> rowBuilder, ExtraColumnTreatment extraTreatment)
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

            // always need to be able to store a whole value separator "character"
            var bufferMinSize = config.ValueSeparatorMemory.Length * 2;
            if (bufferSize < bufferMinSize)
            {
                bufferSize = bufferMinSize;
            }

            var memPool = config.MemoryPool;

            Buffer =
                new BufferWithPushback(
                    memPool,
                    bufferSize
                );
            Partial = new Partial(memPool);

            SharedCharacterLookup = CharacterLookup.MakeCharacterLookup(options, out _);
            StateMachine = new ReaderStateMachine();
            RowBuilder = rowBuilder;

            ExtraColumnTreatment = extraTreatment;
        }

        protected internal ReadWithCommentResultType AdvanceWork(int numInBuffer, out bool madeProgress)
        {
            var res = ProcessBuffer(numInBuffer, out var pushBack);
            if (pushBack > 0)
            {
                PreparingToWriteToBuffer();
                Buffer.PushBackFromBuffer(numInBuffer, pushBack);
            }

            madeProgress = pushBack != numInBuffer;
            return res;
        }

        protected internal ReadWithCommentResult<T> HandleAdvanceResult(ReadWithCommentResultType res, bool returnComments, bool ending)
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
                    if (ending)
                    {
                        EndedWithoutReturningRow();
                    }
                    return ReadWithCommentResult<T>.Empty;

                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(ReadWithCommentResultType)}: {res}");
                    return default;
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
                var res = StateMachine.Advance(c, false);

                var advanceIBy = 0;

                // we only see this _if_ there are multiple characters in the separator, which is rare
                if (res == ReaderStateMachine.AdvanceResult.LookAhead_MultiCharacterSeparator)
                {
                    var valSepLen = Configuration.ValueSeparatorMemory.Length;

                    // do we have enough in the buffer to look ahead?
                    var canCheckForSeparator = bufferLen - i >= valSepLen;
                    if (canCheckForSeparator)
                    {
                        var shouldMatch = buffSpan.Slice(i, valSepLen);
                        var eq = Utils.AreEqual(shouldMatch, Configuration.ValueSeparatorMemory.Span);
                        if (eq)
                        {
                            // treat it like a value separator
                            res = StateMachine.AdvanceValueSeparator();
                            // advance further to the last character in the separator
                            advanceIBy = valSepLen - 1;
                        }
                        else
                        {
                            res = StateMachine.Advance(c, true);
                        }
                    }
                    else
                    {
                        // we don't have enough in the buffer... so deal with any running batches and ask for more
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
                                    var length = i - consistentResultSince;

                                    Partial.AppendCharacters(buffSpan, consistentResultSince, length);
                                    break;
                                default:
                                    unprocessedCharacters = default;
                                    Throw.ImpossibleException($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {inBatchableResult.Value}", Configuration.Options);
                                    return default;
                            }
                        }

                        // we need to keep everything
                        unprocessedCharacters = bufferLen - i;
                        return ReadWithCommentResultType.NoValue;
                    }
                }

                var handledUpTo = i;

                // we _might_ need to modify i a bit if we just processed the fallout from a multi-char value separator
                i += advanceIBy;

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
                                var length = handledUpTo - consistentResultSince;

                                Partial.AppendCharacters(buffSpan, consistentResultSince, length);
                                break;
                            default:
                                unprocessedCharacters = default;
                                Throw.ImpossibleException($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {inBatchableResult.Value}", Configuration.Options);
                                return default;
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

                    case ReaderStateMachine.AdvanceResult.Append_ValueSeparator:
                        Partial.AppendCharactersFromDifferentBuffer(buffSpan, Configuration.ValueSeparatorMemory.Span);
                        break;

                    case ReaderStateMachine.AdvanceResult.Append_CarriageReturnAndValueSeparator:
                        Partial.AppendCarriageReturn(buffSpan);
                        Partial.AppendCharactersFromDifferentBuffer(buffSpan, Configuration.ValueSeparatorMemory.Span);
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
                        Throw.ImpossibleException($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {inBatchableResult.Value}", Configuration.Options);
                        return default;
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
                    Throw.ImpossibleException($"Attempted to append end of data with {nameof(ReaderStateMachine.Advance)} = {res}", Configuration.Options);
                    return default;

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

        [DoesNotReturn]
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

            return
                res switch
                {
                    ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecord => Throw.InvalidOperationException_Returns<ReadWithCommentResultType>($"Encountered '{c}' when expecting end of record"),
                    ReaderStateMachine.AdvanceResult.Exception_InvalidState => Throw.InvalidOperationException_Returns<ReadWithCommentResultType>($"Internal state machine is in an invalid state due to a previous error"),
                    ReaderStateMachine.AdvanceResult.Exception_StartEscapeInValue => Throw.InvalidOperationException_Returns<ReadWithCommentResultType>($"Encountered '{c}', starting an escaped value, when already in a value"),
                    ReaderStateMachine.AdvanceResult.Exception_UnexpectedCharacterInEscapeSequence => Throw.InvalidOperationException_Returns<ReadWithCommentResultType>($"Encountered '{c}' in an escape sequence, which is invalid"),
                    ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecordOrValue => Throw.InvalidOperationException_Returns<ReadWithCommentResultType>($"Encountered '{c}' when expecting the end of a record or value"),
                    ReaderStateMachine.AdvanceResult.Exception_UnexpectedEnd => Throw.InvalidOperationException_Returns<ReadWithCommentResultType>($"Data ended unexpectedly"),
                    // this is CRAZY unlikely, but indicates that the TransitionMatrix used was incorrect
                    ReaderStateMachine.AdvanceResult.Exception_UnexpectedLineEnding => Throw.ImpossibleException_Returns<ReadWithCommentResultType, T>($"Unexpected {nameof(Cesil.ReadRowEnding)} value encountered", Configuration),
                    // likewise, CRAZY unlikely
                    ReaderStateMachine.AdvanceResult.Exception_UnexpectedState => Throw.ImpossibleException_Returns<ReadWithCommentResultType, T>($"Unexpected state value entered", Configuration),
                    _ => Throw.ImpossibleException_Returns<ReadWithCommentResultType, T>($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {res}", Configuration),
                };
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

        protected internal abstract void EndedWithoutReturningRow();

        internal void TryPreAllocateRow(bool checkVal, ref T val)
        {
            var ctx = ReadContext.ReadingRow(Configuration.Options, RowNumber, Context);

            RowBuilder.TryPreAllocate(in ctx, checkVal, ref val);
        }

        protected void StartRow()
        {
            var ctx = ReadContext.ReadingRow(Configuration.Options, RowNumber, Context);
            RowBuilder.StartRow(ctx);
            Partial.ResetColumn();
        }

        private void PushPendingCharactersToValue(bool wasEscaped)
        {
            var dataSpan = Partial.PendingAsMemory(Buffer.Buffer);

            if (Partial.CurrentColumnIndex >= ColumnCount)
            {
                switch (ExtraColumnTreatment)
                {
                    case ExtraColumnTreatment.Ignore:
                        Partial.ClearBufferAndAdvanceColumnIndex();
                        return;
                    case ExtraColumnTreatment.IncludeDynamic:
                        break;
                    case ExtraColumnTreatment.ThrowException:
                        var msg = $"Extra column was encountered on row {RowNumber}, in column index {Partial.CurrentColumnIndex}; column contents were \"{new string(dataSpan.Span)}\"";
                        Throw.InvalidOperationException(msg);
                        return;
                    default:
                        Throw.ImpossibleException($"Unexpected {nameof(ExtraColumnTreatment)}: {ExtraColumnTreatment}", Configuration);
                        return;
                }
            }

            var whitespace = Configuration.Options.WhitespaceTreatment;

            // The state machine will skip leading values outside of values, so we only need to do any trimming IN the values
            //
            // Technically we could probably have the state machine skip leading inside too...
            // todo: do that ^^^ (tracking issue: https://github.com/kevin-montrose/Cesil/issues/5)
            var needsLeadingTrim = whitespace.HasFlag(WhitespaceTreatments.TrimLeadingInValues);
            if (needsLeadingTrim)
            {
                dataSpan = dataSpan.TrimStart();
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
                dataSpan = dataSpan.TrimEnd();
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
                    Throw.InvalidOperationException("First row of input was not a row of headers");
                }
            }

            // what are we _actually_ doing?
            this.ReadHeaders = headers.IsHeader ? ReadHeader.Always : ReadHeader.Never;
            TryMakeStateMachine();

            if (this.ReadHeaders == ReadHeader.Always)
            {
                ColumnCount = headers.Headers.Count;
                RowBuilder.SetColumnOrder(Configuration.Options, headers.Headers);
            }
            else
            {
                ColumnCount = Configuration.DeserializeColumns.Length;
            }

            Buffer.PushBackFromOutsideBuffer(headers.PushBack);
        }

        protected void HandleLineEndingsDetectionResult((ReadRowEnding Ending, Memory<char> PushBack)? res)
        {
            if (res == null)
            {
                Throw.InvalidOperationException($"Unable to automatically detect row endings");
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
