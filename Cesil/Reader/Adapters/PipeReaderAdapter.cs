using System;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Cesil.AwaitHelper;
using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed partial class PipeReaderAdapter : ByteSequenceAdapterBase, IAsyncReaderAdapter
    {
        public bool IsDisposed { get; private set; }

        private readonly PipeReader Inner;

        public PipeReaderAdapter(PipeReader reader, Encoding encoding) : base(encoding)
        {
            Inner = reader;
        }

        public ValueTask<int> ReadAsync(Memory<char> into, CancellationToken cancel)
        {
            AssertNotDisposedInternal(this);

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
            if (handled == 0 && !IsComplete)
            {
                goto tryAgain;
            }

            return new ValueTask<int>(handled);

            // continue after a ReadAsync call completes
            static async ValueTask<int> ReadAsync_ContinueAfterReadAsync(PipeReaderAdapter self, ValueTask<ReadResult> waitFor, Memory<char> into, CancellationToken cancel)
            {
tryAgainAsync:
                var res = await ConfigureCancellableAwait(self, waitFor, cancel);

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
            MapByteSequenceToChar(res.Buffer, res.IsCompleted || res.IsCanceled, into.Span, out var processed, out var examined, out var chars);

            var processedSeqPos = res.Buffer.GetPosition(processed);
            var examinedSeqPos = res.Buffer.GetPosition(examined);

            Inner.AdvanceTo(processedSeqPos, examinedSeqPos);

            return chars;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return default;
        }
    }

#if DEBUG
    // only available in DEBUG for testing purposes
    internal sealed partial class PipeReaderAdapter : ITestableCancellableProvider
    {
        int? ITestableCancellableProvider.CancelAfter { get; set; }
        int ITestableCancellableProvider.CancelCounter { get; set; }
    }

    // only available in DEBUG for testing purposes
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
