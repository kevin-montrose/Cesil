using System;
using System.Buffers;

using static Cesil.ReaderStateMachine;

namespace Cesil
{
    internal struct CharacterLookup : ITestableDisposable
    {
        public bool IsDisposed => Memory == null;

        internal readonly int MinimumCharacter;
        internal readonly int CharLookupOffset;

        // internal for testing purposes
        internal IMemoryOwner<char> Memory;

        internal CharacterLookup(int mc, int clo, IMemoryOwner<char> m)
        {
            MinimumCharacter = mc;
            CharLookupOffset = clo;
            Memory = m;
        }

        internal static unsafe CharacterLookup MakeCharacterLookup(
            MemoryPool<char> memoryPool,
            char escapeStartChar,
            char valueSeparatorChar,
            char escapeChar,
            char? commentChar,
            out int neededSize
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
            neededSize = charLookupOffset * 2;

            var charLookupOwner = memoryPool.Rent(neededSize / sizeof(char));

            fixed (char* charLookupPtr = charLookupOwner.Memory.Span)
            {
                CharacterType* charLookup = (CharacterType*)charLookupPtr;

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
            }

            return new CharacterLookup(minimumCharacter, charLookupOffset, charLookupOwner);
        }

        internal unsafe MemoryHandle Pin(out CharacterType* charLookup)
        {
            var ret = Memory.Memory.Pin();

            charLookup = (CharacterType*)ret.Pointer;

            return ret;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                Memory.Dispose();

                Memory = null;
            }
        }
    }
}
