using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.AwaitHelper;

namespace Cesil
{
    internal sealed partial class RowEndingDetector : ITestableDisposable
    {
        private enum AdvanceResult : byte
        {
            None = 0,

            Continue,
            Continue_PushBackOne,

            Finished,

            Exception_UnexpectedState
        }

        private NonNull<IAsyncReaderAdapter> InnerAsync;

        private NonNull<IReaderAdapter> Inner;

        private ReadRowEnding Ending;

        private readonly ReaderStateMachine State;

        public bool IsDisposed { get; private set; }
        private readonly MemoryPool<char> MemoryPool;
        private readonly int BufferSizeHint;

        // BufferStart is only ever set to 0 or 1, keep it an int makes some other logic easier
        //   but if you set it to 2 or something all hell will break loose
        private int BufferStart;

        private readonly IMemoryOwner<char> BufferOwner;

        private int PushbackLength;
        private NonNull<IMemoryOwner<char>> PushbackOwner;

        private Memory<char> Pushback => PushbackOwner.Value.Memory;

        private readonly ReadOnlyMemory<char> ValueSeparatorMemory;

        internal RowEndingDetector(ReaderStateMachine stateMachine, Options options, MemoryPool<char> memPool, CharacterLookup charLookup, IReaderAdapter inner, ReadOnlyMemory<char> valueSeparatorMemory)
            : this(stateMachine, options, memPool, charLookup, inner, null, valueSeparatorMemory) { }

        internal RowEndingDetector(ReaderStateMachine stateMachine, Options options, MemoryPool<char> memPool, CharacterLookup charLookup, IAsyncReaderAdapter innerAsync, ReadOnlyMemory<char> valueSeparatorMemory)
            : this(stateMachine, options, memPool, charLookup, null, innerAsync, valueSeparatorMemory) { }

        private RowEndingDetector(ReaderStateMachine stateMachine, Options options, MemoryPool<char> memPool, CharacterLookup charLookup, IReaderAdapter? inner, IAsyncReaderAdapter? innerAsync, ReadOnlyMemory<char> valueSeparatorMemory)
        {
            Inner.SetAllowNull(inner);
            InnerAsync.SetAllowNull(innerAsync);

            State = stateMachine;
            stateMachine.Initialize(
                charLookup,
                options.EscapedValueStartAndEnd,
                options.EscapedValueEscapeCharacter,
                default,
                ReadHeader.Never,
                false,
                options.WhitespaceTreatment.HasFlag(WhitespaceTreatments.TrimBeforeValues),
                options.WhitespaceTreatment.HasFlag(WhitespaceTreatments.TrimAfterValues)
            );

            MemoryPool = memPool;

            BufferSizeHint = options.ReadBufferSizeHint;
            if (BufferSizeHint == 0)
            {
                BufferSizeHint = Utils.DEFAULT_BUFFER_SIZE;
            }

            BufferOwner = MemoryPool.Rent(BufferSizeHint);
            BufferStart = 0;

            ValueSeparatorMemory = valueSeparatorMemory;
        }

        internal ValueTask<(ReadRowEnding Ending, Memory<char> PushBack)?> DetectAsync(CancellationToken cancellationToken)
        {
            var handle = State.Pin();
            var disposeHandle = true;

            try
            {
                var continueScan = true;
                while (continueScan)
                {
                    var mem = BufferOwner.Memory[BufferStart..];
                    var endTask = InnerAsync.Value.ReadAsync(mem, cancellationToken);

                    if (!endTask.IsCompletedSuccessfully(this))
                    {
                        disposeHandle = false;
                        return DetectAsync_ContinueAfterReadAsync(this, endTask, handle, cancellationToken);
                    }

                    var end = endTask.Result;
                    var buffSpan = BufferOwner.Memory.Span;

                    if (end == 0)
                    {
                        // only need to check for '\r', because we'll never leave a '\n' pending the buffer
                        if (BufferStart == 1 && buffSpan[0] == '\r')
                        {
                            Ending = ReadRowEnding.CarriageReturn;
                        }
                        break;
                    }
                    else
                    {
                        AddToPushback(buffSpan.Slice(BufferStart, end));

                        var len = end + BufferStart;
                        var res = Advance(buffSpan[..len]);
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
                                return new ValueTask<(ReadRowEnding Ending, Memory<char> PushBack)?>(default((ReadRowEnding Ending, Memory<char> PushBack)?));
                        }
                    }
                }

                // this implies we're only gonna read a row... so whatever
                if (Ending == 0)
                {
                    Ending = ReadRowEnding.CarriageReturnLineFeed;
                }

                return new ValueTask<(ReadRowEnding Ending, Memory<char> PushBack)?>((Ending, GetPushbackResult()));

            }
            finally
            {
                if (disposeHandle)
                {
                    handle.Dispose();
                }
            }

