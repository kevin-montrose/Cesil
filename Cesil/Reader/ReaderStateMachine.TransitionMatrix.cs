using System;

namespace Cesil
{
    internal sealed partial class ReaderStateMachine
    {
        internal enum AdvanceResult : byte
        {
            None = 0,

            Skip_Character,

            Append_Character,
            Append_CarriageReturnAndCurrentCharacter,
            Append_CarriageReturnAndEndComment,

            Finished_Unescaped_Value,
            Finished_Escaped_Value,
            
            Finished_LastValueUnescaped_Record,
            Finished_LastValueEscaped_Record,

            Finished_Comment,

            Exception_InvalidState,
            Exception_StartEscapeInValue,
            Exception_UnexpectedState,
            Exception_ExpectedEndOfRecord,
            Exception_UnexpectedCharacterInEscapeSequence,
            Exception_UnexpectedLineEnding,
            Exception_ExpectedEndOfRecordOrValue,
            Exception_UnexpectedEnd
        }

        internal const byte IN_COMMENT_MASK = 0b0000_1001;
        internal const byte IN_ESCAPED_VALUE_MASK = 0b0001_0010;
        internal const byte CAN_END_RECORD_MASK = 0b0010_0100;

        internal enum State : byte
        {
            NONE = 0b0000_0000,

            // these bit patterns don't matter, so long as they don't collide
            //    with the group rules
            Header_Start = 0b0000_0001,
            Header_InEscapedValue_ExpectingEndOfValueOrRecord = 0b0000_0010,
            Header_Unescaped_NoValue = 0b0000_0011,
            Header_Unescaped_WithValue = 0b0000_0100,
            Header_ExpectingEndOfRecord = 0b0000_0101,
            Record_Start = 0b0000_0110,
            Record_InEscapedValue_ExpectingEndOfValueOrRecord = 0b0000_0111,
            Record_ExpectingEndOfRecord = 0b0000_1000,
            DataEnded = 0b0010_0001,

            // grouped together for easier logical checking
            // always has 0b0000_1001 set
            Comment_BeforeHeader = 0b0000_1001,
            Comment_BeforeHeader_ExpectingEndOfComment = 0b0000_1011,
            Comment_BeforeRecord = 0b0000_1101,
            Comment_BeforeRecord_ExpectingEndOfComment = 0b0000_1111,

            // grouped together for easier logical checking
            // always has 0b001_0010 set
            Header_InEscapedValue = 0b0001_0010,
            Header_InEscapedValueWithPendingEscape = 0b0001_0011,
            Record_InEscapedValue = 0b0001_0110,

            // belongs to both preceding and following group
            // so has both 0b0001_0010 & 0b0010_0100 set
            Record_InEscapedValueWithPendingEscape = 0b0011_0110,

            // grouped together for easier logical checking
            // always has 0b0010_0100 set
            Record_Unescaped_NoValue = 0b0010_0100,
            Record_Unescaped_WithValue = 0b0010_0101,

            // at the end for aesthetic reasons, not functional
            //   ones
            Invalid = 0b0010_0000
        }

        internal enum CharacterType : byte
        {
            None = 0,

            EscapeStartAndEnd,  // normally "
            Escape,             // normally also "
            ValueSeparator,     // normally ,
            CarriageReturn,     // always \r
            LineFeed,           // always \n
            CommentStart,       // often not set, but normally # if set
            Whitespace,         // anything considered a whitespace character
            Other,              // any character not one of the above

            DataEnd             // special end of data symbol
        }

        internal readonly struct TransitionRule
        {
            internal readonly State NextState;
            internal readonly AdvanceResult Result;

            internal TransitionRule(State nextState, AdvanceResult result)
            {
                NextState = nextState;
                Result = result;
            }

            public override string ToString()
            => $" => ({NextState}, {Result})";


            public static implicit operator TransitionRule(ValueTuple<State, AdvanceResult> tuple)
            => new TransitionRule(tuple.Item1, tuple.Item2);
        }

        internal const int RuleCacheStateCount = 55;            // max VALUE of State enum, + 1
        internal const int RuleCacheCharacterCount = 10;        // count of CharacterType enum
        internal const int RuleCacheRowEndingCount = 5;         // max VALUE of RowEndings enum + 1

        internal const int RuleCacheConfigCount = RuleCacheRowEndingCount * 2 * 2 * 2 * 2;              // # line endings, escape char == escape start, reading or not reading comments, trimming / not trimming leading whitespace, trimming / not trimming trailing whitespace
        internal const int RuleCacheConfigSize = RuleCacheStateCount * RuleCacheCharacterCount;

        private static readonly TransitionRule[] RuleCache;

        static ReaderStateMachine()
        {
            const int CACHE_SIZE = RuleCacheConfigCount * RuleCacheConfigSize;

            var trueFalse = new[] { true, false };

            RuleCache = new TransitionRule[CACHE_SIZE];

            // init all the transition matrices
            for (var i = 0; i < RuleCacheRowEndingCount; i++)
            {
                var rowEnding = (RowEnding)i;
                foreach(var escapeStartEqualsEscape in trueFalse)
                {
                    foreach(var readComments in trueFalse)
                    {
                        foreach(var trimLeading in trueFalse)
                        {
                            foreach (var trimTrailing in trueFalse)
                            {
                                InitTransitionMatrix(rowEnding, escapeStartEqualsEscape, readComments, trimLeading, trimTrailing);
                            }
                        }
                    }
                }
            }
        }

