using static Cesil.ReaderStateMachine;

namespace Cesil
{
    // special class to hold these instead of putting them in ReaderStateMachine
    //
    // they work fine in ReaderStateMachine, but their _declaration order_ matters
    //   since RuleCache is also initialized there... and that's just a foot gun
    internal static class TransitionRules
    {
        internal static readonly TransitionRule Record_InEscapedValueWithPendingEscape_Skip_Character = (State.Record_InEscapedValueWithPendingEscape, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Header_InEscapedValueWithPendingEscape_Skip_Character = (State.Header_InEscapedValueWithPendingEscape, AdvanceResult.Skip_Character);

        internal static readonly TransitionRule Invalid_Exception_UnexpectedCharacterInEscapeSequence = (State.Invalid, AdvanceResult.Exception_UnexpectedCharacterInEscapeSequence);
        internal static readonly TransitionRule Header_ExpectingEndOfRecord_SkipCharacter = (State.Header_ExpectingEndOfRecord, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Invalid_Exception_UnexpectedLineEnding = (State.Invalid, AdvanceResult.Exception_UnexpectedLineEnding);
        internal static readonly TransitionRule Invalid_Exception_ExpectedEndOfRecord = (State.Invalid, AdvanceResult.Exception_ExpectedEndOfRecord);
        internal static readonly TransitionRule Record_Start_Finished_LastValueUnescaped_Record = (State.Record_Start, AdvanceResult.Finished_LastValueUnescaped_Record);
        internal static readonly TransitionRule Record_Start_Finished_LastValueEscaped_Record = (State.Record_Start, AdvanceResult.Finished_LastValueEscaped_Record);
        internal static readonly TransitionRule Record_Unescaped_NoValue_Finished_Unescaped_Value = (State.Record_Unescaped_NoValue, AdvanceResult.Finished_Unescaped_Value);
        internal static readonly TransitionRule Record_Unescaped_NoValue_Finished_Escaped_Value = (State.Record_Unescaped_NoValue, AdvanceResult.Finished_Escaped_Value);
        internal static readonly TransitionRule Record_ExpectingEndOfRecord_Skip_Character = (State.Record_ExpectingEndOfRecord, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Invalid_Exception_InvalidState = (State.Invalid, AdvanceResult.Exception_InvalidState);
        internal static readonly TransitionRule Record_InEscapedValue_Append_Character = (State.Record_InEscapedValue, AdvanceResult.Append_Character);
        internal static readonly TransitionRule Header_InEscapedValue_Skip_Character = (State.Header_InEscapedValue, AdvanceResult.Skip_Character);

        internal static readonly TransitionRule Header_Unescaped_NoValue_Skip_Character = (State.Header_Unescaped_NoValue, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Header_Unescaped_WithValue_Skip_Character = (State.Header_Unescaped_WithValue, AdvanceResult.Skip_Character);

        internal static readonly TransitionRule Record_Unescaped_WithValue_Append_Character = (State.Record_Unescaped_WithValue, AdvanceResult.Append_Character);

        internal static readonly TransitionRule Record_Unescaped_NoValue_Skip_Character = (State.Record_Unescaped_NoValue, AdvanceResult.Skip_Character);

        internal static readonly TransitionRule Record_InEscapedValue_Skip_Character = (State.Record_InEscapedValue, AdvanceResult.Skip_Character);

        internal static readonly TransitionRule Record_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character = (State.Record_InEscapedValue_ExpectingEndOfValueOrRecord, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Header_InEscapedValue_ExpectingEndOfValueOrRecord_Skip_Character = (State.Header_InEscapedValue_ExpectingEndOfValueOrRecord, AdvanceResult.Skip_Character);

        internal static readonly TransitionRule Record_Start_SkipCharacter = (State.Record_Start, AdvanceResult.Skip_Character);

        internal static readonly TransitionRule Invalid_Exception_ExpectedEndOfRecordOrValue = (State.Invalid, AdvanceResult.Exception_ExpectedEndOfRecordOrValue);
        internal static readonly TransitionRule Invalid_ExceptionStartEscapeInValue = (State.Invalid, AdvanceResult.Exception_StartEscapeInValue);

        internal static readonly TransitionRule Comment_BeforeHeader_Skip_Character = (State.Comment_BeforeHeader, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Comment_BeforeRecord_Skip_Character = (State.Comment_BeforeRecord, AdvanceResult.Skip_Character);

        internal static readonly TransitionRule Comment_BeforeHeader_Append_Character = (State.Comment_BeforeHeader, AdvanceResult.Append_Character);
        internal static readonly TransitionRule Comment_BeforeRecord_Append_Character = (State.Comment_BeforeRecord, AdvanceResult.Append_Character);

        internal static readonly TransitionRule Header_Start_Skip_Character = (State.Header_Start, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Header_Start_Finished_Comment = (State.Header_Start, AdvanceResult.Finished_Comment);
        internal static readonly TransitionRule Record_Start_Skip_Character = (State.Record_Start, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Record_Start_Finished_Comment = (State.Record_Start, AdvanceResult.Finished_Comment);

        internal static readonly TransitionRule Comment_BeforeHeader_ExpectingEndOfComment_Skip_Character = (State.Comment_BeforeHeader_ExpectingEndOfComment, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Comment_BeforeHeader_ExpectingEndOfComment_Append_Character = (State.Comment_BeforeHeader_ExpectingEndOfComment, AdvanceResult.Append_Character);

        internal static readonly TransitionRule Comment_BeforeRecord_ExpectingEndOfComment_Skip_Character = (State.Comment_BeforeRecord_ExpectingEndOfComment, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Comment_BeforeRecord_ExpectingEndOfComment_Append_Character = (State.Comment_BeforeRecord_ExpectingEndOfComment, AdvanceResult.Append_Character);

        internal static readonly TransitionRule Comment_BeforeHeader_Append_CarriageReturn_And_Current_Character = (State.Comment_BeforeHeader, AdvanceResult.Append_CarriageReturnAndCurrentCharacter);
        internal static readonly TransitionRule Comment_BeforeRecord_Append_CarriageReturn_And_Current_Character = (State.Comment_BeforeRecord, AdvanceResult.Append_CarriageReturnAndCurrentCharacter);

        internal static readonly TransitionRule Data_Ended_Skip_Character = (State.DataEnded, AdvanceResult.Skip_Character);
        internal static readonly TransitionRule Data_Ended_Finished_LastValueUnescaped_Record = (State.DataEnded, AdvanceResult.Finished_LastValueUnescaped_Record);
        internal static readonly TransitionRule Data_Ended_Finished_LastValueEscaped_Record = (State.DataEnded, AdvanceResult.Finished_LastValueEscaped_Record);
        internal static readonly TransitionRule Data_Ended_Append_CarriageReturn_And_End_Comment = (State.DataEnded, AdvanceResult.Append_CarriageReturnAndEndComment);
        internal static readonly TransitionRule Data_Ended_FinishedComment = (State.DataEnded, AdvanceResult.Finished_Comment);
        internal static readonly TransitionRule Data_Ended_Exception_UnexpectedEnd = (State.DataEnded, AdvanceResult.Exception_UnexpectedEnd);
        internal static readonly TransitionRule Data_Ended_Finished_Unescaped_Value = (State.DataEnded, AdvanceResult.Finished_Unescaped_Value);
        internal static readonly TransitionRule Data_Ended_Finished_Escaped_Value = (State.DataEnded, AdvanceResult.Finished_Escaped_Value);

        internal static readonly TransitionRule Invalid_Skip_Character = (State.Invalid, AdvanceResult.Skip_Character);
    }
}
