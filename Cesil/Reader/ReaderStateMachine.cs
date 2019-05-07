using System;
using System.Buffers;

namespace Cesil
{
    internal unsafe sealed partial class ReaderStateMachine: ITestableDisposable
    {
        internal struct CharacterLookup: ITestableDisposable
        {
            public bool IsDisposed => Memory == null;

            private readonly int MinimumCharacter;
            private readonly int CharLookupOffset;
            private IMemoryOwner<char> Memory;
            private readonly MemoryHandle Handle;
            private readonly CharacterType* CharLookup;

            internal CharacterLookup(int mc, int clo, IMemoryOwner<char> m, MemoryHandle h, CharacterType* cl)
            {
                MinimumCharacter = mc;
                CharLookupOffset = clo;
                Memory = m;
                Handle = h;
                CharLookup = cl;
            }

            internal void Deconstruct(out int minimumCharacter, out int charLookupOffset, out IMemoryOwner<char> memory, out MemoryHandle handle, out CharacterType* charLookup)
            {
                minimumCharacter = MinimumCharacter;
                charLookupOffset = CharLookupOffset;
                memory = Memory;
                handle = Handle;
                charLookup = CharLookup;
            }

            public void AssertNotDisposed()
            {
                if (IsDisposed)
                {
                    Throw.ObjectDisposedException(nameof(CharacterLookup));
                }
            }

            public void Dispose()
            {
                if (!IsDisposed)
                {
                    Handle.Dispose();
                    Memory.Dispose();

                    Memory = null;
                }
            }
        }

        public bool IsDisposed => CurrentState == State.NONE;

        public State CurrentState;

        internal readonly RowEndings RowEndings;
        internal readonly ReadHeaders HasHeaders;

        internal readonly MemoryHandle TransitionMatrixHandle;
        internal readonly TransitionRule* TransitionMatrix;

        private readonly bool SuppressCharLookupDispose;

        private readonly int MinimumCharacter;
        private readonly int CharLookupOffset;
        private readonly IMemoryOwner<char> CharLookupOwner;
        private readonly MemoryHandle CharLookupPin;
        private readonly CharacterType* CharLookup;

        internal ReaderStateMachine(
            MemoryPool<char> memoryPool,
            char escapeStartChar,
            char valueSeparatorChar,
            char escapeChar,
            RowEndings rowEndings,
            ReadHeaders hasHeaders,
            char? commentChar
        )
        {
            RowEndings = rowEndings;
            HasHeaders = hasHeaders;

            switch (HasHeaders)
            {
                case ReadHeaders.Always:
                    CurrentState = State.Header_Start;
                    break;
                case ReadHeaders.Never:
                    CurrentState = State.Record_Start;
                    break;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(ReadHeaders)}: {HasHeaders}");
                    break;
            }

            TransitionMatrixHandle = GetTransitionMatrix(RowEndings, escapeStartChar == escapeChar).Pin();
            TransitionMatrix = (TransitionRule*)TransitionMatrixHandle.Pointer;

            SuppressCharLookupDispose = false;
            (MinimumCharacter, CharLookupOffset, CharLookupOwner, CharLookupPin, CharLookup) = 
                MakeCharacterLookup(memoryPool, escapeStartChar, valueSeparatorChar, escapeChar, commentChar);
        }

        internal ReaderStateMachine(
            CharacterLookup preAllocLookup,
            char escapeStartChar,
            char escapeChar,
            RowEndings rowEndings,
            ReadHeaders hasHeaders
        )
        {
            RowEndings = rowEndings;
            HasHeaders = hasHeaders;

            switch (HasHeaders)
            {
                case ReadHeaders.Always:
                    CurrentState = State.Header_Start;
                    break;
                case ReadHeaders.Never:
                    CurrentState = State.Record_Start;
                    break;
                default:
                    Throw.InvalidOperationException($"Unexpected {nameof(ReadHeaders)}: {HasHeaders}");
                    break;
            }

            TransitionMatrixHandle = GetTransitionMatrix(RowEndings, escapeStartChar == escapeChar).Pin();
            TransitionMatrix = (TransitionRule*)TransitionMatrixHandle.Pointer;

            SuppressCharLookupDispose = true;
            (MinimumCharacter, CharLookupOffset, _, _, CharLookup) = preAllocLookup;
        }

