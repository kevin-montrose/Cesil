﻿using System;

namespace Cesil
{
    internal sealed partial class ReaderStateMachine
    {
        internal enum AdvanceResult : byte
        {
            None = 0,

            Skip_Character,

            Append_Character,

            Finished_Value,
            Finished_Record,

            Finish_Comment,

            Exception_InvalidState,
            Exception_StartEscapeInValue,
            Exception_UnexpectedState,
            Exception_ExpectedEndOfRecord,
            Exception_UnexpectedCharacterInEscapeSequence,
            Exception_UnexpectedLineEnding,
            Exception_ExpectedEndOfRecordOrValue

        }

        internal const byte IN_COMMENT_MASK       = 0b0000_1001;
        internal const byte IN_ESCAPED_VALUE_MASK = 0b0001_0010;
        internal const byte CAN_END_RECORD_MASK   = 0b0010_0100;

        internal enum State : byte
        {
            NONE                                                = 0b0000_0000,

            // these bit patterns don't matter, so long as they don't collide
            //    with the group rules
            Header_Start                                        = 0b0000_0001,
            Header_InEscapedValue_ExpectingEndOfValueOrRecord   = 0b0000_0010,
            Header_Unescaped_NoValue                            = 0b0000_0011,
            Header_Unescaped_WithValue                          = 0b0000_0100,
            Header_ExpectingEndOfRecord                         = 0b0000_0101,
            Record_Start                                        = 0b0000_0110,
            Record_InEscapedValue_ExpectingEndOfValueOrRecord   = 0b0000_0111,
            Record_ExpectingEndOfRecord                         = 0b0000_1000,

            // grouped together for easier logical checking
            // always has 0b0000_1001 set
            Comment_BeforeHeader                                = 0b0000_1001,
            Comment_BeforeHeader_ExpectingEndOfComment          = 0b0000_1011,
            Comment_BeforeRecord                                = 0b0000_1101,
            Comment_BeforeRecord_ExpectingEndOfComment          = 0b0000_1111,

            // grouped together for easier logical checking
            // always has 0b001_0010 set
            Header_InEscapedValue                               = 0b0001_0010,
            Header_InEscapedValueWithPendingEscape              = 0b0001_0011,
            Record_InEscapedValue                               = 0b0001_0110,

            // belongs to both preceeding and following group
            // so has both 0b0001_0010 & 0b0010_0100 set
            Record_InEscapedValueWithPendingEscape              = 0b0011_0110,

            // grouped together for easier logical checking
            // always has 0b0010_0100 set
            Record_Unescaped_NoValue                            = 0b0010_0100,
            Record_Unescaped_WithValue                          = 0b0010_0101,

            // at the end for aesthetic reasons, not functional
            //   ones
            Invalid                                             = 0b0010_0000
        }

        internal enum CharacterType : byte
        {
            NONE = 0,

            EscapeStartAndEnd,  // normally "
            Escape,             // normally also "
            ValueSeparator,     // normally ,
            CarriageReturn,     // always \r
            LineFeed,           // always \n
            CommentStart,       // often not set, but normally # if set
            Other               // anything not one of the above
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

        internal const int RuleCacheStateCount = 55;
        internal const int RuleCacheCharacterCount = 8;
        internal const int RuleCacheRowEndingCount = 5;
        internal const int RuleCacheConfigCount = RuleCacheRowEndingCount * 2;
        internal const int RuleCacheConfigSize = RuleCacheStateCount * RuleCacheCharacterCount;
        
        private static readonly TransitionRule[] RuleCache;

        static ReaderStateMachine()
        {
            const int CACHE_SIZE = RuleCacheConfigCount * RuleCacheConfigSize;

            RuleCache = new TransitionRule[CACHE_SIZE];

            // init all the transition matrixes
            for(var i = 0; i < RuleCacheRowEndingCount; i++)
            {
                InitTransitionMatrix((RowEndings)i, false);
                InitTransitionMatrix((RowEndings)i, true);
            }
        }

        private readonly static TransitionRule Record_InEscapedValueWithPendingEscape_Skip_Character = (State.Record_InEscapedValueWithPendingEscape, AdvanceResult.Skip_Character);
        private readonly static TransitionRule Header_InEscapedValueWithPendingEscape_Skip_Character = (State.Header_InEscapedValueWithPendingEscape, AdvanceResult.Skip_Character);

