using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Cesil.AwaitHelper;
using static Cesil.DisposableHelper;

namespace Cesil
{
    [DoesNotEscape("Only ever used as a local or parameter in method (though some async methods)")]
    internal sealed partial class HeadersReader<T> : ITestableDisposable
    {
        private const int LENGTH_SIZE = sizeof(uint) / sizeof(char);

        [DoesNotEscape("Only ever used as a local or parameter in method (though some async methods)")]
        internal struct HeaderEnumerator : IEnumerator<ReadOnlyMemory<char>>, ITestableDisposable
        {
            internal readonly int Count;

            private int NextHeaderIndex;
            private int CurrentBufferIndex;
            private readonly ReadOnlyMemory<char> Buffer;

            private readonly WhitespaceTreatments WhitespaceTreatment;

            public bool IsDisposed { get; private set; }

            private ReadOnlyMemory<char> _Current;
            public readonly ReadOnlyMemory<char> Current
            {
                get
                {
                    AssertNotDisposedInternal(this);
                    return _Current;
                }
            }

            readonly object IEnumerator.Current => Current;

            internal HeaderEnumerator(int count, ReadOnlyMemory<char> buffer, WhitespaceTreatments whitespaceTreatment)
            {
                IsDisposed = false;
                Count = count;
                Buffer = buffer;
                WhitespaceTreatment = whitespaceTreatment;

                _Current = default;
                NextHeaderIndex = 0;
                CurrentBufferIndex = 0;
            }

            public bool MoveNext()
            {
                AssertNotDisposedInternal(this);

                if (NextHeaderIndex >= Count)
                {
                    return false;
                }

                var span = Buffer.Span;
                var lenChars = span.Slice(CurrentBufferIndex, LENGTH_SIZE);
                var lenIntSpan = MemoryMarshal.Cast<char, int>(lenChars);
                var stored = lenIntSpan[0];
                var len = Math.Abs(stored);
                var wasEscaped = stored < 0;

                var dataIx = CurrentBufferIndex + LENGTH_SIZE;
                var endIx = dataIx + len;

                var rawHeader = Buffer.Slice(dataIx, len);

                // The state machine will skip leading values outside of values, so we only need to do any trimming IN the values
                //
                // Technically we could probably have the state machine skip leading inside too...
                // todo: do that ^^^ (tracking issue: https://github.com/kevin-montrose/Cesil/issues/5)
                var needsLeadingTrim = WhitespaceTreatment.HasFlag(WhitespaceTreatments.TrimLeadingInValues);
                if (needsLeadingTrim)
                {
                    rawHeader = Utils.TrimLeadingWhitespace(rawHeader);
                }

                // We need to trim trailing IN values if requested, and we need to trim trailing after values
                //   if requested AND the value wasn't escaped.
                //
                // Trimming trailing requires look ahead, which would greatly complicate the state machine
                //   (technically making it not a state machine) so this will have to do.
                var needsTrailingTrim =
                    WhitespaceTreatment.HasFlag(WhitespaceTreatments.TrimTrailingInValues) ||
                    (WhitespaceTreatment.HasFlag(WhitespaceTreatments.TrimAfterValues) && !wasEscaped);

                if (needsTrailingTrim)
                {
                    rawHeader = Utils.TrimTrailingWhitespace(rawHeader);
                }

                _Current = rawHeader;

                CurrentBufferIndex = endIx;
                NextHeaderIndex++;

                return true;
            }

            public void Reset()
            {
                AssertNotDisposedInternal(this);

                _Current = default;
                NextHeaderIndex = 0;
                CurrentBufferIndex = 0;
            }

            public void Dispose()
            {
                if (!IsDisposed)
                {
                    IsDisposed = true;
                }
            }

            public override string ToString()
            => $"{nameof(HeaderEnumerator)} with {nameof(Count)}={Count}";
        }

        private readonly IBoundConfiguration<T> Configuration;

        private NonNull<IReaderAdapter> Inner;
        private NonNull<IAsyncReaderAdapter> InnerAsync;

