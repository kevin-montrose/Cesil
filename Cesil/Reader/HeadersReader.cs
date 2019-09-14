using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using static Cesil.DisposableHelper;

namespace Cesil
{
    internal sealed partial class HeadersReader<T> : ITestableDisposable
    {
        private const int LENGTH_SIZE = sizeof(uint) / sizeof(char);

        internal struct HeaderEnumerator : IEnumerator<ReadOnlyMemory<char>>, ITestableDisposable
        {
            internal readonly int Count;

            private int NextHeaderIndex;
            private int CurrentBufferIndex;
            private readonly ReadOnlyMemory<char> Buffer;

            public bool IsDisposed { get; private set; }

            private ReadOnlyMemory<char> _Current;
            public ReadOnlyMemory<char> Current
            {
                get
                {
                    AssertNotDisposed(this);
                    return _Current;
                }
                private set
                {
                    _Current = value;
                }
            }

            object IEnumerator.Current => Current;

            internal HeaderEnumerator(int count, ReadOnlyMemory<char> buffer)
            {
                IsDisposed = false;
                Count = count;
                _Current = default;
                NextHeaderIndex = 0;
                CurrentBufferIndex = 0;
                Buffer = buffer;
            }

            public bool MoveNext()
            {
                AssertNotDisposed(this);

                if (NextHeaderIndex >= Count)
                {
                    return false;
                }

                var span = Buffer.Span;
                var lenChars = span.Slice(CurrentBufferIndex, LENGTH_SIZE);
                var lenUint = MemoryMarshal.Cast<char, int>(lenChars);
                var len = lenUint[0];

                var dataIx = CurrentBufferIndex + LENGTH_SIZE;
                var endIx = dataIx + len;

                Current = Buffer.Slice(dataIx, len);

                CurrentBufferIndex = endIx;
                NextHeaderIndex++;

                return true;
            }