        private readonly static TransitionRule Invalid_Exception_UnexpectedCharacterInEscapeSequence = (State.Invalid, AdvanceResult.Exception_UnexpectedCharacterInEscapeSequence);
        private readonly static TransitionRule Header_ExpectingEndOfRecord_SkipCharacter = (State.Header_ExpectingEndOfRecord, AdvanceResult.Skip_Character);
        private readonly static TransitionRule Invalid_Exception_UnexpectedLineEnding = (State.Invalid, AdvanceResult.Exception_UnexpectedLineEnding);
        private readonly static TransitionRule Invalid_Exception_ExpectedEndOfRecord =(State.Invalid, AdvanceResult.Exception_ExpectedEndOfRecord);
        private readonly static TransitionRule Record_Start_Finished_Record =  (State.Record_Start, AdvanceResult.Finished_Record);
        private readonly static TransitionRule Record_Unescaped_NoValue_Finished_Value = (State.Record_Unescaped_NoValue, AdvanceResult.Finished_Value);
        private readonly static TransitionRule Record_ExpectingEndOfRecord_Skip_Character = (State.Record_ExpectingEndOfRecord, AdvanceResult.Skip_Character);
        private readonly static TransitionRule Invalid_Exception_InvalidState = (State.Invalid, AdvanceResult.Exception_InvalidState);
        private readonly static TransitionRule Record_InEscapedValue_Append_Character = (State.Record_InEscapedValue, AdvanceResult.Append_Character);
        private readonly static TransitionRule Header_InEscapedValue_Skip_Character =  (State.Header_InEscapedValue, AdvanceResult.Skip_Character);

        private readonly static TransitionRule Header_Unescaped_NoValue_Skip_Character = (State.Header_Unescaped_NoValue, AdvanceResult.Skip_Character);
        private readonly static TransitionRule Header_Unescaped_WithValue_Skip_Character =  (State.Header_Unescaped_WithValue, AdvanceResult.Skip_Character);
        
        private readonly static TransitionRule Record_Unescaped_WithValue_Append_Character =  (State.Record_Unescaped_WithValue, AdvanceResult.Append_Character);

        private readonly static TransitionRule Record_InEscapedValue_Skip_Character = (State.Record_InEscapedValue, AdvanceResult.Skip_Character);

        private readonly static TransitionRule Record_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character = (State.Record_InEscapedValue_ExpectingEndOfValueOrRecord, AdvanceResult.Skip_Character);
        private readonly static TransitionRule Header_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character = (State.Header_InEscapedValue_ExpectingEndOfValueOrRecord, AdvanceResult.Skip_Character);

        private readonly static TransitionRule Record_Start_SkipCharacter = (State.Record_Start, AdvanceResult.Skip_Character);

        private readonly static TransitionRule Invalid_Exception_ExpectedEndOfRecordOrValue = (State.Invalid, AdvanceResult.Exception_ExpectedEndOfRecordOrValue);
        private readonly static TransitionRule Invalid_ExceptionStartEscapeInValue =  (State.Invalid, AdvanceResult.Exception_StartEscapeInValue);

        private readonly static TransitionRule Comment_BeforeHeader_Skip_Character = (State.Comment_BeforeHeader, AdvanceResult.Skip_Character);
        private readonly static TransitionRule Comment_BeforeRecord_Skip_Character = (State.Comment_BeforeRecord, AdvanceResult.Skip_Character);

        private readonly static TransitionRule Header_Start_Skip_Character = (State.Header_Start, AdvanceResult.Skip_Character);
        private readonly static TransitionRule Record_Start_Skip_Character = (State.Record_Start, AdvanceResult.Skip_Character);

        private readonly static TransitionRule Comment_BeforeHeader_ExpectingEndOfComment_Skip_Character = (State.Comment_BeforeHeader_ExpectingEndOfComment, AdvanceResult.Skip_Character);
        private readonly static TransitionRule Comment_BeforeRecord_ExpectingEndOfComment_Skip_Character = (State.Comment_BeforeRecord_ExpectingEndOfComment, AdvanceResult.Skip_Character);

