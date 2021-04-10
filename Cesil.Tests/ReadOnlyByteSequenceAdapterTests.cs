using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Cesil.Tests
{
    public class ReadOnlyByteSequenceAdapterTests
    {
        private static readonly string[] _NaughtyStrings_TestStrings =
            new[]
                {
                    @" ",
                    @"",
                    @"",
                    @"",
                    @"­؀؁؂؃؄؅؜۝܏᠎​‌‍‎‏‪‫‬‭‮⁠⁡⁢⁣⁤⁦⁧⁨⁩⁪⁫⁬⁭⁮⁯﻿￹￺￻𑂽𛲠𛲡𛲢𛲣𝅳𝅴𝅵𝅶𝅷𝅸𝅹𝅺󠀁󠀠󠀡󠀢󠀣󠀤󠀥󠀦󠀧󠀨󠀩󠀪󠀫󠀬󠀭󠀮󠀯󠀰󠀱󠀲󠀳󠀴󠀵󠀶󠀷󠀸󠀹󠀺󠀻󠀼󠀽󠀾󠀿󠁀󠁁󠁂󠁃󠁄󠁅󠁆󠁇󠁈󠁉󠁊󠁋󠁌󠁍󠁎󠁏󠁐󠁑󠁒󠁓󠁔󠁕󠁖󠁗󠁘󠁙󠁚󠁛󠁜󠁝󠁞󠁟󠁠󠁡󠁢󠁣󠁤󠁥󠁦󠁧󠁨󠁩󠁪󠁫󠁬󠁭󠁮󠁯󠁰󠁱󠁲󠁳󠁴󠁵󠁶󠁷󠁸󠁹󠁺󠁻󠁼󠁽󠁾󠁿",
                    @"ЁЂЃЄЅІЇЈЉЊЋЌЍЎЏАБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдежзийклмнопрстуфхцчшщъыьэюя",
                    @"ด้้้้้็็็็็้้้้้็็็็็้้้้้้้้็็็็็้้้้้็็็็็้้้้้้้้็็็็็้้้้้็็็็็้้้้้้้้็็็็็้้้้้็็็็ ด้้้้้็็็็็้้้้้็็็็็้้้้้้้้็็็็็้้้้้็็็็็้้้้้้้้็็็็็้้้้้็็็็็้้้้้้้้็็็็็้้้้้็็็็ ด้้้้้็็็็็้้้้้็็็็็้้้้้้้้็็็็็้้้้้็็็็็้้้้้้้้็็็็็้้้้้็็็็็้้้้้้้้็็็็็้้้้้็็็็",
                    @"田中さんにあげて下さい",
                    @"パーティーへ行かないか",
                    @"和製漢語",
                    @"사회과학원 어학연구소",
                    @"울란바토르",
                    @"𠜎𠜱𠝹𠱓𠱸𠲖𠳏",
                    @"表ポあA鷗ŒéＢ逍Üßªąñ丂㐀𠀀",
                    @"Ⱥ",
                    @"Ⱦ",
                    @"ヽ༼ຈل͜ຈ༽ﾉ ヽ༼ຈل͜ຈ༽ﾉ",
                    @"😍",
                    @"✋🏿 💪🏿 👐🏿 🙌🏿 👏🏿 🙏🏿",
                    @"🚾 🆒 🆓 🆕 🆖 🆗 🆙 🏧",
                    @"0️⃣ 1️⃣ 2️⃣ 3️⃣ 4️⃣ 5️⃣ 6️⃣ 7️⃣ 8️⃣ 9️⃣ 🔟",
                    @"🇺🇸🇷🇺🇸 🇦🇫🇦🇲🇸",
                    @"בְּרֵאשִׁית, בָּרָא אֱלֹהִים, אֵת הַשָּׁמַיִם, וְאֵת הָאָרֶץ",
                    @"הָיְתָהtestالصفحات التّحول",
                    @"﷽",
                    @"ﷺ",
                    @"مُنَاقَشَةُ سُبُلِ اِسْتِخْدَامِ اللُّغَةِ فِي النُّظُمِ الْقَائِمَةِ وَفِيم يَخُصَّ التَّطْبِيقَاتُ الْحاسُوبِيَّةُ، ",
                    @"˙ɐnbᴉlɐ ɐuƃɐɯ ǝɹolop ʇǝ ǝɹoqɐl ʇn ʇunpᴉpᴉɔuᴉ ɹodɯǝʇ poɯsnᴉǝ op pǝs 'ʇᴉlǝ ƃuᴉɔsᴉdᴉpɐ ɹnʇǝʇɔǝsuoɔ 'ʇǝɯɐ ʇᴉs ɹolop ɯnsdᴉ ɯǝɹo˥",
                    @"00˙Ɩ$-",
                    @"𝚃𝚑𝚎 𝚚𝚞𝚒𝚌𝚔 𝚋𝚛𝚘𝚠𝚗 𝚏𝚘𝚡 𝚓𝚞𝚖𝚙𝚜 𝚘𝚟𝚎𝚛 𝚝𝚑𝚎 𝚕𝚊𝚣𝚢 𝚍𝚘𝚐",
                    @"⒯⒣⒠ ⒬⒰⒤⒞⒦ ⒝⒭⒪⒲⒩ ⒡⒪⒳ ⒥⒰⒨⒫⒮ ⒪⒱⒠⒭ ⒯⒣⒠ ⒧⒜⒵⒴ ⒟⒪⒢"
                };

        private static readonly Encoding[] _NaughtStrings_AllEncodings =
            Encoding
                .GetEncodings()
                .Select(e => e.GetEncoding())
                .OrderBy(
                    e =>
                    {
                        var en = e.EncodingName;
                        if (en == Encoding.ASCII.EncodingName) return -1;
                        if (en == Encoding.BigEndianUnicode.EncodingName) return -1;
                        if (en == Encoding.Unicode.EncodingName) return -1;
                        if (en == Encoding.UTF32.EncodingName) return -1;
#pragma warning disable SYSLIB0001 // UTF7 is garbage, but keep the test
                        if (en == Encoding.UTF7.EncodingName) return -1;
#pragma warning restore SYSLIB0001
                        if (en == Encoding.UTF8.EncodingName) return -1;

                        return 0;
                    }
                )
                .ThenBy(e => e.EncodingName)
                .ToArray();

        private class NaughtyStrings_ByteNode : ReadOnlySequenceSegment<byte>
        {
            public NaughtyStrings_ByteNode(ReadOnlyMemory<byte> m)
            {
                this.Memory = m;
                this.RunningIndex = 0;
            }

            public NaughtyStrings_ByteNode Append(ReadOnlyMemory<byte> m)
            {
                var ret = new NaughtyStrings_ByteNode(m);
                ret.RunningIndex = this.Memory.Length;

                this.Next = ret;

                return ret;
            }
        }

        [Fact]
        public void NaughtyStrings()
        {
            foreach (var str in _NaughtyStrings_TestStrings)
            {
                foreach (var enc in _NaughtStrings_AllEncodings)
                {
                    var bytes = enc.GetBytes(str);

                    foreach (var seq in MakeSequences(bytes))
                    {
                        foreach (var bufferSize in new[] { 8, 9, 10 })
                        {
                            using (var memOwner = MemoryPool<char>.Shared.Rent(bufferSize))
                            {
                                var mem = memOwner.Memory.Slice(0, bufferSize);

                                using (var reader = new ReadOnlyByteSequenceAdapter(seq, enc))
                                {
                                    var readChars = new List<char>();

                                    int read;
                                    while ((read = reader.Read(mem.Span)) != 0)
                                    {
                                        readChars.AddRange(mem.Span.Slice(0, read).ToArray());
                                    }

                                    var actual = new string(readChars.ToArray());
                                    var actualBytes = enc.GetBytes(actual);

                                    Assert.True(bytes.SequenceEqual(actualBytes));
                                }
                            }
                        }
                    }
                }
            }

            static IEnumerable<ReadOnlySequence<byte>> MakeSequences(byte[] buffer)
            {
                if (buffer.Length == 0)
                {
                    return new[] { ReadOnlySequence<byte>.Empty };
                }

                if (buffer.Length < 2)
                {
                    return new[] { new ReadOnlySequence<byte>(buffer) };
                }

                var split = buffer.Length / 2;

                var left = buffer.Take(split).ToArray();
                var right = buffer.Skip(split).ToArray();

                var ret = new List<ReadOnlySequence<byte>>();
                ret.Add(new ReadOnlySequence<byte>(buffer));

                var start = new NaughtyStrings_ByteNode(left.AsMemory());
                var end = start.Append(right.AsMemory());

                ret.Add(new ReadOnlySequence<byte>(start, 0, end, end.Memory.Length));

                return ret;
            }
        }
    }
}