        private static readonly TransitionRule Record_InEscapedValueWithPendingEscape_Skip_Character = (State.Record_InEscapedValueWithPendingEscape, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Header_InEscapedValueWithPendingEscape_Skip_Character = (State.Header_InEscapedValueWithPendingEscape, AdvanceResult.Skip_Character);

        private static readonly TransitionRule Invalid_Exception_UnexpectedCharacterInEscapeSequence = (State.Invalid, AdvanceResult.Exception_UnexpectedCharacterInEscapeSequence);
        private static readonly TransitionRule Header_ExpectingEndOfRecord_SkipCharacter = (State.Header_ExpectingEndOfRecord, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Invalid_Exception_UnexpectedLineEnding = (State.Invalid, AdvanceResult.Exception_UnexpectedLineEnding);
        private static readonly TransitionRule Invalid_Exception_ExpectedEndOfRecord = (State.Invalid, AdvanceResult.Exception_ExpectedEndOfRecord);
        private static readonly TransitionRule Record_Start_Finished_LastValueUnescaped_Record = (State.Record_Start, AdvanceResult.Finished_LastValueUnescaped_Record);
        private static readonly TransitionRule Record_Start_Finished_LastValueEscaped_Record = (State.Record_Start, AdvanceResult.Finished_LastValueEscaped_Record);
        private static readonly TransitionRule Record_Unescaped_NoValue_Finished_Unescaped_Value = (State.Record_Unescaped_NoValue, AdvanceResult.Finished_Unescaped_Value);
        private static readonly TransitionRule Record_Unescaped_NoValue_Finished_Escaped_Value = (State.Record_Unescaped_NoValue, AdvanceResult.Finished_Escaped_Value);
        private static readonly TransitionRule Record_ExpectingEndOfRecord_Skip_Character = (State.Record_ExpectingEndOfRecord, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Invalid_Exception_InvalidState = (State.Invalid, AdvanceResult.Exception_InvalidState);
        private static readonly TransitionRule Record_InEscapedValue_Append_Character = (State.Record_InEscapedValue, AdvanceResult.Append_Character);
        private static readonly TransitionRule Header_InEscapedValue_Skip_Character = (State.Header_InEscapedValue, AdvanceResult.Skip_Character);

        private static readonly TransitionRule Header_Unescaped_NoValue_Skip_Character = (State.Header_Unescaped_NoValue, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Header_Unescaped_WithValue_Skip_Character = (State.Header_Unescaped_WithValue, AdvanceResult.Skip_Character);

        private static readonly TransitionRule Record_Unescaped_WithValue_Append_Character = (State.Record_Unescaped_WithValue, AdvanceResult.Append_Character);

        private static readonly TransitionRule Record_Unescaped_NoValue_Skip_Character = (State.Record_Unescaped_NoValue, AdvanceResult.Skip_Character);

        private static readonly TransitionRule Record_InEscapedValue_Skip_Character = (State.Record_InEscapedValue, AdvanceResult.Skip_Character);

        private static readonly TransitionRule Record_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character = (State.Record_InEscapedValue_ExpectingEndOfValueOrRecord, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Header_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character = (State.Header_InEscapedValue_ExpectingEndOfValueOrRecord, AdvanceResult.Skip_Character);

        private static readonly TransitionRule Record_Start_SkipCharacter = (State.Record_Start, AdvanceResult.Skip_Character);

        private static readonly TransitionRule Invalid_Exception_ExpectedEndOfRecordOrValue = (State.Invalid, AdvanceResult.Exception_ExpectedEndOfRecordOrValue);
        private static readonly TransitionRule Invalid_ExceptionStartEscapeInValue = (State.Invalid, AdvanceResult.Exception_StartEscapeInValue);

        private static readonly TransitionRule Comment_BeforeHeader_Skip_Character = (State.Comment_BeforeHeader, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Comment_BeforeRecord_Skip_Character = (State.Comment_BeforeRecord, AdvanceResult.Skip_Character);

        private static readonly TransitionRule Comment_BeforeHeader_Append_Character = (State.Comment_BeforeHeader, AdvanceResult.Append_Character);
        private static readonly TransitionRule Comment_BeforeRecord_Append_Character = (State.Comment_BeforeRecord, AdvanceResult.Append_Character);

        private static readonly TransitionRule Header_Start_Skip_Character = (State.Header_Start, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Header_Start_Finished_Comment = (State.Header_Start, AdvanceResult.Finished_Comment);
        private static readonly TransitionRule Record_Start_Skip_Character = (State.Record_Start, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Record_Start_Finished_Comment = (State.Record_Start, AdvanceResult.Finished_Comment);

        private static readonly TransitionRule Comment_BeforeHeader_ExpectingEndOfComment_Skip_Character = (State.Comment_BeforeHeader_ExpectingEndOfComment, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Comment_BeforeHeader_ExpectingEndOfComment_Append_Character = (State.Comment_BeforeHeader_ExpectingEndOfComment, AdvanceResult.Append_Character);

        private static readonly TransitionRule Comment_BeforeRecord_ExpectingEndOfComment_Skip_Character = (State.Comment_BeforeRecord_ExpectingEndOfComment, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Comment_BeforeRecord_ExpectingEndOfComment_Append_Character = (State.Comment_BeforeRecord_ExpectingEndOfComment, AdvanceResult.Append_Character);

        private static readonly TransitionRule Comment_BeforeHeader_Append_CarriageReturn_And_Current_Character = (State.Comment_BeforeHeader, AdvanceResult.Append_CarriageReturnAndCurrentCharacter);
        private static readonly TransitionRule Comment_BeforeRecord_Append_CarriageReturn_And_Current_Character = (State.Comment_BeforeRecord, AdvanceResult.Append_CarriageReturnAndCurrentCharacter);

        private static readonly TransitionRule Data_Ended_Skip_Character = (State.DataEnded, AdvanceResult.Skip_Character);
        private static readonly TransitionRule Data_Ended_Finished_LastValueUnescaped_Record = (State.DataEnded, AdvanceResult.Finished_LastValueUnescaped_Record);
        private static readonly TransitionRule Data_Ended_Finished_LastValueEscaped_Record = (State.DataEnded, AdvanceResult.Finished_LastValueEscaped_Record);
        private static readonly TransitionRule Data_Ended_Append_CarriageReturn_And_End_Comment = (State.DataEnded, AdvanceResult.Append_CarriageReturnAndEndComment);
        private static readonly TransitionRule Data_Ended_FinishedComment = (State.DataEnded, AdvanceResult.Finished_Comment);
        private static readonly TransitionRule Data_Ended_Exception_UnexpectedEnd = (State.DataEnded, AdvanceResult.Exception_UnexpectedEnd);
        private static readonly TransitionRule Data_Ended_Finished_Unescaped_Value = (State.DataEnded, AdvanceResult.Finished_Unescaped_Value);
        private static readonly TransitionRule Data_Ended_Finished_Escaped_Value = (State.DataEnded, AdvanceResult.Finished_Escaped_Value);

        private static readonly TransitionRule Invalid_Skip_Character = (State.Invalid, AdvanceResult.Skip_Character);

        private static ReadOnlyMemory<TransitionRule> GetTransitionMatrix(
            RowEnding rowEndings,
            bool escapeStartEqualsEscape,
            bool readComments,
            bool skipLeadingWhitespace,
            bool skipTrailingWhitespace,
            out int configOffset
        )
        {
            configOffset = GetConfigurationStartIndex(rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace);
            var ret = new ReadOnlyMemory<TransitionRule>(RuleCache, configOffset, RuleCacheConfigSize);

            return ret;
        }

        private static void InitTransitionMatrix(
            RowEnding rowEndings,
            bool escapeStartEqualsEscape,
            bool readComments,
            bool skipLeadingWhitespace,
            bool skipTrailingWhitespace
        )
        {
            InitTransitionMatrix_Comment_BeforeHeader(rowEndings, readComments, GetTransitionRulesSpan(State.Comment_BeforeHeader, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));
            InitTransitionMatrix_Comment_BeforeHeader_ExpectingEndOfComment(readComments, GetTransitionRulesSpan(State.Comment_BeforeHeader_ExpectingEndOfComment, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));

            InitTransitionMatrix_Header_Start(rowEndings, GetTransitionRulesSpan(State.Header_Start, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace), skipLeadingWhitespace);
            InitTransitionMatrix_Header_InEscapedValue(escapeStartEqualsEscape, GetTransitionRulesSpan(State.Header_InEscapedValue, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));
            InitTransitionMatrix_Header_InEscapedValueWithPendingEscape(rowEndings, escapeStartEqualsEscape, GetTransitionRulesSpan(State.Header_InEscapedValueWithPendingEscape, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace), skipTrailingWhitespace);
            InitTransitionMatrix_Header_InEscapedValue_ExpectingEndOfValueOrRecord(rowEndings, GetTransitionRulesSpan(State.Header_InEscapedValue_ExpectingEndOfValueOrRecord, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace), skipTrailingWhitespace);
            InitTransitionMatrix_Header_Unescaped_NoValue(rowEndings, escapeStartEqualsEscape, GetTransitionRulesSpan(State.Header_Unescaped_NoValue, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace), skipLeadingWhitespace);
            InitTransitionMatrix_Header_Unescaped_WithValue(rowEndings, escapeStartEqualsEscape, GetTransitionRulesSpan(State.Header_Unescaped_WithValue, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));
            InitTransitionMatrix_Header_ExpectingEndOfRecord(GetTransitionRulesSpan(State.Header_ExpectingEndOfRecord, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));

            InitTransitionMatrix_Comment_BeforeRecord(rowEndings, readComments, GetTransitionRulesSpan(State.Comment_BeforeRecord, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));
            InitTransitionMatrix_Comment_BeforeRecord_ExpectingEndOfComment(readComments, GetTransitionRulesSpan(State.Comment_BeforeRecord_ExpectingEndOfComment, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));

            InitTransitionMatrix_Record_Start(rowEndings, GetTransitionRulesSpan(State.Record_Start, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace), skipLeadingWhitespace);
            InitTransitionMatrix_Record_InEscapedValue(escapeStartEqualsEscape, GetTransitionRulesSpan(State.Record_InEscapedValue, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));
            InitTransitionMatrix_Record_InEscapedValueWithPendingEscape(rowEndings, escapeStartEqualsEscape, GetTransitionRulesSpan(State.Record_InEscapedValueWithPendingEscape, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace), skipTrailingWhitespace);
            InitTransitionMatrix_Record_InEscapedValue_ExpectingEndOfValueOrRecord(rowEndings, GetTransitionRulesSpan(State.Record_InEscapedValue_ExpectingEndOfValueOrRecord, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace), skipTrailingWhitespace);
            InitTransitionMatrix_Record_Unescaped_NoValue(rowEndings, GetTransitionRulesSpan(State.Record_Unescaped_NoValue, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace), skipLeadingWhitespace);
            InitTransitionMatrix_Record_Unescaped_WithValue(rowEndings, GetTransitionRulesSpan(State.Record_Unescaped_WithValue, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));
            InitTransitionMatrix_Record_ExpectingEndOfRecord(GetTransitionRulesSpan(State.Record_ExpectingEndOfRecord, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));

            InitTransitionMatrix_DataEnded(GetTransitionRulesSpan(State.DataEnded, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));

            InitTransitionMatrix_Invalid(GetTransitionRulesSpan(State.Invalid, rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace));
        }

        private static Span<TransitionRule> GetTransitionRulesSpan(
            State state, 
            RowEnding rowEndings, 
            bool escapeStartEqualsEscape,
            bool readComments,
            bool skipLeadingWhitespace,
            bool skipTrailingWhitespace)
        {
            var configStart = GetConfigurationStartIndex(rowEndings, escapeStartEqualsEscape, readComments, skipLeadingWhitespace, skipTrailingWhitespace);

            var stateOffset = (byte)state * RuleCacheCharacterCount;

            var offset = configStart + stateOffset;
            var len = RuleCacheCharacterCount;

            return new Span<TransitionRule>(RuleCache, offset, len);
        }

        private static int GetConfigurationStartIndex(RowEnding rowEndings, bool escapeStartEqualsEscape, bool readComments, bool trimLeadingWhitespace, bool trimTrailingWhitespace)
        {
            const int ESCAPE_START_EQUALS_STEP = RuleCacheConfigCount / 2;
            const int READ_COMMENTS_STEP = RuleCacheConfigCount / 4;
            const int TRIM_LEADING_STEP = RuleCacheConfigCount / 8;
            const int TRIM_TRAILING_STEP = RuleCacheConfigCount / 16;

            var configNum = (byte)rowEndings;
            if (escapeStartEqualsEscape)
            {
                configNum += ESCAPE_START_EQUALS_STEP;
            }
            if (readComments)
            {
                configNum += READ_COMMENTS_STEP;
            }
            if (trimLeadingWhitespace)
            {
                configNum += TRIM_LEADING_STEP;
            }
            if (trimTrailingWhitespace)
            {
                configNum += TRIM_TRAILING_STEP;
            }

            var configStart = configNum * RuleCacheConfigSize;

            return configStart;
        }

        // moving from Comment_BeforeHeader_ExpectingEndOfComment
        private static void InitTransitionMatrix_Comment_BeforeHeader_ExpectingEndOfComment(bool readComment, Span<TransitionRule> innerRet)
        {
            // Looks like
            // - #\r
            //
            // and we haven't read a header yet (but are expecting one)
            // can only happen if LineEndings is \r\n

            if (readComment)
            {
                // \
                innerRet[(int)CharacterType.Escape] = Comment_BeforeHeader_Append_CarriageReturn_And_Current_Character;
                // "
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Comment_BeforeHeader_Append_CarriageReturn_And_Current_Character;
                // ,
                innerRet[(int)CharacterType.ValueSeparator] = Comment_BeforeHeader_Append_CarriageReturn_And_Current_Character;
                // # (or whatever)
                innerRet[(int)CharacterType.CommentStart] = Comment_BeforeHeader_Append_CarriageReturn_And_Current_Character;

                // \r
                // so we're right back where we started
                innerRet[(int)CharacterType.CarriageReturn] = Comment_BeforeHeader_ExpectingEndOfComment_Append_Character;

                // \n
                innerRet[(int)CharacterType.LineFeed] = Header_Start_Finished_Comment;

                // c
                innerRet[(int)CharacterType.Other] = Comment_BeforeHeader_Append_CarriageReturn_And_Current_Character;

                // \t
                innerRet[(int)CharacterType.Whitespace] = Comment_BeforeHeader_Append_CarriageReturn_And_Current_Character;

                // end
                innerRet[(int)CharacterType.DataEnd] = Data_Ended_Append_CarriageReturn_And_End_Comment;
            }
            else
            {
                // \
                innerRet[(int)CharacterType.Escape] = Comment_BeforeHeader_Skip_Character;
                // "
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Comment_BeforeHeader_Skip_Character;
                // ,
                innerRet[(int)CharacterType.ValueSeparator] = Comment_BeforeHeader_Skip_Character;
                // # (or whatever)
                innerRet[(int)CharacterType.CommentStart] = Comment_BeforeHeader_Skip_Character;

                // so we're right back where we started
                innerRet[(int)CharacterType.CarriageReturn] = Comment_BeforeHeader_ExpectingEndOfComment_Skip_Character;

                // \n
                innerRet[(int)CharacterType.LineFeed] = Header_Start_Skip_Character;

                // c
                innerRet[(int)CharacterType.Other] = Comment_BeforeHeader_Skip_Character;

                // \t
                innerRet[(int)CharacterType.Whitespace] = Comment_BeforeHeader_Skip_Character;

                // end
                innerRet[(int)CharacterType.DataEnd] = Data_Ended_Skip_Character;
            }
        }

        // moving from Comment_BeforeRecord_ExpectingEndOfComment
        private static void InitTransitionMatrix_Comment_BeforeRecord_ExpectingEndOfComment(bool readComments, Span<TransitionRule> innerRet)
        {
            // Looks like
            // - #\r
            // 
            // and we're expecting to read a record (either no header, or we've already read it)
            // can only happen if LineEndings is \r\n

            if (readComments)
            {
                // \
                innerRet[(int)CharacterType.Escape] = Comment_BeforeRecord_Append_CarriageReturn_And_Current_Character;
                // "
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Comment_BeforeRecord_Append_CarriageReturn_And_Current_Character;
                // ,
                innerRet[(int)CharacterType.ValueSeparator] = Comment_BeforeRecord_Append_CarriageReturn_And_Current_Character;
                // # (or whatever)
                innerRet[(int)CharacterType.CommentStart] = Comment_BeforeRecord_Append_CarriageReturn_And_Current_Character;

                // \r
                // back where we began
                innerRet[(int)CharacterType.CarriageReturn] = Comment_BeforeRecord_ExpectingEndOfComment_Append_Character;

                // \n
                innerRet[(int)CharacterType.LineFeed] = Record_Start_Finished_Comment;

                // c
                innerRet[(int)CharacterType.Other] = Comment_BeforeRecord_Append_CarriageReturn_And_Current_Character;

                // \t
                innerRet[(int)CharacterType.Whitespace] = Comment_BeforeRecord_Append_CarriageReturn_And_Current_Character;

                // end
                innerRet[(int)CharacterType.DataEnd] = Data_Ended_Append_CarriageReturn_And_End_Comment;
            }
            else
            {
                // \
                innerRet[(int)CharacterType.Escape] = Comment_BeforeRecord_Skip_Character;
                // "
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Comment_BeforeRecord_Skip_Character;
                // ,
                innerRet[(int)CharacterType.ValueSeparator] = Comment_BeforeRecord_Skip_Character;
                // # (or whatever)
                innerRet[(int)CharacterType.CommentStart] = Comment_BeforeRecord_Skip_Character;

                // \r
                // back where we began
                innerRet[(int)CharacterType.CarriageReturn] = Comment_BeforeRecord_ExpectingEndOfComment_Skip_Character;

                // \n
                innerRet[(int)CharacterType.LineFeed] = Record_Start_Skip_Character;

                // c
                innerRet[(int)CharacterType.Other] = Comment_BeforeRecord_Skip_Character;

                // \t
                innerRet[(int)CharacterType.Whitespace] = Comment_BeforeRecord_Skip_Character;

                // end
                innerRet[(int)CharacterType.DataEnd] = Data_Ended_Skip_Character;
            }
        }

        // moving from Comment_BeforeRecord
        private static void InitTransitionMatrix_Comment_BeforeRecord(RowEnding rowEndings, bool readComments, Span<TransitionRule> innerRet)
        {
            // Looks like
            // - # 
            //
            // and we haven't yet parsed a header (and are expecting to)

            var commentCharacterTreatment =
                readComments ?
                    Comment_BeforeRecord_Append_Character :
                    Comment_BeforeRecord_Skip_Character;

            var commentEndTreatment =
                readComments ?
                    Record_Start_Finished_Comment :
                    Record_Start_SkipCharacter;

            // \
            innerRet[(int)CharacterType.Escape] = commentCharacterTreatment;
            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = commentCharacterTreatment;
            // ,
            innerRet[(int)CharacterType.ValueSeparator] = commentCharacterTreatment;
            // # (or whatever)
            innerRet[(int)CharacterType.CommentStart] = commentCharacterTreatment;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturn:
                    // ends the comment
                    forCarriageReturn = commentEndTreatment;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    // may end the comment, if followed by a \n
                    forCarriageReturn = Comment_BeforeRecord_ExpectingEndOfComment_Skip_Character;
                    break;
                case RowEnding.LineFeed:
                    // comment continues
                    forCarriageReturn = commentCharacterTreatment;
                    break;
                default:
                    forCarriageReturn = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // \n
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturn:
                    // comment continues
                    forLineFeed = commentCharacterTreatment;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    // comment continues
                    forLineFeed = commentCharacterTreatment;
                    break;
                case RowEnding.LineFeed:
                    // ends the comment
                    forLineFeed = commentEndTreatment;
                    break;
                default:
                    forLineFeed = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // c
            innerRet[(int)CharacterType.Other] = commentCharacterTreatment;

            // \t
            innerRet[(int)CharacterType.Whitespace] = commentCharacterTreatment;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_FinishedComment;
        }

        // moving from Comment_BeforeHeader
        private static void InitTransitionMatrix_Comment_BeforeHeader(RowEnding rowEndings, bool readComments, Span<TransitionRule> innerRet)
        {
            // Looks like
            // - # 
            //
            // and we haven't yet parsed a header (and are expecting to)

            var commentCharacterTreatment =
                readComments ?
                    Comment_BeforeHeader_Append_Character :
                    Comment_BeforeHeader_Skip_Character;

            var commentEndTreatment =
                readComments ?
                    Header_Start_Finished_Comment :
                    Header_Start_Skip_Character;

            // \
            innerRet[(int)CharacterType.Escape] = commentCharacterTreatment;
            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = commentCharacterTreatment;
            // ,
            innerRet[(int)CharacterType.ValueSeparator] = commentCharacterTreatment;
            // # (or whatever)
            innerRet[(int)CharacterType.CommentStart] = commentCharacterTreatment;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturn:
                    // ends the comment
                    forCarriageReturn = commentEndTreatment;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    // may end the comment, if followed by a \n
                    forCarriageReturn = Comment_BeforeHeader_ExpectingEndOfComment_Skip_Character;
                    break;
                case RowEnding.LineFeed:
                    // comment continues
                    forCarriageReturn = commentCharacterTreatment;
                    break;
                default:
                    forCarriageReturn = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // \n
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturn:
                    // comment continues
                    forLineFeed = commentCharacterTreatment;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    // comment continues
                    forLineFeed = commentCharacterTreatment;
                    break;
                case RowEnding.LineFeed:
                    // ends the comment
                    forLineFeed = commentEndTreatment;
                    break;
                default:
                    forLineFeed = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // c
            innerRet[(int)CharacterType.Other] = commentCharacterTreatment;
            innerRet[(int)CharacterType.Whitespace] = commentCharacterTreatment;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_FinishedComment;
        }

        // moving from Header_Start
        private static void InitTransitionMatrix_Header_Start(RowEnding rowEndings, Span<TransitionRule> innerRet, bool skipLeadingWhitespace)
        {
            // Looks like
            //  - <EMPTY>
            //
            // and there have been no previous values for the header

            // \
            innerRet[(int)CharacterType.Escape] = Header_Unescaped_WithValue_Skip_Character;
            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Header_InEscapedValue_Skip_Character;
            // ,
            innerRet[(int)CharacterType.ValueSeparator] = Header_Unescaped_NoValue_Skip_Character;
            // # (or whatever)
            innerRet[(int)CharacterType.CommentStart] = Comment_BeforeHeader_Skip_Character;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturn:
                    // ends the header
                    forCarriageReturn = Record_Start_SkipCharacter;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    // may end the header, if followed by a \n
                    forCarriageReturn = Header_ExpectingEndOfRecord_SkipCharacter;
                    break;
                default:
                    forCarriageReturn = Invalid_Exception_ExpectedEndOfRecordOrValue;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // \n
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.LineFeed:
                    // ends header
                    forLineFeed = Record_Start_SkipCharacter;
                    break;
                default:
                    forLineFeed = Invalid_Exception_ExpectedEndOfRecordOrValue;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // c
            innerRet[(int)CharacterType.Other] = Header_Unescaped_WithValue_Skip_Character;


            // \t
            TransitionRule forWhitespace;
            if (skipLeadingWhitespace)
            {
                forWhitespace = Header_Start_Skip_Character;
            }
            else
            {
                forWhitespace = Header_Unescaped_WithValue_Skip_Character;
            }
            innerRet[(int)CharacterType.Whitespace] = forWhitespace;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Skip_Character;
        }

        // moving from Record_Start
        private static void InitTransitionMatrix_Record_Start(RowEnding rowEndings, Span<TransitionRule> innerRet, bool skipLeadingWhitespace)
        {
            // Looks like
            //  - <EMPTY>
            //
            // and there have been no previous values for the record

            // \
            innerRet[(int)CharacterType.Escape] = Record_Unescaped_WithValue_Append_Character;
            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Record_InEscapedValue_Skip_Character;
            // ,
            innerRet[(int)CharacterType.ValueSeparator] = Record_Unescaped_NoValue_Finished_Unescaped_Value;
            // # (or whatever)
            innerRet[(int)CharacterType.CommentStart] = Comment_BeforeRecord_Skip_Character;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturn:
                    // ends the header
                    forCarriageReturn = Record_Start_Finished_LastValueUnescaped_Record;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    // may end the header, if followed by a \n
                    forCarriageReturn = Record_ExpectingEndOfRecord_Skip_Character;
                    break;
                default:
                    forCarriageReturn = Invalid_Exception_ExpectedEndOfRecordOrValue;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // \n
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.LineFeed:
                    // ends header
                    forLineFeed = Record_Start_Finished_LastValueUnescaped_Record;
                    break;
                default:
                    forLineFeed = Invalid_Exception_ExpectedEndOfRecordOrValue;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // c
            innerRet[(int)CharacterType.Other] = Record_Unescaped_WithValue_Append_Character;

            // \t
            TransitionRule forWhitespace;
            if (skipLeadingWhitespace)
            {
                forWhitespace = Record_Start_SkipCharacter;
            }
            else
            {
                forWhitespace = Record_Unescaped_WithValue_Append_Character;
            }
            innerRet[(int)CharacterType.Whitespace] = forWhitespace;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Skip_Character;
        }

        // moving from Header_InEscapedValue_ExpectingEndOfValueOrRecord
        private static void InitTransitionMatrix_Header_InEscapedValue_ExpectingEndOfValueOrRecord(RowEnding rowEndings, Span<TransitionRule> innerRet, bool skipTrailingWhitespace)
        {
            // Look like
            // - "df"

            // "df"\
            innerRet[(int)CharacterType.Escape] = Invalid_Exception_ExpectedEndOfRecordOrValue;
            // "df""
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_Exception_ExpectedEndOfRecordOrValue;
            // "df",
            innerRet[(int)CharacterType.ValueSeparator] = Header_Unescaped_NoValue_Skip_Character;

            // "df"\r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturn:
                    // ends the header
                    forCarriageReturn = Record_Start_SkipCharacter;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    // may end the header, if followed by a \n
                    forCarriageReturn = Header_ExpectingEndOfRecord_SkipCharacter;
                    break;
                default:
                    forCarriageReturn = Invalid_Exception_ExpectedEndOfRecordOrValue;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.LineFeed:
                    // ends header
                    forLineFeed = Record_Start_SkipCharacter;
                    break;
                default:
                    forLineFeed = Invalid_Exception_ExpectedEndOfRecordOrValue;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // #
            innerRet[(int)CharacterType.CommentStart] = Invalid_Exception_ExpectedEndOfRecordOrValue;
            // c
            innerRet[(int)CharacterType.Other] = Invalid_Exception_ExpectedEndOfRecordOrValue;

            // \t
            TransitionRule whitespaceRule;
            if (skipTrailingWhitespace)
            {
                whitespaceRule = Header_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character;
            }
            else
            {
                whitespaceRule = Invalid_Exception_ExpectedEndOfRecordOrValue;
            }
            innerRet[(int)CharacterType.Whitespace] = whitespaceRule;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Skip_Character;
        }

        // moving from Record_InEscapedValue_ExpectingEndOfValueOrRecord
        private static void InitTransitionMatrix_Record_InEscapedValue_ExpectingEndOfValueOrRecord(RowEnding rowEndings, Span<TransitionRule> innerRet, bool skipTrailingWhitespace)
        {
            // Look like
            // - "df"

            // "df"\
            innerRet[(int)CharacterType.Escape] = Invalid_Exception_ExpectedEndOfRecordOrValue;
            // "df""
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_Exception_ExpectedEndOfRecordOrValue;
            // "df",
            innerRet[(int)CharacterType.ValueSeparator] = Record_Unescaped_NoValue_Finished_Escaped_Value;

            // "df"\r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturn:
                    // ends the record
                    forCarriageReturn = Record_Start_Finished_LastValueEscaped_Record;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    // may end the header, if followed by a \n
                    forCarriageReturn = Record_ExpectingEndOfRecord_Skip_Character;
                    break;
                default:
                    forCarriageReturn = Invalid_Exception_ExpectedEndOfRecordOrValue;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // "df"\n
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.LineFeed:
                    // ends header
                    forLineFeed = Record_Start_Finished_LastValueEscaped_Record;
                    break;
                default:
                    forLineFeed = Invalid_Exception_ExpectedEndOfRecordOrValue;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // #
            innerRet[(int)CharacterType.CommentStart] = Invalid_Exception_ExpectedEndOfRecordOrValue;
            // c
            innerRet[(int)CharacterType.Other] = Invalid_Exception_ExpectedEndOfRecordOrValue;

            // \t
            TransitionRule whitespaceRule;
            if(skipTrailingWhitespace)
            {
                whitespaceRule = Record_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character;
            }
            else
            {
                whitespaceRule = Invalid_Exception_ExpectedEndOfRecordOrValue;
            }
            innerRet[(int)CharacterType.Whitespace] = whitespaceRule;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Finished_LastValueEscaped_Record;
        }

        // moving from Header_InEscapedValue
        private static void InitTransitionMatrix_Header_InEscapedValue(bool escapeStartEqualsEscape, Span<TransitionRule> innerRet)
        {
            // Looks like (assuming escape is ", separator is ,)
            //  - "
            //  - "df

            if (escapeStartEqualsEscape)
            {
                // "df"
                innerRet[(int)CharacterType.Escape] = Header_InEscapedValueWithPendingEscape_Skip_Character;
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Header_InEscapedValueWithPendingEscape_Skip_Character;
            }
            else
            {
                // "df\
                innerRet[(int)CharacterType.Escape] = Header_InEscapedValueWithPendingEscape_Skip_Character;
                // "df"
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Header_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character;
            }

            // "df,
            innerRet[(int)CharacterType.ValueSeparator] = Header_InEscapedValue_Skip_Character;

            // "df\r
            innerRet[(int)CharacterType.CarriageReturn] = Header_InEscapedValue_Skip_Character;

            // "df\n
            innerRet[(int)CharacterType.LineFeed] = Header_InEscapedValue_Skip_Character;

            // #
            innerRet[(int)CharacterType.CommentStart] = Header_InEscapedValue_Skip_Character;

            // "dfc
            innerRet[(int)CharacterType.Other] = Header_InEscapedValue_Skip_Character;

            // "dfc\t
            innerRet[(int)CharacterType.Whitespace] = Header_InEscapedValue_Skip_Character;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Exception_UnexpectedEnd;
        }

        // moving from Header_InEscapedValueWithPendingEscape
        private static void InitTransitionMatrix_Header_InEscapedValueWithPendingEscape(RowEnding rowEndings, bool escapeStartCharEqualsEscapeChar, Span<TransitionRule> innerRet, bool skipTrailingWhitespace)
        {
            // looks like (assuming escape = ")
            //  - ""
            //  - "foo"
            // looks like (assuming escape = \)
            //  - "\
            //  - "foo\

            // "foo\\
            innerRet[(int)CharacterType.Escape] = Header_InEscapedValue_Skip_Character;

            // - "foo"" (interpreted as foo")
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Header_InEscapedValue_Skip_Character;

            // a , comes in
            TransitionRule forValueSep;
            if (escapeStartCharEqualsEscapeChar)
            {
                // - "df",
                forValueSep = Header_Unescaped_NoValue_Skip_Character;
            }
            else
            {
                // EXPLOSION (assuming escape = \)
                // "df\,
                forValueSep = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
            }
            innerRet[(int)CharacterType.ValueSeparator] = forValueSep;

            // a \r comes in
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    if (escapeStartCharEqualsEscapeChar)
                    {
                        // "df"\r
                        forCarriageReturn = Header_ExpectingEndOfRecord_SkipCharacter;
                    }
                    else
                    {
                        // EXPLOSION (assuming escape = /)
                        // "df/\r
                        forCarriageReturn = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    }
                    break;
                case RowEnding.CarriageReturn:
                    if (escapeStartCharEqualsEscapeChar)
                    {
                        // "df"\r
                        forCarriageReturn = Record_Start_SkipCharacter;
                    }
                    else
                    {
                        // EXPLOSION (assuming escape = /)
                        // "df/\r
                        forCarriageReturn = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    }
                    break;
                default:

                    // EXPLOSION (assuming escape = ")
                    // "df"\r
                    forCarriageReturn = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // a \n comes in
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.LineFeed:
                    if (escapeStartCharEqualsEscapeChar)
                    {
                        // "df"\n
                        forLineFeed = Record_Start_SkipCharacter;
                    }
                    else
                    {
                        // EXPLOSION (assuming escape = /)
                        // "df/\n
                        forLineFeed = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    }
                    break;
                default:

                    // EXPLOSION (assuming escape = ")
                    // "df"\n
                    forLineFeed = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // EXPLOSION (assuming escape = ")
            // "df"#
            innerRet[(int)CharacterType.CommentStart] = Invalid_Exception_UnexpectedCharacterInEscapeSequence;

            // EXPLOSION (assuming escape = ")
            // "df"c
            innerRet[(int)CharacterType.Other] = Invalid_Exception_UnexpectedCharacterInEscapeSequence;

            // "df"\t
            TransitionRule whitespaceRule;
            if (skipTrailingWhitespace)
            {
                whitespaceRule = Header_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character;
            }
            else
            {

                // EXPLOSION (assuming escape = ")
                 whitespaceRule = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
            }
            innerRet[(int)CharacterType.Whitespace] = whitespaceRule;

            // end
            if (escapeStartCharEqualsEscapeChar)
            {
                // "sdafsdf"
                innerRet[(int)CharacterType.DataEnd] = Data_Ended_Skip_Character;
            }
            else
            {
                // "asdfasdf\
                innerRet[(int)CharacterType.DataEnd] = Data_Ended_Exception_UnexpectedEnd;
            }
        }

        // moving from Header_Unescaped_NoValue
        private static void InitTransitionMatrix_Header_Unescaped_NoValue(RowEnding rowEndings, bool escapeStartCharEqualsEscapeChar, Span<TransitionRule> innerRet, bool skipLeadingWhitespace)
        {
            // Looks like (assuming escape is ", separator is ,)
            // - <EMPTY>

            if (escapeStartCharEqualsEscapeChar)
            {
                // "
                innerRet[(int)CharacterType.Escape] = Header_InEscapedValue_Skip_Character;
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Header_InEscapedValue_Skip_Character;
            }
            else
            {
                // \
                innerRet[(int)CharacterType.Escape] = Header_Unescaped_WithValue_Skip_Character;    // outside of a value, just take the char

                // "
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Header_InEscapedValue_Skip_Character;
            }

            // ,
            innerRet[(int)CharacterType.ValueSeparator] = Header_Unescaped_NoValue_Skip_Character;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    // may end header
                    forCarriageReturn = Header_ExpectingEndOfRecord_SkipCharacter;
                    break;
                case RowEnding.CarriageReturn:
                    // ends header
                    forCarriageReturn = Record_Start_SkipCharacter;
                    break;
                case RowEnding.LineFeed:
                    // doesn't end header
                    forCarriageReturn = Header_Unescaped_WithValue_Skip_Character;
                    break;
                default:
                    forCarriageReturn = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // \n
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    // doesn't end header
                    forLineFeed = Header_Unescaped_WithValue_Skip_Character;
                    break;
                case RowEnding.LineFeed:
                    // ends header
                    forLineFeed = Record_Start_SkipCharacter;
                    break;
                case RowEnding.CarriageReturn:
                    // doesn't end header
                    forLineFeed = Header_Unescaped_WithValue_Skip_Character;
                    break;
                default:
                    forLineFeed = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // #
            innerRet[(int)CharacterType.CommentStart] = Header_Unescaped_WithValue_Skip_Character;

            // c
            innerRet[(int)CharacterType.Other] = Header_Unescaped_WithValue_Skip_Character;

            // \t
            TransitionRule whitespaceRule;
            if (skipLeadingWhitespace)
            {
                whitespaceRule = Header_Unescaped_NoValue_Skip_Character;
            }
            else
            {
                whitespaceRule = Header_Unescaped_WithValue_Skip_Character;
            }
            innerRet[(int)CharacterType.Whitespace] = whitespaceRule;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Skip_Character;
        }

        // moving from Header_Unescaped_WithValue
        private static void InitTransitionMatrix_Header_Unescaped_WithValue(RowEnding rowEndings, bool escapeStartCharEqualsEscapeChar, Span<TransitionRule> innerRet)
        {
            // Looks like (assuming escape is ", separator is ,)
            // - df

            if (escapeStartCharEqualsEscapeChar)
            {
                // df"
                innerRet[(int)CharacterType.Escape] = Invalid_ExceptionStartEscapeInValue;
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_ExceptionStartEscapeInValue;
            }
            else
            {
                // df\
                innerRet[(int)CharacterType.Escape] = Header_Unescaped_WithValue_Skip_Character;    // not in an escape, just take it

                // df"
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_ExceptionStartEscapeInValue;
            }

            // df,
            innerRet[(int)CharacterType.ValueSeparator] = Header_Unescaped_NoValue_Skip_Character;

            // df\r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    // will end the record if \n is the next char
                    forCarriageReturn = Header_ExpectingEndOfRecord_SkipCharacter;
                    break;
                case RowEnding.CarriageReturn:
                    // ends the record
                    forCarriageReturn = Record_Start_SkipCharacter;
                    break;
                case RowEnding.LineFeed:
                    // doesn't end the record
                    forCarriageReturn = Header_Unescaped_WithValue_Skip_Character;
                    break;
                default:
                    // wtf
                    forCarriageReturn = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // df\n
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    // doesn't end the record
                    forLineFeed = Header_Unescaped_WithValue_Skip_Character;
                    break;
                case RowEnding.LineFeed:
                    // ends the record
                    forLineFeed = Record_Start_SkipCharacter;
                    break;
                case RowEnding.CarriageReturn:
                    // doesn't end the record
                    forLineFeed = Header_Unescaped_WithValue_Skip_Character;
                    break;
                default:
                    // wtf
                    forLineFeed = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // df#
            innerRet[(int)CharacterType.CommentStart] = Header_Unescaped_WithValue_Skip_Character;

            // dfc
            innerRet[(int)CharacterType.Other] = Header_Unescaped_WithValue_Skip_Character;

            // df\t
            innerRet[(int)CharacterType.Whitespace] = Header_Unescaped_WithValue_Skip_Character;

            // dfc
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Skip_Character;
        }

        // moving from Header_ExpectingEndOfRecord
        private static void InitTransitionMatrix_Header_ExpectingEndOfRecord(Span<TransitionRule> innerRet)
        {
            // this only happens when there's a \r\n line ending
            // looks like
            // - foo\r
            // - "foo"\r

            // foo\r\
            innerRet[(int)CharacterType.Escape] = Invalid_ExceptionStartEscapeInValue;
            // foo\r"
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_Exception_ExpectedEndOfRecord;
            // foo\r,
            innerRet[(int)CharacterType.ValueSeparator] = Header_Unescaped_NoValue_Skip_Character;

            // foo\r\r
            innerRet[(int)CharacterType.CarriageReturn] = Header_ExpectingEndOfRecord_SkipCharacter;

            // foo\r\n
            innerRet[(int)CharacterType.LineFeed] = Record_Start_SkipCharacter;

            // foo\r#
            innerRet[(int)CharacterType.CommentStart] = Header_Unescaped_WithValue_Skip_Character;

            // foo\rc
            innerRet[(int)CharacterType.Other] = Header_Unescaped_WithValue_Skip_Character;

            // foo\r\t
            innerRet[(int)CharacterType.Whitespace] = Header_Unescaped_WithValue_Skip_Character;

            // foo\r
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Skip_Character;
        }

        // moving from Record_InEscapedValue
        private static void InitTransitionMatrix_Record_InEscapedValue(bool escapeStartCharEqualsEscapeChar, Span<TransitionRule> innerRet)
        {
            // looks like
            //  - "
            //  - "df

            if (escapeStartCharEqualsEscapeChar)
            {
                // "df"
                innerRet[(int)CharacterType.Escape] = Record_InEscapedValueWithPendingEscape_Skip_Character;
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Record_InEscapedValueWithPendingEscape_Skip_Character;
            }
            else
            {
                // "df\
                innerRet[(int)CharacterType.Escape] = Record_InEscapedValueWithPendingEscape_Skip_Character;
                // "df"
                innerRet[(int)CharacterType.EscapeStartAndEnd] = Record_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character;
            }

            // "df,
            innerRet[(int)CharacterType.ValueSeparator] = Record_InEscapedValue_Append_Character;
            // "df\r
            innerRet[(int)CharacterType.CarriageReturn] = Record_InEscapedValue_Append_Character;
            // "df\n
            innerRet[(int)CharacterType.LineFeed] = Record_InEscapedValue_Append_Character;
            // "df#
            innerRet[(int)CharacterType.CommentStart] = Record_InEscapedValue_Append_Character;
            // "dfc
            innerRet[(int)CharacterType.Other] = Record_InEscapedValue_Append_Character;
            // "df\t
            innerRet[(int)CharacterType.Whitespace] = Record_InEscapedValue_Append_Character;

            // "dfc
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Exception_UnexpectedEnd;
        }

        // moving from Record_InEscapedValueWithPendingEscape
        private static void InitTransitionMatrix_Record_InEscapedValueWithPendingEscape(RowEnding rowEndings, bool escapeStartCharEqualsEscapeChar, Span<TransitionRule> innerRet, bool skipTrailingWhitespace)
        {
            // looks like (assuming escape = ")
            //  - ""
            //  - "foo"
            // looks like (assuming escape = \)
            //  - "\
            //  - "foo\

            // "foo\\
            innerRet[(int)CharacterType.Escape] = Record_InEscapedValue_Append_Character;

            // - "foo"" (interpreted as foo")
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Record_InEscapedValue_Append_Character;

            TransitionRule forValueSep;
            if (escapeStartCharEqualsEscapeChar)
            {
                // - "df",
                forValueSep = Record_Unescaped_NoValue_Finished_Escaped_Value;
            }
            else
            {
                // EXPLOSION (assuming escape = \)
                // "df\,
                forValueSep = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
            }
            innerRet[(int)CharacterType.ValueSeparator] = forValueSep;

            // a \r comes in
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    if (escapeStartCharEqualsEscapeChar)
                    {
                        // "df"\r
                        forCarriageReturn = Record_ExpectingEndOfRecord_Skip_Character;
                    }
                    else
                    {
                        // EXPLOSION (assuming escape = /)
                        // "df/\r
                        forCarriageReturn = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    }
                    break;
                case RowEnding.CarriageReturn:
                    if (escapeStartCharEqualsEscapeChar)
                    {
                        // "df"\r
                        forCarriageReturn = Record_Start_Finished_LastValueEscaped_Record;
                    }
                    else
                    {
                        // EXPLOSION (assuming escape = /)
                        // "df/\r
                        forCarriageReturn = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    }
                    break;
                default:

                    // EXPLOSION (assuming escape = ")
                    // "df"\r
                    forCarriageReturn = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // a \n comes in
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.LineFeed:
                    if (escapeStartCharEqualsEscapeChar)
                    {
                        // "df"\n
                        forLineFeed = Record_Start_Finished_LastValueEscaped_Record;
                    }
                    else
                    {
                        // EXPLOSION (assuming escape = /)
                        // "df/\n
                        forLineFeed = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    }
                    break;
                default:

                    // EXPLOSION (assuming escape = ")
                    // "df"\n
                    forLineFeed = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // "foo"#
            innerRet[(int)CharacterType.CommentStart] = Invalid_Exception_UnexpectedCharacterInEscapeSequence;

            // "foo"c
            innerRet[(int)CharacterType.Other] = Invalid_Exception_UnexpectedCharacterInEscapeSequence;

            // "foo"\t
            TransitionRule whitespaceRule;
            if (skipTrailingWhitespace)
            {
                whitespaceRule = Record_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character;
            }
            else
            {
                whitespaceRule = Invalid_Exception_UnexpectedCharacterInEscapeSequence;
            }
            innerRet[(int)CharacterType.Whitespace] = whitespaceRule;

            // end
            if (escapeStartCharEqualsEscapeChar)
            {
                // "foo"
                innerRet[(int)CharacterType.DataEnd] = Data_Ended_Finished_Escaped_Value;
            }
            else
            {
                // "foo\
                innerRet[(int)CharacterType.DataEnd] = Data_Ended_Exception_UnexpectedEnd;
            }
        }

        // moving from Record_Unescaped_NoValue
        private static void InitTransitionMatrix_Record_Unescaped_NoValue(RowEnding rowEndings, Span<TransitionRule> innerRet, bool skipLeadingWhitespace)
        {
            // Looks like (assuming escape is ", separator is ,)
            // - <EMPTY>

            // \
            innerRet[(int)CharacterType.Escape] = Record_InEscapedValue_Skip_Character;

            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Record_InEscapedValue_Skip_Character;

            // ,
            innerRet[(int)CharacterType.ValueSeparator] = Record_Unescaped_NoValue_Finished_Unescaped_Value;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    // may end record, if followed by a \n
                    forCarriageReturn = Record_ExpectingEndOfRecord_Skip_Character;
                    break;
                case RowEnding.CarriageReturn:
                    // ends record
                    forCarriageReturn = Record_Start_Finished_LastValueUnescaped_Record;
                    break;
                case RowEnding.LineFeed:
                    // does not end record
                    forCarriageReturn = Record_Unescaped_WithValue_Append_Character;
                    break;
                default:
                    forCarriageReturn = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // \n
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    // does not end record
                    forLineFeed = Record_Unescaped_WithValue_Append_Character;
                    break;
                case RowEnding.LineFeed:
                    // ends record
                    forLineFeed = Record_Start_Finished_LastValueUnescaped_Record;
                    break;
                case RowEnding.CarriageReturn:
                    // does not end record
                    forLineFeed = Record_Unescaped_WithValue_Append_Character;
                    break;
                default:
                    forLineFeed = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // #
            innerRet[(int)CharacterType.CommentStart] = Record_Unescaped_WithValue_Append_Character;

            // c
            innerRet[(int)CharacterType.Other] = Record_Unescaped_WithValue_Append_Character;

            // \t
            TransitionRule whitespaceRule;
            if (skipLeadingWhitespace)
            {
                whitespaceRule = Record_Unescaped_NoValue_Skip_Character;
            }
            else
            {
                whitespaceRule = Record_Unescaped_WithValue_Append_Character;
            }
            innerRet[(int)CharacterType.Whitespace] = whitespaceRule;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Finished_LastValueUnescaped_Record;
        }

        // moving from Record_Unescaped_WithValue
        private static void InitTransitionMatrix_Record_Unescaped_WithValue(RowEnding rowEndings, Span<TransitionRule> innerRet)
        {
            // looks like
            //  - df

            // df\
            innerRet[(int)CharacterType.Escape] = Invalid_ExceptionStartEscapeInValue;

            // df"
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_ExceptionStartEscapeInValue;

            // df,
            innerRet[(int)CharacterType.ValueSeparator] = Record_Unescaped_NoValue_Finished_Unescaped_Value;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    // may end record, if followed by \n
                    forCarriageReturn = Record_ExpectingEndOfRecord_Skip_Character;
                    break;
                case RowEnding.CarriageReturn:
                    // ends record
                    forCarriageReturn = Record_Start_Finished_LastValueUnescaped_Record;
                    break;
                case RowEnding.LineFeed:
                    // does not end record
                    forCarriageReturn = Record_Unescaped_WithValue_Append_Character;
                    break;
                default:
                    forCarriageReturn = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.CarriageReturn] = forCarriageReturn;

            // \n
            TransitionRule forLineFeed;
            switch (rowEndings)
            {
                case RowEnding.CarriageReturnLineFeed:
                    // does not end record
                    forLineFeed = Record_Unescaped_WithValue_Append_Character;
                    break;
                case RowEnding.LineFeed:
                    // ends record
                    forLineFeed = Record_Start_Finished_LastValueUnescaped_Record;
                    break;
                case RowEnding.CarriageReturn:
                    // does not end record
                    forLineFeed = Record_Unescaped_WithValue_Append_Character;
                    break;
                default:
                    forLineFeed = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // df#
            innerRet[(int)CharacterType.CommentStart] = Record_Unescaped_WithValue_Append_Character;

            // dfc
            innerRet[(int)CharacterType.Other] = Record_Unescaped_WithValue_Append_Character;

            // df\t
            innerRet[(int)CharacterType.Whitespace] = Record_Unescaped_WithValue_Append_Character;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Finished_Unescaped_Value;
        }

        // moving from Record_ExpectingEndOfRecord
        private static void InitTransitionMatrix_Record_ExpectingEndOfRecord(Span<TransitionRule> innerRet)
        {
            // only happens when line endings == \r\n and there's a pending \r
            // looks like
            //  - foo\r
            //  - "foo"\r
            //  - \r

            // \r\
            innerRet[(int)CharacterType.Escape] = Invalid_Exception_ExpectedEndOfRecord;
            // \r"
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_Exception_ExpectedEndOfRecord;
            // \r,
            innerRet[(int)CharacterType.ValueSeparator] = Invalid_Exception_ExpectedEndOfRecord;
            // \r\r
            innerRet[(int)CharacterType.CarriageReturn] = Invalid_Exception_ExpectedEndOfRecord;
            // \r\n
            innerRet[(int)CharacterType.LineFeed] = Record_Start_Finished_LastValueUnescaped_Record;
            // \r#
            innerRet[(int)CharacterType.CommentStart] = Invalid_Exception_ExpectedEndOfRecord;
            // \rc
            innerRet[(int)CharacterType.Other] = Invalid_Exception_ExpectedEndOfRecord;
            // \r\t
            innerRet[(int)CharacterType.Whitespace] = Invalid_Exception_ExpectedEndOfRecord;

            // end
            innerRet[(int)CharacterType.DataEnd] = Data_Ended_Exception_UnexpectedEnd;
        }

        // move from end
        private static void InitTransitionMatrix_DataEnded(Span<TransitionRule> innerRet)
        {
            // end transitions to invalid, and ignores whatever was presented
            innerRet[(int)CharacterType.Escape] = Invalid_Skip_Character;
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_Skip_Character;
            innerRet[(int)CharacterType.ValueSeparator] = Invalid_Skip_Character;
            innerRet[(int)CharacterType.CarriageReturn] = Invalid_Skip_Character;
            innerRet[(int)CharacterType.LineFeed] = Invalid_Skip_Character;
            innerRet[(int)CharacterType.CommentStart] = Invalid_Skip_Character;
            innerRet[(int)CharacterType.Other] = Invalid_Skip_Character;
            innerRet[(int)CharacterType.Whitespace] = Invalid_Skip_Character;
            innerRet[(int)CharacterType.DataEnd] = Invalid_Skip_Character;
        }

        // moving from invalid
        private static void InitTransitionMatrix_Invalid(Span<TransitionRule> innerRet)
        {
            // invalid cannot transition to anything but invalid, and always throws

            innerRet[(int)CharacterType.Escape] = Invalid_Exception_InvalidState;
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_Exception_InvalidState;
            innerRet[(int)CharacterType.ValueSeparator] = Invalid_Exception_InvalidState;
            innerRet[(int)CharacterType.CarriageReturn] = Invalid_Exception_InvalidState;
            innerRet[(int)CharacterType.LineFeed] = Invalid_Exception_InvalidState;
            innerRet[(int)CharacterType.CommentStart] = Invalid_Exception_InvalidState;
            innerRet[(int)CharacterType.Other] = Invalid_Exception_InvalidState;
            innerRet[(int)CharacterType.Whitespace] = Invalid_Exception_InvalidState;
            innerRet[(int)CharacterType.DataEnd] = Invalid_Exception_InvalidState;
        }
    }
}