            static async ValueTask<(ReadRowEnding Ending, Memory<char> PushBack)?> DetectAsync_ContinueAfterReadAsync(
                RowEndingDetector self,
                ValueTask<int> waitFor,
                ReaderStateMachine.PinHandle handle,
                CancellationToken cancel
            )
            {
                using (handle)
                {
                    int end;
                    self.State.ReleasePinForAsync(waitFor);
                    {
                        end = await ConfigureCancellableAwait(self, waitFor, cancel);
                    }

                    // handle the results that were in flight
                    var continueScan = true;

                    var buffMem = self.BufferOwner.Memory;

                    if (end == 0)
                    {
                        // only need to check for '\r', because we'll never leave a '\n' pending the buffer
                        if (self.BufferStart == 1 && buffMem.Span[0] == '\r')
                        {
                            self.Ending = ReadRowEnding.CarriageReturn;
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
                        var mem = self.BufferOwner.Memory[self.BufferStart..];

                        var readTask = self.InnerAsync.Value.ReadAsync(mem, cancel);
                        self.State.ReleasePinForAsync(readTask);
                        {
                            end = await ConfigureCancellableAwait(self, readTask, cancel);
                        }

                        buffMem = self.BufferOwner.Memory;

                        if (end == 0)
                        {
                            // only need to check for '\r', because we'll never leave a '\n' pending the buffer
                            if (self.BufferStart == 1 && buffMem.Span[0] == '\r')
                            {
                                self.Ending = ReadRowEnding.CarriageReturn;
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
                        self.Ending = ReadRowEnding.CarriageReturnLineFeed;
                    }

                    return (self.Ending, self.GetPushbackResult());
                }
            }
        }

        internal (ReadRowEnding Ending, Memory<char> PushBack)? Detect()
        {
            using (State.Pin())
            {
                var buffSpan = BufferOwner.Memory.Span;

                var continueScan = true;
                while (continueScan)
                {
                    var end = Inner.Value.Read(buffSpan[BufferStart..]);
                    if (end == 0)
                    {
                        // only need to check for '\r', because we'll never leave a '\n' pending the buffer
                        if (BufferStart == 1 && buffSpan[0] == '\r')
                        {
                            Ending = ReadRowEnding.CarriageReturn;
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
                    Ending = ReadRowEnding.CarriageReturnLineFeed;
                }
            }

            return (Ending, GetPushbackResult());
        }

        private Memory<char> GetPushbackResult()
        {
            if (!PushbackOwner.HasValue)
            {
                // have to handle the "never actually pushed anything back"-case
                return Memory<char>.Empty;
            }

            return Pushback.Slice(0, PushbackLength);
        }

        private void AddToPushback(ReadOnlyMemory<char> mem)
        => AddToPushback(mem.Span);

        private void AddToPushback(ReadOnlySpan<char> span)
        {
            if (!PushbackOwner.HasValue)
            {
                PushbackOwner.Value = MemoryPool.Rent(BufferSizeHint);
            }

            var pushbackOwnerValue = PushbackOwner.Value;
            if (PushbackLength + span.Length > pushbackOwnerValue.Memory.Length)
            {
                var oldSize = PushbackOwner.Value.Memory.Length;
                var newSize = PushbackLength + span.Length;
                var newOwner = Utils.RentMustIncrease(MemoryPool, newSize, oldSize);

                Pushback.CopyTo(newOwner.Memory);
                pushbackOwnerValue.Dispose();
                PushbackOwner.Value = newOwner;
            }

            span.CopyTo(Pushback.Span.Slice(PushbackLength));
            PushbackLength += span.Length;
        }

        private AdvanceResult Advance(ReadOnlySpan<char> buffer)
        {
            State.EnsurePinned();

            var bufferLen = buffer.Length;

            for (var i = 0; i < bufferLen; i++)
            {
                var cc = buffer[i];

                var curState = State.CurrentState;

                var legalToEndRecord = 
                    ((((byte)curState) & ReaderStateMachine.CAN_END_RECORD_MASK) == ReaderStateMachine.CAN_END_RECORD_MASK) ||
                    (curState == ReaderStateMachine.State.Record_Start) ||
                    ((((byte)curState) & ReaderStateMachine.IN_COMMENT_MASK) == ReaderStateMachine.IN_COMMENT_MASK);

                if (legalToEndRecord)
                {
                    if (cc == '\n')
                    {
                        Ending = ReadRowEnding.LineFeed;
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
                            Ending = ReadRowEnding.CarriageReturnLineFeed;
                        }
                        else
                        {
                            Ending = ReadRowEnding.CarriageReturn;
                        }

                        return AdvanceResult.Finished;
                    }
                }

                int advanceIBy = 0;

                var res = State.Advance(cc, false);

                if (res == ReaderStateMachine.AdvanceResult.LookAhead_MultiCharacterSeparator)
                {
                    var valSepLen = ValueSeparatorMemory.Length;

                    // do we have enough in the buffer to look ahead?
                    var canCheckForSeparator = bufferLen - i >= valSepLen;
                    if (canCheckForSeparator)
                    {
                        var shouldMatch = buffer.Slice(i, valSepLen);
                        var eq = Utils.AreEqual(shouldMatch, ValueSeparatorMemory.Span);
                        if (eq)
                        {
                            // treat it like a value separator
                            res = State.AdvanceValueSeparator();
                            // advance further to the last character in the separator
                            advanceIBy = valSepLen - 1;
                        }
                        else
                        {
                            res = State.Advance(cc, true);
                        }
                    }
                    else
                    {
                        // we need to read more into the buffer before we can tell if we've got a separator
                        return AdvanceResult.Continue_PushBackOne;
                    }
                }

                switch (res)
                {
                    case ReaderStateMachine.AdvanceResult.Append_Character:
                    case ReaderStateMachine.AdvanceResult.Append_CarriageReturnAndCurrentCharacter:
                    case ReaderStateMachine.AdvanceResult.Finished_Unescaped_Value:
                    case ReaderStateMachine.AdvanceResult.Finished_Escaped_Value:
                    case ReaderStateMachine.AdvanceResult.Skip_Character:
                    case ReaderStateMachine.AdvanceResult.Append_CarriageReturnAndValueSeparator:
                    case ReaderStateMachine.AdvanceResult.Append_ValueSeparator:
                        break;

                    default:
                        return AdvanceResult.Exception_UnexpectedState;
                }

                i += advanceIBy;
            }

            return AdvanceResult.Continue;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                // Intentionally NOT disposing State, it's reused
                BufferOwner.Dispose();
                if (PushbackOwner.HasValue)
                {
                    PushbackOwner.Value.Dispose();
                }
                PushbackOwner.Clear();
                Inner.Clear();
                InnerAsync.Clear();

                IsDisposed = true;
            }
        }
    }

#if DEBUG
    internal sealed partial class RowEndingDetector : ITestableCancellableProvider
    {
        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
        int? ITestableCancellableProvider.CancelAfter { get; set; }
        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
        int ITestableCancellableProvider.CancelCounter { get; set; }
    }

    // this is only implemented in DEBUG builds, so tests (and only tests) can force
    //    particular async paths
    internal sealed partial class RowEndingDetector : ITestableAsyncProvider
    {
        private int _GoAsyncAfter;
        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
        int ITestableAsyncProvider.GoAsyncAfter { set { _GoAsyncAfter = value; } }

        private int _AsyncCounter;
        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
        int ITestableAsyncProvider.AsyncCounter => _AsyncCounter;

        [ExcludeFromCoverage("Just for testing, shouldn't contribute to coverage")]
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
