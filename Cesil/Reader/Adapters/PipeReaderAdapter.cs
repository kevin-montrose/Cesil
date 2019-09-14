using System;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed partial class PipeReaderAdapter : IAsyncReaderAdapter
    {
        public bool IsDisposed => Inner == null;

        private bool IsComplete;
        private PipeReader Inner;
        private readonly Decoder Decoder;

        public PipeReaderAdapter(PipeReader reader, Encoding encoding)
        {
            Inner = reader;
            Decoder = encoding.GetDecoder();
        }

        public ValueTask<int> ReadAsync(Memory<char> into, CancellationToken cancel)
        {
            AssertNotDisposed(this);

            if (IsComplete)
            {
                return new ValueTask<int>(0);
            }

            tryAgain:

            var readTask = Inner.ReadAsync(cancel);
            if (!readTask.IsCompletedSuccessfully(this))
            {
                return ReadAsync_ContinueAfterReadAsync(this, readTask, into, cancel);
            }

            var res = readTask.Result;
            var handled = MapByteSequenceToChar(res, into);

            // we need to wait for more to happen, returning 0 will
            //    incorrectly signal that the writer has finished
            if(handled == 0 && !IsComplete)
            {
                goto tryAgain;
            }

            return new ValueTask<int>(handled);

            // continue after a ReadAsync call completes
            static async ValueTask<int> ReadAsync_ContinueAfterReadAsync(PipeReaderAdapter self, ValueTask<ReadResult> waitFor, Memory<char> into, CancellationToken cancel)
            {
                tryAgainAsync:
                var res = await waitFor;

                var handled = self.MapByteSequenceToChar(res, into);

                // we need to wait for more to happen, returning 0 will
                //    incorrectly signal that the writer has finished
                if (handled == 0 && !self.IsComplete)
                {
                    waitFor = self.Inner.ReadAsync(cancel);
                    goto tryAgainAsync;
                }

                return handled;
            }
        }

        private int MapByteSequenceToChar(ReadResult res, Memory<char> into)
        {
            var handledAny = false;

            var copyTo = into.Span;
            var handledAll = true;

            var handledBytes = 0;
            var readChars = 0;

            foreach (var seq in res.Buffer)
            {
                if (copyTo.IsEmpty)
                {
                    handledAll = false;
                    break;
                }

                var seqBytes = seq.Span;
                Decoder.Convert(seqBytes, copyTo, false, out var bytesUsed, out var charsUsed, out _);

                if(bytesUsed > 0)
                {
                    handledAny = true;
                }

                handledBytes += bytesUsed;
                readChars += charsUsed;

                copyTo = copyTo.Slice(charsUsed);

                if (bytesUsed != seqBytes.Length)
                {
                    handledAll = false;
                    break;
                }
            }

            var processed = res.Buffer.GetPosition(handledBytes);

            // if we read the whole buffer, but couldn't extract a single char then we need to wait for more data to come in,
            //    so mark the whole buffer as examined
            // otherwise, we just ran out of space and want the next call to immediately return the old data
            var examined = handledAny ? processed : res.Buffer.End;

            Inner.AdvanceTo(processed, examined);

            if (handledAll)
            {
                // record if we're done for the next caller
                IsComplete = res.IsCompleted || res.IsCanceled;
            }

            return readChars;
        }

        public ValueTask DisposeAsync()
        {
            Inner = null;
            return default;
        }
    }

#if DEBUG
    internal sealed partial class PipeReaderAdapter : ITestableAsyncProvider
    {
        private int _GoAsyncAfter;
        int ITestableAsyncProvider.GoAsyncAfter { set { _GoAsyncAfter = value; } }

        private int _AsyncCounter;
        int ITestableAsyncProvider.AsyncCounter => _AsyncCounter;

        bool ITestableAsyncProvider.ShouldGoAsync()
        {
            lock (this)
            {
                _AsyncCounter++;

                var ret = _AsyncCounter >= _GoAsyncAfter;

                return ret;
            }
        }
    }
#endif
}