            public void Reset()
            {
                AssertNotDisposed(this);

                Current = default;
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

        private readonly IReaderAdapter Inner;
        private readonly IAsyncReaderAdapter InnerAsync;

        private readonly Column[] Columns;
        private readonly ReaderStateMachine StateMachine;
        private readonly BufferWithPushback Buffer;
        private readonly int BufferSizeHint;

        private int CurrentBuilderStart;
        private int CurrentBuilderLength;
        private IMemoryOwner<char> BuilderOwner;
        private Memory<char> BuilderBacking => BuilderOwner?.Memory ?? Memory<char>.Empty;

        public bool IsDisposed => MemoryPool == null;
        private MemoryPool<char> MemoryPool;

        private int HeaderCount;

        private int PushBackLength;
        private IMemoryOwner<char> PushBackOwner;
        private Memory<char> PushBack
        {
            get
            {
                if (PushBackOwner == null) return Memory<char>.Empty;

                return PushBackOwner.Memory;
            }
        }

        internal HeadersReader(
            ReaderStateMachine stateMachine,
            BoundConfigurationBase<T> config,
            CharacterLookup charLookup,
            IReaderAdapter inner,
            BufferWithPushback buffer
        )
        : this(stateMachine, config, charLookup, inner, null, buffer) { }

        internal HeadersReader(
            ReaderStateMachine stateMachine,
            BoundConfigurationBase<T> config,
            CharacterLookup charLookup,
            IAsyncReaderAdapter inner,
            BufferWithPushback buffer
        )
        : this(stateMachine, config, charLookup, null, inner, buffer) { }

        private HeadersReader(
            ReaderStateMachine stateMachine,
            BoundConfigurationBase<T> config,
            CharacterLookup charLookup,
            IReaderAdapter inner,
            IAsyncReaderAdapter innerAsync,
            BufferWithPushback buffer
        )
        {
            Inner = inner;
            InnerAsync = innerAsync;

            MemoryPool = config.MemoryPool;
            BufferSizeHint = config.ReadBufferSizeHint;
            Columns = config.DeserializeColumns;

            StateMachine = stateMachine;
            stateMachine.Initialize(
                charLookup,
                config.EscapedValueStartAndStop,
                config.EscapeValueEscapeChar,
                config.RowEnding,
                ReadHeaders.Never,
                false
            );

            Buffer = buffer;

            HeaderCount = 0;
            PushBackLength = 0;
        }

        internal (HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack) Read()
        {
            StateMachine.Pin();

            while (true)
            {
                var available = Buffer.Read(Inner);
                if (available == 0)
                {
                    if (BuilderBacking.Length > 0)
                    {
                        PushPendingCharactersToValue();
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

            StateMachine.Unpin();

            return IsHeaderResult();
        }

        private void AddToPushback(ReadOnlySpan<char> c)
        {
            if (PushBackOwner == null)
            {
                PushBackOwner = MemoryPool.Rent(BufferSizeHint);
            }

            if (PushBackLength + c.Length > PushBackOwner.Memory.Length)
            {
                var oldSize = PushBackOwner.Memory.Length;

                var newSize = (PushBackLength + c.Length) * 2;    // double size, because we're sharing the buffer
                var newOwner = Utils.RentMustIncrease(MemoryPool, newSize, oldSize);
                PushBackOwner.Memory.CopyTo(newOwner.Memory);

                PushBackOwner.Dispose();
                PushBackOwner = newOwner;
            }

            if (PushBackLength + c.Length > PushBackOwner.Memory.Length)
            {
                Throw.InvalidOperationException<object>($"Could not allocate large enough buffer to read headers");
            }

            c.CopyTo(PushBack.Span.Slice(PushBackLength));
            PushBackLength += c.Length;
        }

        internal ValueTask<(HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> ReadAsync(CancellationToken cancel)
        {
            StateMachine.Pin();

            while (true)
            {
                var availableTask = Buffer.ReadAsync(InnerAsync, cancel);
                if (!availableTask.IsCompletedSuccessfully(this))
                {
                    return ReadAsync_ContinueAfterReadAsync(this, availableTask, cancel);
                }

                var available = availableTask.Result;
                if (available == 0)
                {
                    if (BuilderBacking.Length > 0)
                    {
                        PushPendingCharactersToValue();
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

            StateMachine.Unpin();

            return new ValueTask<(HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)>(IsHeaderResult());

            // wait for read to complete, then continue async
            static async ValueTask<(HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> ReadAsync_ContinueAfterReadAsync(HeadersReader<T> self, ValueTask<int> waitFor, CancellationToken cancel)
            {
                int available;
                using (self.StateMachine.ReleaseAndRePinForAsync(waitFor))
                {
                    available = await waitFor;
                }

                // handle the in flight task
                if (available == 0)
                {
                    if (self.BuilderBacking.Length > 0)
                    {
                        self.PushPendingCharactersToValue();
                    }

                    self.StateMachine.Unpin();

                    return self.IsHeaderResult();
                }
                else
                {
                    self.AddToPushback(self.Buffer.Buffer.Span.Slice(0, available));
                }

                if (self.AdvanceWork(available))
                {
                    self.StateMachine.Unpin();

                    return self.IsHeaderResult();
                }


                // go back into the loop
                while (true)
                {
                    var readTask = self.Buffer.ReadAsync(self.InnerAsync, cancel);
                    using (self.StateMachine.ReleaseAndRePinForAsync(readTask))
                    {
                        available = await readTask;
                    }

                    if (available == 0)
                    {
                        if (self.BuilderBacking.Length > 0)
                        {
                            self.PushPendingCharactersToValue();
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

                self.StateMachine.Unpin();

                return self.IsHeaderResult();
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
            var buffSpan = Buffer.Buffer.Span;

            var appendingSince = -1;

            for (var i = 0; i < bufferLen; i++)
            {
                var c = buffSpan[i];

                var res = StateMachine.Advance(c);

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
                        var toAppend = buffSpan.Slice(appendingSince, i - appendingSince);
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

                    case ReaderStateMachine.AdvanceResult.Finished_Value:
                        PushPendingCharactersToValue();
                        break;
                    case ReaderStateMachine.AdvanceResult.Finished_Record:
                        if (CurrentBuilderLength > 0)
                        {
                            PushPendingCharactersToValue();
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
                        return Throw.Exception<bool>($"Unexpected {nameof(RowEndings)} value encountered");
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedState:
                        unprocessedCharacters = default;
                        return Throw.Exception<bool>($"Unexpected state value entered");
                    case ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecordOrValue:
                        unprocessedCharacters = default;
                        return Throw.InvalidOperationException<bool>($"Encountered '{c}' when expecting the end of a record or value");

                    default:
                        unprocessedCharacters = default;
                        return Throw.Exception<bool>($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {res}");
                }
            }

            if (appendingSince != -1)
            {
                var toAppend = buffSpan.Slice(appendingSince, bufferLen - appendingSince);
                AddToBuilder(toAppend);
            }

            unprocessedCharacters = 0;
            return false;
        }

        private void AddToBuilder(ReadOnlySpan<char> chars)
        {
            if (BuilderOwner == null)
            {
                CurrentBuilderStart = LENGTH_SIZE;
                CurrentBuilderLength = 0;
                BuilderOwner = MemoryPool.Rent(BufferSizeHint);
            }

            var ix = CurrentBuilderStart + CurrentBuilderLength;
            var endIx = ix + chars.Length;

            if (endIx >= BuilderBacking.Length)
            {
                var oldLength = BuilderBacking.Length;
                var newLength = endIx * 2;
                var newOwner = Utils.RentMustIncrease(MemoryPool, newLength, oldLength);
                BuilderBacking.CopyTo(newOwner.Memory);

                BuilderOwner.Dispose();
                BuilderOwner = newOwner;
            }

            chars.CopyTo(BuilderBacking.Span.Slice(ix));

            CurrentBuilderLength += chars.Length;
        }

        private void RecordLength(int curHeaderLength)
        {
            var lengthIx = CurrentBuilderStart - LENGTH_SIZE;
            var destSlice = BuilderBacking.Slice(lengthIx, LENGTH_SIZE);
            var destSpan = destSlice.Span;

            var uintDestSpan = MemoryMarshal.Cast<char, int>(destSpan);
            uintDestSpan[0] = curHeaderLength;
        }

        private void PushPendingCharactersToValue()
        {
            RecordLength(CurrentBuilderLength);

            CurrentBuilderStart += CurrentBuilderLength;
            CurrentBuilderStart += LENGTH_SIZE;

            CurrentBuilderLength = 0;

            HeaderCount++;
        }

        private HeaderEnumerator MakeEnumerator()
        {
            return new HeaderEnumerator(HeaderCount, BuilderBacking);
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                // Intentionally NOT disposing StateMachine, it's reused
                PushBackOwner?.Dispose();
                BuilderOwner?.Dispose();
                PushBackOwner = null;
                MemoryPool = null;
            }
        }
    }

#if DEBUG
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