        internal AdvanceResult Advance(char c)
        {
            var fromState = CurrentState;

            CharacterType cType;
            var cOffset = (c - MinimumCharacter);
            if (cOffset < 0 || cOffset >= CharLookupOffset)
            {
                cType = CharacterType.Other;
            }
            else
            {
                var inEscapedValue = (((byte)fromState) & IN_ESCAPED_VALUE_MASK) == IN_ESCAPED_VALUE_MASK;
                if (inEscapedValue)
                {
                    cOffset += CharLookupOffset;
                }

                cType = CharLookup[cOffset];
            }

            var stateOffset = (byte)fromState * RuleCacheCharacterCount;
            var forCharOffset = stateOffset + (byte)cType;

            var forChar = TransitionMatrix[forCharOffset];

            CurrentState = forChar.NextState;
            return forChar.Result;
        }

        internal static unsafe CharacterLookup MakeCharacterLookup(
            MemoryPool<char> memoryPool,
            char escapeStartChar,
            char valueSeparatorChar,
            char escapeChar,
            char? commentChar
        )
        {
            var minimumCharacter =
                Math.Min(
                    Math.Min(
                        Math.Min(
                            Math.Min(
                                Math.Min(escapeStartChar, valueSeparatorChar),
                                escapeChar
                            ),
                            commentChar ?? char.MaxValue
                        ),
                        '\r'
                    ),
                    '\n'
                );
            var maxChar =
                Math.Max(
                    Math.Max(
                        Math.Max(
                            Math.Max(
                                Math.Max(escapeStartChar, valueSeparatorChar),
                                escapeChar
                            ),
                            commentChar ?? char.MinValue
                        ),
                        '\r'
                    ),
                    '\n'
                );
            var charLookupOffset = (maxChar - minimumCharacter) + 1;
            var charLookupOwner = memoryPool.Rent(charLookupOffset * 2 / sizeof(char));
            var charLookupPin = charLookupOwner.Memory.Pin();
            var charLookup = (CharacterType*)charLookupPin.Pointer;

            for (var i = 0; i < charLookupOffset; i++)
            {
                var c = (char)(minimumCharacter + i);

                CharacterType cType;
                if (c == escapeStartChar)
                {
                    cType = CharacterType.EscapeStartAndEnd;
                }
                else if (c == valueSeparatorChar)
                {
                    cType = CharacterType.ValueSeparator;
                }
                else if (c == '\r')
                {
                    cType = CharacterType.CarriageReturn;
                }
                else if (c == '\n')
                {
                    cType = CharacterType.LineFeed;
                }
                else if (commentChar != null && c == commentChar)
                {
                    cType = CharacterType.CommentStart;
                }
                else
                {
                    cType = CharacterType.Other;
                }

                charLookup[i] = cType;
            }

            for (var i = 0; i < charLookupOffset; i++)
            {
                var c = (char)(minimumCharacter + i);

                CharacterType cType;
                if (c == escapeChar)
                {
                    cType = CharacterType.Escape;
                }
                else if (c == escapeStartChar)
                {
                    cType = CharacterType.EscapeStartAndEnd;
                }
                else if (c == valueSeparatorChar)
                {
                    cType = CharacterType.ValueSeparator;
                }
                else if (c == '\r')
                {
                    cType = CharacterType.CarriageReturn;
                }
                else if (c == '\n')
                {
                    cType = CharacterType.LineFeed;
                }
                else if (commentChar != null && c == commentChar)
                {
                    cType = CharacterType.CommentStart;
                }
                else
                {
                    cType = CharacterType.Other;
                }

                charLookup[i + charLookupOffset] = cType;
            }

            return new CharacterLookup(minimumCharacter, charLookupOffset, charLookupOwner, charLookupPin, charLookup);
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                TransitionMatrixHandle.Dispose();
                if (!SuppressCharLookupDispose)
                {
                    CharLookupPin.Dispose();
                    CharLookupOwner.Dispose();
                }
                CurrentState = State.NONE;
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(ReaderStateMachine));
            }
        }
    }
}
