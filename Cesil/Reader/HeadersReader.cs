using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed class HeadersReader<T> : ITestableDisposable
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
                    AssertNotDisposed();
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
                AssertNotDisposed();

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
                AssertNotDisposed();
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

            public void AssertNotDisposed()
            {
                if (IsDisposed)
                {
                    Throw.ObjectDisposedException(nameof(HeaderEnumerator));
                }
            }

            public override string ToString()
            => nameof(HeaderEnumerator);
        }

        private readonly Column[] Columns;
        private readonly TextReader Inner;
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
        private Memory<char> PushBack => PushBackOwner.Memory;

        internal HeadersReader(
            BoundConfigurationBase<T> config,
            ReaderStateMachine.CharacterLookup charLookup,
            TextReader inner,
            BufferWithPushback buffer
        )
        {
            MemoryPool = config.MemoryPool;
            BufferSizeHint = config.ReadBufferSizeHint;
            Columns = config.DeserializeColumns;
            Inner = inner;

            StateMachine =
                new ReaderStateMachine(
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
                Throw.InvalidOperationException($"Could not allocate large enough buffer to read headers");
            }

            c.CopyTo(PushBack.Span.Slice(PushBackLength));
            PushBackLength += c.Length;
        }

        internal ValueTask<(HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> ReadAsync(CancellationToken cancel)
        {
            while (true)
            {
                var availableTask = Buffer.ReadAsync(Inner, cancel);
                if (!availableTask.IsCompletedSuccessfully)
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

            return new ValueTask<(HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)>(IsHeaderResult());

            // wait for read to complete, then continue async
            static async ValueTask<(HeaderEnumerator Headers, bool IsHeader, Memory<char> PushBack)> ReadAsync_ContinueAfterReadAsync(HeadersReader<T> self, ValueTask<int> waitFor, CancellationToken cancel)
            {
                var available = await waitFor;

                // handle the in flight task
                if (available == 0)
                {
                    if (self.BuilderBacking.Length > 0)
                    {
                        self.PushPendingCharactersToValue();
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
                    available = await self.Buffer.ReadAsync(self.Inner, cancel);

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
                else if (res == ReaderStateMachine.AdvanceResult.Append_Previous_And_Current_Character)
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
                        Throw.InvalidOperationException($"Encountered '{c}' when expecting end of record");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_InvalidState:
                        Throw.InvalidOperationException($"Internal state machine is in an invalid state due to a previous error");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_StartEscapeInValue:
                        Throw.InvalidOperationException($"Encountered '{c}', starting an escaped value, when already in a value");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedCharacterInEscapeSequence:
                        Throw.InvalidOperationException($"Encountered '{c}' in an escape sequence, which is invalid");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedLineEnding:
                        Throw.Exception($"Unexpected {nameof(RowEndings)} value encountered");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_UnexpectedState:
                        Throw.Exception($"Unexpected state value entered");
                        break;
                    case ReaderStateMachine.AdvanceResult.Exception_ExpectedEndOfRecordOrValue:
                        Throw.InvalidOperationException($"Encountered '{c}' when expecting the end of a record or value");
                        break;

                    default:
                        Throw.Exception($"Unexpected {nameof(ReaderStateMachine.AdvanceResult)}: {res}");
                        break;
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
                PushBackOwner?.Dispose();
                BuilderOwner?.Dispose();
                StateMachine.Dispose();
                PushBackOwner = null;
                MemoryPool = null;
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(HeadersReader<T>));
            }
        }
    }
}