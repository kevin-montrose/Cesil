using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Cesil.Tests
{
    public class PipeWriterAdapterTests
    {
#if DEBUG
        [Fact]
        public async Task TransitionsAsync()
        {
            var data =
                string.Join(
                    ", ",
                    Enumerable.Repeat("hello world", 1_000)
                );
            var dataBytes = Encoding.UTF8.GetBytes(data);

            // walk each async transition point
            var forceUpTo = 0;
            while (true)
            {
                var pipe = new Pipe();

                await using (var adapter = new PipeWriterAdapter(pipe.Writer, Encoding.UTF8, MemoryPool<char>.Shared))
                {
                    var provider = (ITestableAsyncProvider)adapter;
                    provider.GoAsyncAfter = forceUpTo;

                    var dataMem = data.AsMemory();

                    for (var i = 0; i < data.Length; i += 100)
                    {
                        var segment = dataMem.Slice(i);
                        if (segment.Length > 100)
                        {
                            segment = segment.Slice(0, 100);
                        }

                        await adapter.WriteAsync(segment, default);
                    }

                    pipe.Writer.Complete();

                    var readBytes = new List<byte>();
                    while (true)
                    {
                        var res = await pipe.Reader.ReadAsync();

                        foreach (var seg in res.Buffer)
                        {
                            readBytes.AddRange(seg.ToArray());
                        }

                        if (res.IsCompleted)
                        {
                            break;
                        }
                    }

                    Assert.True(dataBytes.SequenceEqual(readBytes));

                    if (provider.AsyncCounter >= forceUpTo)
                    {
                        forceUpTo++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
#endif

        private static readonly string[] _NaughtStringsAsync_Strings =
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

        private static readonly Encoding[] _NaughtStringsAsync_AllEncodings =
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
                        if (en == Encoding.UTF7.EncodingName) return -1;
                        if (en == Encoding.UTF8.EncodingName) return -1;

                        return 0;
                    }
                )
                .ThenBy(e => e.EncodingName)
                .ToArray();

        [Fact]
        public async Task NaughtyStringsAsync()
        {
            var failures = new StringBuilder();

            for (var i = 0; i < _NaughtStringsAsync_AllEncodings.Length; i++)
            {
                var encoding = _NaughtStringsAsync_AllEncodings[i];

                for (var j = 0; j < _NaughtStringsAsync_Strings.Length; j++)
                {
                    var str = _NaughtStringsAsync_Strings[j];
                    var shouldMatch = encoding.GetBytes(str);

                    Debug.WriteLine($"Starting ({shouldMatch.Length}) ({i}, {j}) with ({encoding.EncodingName}): {str}");

                    var pipe = new Pipe();
                    var writer = pipe.Writer;
                    var reader = pipe.Reader;

                    await using (var adapter = new PipeWriterAdapter(writer, encoding, MemoryPool<char>.Shared))
                    {
                        await adapter.WriteAsync(str.AsMemory(), default);

                        writer.Complete();
                        var writtenBytes = await ReadAsync(reader);

                        var success = shouldMatch.SequenceEqual(writtenBytes);
                        if (success) continue;

                        failures.AppendLine($"Starting ({shouldMatch.Length}) ({i}, {j}) with ({encoding.EncodingName}): {str}");
                    }
                }
            }

            var allFails = failures.ToString();
            Assert.Equal("", allFails);

            static async ValueTask<byte[]> ReadAsync(PipeReader read)
            {
                var ret = new List<byte>();

                while (true)
                {
                    var res = await read.ReadAsync();
                    foreach (var seq in res.Buffer)
                    {
                        ret.AddRange(seq.ToArray());
                    }

                    if (res.IsCompleted) break;
                }

                return ret.ToArray();
            }
        }
    }
}
