using System;
using System.Buffers;

namespace Cesil
{
    internal sealed class MaybeInPlaceBuffer<T> : ITestableDisposable
    {
        internal enum Mode : byte
        {
            None = 0,

            Uninitialized,

            InPlace,
            CopyOnNextAppend,
            Copy
        }

        public bool IsDisposed { get; private set; }
        private readonly MemoryPool<T> MemoryPool;

        internal Mode CurrentMode;
        private int StartIndex;
        internal int Length;

        private NonNull<IMemoryOwner<T>> CopyOwner;
        internal Span<T> Copy => CopyOwner.Value.Memory.Span;         // internal for testing purposes, don't use directly

        internal MaybeInPlaceBuffer(MemoryPool<T> memoryPool)
        {
            MemoryPool = memoryPool;

            CurrentMode = Mode.Uninitialized;
        }

        internal void AppendSingle(ReadOnlySpan<T> previousBuffer, T val)
        {
            if (CurrentMode == Mode.CopyOnNextAppend || CurrentMode == Mode.InPlace)
            {
                SwitchToCopy(previousBuffer);
            }

            switch (CurrentMode)
            {
                case Mode.Uninitialized:
                    CurrentMode = Mode.Copy;
                    ResizeCopy(1);
                    Copy[0] = val;
                    StartIndex = 0;
                    Length = 1;
                    break;
                case Mode.Copy:
                    if (Copy.Length == Length)
                    {
                        ResizeCopy(Copy.Length * 2);
                    }
                    Copy[Length] = val;
                    Length++;
                    break;
                default:
                    Throw.ImpossibleException($"Unexpected {nameof(Mode)}: {CurrentMode}");
                    break;
            }
        }

        // only call this with a buffer OTHER than the one we're actively reading
        internal void AppendDifferentBuffer(ReadOnlySpan<T> fromBuffer, ReadOnlySpan<T> otherBuffer)
        {
            SwitchToCopy(fromBuffer);

            var desiredSize = Length + otherBuffer.Length;
            if (desiredSize >= Copy.Length)
            {
                ResizeCopy(desiredSize * 2);
            }

            otherBuffer.CopyTo(Copy.Slice(Length));
            Length += otherBuffer.Length;
        }

        // only call this with the buffer we're actively reading from
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
                    Length += length;
                    break;
                default:
                    Throw.ImpossibleException($"Unexpected {nameof(Mode)}: {CurrentMode}");
                    break;
            }
        }

        private void ResizeCopy(int newDesiredSize)
        {
            newDesiredSize = Math.Max(1, newDesiredSize);

            var oldSize = CopyOwner.HasValue ? CopyOwner.Value.Memory.Length : 0;
            var newCopy = Utils.RentMustIncrease(MemoryPool, newDesiredSize, oldSize);

            if (CopyOwner.HasValue)
            {
                Copy.CopyTo(newCopy.Memory.Span);

                CopyOwner.Value.Dispose();
            }

            CopyOwner.Value = newCopy;
        }

        internal void SwitchToCopy(ReadOnlySpan<T> fromBuffer)
        {
            // if we're already copying, nothing to switch to
            if (CurrentMode == Mode.Copy) return;

            if (!CopyOwner.HasValue || Copy.Length < Length)
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
                    Throw.ImpossibleException($"Unexpected {nameof(Mode)}: {CurrentMode}");
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
                    return CopyOwner.Value.Memory.Slice(StartIndex, Length);

                case Mode.Uninitialized:
                    return ReadOnlyMemory<T>.Empty;

                default:
                    Throw.ImpossibleException($"Unexpected {nameof(Mode)}: {CurrentMode}");
                    return default;
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                if (CopyOwner.HasValue)
                {
                    CopyOwner.Value.Dispose();
                    CopyOwner.Clear();
                }
                IsDisposed = true;
            }
        }
    }
}