        private static ReadOnlyMemory<TransitionRule> GetTransitionMatrix(RowEndings rowEndings, bool escapeStartEqualsEscape)
        {
            var configStart = GetConfigurationStartIndex(rowEndings, escapeStartEqualsEscape);
            var ret = new ReadOnlyMemory<TransitionRule>(RuleCache, configStart, RuleCacheConfigSize);

            return ret;
        }

        private static void InitTransitionMatrix(RowEndings rowEndings, bool escapeStartEqualsEscape)
        {
            InitTransitionMatrix_Comment_BeforeHeader(rowEndings, GetSpan(State.Comment_BeforeHeader));
            InitTransitionMatrix_Comment_BeforeHeader_ExpectingEndOfComment(GetSpan(State.Comment_BeforeHeader_ExpectingEndOfComment));

            InitTransitionMatrix_Header_Start(rowEndings, GetSpan(State.Header_Start));
            InitTransitionMatrix_Header_InEscapedValue(escapeStartEqualsEscape, GetSpan(State.Header_InEscapedValue));
            InitTransitionMatrix_Header_InEscapedValueWithPendingEscape(rowEndings, escapeStartEqualsEscape, GetSpan(State.Header_InEscapedValueWithPendingEscape));
            InitTransitionMatrix_Header_InEscapedValue_ExpectingEndOfValueOrRecord(rowEndings, GetSpan(State.Header_InEscapedValue_ExpectingEndOfValueOrRecord));
            InitTransitionMatrix_Header_Unescaped_NoValue(rowEndings, escapeStartEqualsEscape, GetSpan(State.Header_Unescaped_NoValue));
            InitTransitionMatrix_Header_Unescaped_WithValue(rowEndings, escapeStartEqualsEscape, GetSpan(State.Header_Unescaped_WithValue));
            InitTransitionMatrix_Header_ExpectingEndOfRecord(GetSpan(State.Header_ExpectingEndOfRecord));

            InitTransitionMatrix_Comment_BeforeRecord(rowEndings, GetSpan(State.Comment_BeforeRecord));
            InitTransitionMatrix_Comment_BeforeRecord_ExpectingEndOfComment(GetSpan(State.Comment_BeforeRecord_ExpectingEndOfComment));

            InitTransitionMatrix_Record_Start(rowEndings, GetSpan(State.Record_Start));
            InitTransitionMatrix_Record_InEscapedValue(escapeStartEqualsEscape, GetSpan(State.Record_InEscapedValue));
            InitTransitionMatrix_Record_InEscapedValueWithPendingEscape(rowEndings, escapeStartEqualsEscape, GetSpan(State.Record_InEscapedValueWithPendingEscape));
            InitTransitionMatrix_Record_InEscapedValue_ExpectingEndOfValueOrRecord(rowEndings, GetSpan(State.Record_InEscapedValue_ExpectingEndOfValueOrRecord));
            InitTransitionMatrix_Record_Unescaped_NoValue(rowEndings, GetSpan(State.Record_Unescaped_NoValue));
            InitTransitionMatrix_Record_Unescaped_WithValue(rowEndings, GetSpan(State.Record_Unescaped_WithValue));
            InitTransitionMatrix_Record_ExpectingEndOfRecord(GetSpan(State.Record_ExpectingEndOfRecord));
            InitTransitionMatrix_Invalid(GetSpan(State.Invalid));

            // helper to DRY this up
            Span<TransitionRule> GetSpan(State state)
            {
                return GetTransitionRulesSpan(rowEndings, escapeStartEqualsEscape, state);
            }
        }

        private static Span<TransitionRule> GetTransitionRulesSpan(RowEndings rowEndings, bool escapeStartEqualsEscape, State state)
        {
            var configStart = GetConfigurationStartIndex(rowEndings, escapeStartEqualsEscape);

            var stateOffset = (byte)state * RuleCacheCharacterCount;

            var offset = configStart + stateOffset;
            var len = RuleCacheCharacterCount;

            return new Span<TransitionRule>(RuleCache, offset, len);
        }

