using System;
using System.Buffers;

namespace Cesil
{
    internal sealed class MaybeInPlaceBuffer<T>: ITestableDisposable
    {
        private enum Mode: byte
        {
            NONE = 0,

            Uninitialized,

            InPlace,
            CopyOnNextAppend,
            Copy
        }

        public bool IsDisposed => MemoryPool == null;
        private MemoryPool<T> MemoryPool;

        private Mode CurrentMode;
        private int StartIndex;
        internal int Length;

        private IMemoryOwner<T> CopyOwner;
        private Span<T> Copy => CopyOwner.Memory.Span;

        internal MaybeInPlaceBuffer(MemoryPool<T> memoryPool)
        {
            MemoryPool = memoryPool;

            CurrentMode = Mode.Uninitialized;
        }

        internal void Append(ReadOnlySpan<T> fromBuffer, int index, int length)
        {
            // we figured out that we can't be in place on
            //   the other buffer... but that doesn't actually
            //   matter until we next try to append.
            //
            // which is now, so copy everything out of the span
            //   into our local buffer and switch the mode
            //   to plain old Copy mode
            if (CurrentMode == Mode.CopyOnNextAppend)
            {
                SwitchToCopy(fromBuffer);
            }

            switch (CurrentMode)
            {
                case Mode.Uninitialized:
                    CurrentMode = Mode.InPlace;
                    StartIndex = index;
                    Length = length;
                    break;
                case Mode.Copy:
                    var desiredSize = Length + length;
                    if (desiredSize >= Copy.Length)
                    {
                        ResizeCopy(desiredSize * 2);
                    }

                    fromBuffer.Slice(index, length).CopyTo(Copy.Slice(Length));
                    Length += length;
                    break;
                case Mode.InPlace:
                    Length++;
                    break;
                default:
                    Throw.Exception($"Unexpected {nameof(Mode)}: {CurrentMode}");
                    break;
            }
        }

        private void ResizeCopy(int newDesiredSize)
        {
            var oldSize = CopyOwner != null ? CopyOwner.Memory.Length : 0;
            var newCopy = Utils.RentMustIncrease(MemoryPool, newDesiredSize, oldSize);

            if (CopyOwner != null)
            {
                Copy.CopyTo(newCopy.Memory.Span);

                CopyOwner.Dispose();
            }

            CopyOwner = newCopy;
        }

        internal void SwitchToCopy(ReadOnlySpan<T> fromBuffer)
        {
            // if we're already copying, nothing to switch to
            if (CurrentMode == Mode.Copy) return;
            // if nothing has been appended, there's nothing to copy
            if (CurrentMode == Mode.Uninitialized) return;

            if (CopyOwner == null || Copy.Length < Length)
            {
                ResizeCopy(Length * 2);
            }

            fromBuffer.Slice(StartIndex, Length).CopyTo(Copy);
            StartIndex = 0;
            CurrentMode = Mode.Copy;
        }

        internal void Skipped()
        {
            // this means we can't jut blindly copy of out the backing buffer if
            //   another append comes through, so change state accordingly
            switch (CurrentMode)
            {
                case Mode.Uninitialized:
                case Mode.CopyOnNextAppend:
                case Mode.Copy:
                    // nothing to do
                    break;
                case Mode.InPlace:
                    CurrentMode = Mode.CopyOnNextAppend;
                    break;
                default:
                    Throw.Exception($"Unexpected {nameof(Mode)}: {CurrentMode}");
                    break;
            }
        }

        internal void Clear()
        {
            CurrentMode = Mode.Uninitialized;
            Length = 0;
        }

        internal ReadOnlyMemory<T> AsMemory(ReadOnlyMemory<T> fromBufferMem)
        {
            switch (CurrentMode)
            {
                case Mode.CopyOnNextAppend:
                case Mode.InPlace:
                    // we're able to just read out of the buffer, no intermediate copy required
                    return fromBufferMem.Slice(StartIndex, Length);
                case Mode.Copy:
                    // whelp, we had to make a copy... better return it
                    return CopyOwner.Memory.Slice(StartIndex, Length);

                case Mode.Uninitialized:
                    return ReadOnlyMemory<T>.Empty;

                default:
                    Throw.Exception($"Unexpected {nameof(Mode)}: {CurrentMode}");
                    return default;
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                CopyOwner?.Dispose();
                MemoryPool = null;
            }
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
            {
                Throw.ObjectDisposed(nameof(MaybeInPlaceBuffer<T>));
            }
        }
    }
}