        private readonly IEnumerable<DeserializableMember> Columns;
        private readonly ReaderStateMachine StateMachine;
        private readonly BufferWithPushback Buffer;
        private readonly int BufferSizeHint;
        private readonly WhitespaceTreatments WhitespaceTreatment;

        private int CurrentBuilderStart;
        private int CurrentBuilderLength;
        private NonNull<IMemoryOwner<char>> BuilderOwner;
        private Memory<char> BuilderBacking
        {
            get
            {
                if (!BuilderOwner.HasValue) return Memory<char>.Empty;

                return BuilderOwner.Value.Memory;
            }
        }

        public bool IsDisposed { get; private set; }
        private readonly MemoryPool<char> MemoryPool;

        private int HeaderCount;

        private int PushBackLength;
        private NonNull<IMemoryOwner<char>> PushBackOwner;
        private Memory<char> PushBack
        {
            get
            {
                if (!PushBackOwner.HasValue) return Memory<char>.Empty;

                return PushBackOwner.Value.Memory;
            }
        }

        internal HeadersReader(
            ReaderStateMachine stateMachine,
            BoundConfigurationBase<T> config,
            CharacterLookup charLookup,
            IReaderAdapter inner,
            BufferWithPushback buffer,
            RowEnding rowEndingOverride
        )
        : this(stateMachine, config, charLookup, inner, null, buffer, rowEndingOverride) { }

        internal HeadersReader(
            ReaderStateMachine stateMachine,
            BoundConfigurationBase<T> config,
            CharacterLookup charLookup,
            IAsyncReaderAdapter inner,
            BufferWithPushback buffer,
            RowEnding rowEndingOverride
        )
        : this(stateMachine, config, charLookup, null, inner, buffer, rowEndingOverride) { }

        private HeadersReader(
            ReaderStateMachine stateMachine,
            BoundConfigurationBase<T> config,
            CharacterLookup charLookup,
            IReaderAdapter? inner,
            IAsyncReaderAdapter? innerAsync,
            BufferWithPushback buffer,
            RowEnding rowEndingOverride
        )
        {
            System.Diagnostics.Debug.WriteLineIf(LogConstants.STATE_TRANSITION, $"New {nameof(HeadersReader<T>)}");

            Configuration = config;

            Inner.SetAllowNull(inner);
            InnerAsync.SetAllowNull(innerAsync);

            var options = Configuration.Options;

            MemoryPool = options.MemoryPool;
            BufferSizeHint = options.ReadBufferSizeHint;
            Columns = config.DeserializeColumns;

            StateMachine = stateMachine;
            stateMachine.Initialize(
                charLookup,
                options.EscapedValueStartAndEnd,
                options.EscapedValueEscapeCharacter,
                rowEndingOverride,                      // this can be something OTHER than what was provided, due to RowEnding.Detect
                ReadHeader.Never,
                false,
                options.WhitespaceTreatment.HasFlag(WhitespaceTreatments.TrimBeforeValues),
                options.WhitespaceTreatment.HasFlag(WhitespaceTreatments.TrimAfterValues)
            );

            Buffer = buffer;

            HeaderCount = 0;
            PushBackLength = 0;
            WhitespaceTreatment = options.WhitespaceTreatment;
        }

        internal (HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack) Read()
        {
            using (StateMachine.Pin())
            {
                while (true)
                {
                    var available = Buffer.Read(Inner.Value);
                    if (available == 0)
                    {
                        if (BuilderBacking.Length > 0)
                        {
                            var inEscapedValue = ReaderStateMachine.IsInEscapedValue(StateMachine.CurrentState);
                            PushPendingCharactersToValue(inEscapedValue);
                        }
                        break;
                    }
                    else
                    {
                        AddToPushback(Buffer.Buffer.Span.Slice(0, available));
                    }

                    if (AdvanceWork(available))
                    {
                        break;
                    }
                }
            }

            return IsHeaderResult();
        }