        private static int GetConfigurationStartIndex(RowEndings rowEndings, bool escapeStartEqualsEscape)
        {
            var configNum = (byte)rowEndings;
            if (escapeStartEqualsEscape)
            {
                configNum += RuleCacheConfigCount / 2;
            }

            var configStart = configNum * RuleCacheConfigSize;

            return configStart;
        }

        // moving from Comment_BeforeHeader_ExpectingEndOfComment
        private static void InitTransitionMatrix_Comment_BeforeHeader_ExpectingEndOfComment(Span<TransitionRule> innerRet)
        {
            // Looks like
            // - #\r
            //
            // and we haven't read a header yet (but are expecting one)
            // can only happen if LineEndings is \r\n

            // \
            innerRet[(int)CharacterType.Escape] = Comment_BeforeHeader_Skip_Character;
            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Comment_BeforeHeader_Skip_Character;
            // ,
            innerRet[(int)CharacterType.ValueSeparator] = Comment_BeforeHeader_Skip_Character;
            // # (or whatever)
            innerRet[(int)CharacterType.CommentStart] = Comment_BeforeHeader_Skip_Character;
            // \r
            innerRet[(int)CharacterType.CarriageReturn] = Comment_BeforeHeader_Skip_Character;
            
            // \n
            innerRet[(int)CharacterType.LineFeed] = Header_Start_Skip_Character;

            // c
            innerRet[(int)CharacterType.Other] = Comment_BeforeHeader_Skip_Character;
        }

        // moving from Comment_BeforeRecord_ExpectingEndOfComment
        private static void InitTransitionMatrix_Comment_BeforeRecord_ExpectingEndOfComment(Span<TransitionRule> innerRet)
        {
            // Looks like
            // - #\r
            // 
            // and we're expecting to read a record (either no header, or we've already read it)
            // can only happen if LineEndings is \r\n

            // \
            innerRet[(int)CharacterType.Escape] = Comment_BeforeRecord_Skip_Character;
            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Comment_BeforeRecord_Skip_Character;
            // ,
            innerRet[(int)CharacterType.ValueSeparator] = Comment_BeforeRecord_Skip_Character;
            // # (or whatever)
            innerRet[(int)CharacterType.CommentStart] = Comment_BeforeRecord_Skip_Character;
            // \r
            innerRet[(int)CharacterType.CarriageReturn] = Comment_BeforeRecord_Skip_Character;

            // \n
            innerRet[(int)CharacterType.LineFeed] = Record_Start_Skip_Character;

            // c
            innerRet[(int)CharacterType.Other] = Comment_BeforeRecord_Skip_Character;
        }

        // moving from Comment_BeforeRecord
        private static void InitTransitionMatrix_Comment_BeforeRecord(RowEndings rowEndings, Span<TransitionRule> innerRet)
        {
            // Looks like
            // - # 
            //
            // and we haven't yet parsed a header (and are expecting to)

            // \
            innerRet[(int)CharacterType.Escape] = Comment_BeforeRecord_Skip_Character;
            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Comment_BeforeRecord_Skip_Character;
            // ,
            innerRet[(int)CharacterType.ValueSeparator] = Comment_BeforeRecord_Skip_Character;
            // # (or whatever)
            innerRet[(int)CharacterType.CommentStart] = Comment_BeforeRecord_Skip_Character;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEndings.CarriageReturn:
                    // ends the comment
                    forCarriageReturn = Record_Start_Skip_Character;
                    break;
                case RowEndings.CarriageReturnLineFeed:
                    // may end the comment, if followed by a \n
                    forCarriageReturn = Comment_BeforeRecord_ExpectingEndOfComment_Skip_Character;
                    break;
                case RowEndings.LineFeed:
                    // comment continues
                    forCarriageReturn = Comment_BeforeRecord_Skip_Character;
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
                case RowEndings.CarriageReturn:
                    // comment continues
                    forLineFeed = Comment_BeforeRecord_Skip_Character;
                    break;
                case RowEndings.CarriageReturnLineFeed:
                    // comment continues
                    forLineFeed = Comment_BeforeRecord_Skip_Character;
                    break;
                case RowEndings.LineFeed:
                    // ends the comment
                    forLineFeed = Record_Start_Skip_Character;
                    break;
                default:
                    forLineFeed = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // c
            innerRet[(int)CharacterType.Other] = Comment_BeforeRecord_Skip_Character;
        }

