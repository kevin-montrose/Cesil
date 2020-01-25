using System;
using System.Buffers;

namespace Cesil
{
    internal sealed class Partial : ITestableDisposable
    {
        private int _CurrentColumnIndex;
        internal int CurrentColumnIndex
        {
            get
            {
                return _CurrentColumnIndex;
            }
            private set
            {
                _CurrentColumnIndex = value;
            }
        }

        internal int PendingCharsCount
        {
            get
            {
                return PendingCharacters.Length;
            }
        }

        public bool IsDisposed { get; private set; }
        private readonly MaybeInPlaceBuffer<char> PendingCharacters;

        internal Partial(MemoryPool<char> memoryPool)
        {
            CurrentColumnIndex = 0;
            PendingCharacters = new MaybeInPlaceBuffer<char>(memoryPool);
        }

        internal void ResetColumn(bool setHasPending)
        {
            CurrentColumnIndex = 0;
        }

        internal void AppendCarriageReturn(ReadOnlySpan<char> from)
        {
            PendingCharacters.AppendSingle(from, '\r');
        }

        internal void AppendCharacters(ReadOnlySpan<char> from, int atIndex, int length)
        {
            PendingCharacters.Append(from, atIndex, length);
        }

        internal void SkipCharacter()
        {
            PendingCharacters.Skipped();
        }

        internal void BufferToBeReused(ReadOnlySpan<char> buffer)
        {
            PendingCharacters.SwitchToCopy(buffer);
        }

        internal void ClearBufferAndAdvanceColumnIndex()
        {
            CurrentColumnIndex++;
            ClearBuffer();
        }

        internal ReadOnlyMemory<char> PendingAsMemory(ReadOnlyMemory<char> buffer)
        {
            return PendingCharacters.AsMemory(buffer);
        }

        internal string PendingAsString(ReadOnlyMemory<char> buffer)
        {
            if (buffer.Length == 0) return "";

            return new string(PendingCharacters.AsMemory(buffer).Span);
        }

        internal void ClearBuffer()
        {
            PendingCharacters.Clear();
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                PendingCharacters.Dispose();

                IsDisposed = true;
            }
        }
    }
}
