using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Cesil.Tests
{
    public class PipeReaderAdapterTests
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

            // walk each async transition point
            var forceUpTo = 0;
            while (true)
            {
                var pipe = new Pipe();
                await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(data).AsMemory());
                pipe.Writer.Complete();

                var res = new StringBuilder();

                using(var mem = MemoryPool<char>.Shared.Rent())
                await using (var adapter = new PipeReaderAdapter(pipe.Reader, Encoding.UTF8))
                {
                    var provider = (ITestableAsyncProvider)adapter;
                    provider.GoAsyncAfter = forceUpTo;

                    var into = mem.Memory.Slice(0, 100);

                    var iter = 0;
                    while (true)
                    {
                        var count = await adapter.ReadAsync(into, default);

                        if (count == 0) break;

                        res.Append(new string(into.Slice(0, count).Span));
                        iter++;
                    }

                    var resStr = res.ToString();

                    Assert.Equal(data, resStr);

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

        private const int TIMEOUT_SECS = 60;

        private interface _NaughtyStrings_Do
        {
            ValueTask DoAsync();
        }

        private sealed class _NaughtyStrings_WriteStep : _NaughtyStrings_Do
        {
            public IDisposable MemoryOwner;
            public ReadOnlyMemory<byte> ToWrite;
            public PipeWriter Writer;

            public async ValueTask DoAsync()
            {
                var copyTo = Writer.GetMemory(ToWrite.Length);

                ToWrite.CopyTo(copyTo);
                Writer.Advance(ToWrite.Length);
                await Writer.FlushAsync();

                MemoryOwner?.Dispose();

                MemoryOwner = null;
                ToWrite = null;
                Writer = null;
            }
        }

        private sealed class _NaughtyStrings_FinishedWritingStep : _NaughtyStrings_Do
        {
            public PipeWriter Writer;

            public ValueTask DoAsync()
            {
                Writer.Complete();
                return default;
            }
        }

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
            var threads = new List<Thread>();
            var tasks = new List<Task>();

            foreach (var encoding in _NaughtStrings_AllEncodings)
            {
                var tcs = new TaskCompletionSource<object>();
                var subThread =
                    new Thread(
                        () =>
                        {
                            try
                            {
                                var task = TryAllStringsForEncoding(encoding);
                                task.Wait();
                                tcs.SetResult(null);
                            }
                            catch (Exception e)
                            {
                                tcs.SetException(e);
                            }
                        }
                    );

                subThread.Name = "Outer Thread (" + encoding.EncodingName + ")";

                threads.Add(subThread);
                tasks.Add(tcs.Task);
            }

            threads.ForEach(t => t.Start());

            Debug.WriteLine("Waiting for outer threads to stop");
            await Task.WhenAll(tasks);
            Debug.WriteLine("Outer threads stopped");

            static async Task TryAllStringsForEncoding(Encoding encoding)
            {
                var mostBytesPerChar = encoding.GetMaxByteCount(1);

                var writeThreadFinished = new TaskCompletionSource<object>();
                var readThreadFinished = new TaskCompletionSource<object>();

                var runWriteQueue = true;
                var writeQueue = new ConcurrentQueue<Func<Task>>();
                var writeThread =
                    new Thread(
                        () =>
                        {
                            while (runWriteQueue)
                            {
                                if (writeQueue.TryDequeue(out var taskMaker))
                                {
                                    try
                                    {
                                        var task = taskMaker();
                                        task.Wait();
                                    }
                                    catch (Exception e)
                                    {
                                        writeThreadFinished.SetException(e);
                                        return;
                                    }
                                }
                            }

                            writeThreadFinished.SetResult(null);
                        }
                    );
                writeThread.Name = "Write (" + encoding.EncodingName + ")";

                var runReadQueue = true;
                var readQueue = new ConcurrentQueue<Func<Task>>();
                var readThread =
                    new Thread(
                        () =>
                        {
                            while (runReadQueue)
                            {
                                if (readQueue.TryDequeue(out var taskMaker))
                                {
                                    try
                                    {
                                        var task = taskMaker();
                                        task.Wait();
                                    }
                                    catch (Exception e)
                                    {
                                        readThreadFinished.SetException(e);
                                        return;
                                    }
                                }
                            }

                            readThreadFinished.SetResult(null);
                        }
                    );
                readThread.Name = "Read (" + encoding.EncodingName + ")";

                writeThread.Start();
                readThread.Start();

                var threadId = Thread.CurrentThread.ManagedThreadId;

                var readyForWriteSem = new SemaphoreSlim(0, 1);
                var writeFinishedSem = new SemaphoreSlim(0, 1);

                List<_NaughtyStrings_Do> writeChunkTasks = null;

                for (var j = 0; j < _NaughtyStrings_TestStrings.Length; j++)
                {
                    var str = _NaughtyStrings_TestStrings[j];
                    var bytes = encoding.GetBytes(str);
                    var decodedStr = encoding.GetString(bytes);

                    var byteMem = bytes.AsMemory();

                    var maxStep = Math.Min(bytes.Length, mostBytesPerChar);
                    for (var step = 1; step <= maxStep; step++)
                    {
                        var offsetMax = Math.Min(bytes.Length, mostBytesPerChar);
                        Debug.WriteLine($"Thread #{threadId}: Starting {j} ({decodedStr}) for {encoding.EncodingName} at {step}");

                        var pipe = new Pipe();
                        var writer = pipe.Writer;

                        await using (var reader = new PipeReaderAdapter(pipe.Reader, encoding))
                        {
                            var dest = new StringBuilder();

                            PrepareWriteBytes(writer, byteMem, step, ref writeChunkTasks);

                            var writingFinishedTCS = new TaskCompletionSource<object>();
                            Action<Exception> writingFinished =
                                e =>
                                {
                                    if (e == null)
                                    {
                                        writingFinishedTCS.SetResult(null);
                                    }
                                    else
                                    {
                                        writingFinishedTCS.SetException(e);
                                    }
                                };

                            var readingFinishedTCS = new TaskCompletionSource<object>();
                            Action<Exception> readingFinished =
                                e =>
                                {
                                    if (e == null)
                                    {
                                        readingFinishedTCS.SetResult(null);
                                    }
                                    else
                                    {
                                        readingFinishedTCS.SetException(e);
                                    }
                                };

                            var writeTask = MakeWriteTask(writeChunkTasks, readyForWriteSem, writeFinishedSem, writingFinished);
                            var readTask = MakeReadTask(reader, dest, readyForWriteSem, writeFinishedSem, readingFinished);

                            writeQueue.Enqueue(writeTask);
                            readQueue.Enqueue(readTask);

                            await writingFinishedTCS.Task;
                            await readingFinishedTCS.Task;

                            Assert.Equal(0, readyForWriteSem.CurrentCount);
                            Assert.Equal(0, writeFinishedSem.CurrentCount);

                            var resultString = dest.ToString();

                            Assert.Equal(decodedStr, resultString);
                        }
                    }
                }

                runReadQueue = runWriteQueue = false;

                Debug.WriteLine("Waiting for read & write threads to stop");
                await Task.WhenAll(readThreadFinished.Task, writeThreadFinished.Task);
                Debug.WriteLine("Threads stopped");
            }

            // create a thread that will work through these tasks, signaling and waiting appropriately
            static Func<Task> MakeWriteTask(List<_NaughtyStrings_Do> tasks, SemaphoreSlim waitForRequest, SemaphoreSlim signalAfterResponse, Action<Exception> writesFinished)
            {
                return () => Do(tasks, waitForRequest, signalAfterResponse, writesFinished);

                // actually do it
                static async Task Do(List<_NaughtyStrings_Do> tasks, SemaphoreSlim waitForRequest, SemaphoreSlim signalAfterResponse, Action<Exception> writesFinished)
                {
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(TIMEOUT_SECS));

                    foreach (var task in tasks)
                    {
                        var waitForRequestTask = waitForRequest.WaitAsync();
                        var finished = await Task.WhenAny(waitForRequestTask, timeoutTask);

                        if (finished == timeoutTask)
                        {
                            var exc = new Exception("Write thread timed out waiting for request");
                            writesFinished(exc);
                            Assert.Null(exc.Message);
                        }

                        var doTask = task.DoAsync().AsTask();
                        finished = await Task.WhenAny(doTask, timeoutTask);
                        if (finished == timeoutTask)
                        {
                            var exc = new Exception("Write thread timed out waiting for Do to complete");
                            writesFinished(exc);
                            Assert.Null(exc.Message);
                        }

                        signalAfterResponse.Release();
                    }

                    writesFinished(null);
                }
            }

            static Func<Task> MakeReadTask(
                PipeReaderAdapter adapter,
                StringBuilder readInto,
                SemaphoreSlim requestWrite,
                SemaphoreSlim writeCompleted,
                Action<Exception> readsFinished
            )
            {
                return () => Do(adapter, readInto, requestWrite, writeCompleted, readsFinished);

                // actually do it
                static async Task Do(
                    PipeReaderAdapter adapter,
                    StringBuilder readInto,
                    SemaphoreSlim requestWrite,
                    SemaphoreSlim writeCompleted,
                    Action<Exception> readsFinished
                )
                {
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(TIMEOUT_SECS));

                    var buffer = new char[1024].AsMemory();

                    while (true)
                    {
                        var readTask = adapter.ReadAsync(buffer, default).AsTask();
                        while (!readTask.IsCompleted)
                        {
                            var finished = await Task.WhenAny(readTask, Task.Delay(5), timeoutTask);
                            if (finished == timeoutTask)
                            {
                                var exc = new Exception("Read thread timed out waiting for read");
                                readsFinished(exc);
                                Assert.Null(exc.Message);
                            }

                            var didFinish = finished == readTask;
                            if (didFinish)
                            {
                                break;
                            }

                            requestWrite.Release();
                            var waitForWriteTask = writeCompleted.WaitAsync();
                            finished = await Task.WhenAny(waitForWriteTask, timeoutTask);
                            if (finished == timeoutTask)
                            {
                                var exc = new Exception("Read thread timed out waiting for write");
                                readsFinished(exc);
                                Assert.Null(exc.Message);
                            }
                        }

                        var res = readTask.Result;
                        if (res == 0)
                        {
                            break;
                        }

                        var str = new string(buffer.Slice(0, res).Span);
                        readInto.Append(str);
                    }

                    readsFinished(null);
                }
            }

            // break everything up we can write in chunks
            static void ChunkUp(List<_NaughtyStrings_Do> into, PipeWriter writer, ReadOnlyMemory<byte> b, int step)
            {
                // loop until we've done it all
                while (b.Length > 0)
                {
                    var copyLen = Math.Min(step, b.Length);

                    var chunkMem = b.Slice(0, copyLen);

                    into.Add(
                        new _NaughtyStrings_WriteStep
                        {
                            Writer = writer,
                            ToWrite = chunkMem
                        }
                    );

                    b = b.Slice(copyLen);
                }
            }

            // returns a list of things that will write stuff in chunks to the writer, and then finish the writer
            static void PrepareWriteBytes(PipeWriter writer, ReadOnlyMemory<byte> b, int step, ref List<_NaughtyStrings_Do> list)
            {
                list = list ?? new List<_NaughtyStrings_Do>();
                list.Clear();

                ChunkUp(list, writer, b, step);

                list.Add(new _NaughtyStrings_FinishedWritingStep { Writer = writer });
            }
        }
    }
}