        // moving from Comment_BeforeHeader
        private static void InitTransitionMatrix_Comment_BeforeHeader(RowEndings rowEndings, Span<TransitionRule> innerRet)
        {
            // Looks like
            // - # 
            //
            // and we haven't yet parsed a header (and are expecting to)

            // \
            innerRet[(int)CharacterType.Escape] = Comment_BeforeHeader_Skip_Character;
            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Comment_BeforeHeader_Skip_Character;
            // ,
            innerRet[(int)CharacterType.ValueSeparator] = Comment_BeforeHeader_Skip_Character;
            // # (or whatever)
            innerRet[(int)CharacterType.CommentStart] = Comment_BeforeHeader_Skip_Character;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEndings.CarriageReturn:
                    // ends the comment
                    forCarriageReturn = Header_Start_Skip_Character;
                    break;
                case RowEndings.CarriageReturnLineFeed:
                    // may end the comment, if followed by a \n
                    forCarriageReturn = Comment_BeforeHeader_ExpectingEndOfComment_Skip_Character;
                    break;
                case RowEndings.LineFeed:
                    // comment continues
                    forCarriageReturn = Comment_BeforeHeader_Skip_Character;
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
                case RowEndings.CarriageReturn:
                    // comment continues
                    forLineFeed = Comment_BeforeHeader_Skip_Character;
                    break;
                case RowEndings.CarriageReturnLineFeed:
                    // comment continues
                    forLineFeed = Comment_BeforeHeader_Skip_Character;
                    break;
                case RowEndings.LineFeed:
                    // ends the comment
                    forLineFeed = Header_Start_Skip_Character;
                    break;
                default:
                    forLineFeed = Invalid_Exception_UnexpectedLineEnding;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // c
            innerRet[(int)CharacterType.Other] = Comment_BeforeHeader_Skip_Character;
        }

        // moving from Header_Start
        private static void InitTransitionMatrix_Header_Start(RowEndings rowEndings, Span<TransitionRule> innerRet)
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
                case RowEndings.CarriageReturn:
                    // ends the header
                    forCarriageReturn = Record_Start_SkipCharacter;
                    break;
                case RowEndings.CarriageReturnLineFeed:
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
                case RowEndings.LineFeed:
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
        }

        // moving from Record_Start
        private static void InitTransitionMatrix_Record_Start(RowEndings rowEndings, Span<TransitionRule> innerRet)
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
            innerRet[(int)CharacterType.ValueSeparator] = Record_Unescaped_NoValue_Finished_Value;
            // # (or whatever)
            innerRet[(int)CharacterType.CommentStart] = Comment_BeforeRecord_Skip_Character;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEndings.CarriageReturn:
                    // ends the header
                    forCarriageReturn = Record_Start_Finished_Record;
                    break;
                case RowEndings.CarriageReturnLineFeed:
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
                case RowEndings.LineFeed:
                    // ends header
                    forLineFeed = Record_Start_Finished_Record;
                    break;
                default:
                    forLineFeed = Invalid_Exception_ExpectedEndOfRecordOrValue;
                    break;
            }
            innerRet[(int)CharacterType.LineFeed] = forLineFeed;

