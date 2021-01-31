using System;
using System.Buffers;

using static Cesil.ReaderStateMachine;

namespace Cesil
{
    internal struct CharacterLookup
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

        internal readonly int MinimumCharacter;
        internal readonly int CharLookupOffset;

        // internal for testing purposes
        private readonly char[] MemoryArry;
        internal readonly unsafe char* Memory;

        internal unsafe CharacterLookup(int mc, int clo, char[] mArr, char* m)
        {
            MinimumCharacter = mc;
            CharLookupOffset = clo;
            MemoryArry = mArr;
            Memory = m;
        }

        internal static unsafe CharacterLookup MakeCharacterLookup(
            Options options,
            out int neededSize
        )
        {
            var escapeStartChar = options.EscapedValueStartAndEnd;
            var valueSeparatorChar = options.ValueSeparator[0];
            var valueSepIsMultiChar = options.ValueSeparator.Length > 1;
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

            var charLookupArr = GC.AllocateArray<char>(neededSize, pinned: true);

            // this is a no-op, because RuleCache is on the pinned heap
            fixed (char* charLookupPtr = charLookupArr)
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
                        if (valueSepIsMultiChar)
                        {
                            cType = CharacterType.MaybeValueSeparator;
                        }
                        else
                        {
                            cType = CharacterType.ValueSeparator;
                        }
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

                // need to capture the array so it isn't GC'd
                return new CharacterLookup(minimumCharacter, neededSize, charLookupArr, charLookupPtr);
            }
        }
    }
}
