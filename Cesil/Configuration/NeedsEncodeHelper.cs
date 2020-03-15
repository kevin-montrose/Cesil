using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Cesil
{
    [StructLayout(LayoutKind.Explicit, Size = sizeof(short) * PROBABILITY_MAP_SIZE + sizeof(short) * AVX_REGISTER_VALUES)]
    internal unsafe struct NeedsEncodeHelper
    {
        private const int PROBABILITY_MAP_SIZE = 16;
        private const int AVX_REGISTER_VALUES = 5;
        private const int CHARS_PER_VECTOR = 256 / (sizeof(char) * 8);

        private const int END_OF_PROBILITY_MAP = PROBABILITY_MAP_SIZE * sizeof(short);

        // array for initializing a vector mask
        //
        // i * 8  = a mask for selecting (i * 2) characters
        //
        // 0  => 0x0000_0000_0000_0000_...._0000_0000 // 256 bits
        // 1  => 0xFFFF_FFFF_0000_0000_...._0000_0000 // 256 bits
        // ...
        // 7 => 0xFFFF_FFFF_FFFF_FFFF_...._FFFF_FFFF // 256 bits
        internal static ushort[] SUB_VECTOR_MASK =
            new ushort[]
            {   0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                0xFFFF, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
                0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000,
                0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x0000, 0x0000,
                0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
            };

        [FieldOffset(0)]
        internal fixed short MAP[PROBABILITY_MAP_SIZE];

        [FieldOffset(END_OF_PROBILITY_MAP)]
        private readonly short FirstChar;

        [FieldOffset(END_OF_PROBILITY_MAP + sizeof(short) * 1)]
        private readonly short SecondChar;

        [FieldOffset(END_OF_PROBILITY_MAP + sizeof(short) * 2)]
        private readonly short ThirdChar;

        [FieldOffset(END_OF_PROBILITY_MAP + sizeof(short) * 3)]
        private readonly short Char2Mask;
        [FieldOffset(END_OF_PROBILITY_MAP + sizeof(short) * 4)]
        private readonly short Char3Mask;

        public NeedsEncodeHelper(char c1, char? c2, char? c3)
        {
            FirstChar = (short)c1;
            SecondChar = (short)(c2.HasValue ? c2.Value : '\0');
            ThirdChar = (short)(c3.HasValue ? c3.Value : '\0');

            fixed (short* mapPtr = MAP)
            {
                // put \r and \n in there
                mapPtr[0] = 9216;

                AddCharacterToProbMap(mapPtr, FirstChar);
                if (c2.HasValue) AddCharacterToProbMap(mapPtr, (short)c2.Value);
                if (c3.HasValue) AddCharacterToProbMap(mapPtr, (short)c3.Value);
            }

            Char2Mask = (short)(c2.HasValue ? -1 : 0);
            Char3Mask = (short)(c3.HasValue ? -1 : 0);
        }

        // primary interface
        public int ContainsCharRequiringEncoding(char* strPtr, int len)
        {
            // We use Avx2, Avx2, and Bmi1 in CharRequiringEncodingAvx2
            //    but only need to check for Avx2 and Bmi1 because Avx is implied
            //    by Avx2.
            if (len < CHARS_PER_VECTOR || !Avx2.IsSupported || !Bmi1.IsSupported)
            {
                return ProbabilisticContainsChar(strPtr, len);
            }

            return Avx2ContainsChar(strPtr, len);
        }

        // internal for testing and benchmarking
        internal unsafe int Avx2ContainsChar(char* strPtr, int len)
        {
            const int CHARS_PER_INT = sizeof(int) / sizeof(char);
            const int BITS_IN_INT = sizeof(int) * 8;

            var v256Count = len / CHARS_PER_VECTOR;
            var remainingChars = len % CHARS_PER_VECTOR;

            short* shortPtr = (short*)strPtr;

            var char1Vec = Vector256.Create((short)'\r');
            var char2Vec = Vector256.Create((short)'\n');
            var char3Vec = Vector256.Create(FirstChar);
            var char4Vec = Vector256.Create(SecondChar);
            var char4VecMask = Vector256.Create(Char2Mask);
            var char5Vec = Vector256.Create(ThirdChar);
            var char5VecMask = Vector256.Create(Char3Mask);

            for (var i = 0; i < v256Count; i++)
            {
                var chars = Avx.LoadVector256(shortPtr);
                shortPtr += CHARS_PER_VECTOR;

                // chars is now: 0xAAAA_BBBB_CCCC_DDDD_EEEE_FFFF_GGGG_HHHH_IIII_JJJJ_KKKK_LLLL_MMMM_NNNN_OOOO_PPPP
                // 
                // each letter is 4 bits of a char, chunks with the same letter are the same char

                // The first three chars will always be set, no mask needed
                var a = Avx2.CompareEqual(char1Vec, chars);
                var res = a;

                var b = Avx2.CompareEqual(char2Vec, chars);
                res = Avx2.Or(res, b);

                var c = Avx2.CompareEqual(char3Vec, chars);
                res = Avx2.Or(res, c);

                // The last 2 chars are optional, so we use a mask to invalidate the compare if they're not set
                var d = Avx2.CompareEqual(char4Vec, chars);
                d = Avx2.And(char4VecMask, d);
                res = Avx2.Or(res, d);

                var e = Avx2.CompareEqual(char5Vec, chars);
                e = Avx2.And(char5VecMask, e);
                res = Avx2.Or(res, e);

                // res is now: 0xAAAA_BBBB_CCCC_DDDD_EEEE_FFFF_GGGG_HHHH_IIII_JJJJ_KKKK_LLLL_MMMM_NNNN_OOOO_PPPP
                //
                // each letter is either four 1s or four 0s, and corresponds to character with the same letter in chars

                var resBytes = res.AsByte();
                var matchingBytes = Avx2.MoveMask(resBytes);

                // mask is now 0bAA_BB_CC_DD_EE_FF_GG_HH_II_JJ_KK_LL_MM_NN_OO_PP
                //
                // each letter is a bit, and is the high bit of a 2 letter pair from res

                var trailingZeros = (int)Bmi1.TrailingZeroCount((uint)matchingBytes);

                // trailingZeros is now the count of the number of trailing zeros in mask
                //  
                // every 2 trailing zeros corresponds to one character that DID NOT 
                //   match

                if (trailingZeros != BITS_IN_INT)
                {
                    var charsToSkip = trailingZeros / sizeof(char);

                    return i * CHARS_PER_VECTOR + charsToSkip;
                }
            }

            // if there are any trailing chars, try and handle as many as we can still in parallel
            //   because of AVX limitations, we can only deal with an even number of chars
            //   so there can be one left over
            if (remainingChars >= 2)
            {
                var remainingInts = remainingChars / CHARS_PER_INT;

                int* remainingIntPtr = (int*)shortPtr;

                // figure out how many CHARS to take (and build a mask for it),
                //   but we can only take INTS, so we need to round down
                Vector256<short> maskShort;
                fixed (ushort* maskPtr = SUB_VECTOR_MASK)
                {
                    short* offsetMaskPtr = (short*)maskPtr;
                    offsetMaskPtr += CHARS_PER_VECTOR * remainingInts;
                    maskShort = Avx.LoadVector256(offsetMaskPtr);
                }

                // need to use a mask here so we don't load past the end of the buffer
                var maskInts = maskShort.AsInt32();
                var ints = Avx2.MaskLoad(remainingIntPtr, maskInts);

                var chars = ints.AsInt16();

                // chars is now: 0xAAAA_BBBB_CCCC_DDDD_EEEE_FFFF_GGGG_HHHH_IIII_JJJJ_KKKK_LLLL_MMMM_NNNN_OOOO_PPPP
                // 
                // each letter is 4 bits of a char, chunks with the same letter are the same char
                // 
                // if they were masked out, the bits should be all zeros (but treat them as garbage)

                // The first three chars will always be set, no mask needed
                var a = Avx2.CompareEqual(char1Vec, chars);
                var res = a;

                var b = Avx2.CompareEqual(char2Vec, chars);
                res = Avx2.Or(res, b);

                var c = Avx2.CompareEqual(char3Vec, chars);
                res = Avx2.Or(res, c);

                // The last 2 chars are optional, so we use a mask to invalidate the compare if they're not set
                var d = Avx2.CompareEqual(char4Vec, chars);
                d = Avx2.And(char4VecMask, d);
                res = Avx2.Or(res, d);

                var e = Avx2.CompareEqual(char5Vec, chars);
                e = Avx2.And(char5VecMask, e);
                res = Avx2.Or(res, e);

                // res is now: 0xAAAA_BBBB_CCCC_DDDD_EEEE_FFFF_GGGG_HHHH_IIII_JJJJ_KKKK_LLLL_MMMM_NNNN_OOOO_PPPP
                //
                // each letter is either four 1s or four 0s, and corresponds to character with the same letter in chars

                // need to do one last mask to clear any junk out of res before we check matching bits
                res = Avx2.And(res, maskShort);

                var resBytes = res.AsByte();
                var matchingBytes = Avx2.MoveMask(resBytes);

                // mask is now 0bAA_BB_CC_DD_EE_FF_GG_HH_II_JJ_KK_LL_MM_NN_OO_PP
                //
                // each letter is a bit, and is the high bit of a 2 letter pair from res

                var trailingZeros = (int)Bmi1.TrailingZeroCount((uint)matchingBytes);

                // trailingZeros is now the count of the number of trailing zeros in mask
                //  
                // every 2 trailing zeros corresponds to one character that DID NOT 
                //   match

                if (trailingZeros != BITS_IN_INT)
                {
                    var charsToSkip = trailingZeros / sizeof(char);

                    return v256Count * CHARS_PER_VECTOR + charsToSkip;
                }

                remainingIntPtr += remainingInts;
                shortPtr = (short*)remainingIntPtr;
            }

            var hasRemainingChar = (remainingChars % CHARS_PER_INT) != 0;
            if (hasRemainingChar)
            {
                var finalChar = *shortPtr;
                var needEncode =
                    finalChar == '\n' ||
                    finalChar == '\r' ||
                    finalChar == FirstChar ||
                    (Char2Mask != 0 && finalChar == SecondChar) ||
                    (Char3Mask != 0 && finalChar == ThirdChar);

                if (needEncode)
                {
                    return len - 1;
                }
            }

            return -1;
        }

        // internal for testing and benchmarking
        internal int ProbabilisticContainsChar(char* strPtr, int len)
        {
tryAgain:
            var ix = ProbablyContains(ref strPtr, len);
            if (ix == -1)
            {
                return -1;
            }

            var c = *strPtr;
            if (c == '\r' || c == '\n' || c == FirstChar || c == SecondChar || c == ThirdChar)
            {
                return ix;
            }

            strPtr++;
            len = len - ix - 1;
            if (len <= 0) return -1;

            goto tryAgain;
        }

        // inspired by https://github.com/bbowyersmyth/coreclr/blob/d59b674ee9cd6d092073f9d8d321f935a757e53d/src/classlibnative/bcltype/stringnative.cpp
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe int ProbablyContains(ref char* strPtr, int len)
        {
            fixed (short* mapPtr = MAP)
            {
                for (var i = 0; i < len; i++)
                {
                    var c = *strPtr;
                    var b = (byte)c;

                    // 0 to 15
                    var ln = (byte)(b & 0x00_00_00_FF);
                    var hn = (byte)(b >> 4);

                    // based on the low half
                    {
                        var mask = (short)(1 << ln);

                        var inMap = (mapPtr[hn] & mask) != 0;
                        if (!inMap)
                        {
                            strPtr++;
                            continue;
                        }
                    }

                    return i;
                }

                return -1;
            }
        }

        private static void AddCharacterToProbMap(short* map, short c)
        {
            var b = (byte)c;

            var ln = (byte)(b & 0x00_00_00_FF);
            var hn = (byte)(b >> 4);

            var mask = (short)(1 << ln);
            map[hn] |= mask;
        }
    }
}