            // c
            innerRet[(int)CharacterType.Other] = Record_Unescaped_WithValue_Append_Character;
        }

        // moving from Header_InEscapedValue_ExpectingEndOfValueOrRecord
        private static void InitTransitionMatrix_Header_InEscapedValue_ExpectingEndOfValueOrRecord(RowEndings rowEndings, Span<TransitionRule> innerRet)
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
                case RowEndings.CarriageReturn:
                    // ends the header
                    forCarriageReturn = Record_Start_SkipCharacter;
                    break;
                case RowEndings.CarriageReturnLineFeed:
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
                case RowEndings.LineFeed:
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
        }

        // moving from Record_InEscapedValue_ExpectingEndOfValueOrRecord
        private static void InitTransitionMatrix_Record_InEscapedValue_ExpectingEndOfValueOrRecord(RowEndings rowEndings, Span<TransitionRule> innerRet)
        {
            // Look like
            // - "df"

            // "df"\
            innerRet[(int)CharacterType.Escape] = Invalid_Exception_ExpectedEndOfRecordOrValue;
            // "df""
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_Exception_ExpectedEndOfRecordOrValue;
            // "df",
            innerRet[(int)CharacterType.ValueSeparator] = Record_Unescaped_NoValue_Finished_Value;

            // "df"\r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEndings.CarriageReturn:
                    // ends the record
                    forCarriageReturn = Record_Start_Finished_Record;
                    break;
                case RowEndings.CarriageReturnLineFeed:
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
                case RowEndings.LineFeed:
                    // ends header
                    forLineFeed = Record_Start_Finished_Record;
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
        }

        // moving from Header_InEscapedValue
        private static void InitTransitionMatrix_Header_InEscapedValue(bool escapeStartEqualsEscape, Span<TransitionRule> innerRet)
        {
            // Looks like (assuming escape is ", sep is ,)
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
        }

        // moving from Header_InEscapedValueWithPendingEscape
        private static void InitTransitionMatrix_Header_InEscapedValueWithPendingEscape(RowEndings rowEndings, bool escapeStartCharEqualsEscapeChar, Span<TransitionRule> innerRet)
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
                case RowEndings.CarriageReturnLineFeed:
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
                case RowEndings.CarriageReturn:
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
                case RowEndings.LineFeed:
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
        }

        // moving from Header_Unescaped_NoValue
        private static void InitTransitionMatrix_Header_Unescaped_NoValue(RowEndings rowEndings, bool escapeStartCharEqualsEscapeChar, Span<TransitionRule> innerRet)
        {
            // Looks like (assuming escape is ", sep is ,)
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
                case RowEndings.CarriageReturnLineFeed:
                    // may end header
                    forCarriageReturn = Header_ExpectingEndOfRecord_SkipCharacter;
                    break;
                case RowEndings.CarriageReturn:
                    // ends header
                    forCarriageReturn = Record_Start_SkipCharacter;
                    break;
                case RowEndings.LineFeed:
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
                case RowEndings.CarriageReturnLineFeed:
                    // doesn't end header
                    forLineFeed = Header_Unescaped_WithValue_Skip_Character;
                    break;
                case RowEndings.LineFeed:
                    // ends header
                    forLineFeed = Record_Start_SkipCharacter;
                    break;
                case RowEndings.CarriageReturn:
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
        }

        // moving from Header_Unescaped_WithValue
        private static void InitTransitionMatrix_Header_Unescaped_WithValue(RowEndings rowEndings, bool escapeStartCharEqualsEscapeChar, Span<TransitionRule> innerRet)
        {
            // Looks like (assuming escape is ", sep is ,)
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
                case RowEndings.CarriageReturnLineFeed:
                    // will end the record if \n is the next char
                    forCarriageReturn = Header_ExpectingEndOfRecord_SkipCharacter;
                    break;
                case RowEndings.CarriageReturn:
                    // ends the record
                    forCarriageReturn = Record_Start_SkipCharacter;
                    break;
                case RowEndings.LineFeed:
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
                case RowEndings.CarriageReturnLineFeed:
                    // doesn't end the record
                    forLineFeed = Header_Unescaped_WithValue_Skip_Character;
                    break;
                case RowEndings.LineFeed:
                    // ends the record
                    forLineFeed = Record_Start_SkipCharacter;
                    break;
                case RowEndings.CarriageReturn:
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
        }

        // moving from Header_ExpectingEndOfRecord
        private static void InitTransitionMatrix_Header_ExpectingEndOfRecord(Span<TransitionRule> innerRet)
        {
            // this only happens when there's a \r\n line ending
            // looks like
            // - foo\r
            // - "foo"\r

            // foo\r\
            innerRet[(int)CharacterType.Escape] = Invalid_Exception_ExpectedEndOfRecord;
            // foo\r"
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_Exception_ExpectedEndOfRecord;
            // foo\r,
            innerRet[(int)CharacterType.ValueSeparator] = Invalid_Exception_ExpectedEndOfRecord;

            // foo\r\r
            innerRet[(int)CharacterType.CarriageReturn] = Invalid_Exception_ExpectedEndOfRecord;

            // foo\r\n
            innerRet[(int)CharacterType.LineFeed] = Record_Start_SkipCharacter;

            // foo\r#
            innerRet[(int)CharacterType.CommentStart] = Invalid_Exception_ExpectedEndOfRecord;

            // foo\rc
            innerRet[(int)CharacterType.Other] = Invalid_Exception_ExpectedEndOfRecord;
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
        }

        // moving from Record_InEscapedValueWithPendingEscape
        private static void InitTransitionMatrix_Record_InEscapedValueWithPendingEscape(RowEndings rowEndings, bool escapeStartCharEqualsEscapeChar, Span<TransitionRule> innerRet)
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
                forValueSep = Record_Unescaped_NoValue_Finished_Value;
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
                case RowEndings.CarriageReturnLineFeed:
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
                case RowEndings.CarriageReturn:
                    if (escapeStartCharEqualsEscapeChar)
                    {
                        // "df"\r
                        forCarriageReturn = Record_Start_Finished_Record;
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
                case RowEndings.LineFeed:
                    if (escapeStartCharEqualsEscapeChar)
                    {
                        // "df"\n
                        forLineFeed = Record_Start_Finished_Record;
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
        }

        // moving from Record_Unescaped_NoValue
        private static void InitTransitionMatrix_Record_Unescaped_NoValue(RowEndings rowEndings, Span<TransitionRule> innerRet)
        {
            // Looks like (assuming escape is ", sep is ,)
            // - <EMPTY>

            // \
            innerRet[(int)CharacterType.Escape] = Record_InEscapedValue_Skip_Character;

            // "
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Record_InEscapedValue_Skip_Character;

            // ,
            innerRet[(int)CharacterType.ValueSeparator] = Record_Unescaped_NoValue_Finished_Value;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEndings.CarriageReturnLineFeed:
                    // may end record, if followed by a \n
                    forCarriageReturn = Record_ExpectingEndOfRecord_Skip_Character;
                    break;
                case RowEndings.CarriageReturn:
                    // ends record
                    forCarriageReturn = Record_Start_Finished_Record;
                    break;
                case RowEndings.LineFeed:
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
                case RowEndings.CarriageReturnLineFeed:
                    // does not end record
                    forLineFeed = Record_Unescaped_WithValue_Append_Character;
                    break;
                case RowEndings.LineFeed:
                    // ends record
                    forLineFeed = Record_Start_Finished_Record;
                    break;
                case RowEndings.CarriageReturn:
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
        }

        // moving from Record_Unescaped_WithValue
        private static void InitTransitionMatrix_Record_Unescaped_WithValue(RowEndings rowEndings, Span<TransitionRule> innerRet)
        {
            // looks like
            //  - df

            // df\
            innerRet[(int)CharacterType.Escape] = Invalid_ExceptionStartEscapeInValue;

            // df"
            innerRet[(int)CharacterType.EscapeStartAndEnd] = Invalid_ExceptionStartEscapeInValue;

            // df,
            innerRet[(int)CharacterType.ValueSeparator] = Record_Unescaped_NoValue_Finished_Value;

            // \r
            TransitionRule forCarriageReturn;
            switch (rowEndings)
            {
                case RowEndings.CarriageReturnLineFeed:
                    // may end record, if followed by \n
                    forCarriageReturn = Record_ExpectingEndOfRecord_Skip_Character;
                    break;
                case RowEndings.CarriageReturn:
                    // ends record
                    forCarriageReturn = Record_Start_Finished_Record;
                    break;
                case RowEndings.LineFeed:
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
                case RowEndings.CarriageReturnLineFeed:
                    // does not end record
                    forLineFeed = Record_Unescaped_WithValue_Append_Character;
                    break;
                case RowEndings.LineFeed:
                    // ends record
                    forLineFeed = Record_Start_Finished_Record;
                    break;
                case RowEndings.CarriageReturn:
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
            innerRet[(int)CharacterType.LineFeed] = Record_Start_Finished_Record;
            // \r#
            innerRet[(int)CharacterType.CommentStart] = Invalid_Exception_ExpectedEndOfRecord;
            // \rc
            innerRet[(int)CharacterType.Other] = Invalid_Exception_ExpectedEndOfRecord;
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
        }
    }
}
