using System.Linq;
using Xunit;

namespace Cesil.Tests
{
    public class NeedsEncodeHelper_AVX_Tests
    {
        [Theory]
        [InlineData("0123456789ABCDE", -1)]
        [InlineData("0123456789ABCDEF", -1)]
        [InlineData("0123456789ABCDEFG", -1)]

        [InlineData("012#*#6789ABCDE", 3)]
        [InlineData("0123456789ABC#*#", 13)]
        [InlineData("#*#123456789ABCDEFG", 0)]

        [InlineData("#123456789ABCDE", -1)]
        [InlineData("#*23456789ABCDEF", -1)]
        [InlineData("*#23456789ABCDEFG", -1)]

        [InlineData("0123456789ABCD#", -1)]
        [InlineData("0123456789ABCD#*", -1)]
        [InlineData("0123456789ABCDEF#", -1)]

        public unsafe void MultiCharacterValueSeparator(string txt, int expected)
        {
            var state = new NeedsEncodeHelper("#*#", '"', '#');

            fixed (char* charPtr = txt)
            {
                var res = state.ContainsCharRequiringEncoding(charPtr, txt.Length);

                Assert.Equal(expected, res);
            }
        }

        [Theory]
        [InlineData("0123456789ABCDEFa", -1)]
        [InlineData("0123456789ABCDEF,", 16)]
        [InlineData("0123456789ABCDEFab", -1)]
        [InlineData("0123456789ABCDEF,b", 16)]
        [InlineData("0123456789ABCDEFa,", 17)]
        [InlineData("0123456789ABCDEFabc", -1)]
        [InlineData("0123456789ABCDEFab,", 18)]
        public unsafe void AwkwardLengths(string txt, int expected)
        {
            var charsFor256Bits = 256 / (sizeof(char) * 8);

            Assert.NotEqual(0, txt.Length % charsFor256Bits);

            var state = new NeedsEncodeHelper(",", '"', '#');

            fixed (char* charPtr = txt)
            {
                var res = state.ContainsCharRequiringEncoding(charPtr, txt.Length);

                Assert.Equal(expected, res);
            }
        }

        [Theory]
        [InlineData("0123456789ABCDEF", -1)]
        [InlineData("\r123456789ABCDEF", 0)]
        [InlineData("0\n23456789ABCDEF", 1)]
        [InlineData("01,3456789ABCDEF", 2)]
        [InlineData("012\"456789ABCDEF", 3)]
        [InlineData("0123#56789ABCDEF", 4)]
        [InlineData("0123456789ABCDEF0123456789abcdef", -1)]
        [InlineData("0123456789ABCDEF0123456789abcd\rf", 30)]
        [InlineData("0123456789ABCDEF0123456789abcde\n", 31)]
        public unsafe void Exactly256Bits(string txt, int expected)
        {
            var charsFor256Bits = 256 / (sizeof(char) * 8);

            Assert.Equal(0, txt.Length % charsFor256Bits);

            var state = new NeedsEncodeHelper(",", '"', '#');

            fixed (char* charPtr = txt)
            {
                var res = state.ContainsCharRequiringEncoding(charPtr, txt.Length);

                Assert.Equal(expected, res);
            }
        }

        [Fact]
        public unsafe void LessThan256Bits()
        {
            var state = new NeedsEncodeHelper(",", '"', '#');

            for (var len = 0; len < 16; len++)
            {
                var str = string.Join("", Enumerable.Range(0, len).Select(i => (char)('A' + i)));

                fixed (char* charPtr = str)
                {
                    var res = state.ContainsCharRequiringEncoding(charPtr, str.Length);

                    Assert.Equal(-1, res);
                }

                for (var j = 0; j < len; j++)
                {
                    var newStr1 = str.Substring(0, j) + "\r" + str.Substring(j + 1);
                    var newStr2 = str.Substring(0, j) + "\n" + str.Substring(j + 1);
                    var newStr3 = str.Substring(0, j) + "," + str.Substring(j + 1);
                    var newStr4 = str.Substring(0, j) + "\"" + str.Substring(j + 1);
                    var newStr5 = str.Substring(0, j) + "#" + str.Substring(j + 1);

                    var arr = new[] { newStr1, newStr2, newStr3, newStr4, newStr5 };
                    foreach (var s in arr)
                    {
                        fixed (char* charPtr = s)
                        {
                            var res = state.ContainsCharRequiringEncoding(charPtr, s.Length);

                            Assert.Equal(j, res);
                        }
                    }
                }
            }
        }

        [Theory]
        [InlineData("hello", -1)]
        [InlineData("world", -1)]
        public unsafe void Simple(string txt, int expected)
        {
            var s1 = new NeedsEncodeHelper(",", '"', '#');
            var s2 = new NeedsEncodeHelper(",", '"', null);
            var s3 = new NeedsEncodeHelper(",", null, null);

            fixed (char* charPtr = txt)
            {
                var res1 = s1.ContainsCharRequiringEncoding(charPtr, txt.Length);
                var res2 = s2.ContainsCharRequiringEncoding(charPtr, txt.Length);
                var res3 = s3.ContainsCharRequiringEncoding(charPtr, txt.Length);

                Assert.Equal(expected, res1);
                Assert.Equal(expected, res2);
                Assert.Equal(expected, res3);
            }
        }
    }
}
