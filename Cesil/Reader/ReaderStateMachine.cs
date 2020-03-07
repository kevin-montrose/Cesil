using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Cesil
{
    internal sealed partial class ReaderStateMachine : ITestableDisposable
    {
        public bool IsDisposed => CurrentState == State.NONE;

        public State CurrentState;

        internal RowEnding RowEndings;
        internal ReadHeader HasHeaders;

#if DEBUG
        private int TransitionMatrixMemoryOffset;
#endif
        private ReadOnlyMemory<TransitionRule> TransitionMatrixMemory;
        private CharacterLookup CharacterLookup;

        private MemoryHandle CharLookupPin;
        private unsafe CharacterType* CharLookup;



        private MemoryHandle TransitionMatrixHandle;
        private unsafe TransitionRule* TransitionMatrix;

        internal unsafe bool IsPinned => CharLookup != null || TransitionMatrix != null;

        internal ReaderStateMachine() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsInEscapedValue(State state)
        => (((byte)state) & IN_ESCAPED_VALUE_MASK) == IN_ESCAPED_VALUE_MASK;

        internal void Initialize(
            CharacterLookup preAllocLookup,
            char? escapeStartChar,
            char? escapeChar,
            RowEnding rowEndings,
            ReadHeader hasHeaders,
            bool readingComments,
            bool skipLeadingWhitespace,
            bool skipTrailingWhitespace
        )
        {
            CharacterLookup = preAllocLookup;
            RowEndings = rowEndings;
            HasHeaders = hasHeaders;

            switch (HasHeaders)
            {
                case ReadHeader.Always:
                    CurrentState = State.Header_Start;
                    break;
                case ReadHeader.Never:
                    CurrentState = State.Record_Start;
                    break;
                default:
                    Throw.InvalidOperationException<object>($"Unexpected {nameof(ReadHeader)}: {HasHeaders}");
                    break;
            }

            TransitionMatrixMemory =
                GetTransitionMatrix(
                    RowEndings,
                    escapeStartChar.HasValue && escapeStartChar == escapeChar,
                    readingComments,
                    skipLeadingWhitespace,
                    skipTrailingWhitespace,
#if DEBUG
                    out TransitionMatrixMemoryOffset
#else
                    out _
#endif
            );


        }

        internal AdvanceResult EndOfData()
        => AdvanceInner(CurrentState, CharacterType.DataEnd);

        internal unsafe AdvanceResult Advance(char c)
        {
            var fromState = CurrentState;

            var cOffset = GetCharLookupOffset(in CharacterLookup, fromState, c);
            CharacterType cType;
            if (cOffset == null)
            {
                cType = CharacterType.Other;
            }
            else
            {
                cType = CharLookup[cOffset.Value];
            }

            return AdvanceInner(fromState, cType);
        }

        // internal for testing purposes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int? GetCharLookupOffset(in CharacterLookup charLookup, State fromState, char c)
        {
            var cOffset = (c - charLookup.MinimumCharacter);
            if (cOffset < 0 || cOffset >= charLookup.CharLookupOffset)
            {
                return null;
            }

            return cOffset;
        }

        internal void EnsurePinned()
        {
            if (!IsPinned)
            {
                PinInner();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe AdvanceResult AdvanceInner(State fromState, CharacterType cType)
        {
            var offset = GetTransitionMatrixOffset(fromState, cType);

            var forChar = TransitionMatrix[offset];

            CurrentState = forChar.NextState;
            return forChar.Result;
        }

        // internal for testing purposes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetTransitionMatrixOffset(State fromState, CharacterType cType)
        {
            var stateOffset = (byte)fromState * RuleCacheCharacterCount;
            var forCharOffset = stateOffset + (byte)cType;

            return forCharOffset;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                CurrentState = State.NONE;
            }
        }

#if DEBUG
        // just for debugging purposes
        internal unsafe string ToDebugString()
        {
            PinHandle? pin = null;
            if (!IsPinned)
            {
                pin = Pin();
            }

            using (pin)
            {
                var ret = new System.Text.StringBuilder();
                ret.AppendLine($"Offset = {TransitionMatrixMemoryOffset}");

                foreach (State? sNull in Enum.GetValues(typeof(State)))
                {
                    var s = sNull!.Value;

                    ret.AppendLine();
                    ret.AppendLine($"From {s}");
                    ret.AppendLine("===");
                    foreach (CharacterType? cNull in Enum.GetValues(typeof(CharacterType)))
                    {
                        var c = cNull!.Value;

                        var offset = GetTransitionMatrixOffset(s, c);

                        EnsurePinned();
                        var rule = TransitionMatrix[offset];

                        ret.AppendLine($"  {c} => ({rule.NextState}, {rule.Result})");
                    }
                }

                return ret.ToString();
            }
        }
#endif
    }
}
