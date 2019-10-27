using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed partial class RowEndingDetector<T> : ITestableDisposable
    {
        private enum AdvanceResult : byte
        {
            None = 0,

            Continue,
            Continue_PushBackOne,

            Finished,

            Exception_UnexpectedState
        }

        private readonly IAsyncReaderAdapter? _InnerAsync;
        private IAsyncReaderAdapter InnerAsync => Utils.NonNull(_InnerAsync);
        
        private readonly IReaderAdapter? _Inner;
        private IReaderAdapter Inner => Utils.NonNull(_Inner);

        private RowEndings Ending;

        private readonly ReaderStateMachine State;

        public bool IsDisposed { get; private set; }
        private readonly MemoryPool<char> MemoryPool;
        private readonly int BufferSizeHint;

        private int BufferStart;
        private readonly IMemoryOwner<char> BufferOwner;

        private int PushbackLength;
        private bool HasPushbackOwner => _PushbackOwner != null;
        private IMemoryOwner<char>? _PushbackOwner;
        private IMemoryOwner<char> PushbackOwner
        {
            get => Utils.NonNull(_PushbackOwner);
            set
            {
                _PushbackOwner = value;
            }
        }

        private Memory<char> Pushback => PushbackOwner.Memory;

        internal RowEndingDetector(ReaderStateMachine stateMachine, BoundConfigurationBase<T> config, CharacterLookup charLookup, IReaderAdapter inner)
            : this(stateMachine, config, charLookup, inner, null) { }

        internal RowEndingDetector(ReaderStateMachine stateMachine, BoundConfigurationBase<T> config, CharacterLookup charLookup, IAsyncReaderAdapter innerAsync)
            : this(stateMachine, config, charLookup, null, innerAsync) { }

        private RowEndingDetector(ReaderStateMachine stateMachine, BoundConfigurationBase<T> config, CharacterLookup charLookup, IReaderAdapter? inner, IAsyncReaderAdapter? innerAsync)
        {
            _Inner = inner;
            _InnerAsync = innerAsync;

            State = stateMachine;
            stateMachine.Initialize(
                charLookup,
                config.EscapedValueStartAndStop,
                config.EscapeValueEscapeChar,
                default,
                ReadHeaders.Never,
                false
            );

            MemoryPool = config.MemoryPool;

            BufferSizeHint = config.ReadBufferSizeHint;
            if (BufferSizeHint == 0)
            {
                BufferSizeHint = ReaderBase<T>.DEFAULT_BUFFER_SIZE;
            }

            BufferOwner = MemoryPool.Rent(BufferSizeHint);
            BufferStart = 0;
        }

        internal ValueTask<(RowEndings Ending, Memory<char> PushBack)?> DetectAsync(CancellationToken cancel)
        {
            var handle = State.Pin();
            var disposeHandle = true;

            try
            {
                var continueScan = true;
                while (continueScan)
                {
                    var mem = BufferOwner.Memory.Slice(BufferStart, BufferOwner.Memory.Length - BufferStart);
                    var endTask = InnerAsync.ReadAsync(mem, cancel);

                    if (!endTask.IsCompletedSuccessfully(this))
                    {
                        disposeHandle = false;
                        return DetectAsync_ContinueAfterReadAsync(this, endTask, handle, cancel);
                    }

                    var end = endTask.Result;
                    var buffSpan = BufferOwner.Memory.Span;

                    if (end == 0)
                    {
                        if (BufferStart > 0)
                        {
                            switch (buffSpan[0])
                            {
                                case '\r': Ending = RowEndings.CarriageReturn; break;
                                case '\n': Ending = RowEndings.LineFeed; break;
                            }
                        }
                        break;
                    }
                    else
                    {
                        AddToPushback(buffSpan.Slice(BufferStart, end));

                        var len = end + BufferStart;
                        var res = Advance(buffSpan.Slice(0, len));
                        switch (res)
                        {
                            case AdvanceResult.Continue:
                                BufferStart = 0;
                                continue;
                            case AdvanceResult.Finished:
                                continueScan = false;
                                continue;
                            case AdvanceResult.Continue_PushBackOne:
                                buffSpan[0] = buffSpan[len - 1];
                                BufferStart = 1;
                                continue;
                            default:
                                return new ValueTask<(RowEndings Ending, Memory<char> PushBack)?>(default((RowEndings Ending, Memory<char> PushBack)?));
                        }
                    }
                }

                // this implies we're only gonna read a row... so whatever
                if (Ending == 0)
                {
                    Ending = RowEndings.CarriageReturnLineFeed;
                }

                return new ValueTask<(RowEndings Ending, Memory<char> PushBack)?>((Ending, Pushback.Slice(0, PushbackLength)));

            }
            finally
            {
                if (disposeHandle)
                {
                    handle.Dispose();
                }
            }

            static async ValueTask<(RowEndings Ending, Memory<char> PushBack)?> DetectAsync_ContinueAfterReadAsync(
                RowEndingDetector<T> self, 
                ValueTask<int> waitFor, 
                ReaderStateMachine.PinHandle handle,
                CancellationToken cancel
            )
            {
                using (handle)
                {
                    int end;
                    using (self.State.ReleaseAndRePinForAsync(waitFor))
                    {
                        end = await waitFor;
                    }

                    // handle the results that were in flight
                    var continueScan = true;

                    var buffMem = self.BufferOwner.Memory;

                    if (end == 0)
                    {
                        if (self.BufferStart > 0)
                        {
                            switch (buffMem.Span[0])
                            {
                                case '\r': self.Ending = RowEndings.CarriageReturn; break;
                                case '\n': self.Ending = RowEndings.LineFeed; break;
                            }
                        }
                        goto end;
                    }
                    else
                    {
                        self.AddToPushback(buffMem.Slice(self.BufferStart, end));

                        var len = end + self.BufferStart;
                        var res = self.Advance(buffMem.Span.Slice(0, len));
                        switch (res)
                        {
                            case AdvanceResult.Continue:
                                self.BufferStart = 0;
                                goto loopStart;
                            case AdvanceResult.Finished:
                                continueScan = false;
                                goto loopStart;
                            case AdvanceResult.Continue_PushBackOne:
                                buffMem.Span[0] = buffMem.Span[len - 1];
                                self.BufferStart = 1;
                                goto loopStart;
                            default:
                                return default;
                        }
                    }


// resume the loop
loopStart:
                    while (continueScan)
                    {
                        var mem = self.BufferOwner.Memory.Slice(self.BufferStart, self.BufferOwner.Memory.Length - self.BufferStart);

                        var readTask = self.InnerAsync.ReadAsync(mem, cancel);
                        using (self.State.ReleaseAndRePinForAsync(readTask))
                        {
                            end = await readTask;
                        }

                        if (end == 0)
                        {
                            buffMem = self.BufferOwner.Memory;

                            if (self.BufferStart > 0)
                            {
                                switch (buffMem.Span[0])
                                {
                                    case '\r': self.Ending = RowEndings.CarriageReturn; break;
                                    case '\n': self.Ending = RowEndings.LineFeed; break;
                                }
                            }
                            break;
                        }
                        else
                        {
                            self.AddToPushback(buffMem.Slice(self.BufferStart, end));

                            var len = end + self.BufferStart;
                            var res = self.Advance(buffMem.Span.Slice(0, len));
                            switch (res)
                            {
                                case AdvanceResult.Continue:
                                    self.BufferStart = 0;
                                    continue;
                                case AdvanceResult.Finished:
                                    continueScan = false;
                                    continue;
                                case AdvanceResult.Continue_PushBackOne:
                                    buffMem.Span[0] = buffMem.Span[len - 1];
                                    self.BufferStart = 1;
                                    continue;
                                default:
                                    return default;
                            }
                        }
                    }

end:
                    if (self.Ending == 0)
                    {
                        self.Ending = RowEndings.CarriageReturnLineFeed;
                    }

                    return (self.Ending, self.Pushback.Slice(0, self.PushbackLength));
                }
            }
        }

        internal (RowEndings Ending, Memory<char> PushBack)? Detect()
        {
            using (State.Pin())
            {

                var buffSpan = BufferOwner.Memory.Span;

                var continueScan = true;
                while (continueScan)
                {
                    var end = Inner.Read(buffSpan.Slice(BufferStart, buffSpan.Length - BufferStart));
                    if (end == 0)
                    {
                        if (BufferStart > 0)
                        {
                            switch (buffSpan[0])
                            {
                                case '\r': Ending = RowEndings.CarriageReturn; break;
                                case '\n': Ending = RowEndings.LineFeed; break;
                            }
                        }
                        break;
                    }
                    else
                    {
                        AddToPushback(buffSpan.Slice(BufferStart, end));

                        var len = end + BufferStart;
                        var res = Advance(buffSpan.Slice(0, len));
                        switch (res)
                        {
                            case AdvanceResult.Continue:
                                BufferStart = 0;
                                continue;
                            case AdvanceResult.Finished:
                                continueScan = false;
                                continue;
                            case AdvanceResult.Continue_PushBackOne:
                                buffSpan[0] = buffSpan[len - 1];
                                BufferStart = 1;
                                continue;
                            default:
                                return null;
                        }
                    }
                }

                // this implies we're only gonna read a row... so whatever
                if (Ending == 0)
                {
                    Ending = RowEndings.CarriageReturnLineFeed;
                }
            }

            return (Ending, Pushback.Slice(0, PushbackLength));
        }

        private void AddToPushback(ReadOnlyMemory<char> mem)
        => AddToPushback(mem.Span);

        private void AddToPushback(ReadOnlySpan<char> span)
        {
            if (!HasPushbackOwner)
            {
                PushbackOwner = MemoryPool.Rent(BufferSizeHint);
            }

            if (PushbackLength + span.Length > PushbackOwner.Memory.Length)
            {
                var oldSize = PushbackOwner.Memory.Length;
                var newSize = PushbackLength + span.Length;
                var newOwner = Utils.RentMustIncrease(MemoryPool, newSize, oldSize);

                Pushback.CopyTo(newOwner.Memory);
                PushbackOwner.Dispose();
                PushbackOwner = newOwner;
            }

            span.CopyTo(Pushback.Span.Slice(PushbackLength));
            PushbackLength += span.Length;
        }

        private AdvanceResult Advance(ReadOnlySpan<char> buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var cc = buffer[i];

                var curState = State.CurrentState;

                var legalToEndRecord = (((byte)curState) & ReaderStateMachine.CAN_END_RECORD_MASK) == ReaderStateMachine.CAN_END_RECORD_MASK;

                if (legalToEndRecord)
                {
                    if (cc == '\n')
                    {
                        Ending = RowEndings.LineFeed;
                        return AdvanceResult.Finished;
                    }

                    if (cc == '\r')
                    {
                        // we need the next character, so try
                        if (i + 1 == buffer.Length)
                        {
                            return AdvanceResult.Continue_PushBackOne;
                        }

                        var nextCC = buffer[i + 1];

                        if (nextCC == '\n')
                        {
                            Ending = RowEndings.CarriageReturnLineFeed;
                        }
                        else
                        {
                            Ending = RowEndings.CarriageReturn;
                        }

                        return AdvanceResult.Finished;
                    }
                }

                var res = State.Advance(cc);
                switch (res)
                {
                    case ReaderStateMachine.AdvanceResult.Append_Character:
                    case ReaderStateMachine.AdvanceResult.Append_CarriageReturnAndCurrentCharacter:
                    case ReaderStateMachine.AdvanceResult.Finished_Value:
                    case ReaderStateMachine.AdvanceResult.Skip_Character:
                        break;

                    default:
                        return AdvanceResult.Exception_UnexpectedState;
                }
            }

            return AdvanceResult.Continue;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                // Intentionally NOT disposing State, it's reused
                BufferOwner.Dispose();
                if (HasPushbackOwner)
                {
                    PushbackOwner.Dispose();
                }

                IsDisposed = true;
            }
        }
    }

#if DEBUG
    // this is only implemented in DEBUG builds, so tests (and only tests) can force
    //    particular async paths
    internal sealed partial class RowEndingDetector<T> : ITestableAsyncProvider
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