        private void AddToPushback(ReadOnlySpan<char> c)
        {
            if (!PushBackOwner.HasValue)
            {
                PushBackOwner.Value = MemoryPool.Rent(BufferSizeHint);
            }

            var pushBackOwnerValue = PushBackOwner.Value;
            if (PushBackLength + c.Length > pushBackOwnerValue.Memory.Length)
            {
                var oldSize = pushBackOwnerValue.Memory.Length;

                var newSize = (PushBackLength + c.Length) * 2;    // double size, because we're sharing the buffer
                var newOwner = Utils.RentMustIncrease(MemoryPool, newSize, oldSize);
                pushBackOwnerValue.Memory.CopyTo(newOwner.Memory);

                pushBackOwnerValue.Dispose();
                PushBackOwner.Value = pushBackOwnerValue = newOwner;
            }

            if (PushBackLength + c.Length > pushBackOwnerValue.Memory.Length)
            {
                Throw.InvalidOperationException<object>($"Could not allocate large enough buffer to read headers");
            }

            c.CopyTo(PushBack.Span.Slice(PushBackLength));
            PushBackLength += c.Length;
        }

        internal ValueTask<(HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> ReadAsync(CancellationToken cancellationToken)
        {
            var handle = StateMachine.Pin();
            var disposeHandle = true;

            try
            {
                while (true)
                {
                    var availableTask = Buffer.ReadAsync(InnerAsync.Value, cancellationToken);
                    if (!availableTask.IsCompletedSuccessfully(this))
                    {
                        disposeHandle = false;
                        return ReadAsync_ContinueAfterReadAsync(this, availableTask, handle, cancellationToken);
                    }

                    var available = availableTask.Result;
                    if (available == 0)
                    {
                        if (BuilderBacking.Length > 0)
                        {
                            var inEscapedValue = ReaderStateMachine.IsInEscapedValue(StateMachine.CurrentState);
                            PushPendingCharactersToValue(inEscapedValue);
                        }
                        break;
                    }
                    else
                    {
                        AddToPushback(Buffer.Buffer.Span.Slice(0, available));
                    }

                    if (AdvanceWork(available))
                    {
                        break;
                    }
                }
            }
            finally
            {
                if (disposeHandle)
                {
                    handle.Dispose();
                }
            }

            return new ValueTask<(HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)>(IsHeaderResult());

            // wait for read to complete, then continue async
            static async ValueTask<(HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> ReadAsync_ContinueAfterReadAsync(
                HeadersReader<T> self,
                ValueTask<int> waitFor,
                ReaderStateMachine.PinHandle handle,
                CancellationToken cancellationToken)
            {
                using (handle)
                {
                    int available;
                    self.StateMachine.ReleasePinForAsync(waitFor);
                    {
                        available = await ConfigureCancellableAwait(self, waitFor, cancellationToken);
                    }

                    // handle the in flight task
                    if (available == 0)
                    {
                        if (self.BuilderBacking.Length > 0)
                        {
                            var inEscapedValue = ReaderStateMachine.IsInEscapedValue(self.StateMachine.CurrentState);
                            self.PushPendingCharactersToValue(inEscapedValue);
                        }

                        return self.IsHeaderResult();
                    }
                    else
                    {
                        self.AddToPushback(self.Buffer.Buffer.Span.Slice(0, available));
                    }

                    if (self.AdvanceWork(available))
                    {
                        return self.IsHeaderResult();
                    }

                    // go back into the loop
                    while (true)
                    {
                        var readTask = self.Buffer.ReadAsync(self.InnerAsync.Value, cancellationToken);
                        self.StateMachine.ReleasePinForAsync(readTask);
                        {
                            available = await ConfigureCancellableAwait(self, readTask, cancellationToken);
                        }

                        if (available == 0)
                        {
                            if (self.BuilderBacking.Length > 0)
                            {
                                var inEscapedValue = ReaderStateMachine.IsInEscapedValue(self.StateMachine.CurrentState);
                                self.PushPendingCharactersToValue(inEscapedValue);
                            }
                            break;
                        }
                        else
                        {
                            self.AddToPushback(self.Buffer.Buffer.Span.Slice(0, available));
                        }

                        if (self.AdvanceWork(available))
                        {
                            break;
                        }
                    }

                    return self.IsHeaderResult();
                }
            }
        }

        private (HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack) IsHeaderResult()
        {
            var isHeader = false;

            using (var e = MakeEnumerator())
            {
                while (e.MoveNext())
                {
                    var val = e.Current;

                    foreach (var col in Columns)
                    {
                        var colNameMem = col.Name.AsMemory();
                        if (Utils.AreEqual(colNameMem, val))
                        {
                            isHeader = true;
                            goto finish;
                        }
                    }
                }
            }

finish:
            return (MakeEnumerator(), isHeader, PushBack.Slice(0, PushBackLength));
        }

        private bool AdvanceWork(int numInBuffer)
        {
            var res = ProcessBuffer(numInBuffer, out var pushBack);
            if (pushBack > 0)
            {
                Buffer.PushBackFromBuffer(numInBuffer, pushBack);
            }

            return res;
        }

        private bool ProcessBuffer(int bufferLen, out int unprocessedCharacters)
        {
            StateMachine.EnsurePinned();

            var buffSpan = Buffer.Buffer.Span;

            var appendingSince = -1;

            for (var i = 0; i < bufferLen; i++)
            {
                var c = buffSpan[i];

                var res = StateMachine.Advance(c);

                System.Diagnostics.Debug.WriteLineIf(LogConstants.STATE_TRANSITION, $"{StateMachine.CurrentState} + {c} => {StateMachine.CurrentState } & {res}");

                if (res == ReaderStateMachine.AdvanceResult.Append_Character)
                {
                    if (appendingSince == -1)
                    {
                        appendingSince = i;
                    }

                    continue;
                }
                else if (res == ReaderStateMachine.AdvanceResult.Append_CarriageReturnAndCurrentCharacter)
                {
                    if (appendingSince == -1)
                    {
                        appendingSince = i - 1;
                    }

                    continue;
                }
                else
                {
                    if (appendingSince != -1)
                    {
                        var toAppend = buffSpan[appendingSince..i];
                        AddToBuilder(toAppend);

                        appendingSince = -1;
                    }
                }

                switch (res)
                {
                    case ReaderStateMachine.AdvanceResult.Skip_Character:
                        break;

                    // case ReaderStateMachine.AdvanceResult.Append_Character is handled by
                    //      the above buffering logic

                    // case ReaderStateMachine.AdvanceResult.Append_CarriageReturn_And_Character is handled by
                    //      the above buffering logic

                    case ReaderStateMachine.AdvanceResult.Finished_Unescaped_Value:
                        PushPendingCharactersToValue(false);
                        break;
                    case ReaderStateMachine.AdvanceResult.Finished_Escaped_Value:
                        PushPendingCharactersToValue(true);
                        break;

                    case ReaderStateMachine.AdvanceResult.Finished_LastValueUnescaped_Record:
                        if (CurrentBuilderLength > 0)
                        {
                            PushPendingCharactersToValue(false);
                        }

                        unprocessedCharacters = bufferLen - i - 1;
                        return true;
                    case ReaderStateMachine.AdvanceResult.Finished_LastValueEscaped_Record:
                        if (CurrentBuilderLength > 0)
                        {
                            PushPendingCharactersToValue(true);
                        }

                        unprocessedCharacters = bufferLen - i - 1;
                        return true;

                    case ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecord:
                        unprocessedCharacters = default;
                        return Throw.InvalidOperationException<bool>($"Encountered '{c}' when expecting end of record");
                    case ReaderStateMachine.AdvanceResult.Exception_InvalidState:
                        unprocessedCharacters = default;
                        return Throw.InvalidOperationException<bool>($"Internal state machine is in an invalid state due to a previous error");
                    case ReaderStateMachine.AdvanceResult.Exception_StartEscapeInValue:
                        unprocessedCharacters = default;
                        return Throw.InvalidOperationException<bool>($"Encountered '{c}', starting an escaped value, when already in a value");
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedCharacterInEscapeSequence:
                        unprocessedCharacters = default;
                        return Throw.InvalidOperationException<bool>($"Encountered '{c}' in an escape sequence, which is invalid");
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedLineEnding:
                        unprocessedCharacters = default;
                        return Throw.ImpossibleException<bool, T>($"Unexpected {nameof(RowEnding)} value encountered", Configuration);
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedState:
                        unprocessedCharacters = default;
                        return Throw.ImpossibleException<bool, T>($"Unexpected state value entered", Configuration);
                    case ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecordOrValue:
                        unprocessedCharacters = default;
                        return Throw.InvalidOperationException<bool>($"Encountered '{c}' when expecting the end of a record or value");

                    default:
                        unprocessedCharacters = default;
                        return Throw.ImpossibleException<bool, T>($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {res}", Configuration);
                }
            }

            if (appendingSince != -1)
            {
                var toAppend = buffSpan[appendingSince..bufferLen];
                AddToBuilder(toAppend);
            }

            unprocessedCharacters = 0;
            return false;
        }

        private void AddToBuilder(ReadOnlySpan<char> chars)
        {
            if (!BuilderOwner.HasValue)
            {
                CurrentBuilderStart = LENGTH_SIZE;
                CurrentBuilderLength = 0;
                BuilderOwner.Value = MemoryPool.Rent(BufferSizeHint);
            }

            var ix = CurrentBuilderStart + CurrentBuilderLength;
            var endIx = ix + chars.Length;

            if (endIx >= BuilderBacking.Length)
            {
                var oldLength = BuilderBacking.Length;
                var newLength = endIx * 2;
                var newOwner = Utils.RentMustIncrease(MemoryPool, newLength, oldLength);
                BuilderBacking.CopyTo(newOwner.Memory);

                BuilderOwner.Value.Dispose();
                BuilderOwner.Value = newOwner;
            }

            chars.CopyTo(BuilderBacking.Span.Slice(ix));

            CurrentBuilderLength += chars.Length;
        }

        private void RecordLengthAndEscaped(int curHeaderLength, bool valueWasEscaped)
        {
            var lengthIx = CurrentBuilderStart - LENGTH_SIZE;
            var destSlice = BuilderBacking.Slice(lengthIx, LENGTH_SIZE);
            var destSpan = destSlice.Span;

            var uintDestSpan = MemoryMarshal.Cast<char, int>(destSpan);

            var toStore = curHeaderLength;
            if (valueWasEscaped)
            {
                toStore = -toStore;
            }

            uintDestSpan[0] = toStore;
        }

        private void PushPendingCharactersToValue(bool valueWasEscaped)
        {
            RecordLengthAndEscaped(CurrentBuilderLength, valueWasEscaped);
            CurrentBuilderStart += CurrentBuilderLength;
            CurrentBuilderStart += LENGTH_SIZE;

            CurrentBuilderLength = 0;

            HeaderCount++;
        }

        private HeaderEnumerator MakeEnumerator()
        {
            return new HeaderEnumerator(HeaderCount, BuilderBacking, WhitespaceTreatment);
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                // Intentionally NOT disposing StateMachine, it's reused
                if (PushBackOwner.HasValue)
                {
                    PushBackOwner.Value.Dispose();
                }
                PushBackOwner.Clear();

                if (BuilderOwner.HasValue)
                {
                    BuilderOwner.Value.Dispose();
                }
                BuilderOwner.Clear();

                IsDisposed = true;
            }
        }
    }

#if DEBUG
    // only available in DEBUG for testing purposes
    internal sealed partial class HeadersReader<T> : ITestableCancellableProvider
    {
        int? ITestableCancellableProvider.CancelAfter { get; set; }
        int ITestableCancellableProvider.CancelCounter { get; set; }
    }

    // this is only implemented in DEBUG builds, so tests (and only tests) can force
    //    particular async paths
    internal sealed partial class HeadersReader<T> : ITestableAsyncProvider
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