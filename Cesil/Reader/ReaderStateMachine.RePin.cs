using System;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed partial class ReaderStateMachine
    {
        internal struct PinHandle : IDisposable
        {
            private bool Pinned;
            private readonly ReaderStateMachine Outer;

            internal PinHandle(ReaderStateMachine outer)
            {
                Outer = outer;
                Pinned = true;
            }

            public void Dispose()
            {
                if (Pinned)
                {
                    Outer.Unpin();
                    Pinned = false;
                }
            }
        }

        private unsafe void PinInner()
        {
            CharLookupPin = CharacterLookup.Pin(out CharLookup);
            TransitionMatrixHandle = TransitionMatrixMemory.Pin();
            TransitionMatrix = (TransitionRule*)TransitionMatrixHandle.Pointer;
        }

        internal PinHandle Pin()
        {
            PinInner();

            return new PinHandle(this);
        }

        // it's actually kind of expensive to pin, so we don't want to unpin and re-pin before
        //   every await IF the await isn't actually going to do anything
        internal void ReleasePinForAsync<T>(ValueTask<T> task)
        {
#pragma warning disable CES0001 // this isn't actually a transition point, we care what _actually_ has happened
            if (task.IsCompletedSuccessfully)
            {
                return;
            }
#pragma warning restore CES0001

            Unpin();
        }

        private unsafe void Unpin()
        {
            CharLookup = null;
            CharLookupPin.Dispose();

            TransitionMatrix = null;
            TransitionMatrixHandle.Dispose();
        }
    }
}
