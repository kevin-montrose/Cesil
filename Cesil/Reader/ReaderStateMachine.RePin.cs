using System;
using System.Threading.Tasks;

namespace Cesil
{
    internal sealed partial class ReaderStateMachine
    {
        internal readonly struct RePin : IDisposable
        {
            private readonly ReaderStateMachine Outer;

            internal RePin(ReaderStateMachine outer)
            {
                Outer = outer;
            }

            public void Dispose()
            {
                Outer.Pin();
            }
        }

        internal unsafe void Pin()
        {
            CharLookupPin = CharacterLookup.Pin(out CharLookup);
            TransitionMatrixHandle = TransitionMatrixMemory.Pin();
            TransitionMatrix = (TransitionRule*)TransitionMatrixHandle.Pointer;
        }

        // it's actually kind of expensive to pin, so we don't want to unpin and repin before
        //   every await IF the await isn't actually going to do anything
        internal RePin? ReleaseAndRePinForAsync<T>(ValueTask<T> task)
        {
            if (task.IsCompletedSuccessfully)
            {
                return null;
            }

            Unpin();

            return new RePin(this);
        }

        internal unsafe void Unpin()
        {
            CharLookup = null;
            CharLookupPin.Dispose();

            TransitionMatrix = null;
            TransitionMatrixHandle.Dispose();
        }
    }
}
