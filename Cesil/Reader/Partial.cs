using System;
using System.Buffers;

namespace Cesil
{
    internal sealed class Partial<T> : ITestableDisposable
    {
        private T _Value;
        internal T Value
        {
            get
            {
                return _Value;
            }
            private set
            {
                _Value = value;
            }
        }

        private bool _HasPending;
        internal bool HasPending
        {
            get
            {
                return _HasPending;
            }
            private set
            {
                _HasPending = value;
            }
        }

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

        public bool IsDisposed => PendingCharacters == null;
        private MaybeInPlaceBuffer<char> PendingCharacters;

        internal Partial(MemoryPool<char> memoryPool)
        {
            Value = default;
            HasPending = false;
            CurrentColumnIndex = 0;
            PendingCharacters = new MaybeInPlaceBuffer<char>(memoryPool);
        }

        internal void SetValueAndResetColumn(T v)
        {
            Value = v;
            HasPending = true;
            CurrentColumnIndex = 0;
        }

        internal void ClearValue()
        {
            Value = default;
            HasPending = false;
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
                PendingCharacters = null;
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposedException(nameof(Partial<T>));
            }
        }
    }
}
