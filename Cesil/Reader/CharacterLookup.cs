using System;
using System.Buffers;

using static Cesil.ReaderStateMachine;

namespace Cesil
{
    internal struct CharacterLookup : ITestableDisposable
    {
        // internal for testing purposes
        internal static readonly char[] WhitespaceCharacters =
            new[]
            {
                '\u0009',
                '\u000A',
                '\u000B',
                '\u000C',
                '\u000D',
                '\u0020',
                '\u0085',
                '\u00A0',
                '\u1680',
                '\u2000',
                '\u2001',
                '\u2002',
                '\u2003',
                '\u2004',
                '\u2005',
                '\u2006',
                '\u2007',
                '\u2008',
                '\u2009',
                '\u200A',
                '\u2028',
                '\u2029',
                '\u202F',
                '\u205F',
                '\u3000'
            };

        public bool IsDisposed { get; private set; }

        internal readonly int MinimumCharacter;
        internal readonly int CharLookupOffset;

        // internal for testing purposes
        internal readonly IMemoryOwner<char> Memory;

        internal CharacterLookup(int mc, int clo, IMemoryOwner<char> m)
        {
            IsDisposed = false;
            MinimumCharacter = mc;
            CharLookupOffset = clo;
            Memory = m;
        }

        internal static unsafe CharacterLookup MakeCharacterLookup(
            Options options,
            out int neededSize
        )
        {
            var memoryPool = options.MemoryPool;
            var escapeStartChar = options.EscapedValueStartAndEnd;
            var valueSeparatorChar = options.ValueSeparator;
            var escapeChar = options.EscapedValueEscapeCharacter;
            var commentChar = options.CommentCharacter;

            var whitespaceSpecial = options.WhitespaceTreatment != WhitespaceTreatments.Preserve;

            var minimumCharacter =
                Math.Min(
                    Math.Min(
                        Math.Min(
                            Math.Min(
                                Math.Min(escapeStartChar ?? char.MaxValue, valueSeparatorChar),
                                escapeChar ?? char.MaxValue
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
                                Math.Max(escapeStartChar ?? char.MinValue, valueSeparatorChar),
                                escapeChar ?? char.MinValue
                            ),
                            commentChar ?? char.MinValue
                        ),
                        '\r'
                    ),
                    '\n'
                );

            if (whitespaceSpecial)
            {
                foreach (var c in WhitespaceCharacters)
                {
                    maxChar = Math.Max(maxChar, c);
                    minimumCharacter = Math.Min(minimumCharacter, c);
                }
            }

            neededSize = (maxChar - minimumCharacter) + 1;

            var charLookupOwner = memoryPool.Rent(neededSize / sizeof(char));

            fixed (char* charLookupPtr = charLookupOwner.Memory.Span)
            {
                CharacterType* charLookup = (CharacterType*)charLookupPtr;

                for (var i = 0; i < neededSize; i++)
                {
                    var c = (char)(minimumCharacter + i);

                    CharacterType cType;
                    if (c == escapeStartChar)
                    {
                        cType = CharacterType.EscapeStartAndEnd;
                    }
                    else if (c == escapeChar)
                    {
                        cType = CharacterType.Escape;
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
                    else if (whitespaceSpecial && Array.IndexOf(WhitespaceCharacters, c) != -1)
                    {
                        cType = CharacterType.Whitespace;
                    }
                    else
                    {
                        cType = CharacterType.Other;
                    }

                    charLookup[i] = cType;
                }
            }

            return new CharacterLookup(minimumCharacter, neededSize, charLookupOwner);
        }

        internal readonly unsafe MemoryHandle Pin(out CharacterType* charLookup)
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

                IsDisposed = true;
            }
        }
    }
}
