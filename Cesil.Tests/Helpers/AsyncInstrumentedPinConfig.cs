using System;

namespace Cesil.Tests
{
    internal sealed class AsyncInstrumentedPinConfig<T> : BoundConfigurationBase<T>
    {
        internal readonly BoundConfigurationBase<T> Inner;

        public bool IsUnpinned => StateMachine != null && !StateMachine.IsPinned;

        internal ReaderStateMachine StateMachine;

        public AsyncInstrumentedPinConfig(BoundConfigurationBase<T> inner) : base()
        {
            Inner = inner;
        }

        internal override IAsyncReader<T> CreateAsyncReader(IAsyncReaderAdapter adapter, object context = null)
        {
            var ret = Inner.CreateAsyncReader(adapter, context);
            StateMachine = ((AsyncReaderBase<T>)ret).StateMachine;

            return ret;
        }

        internal override IAsyncWriter<T> CreateAsyncWriter(IAsyncWriterAdapter writer, object context = null)
        => Inner.CreateAsyncWriter(writer, context);

        internal override IReader<T> CreateReader(IReaderAdapter reader, object context = null)
        => throw new NotImplementedException();

        internal override IWriter<T> CreateWriter(IWriterAdapter writer, object context = null)
        => throw new NotImplementedException();
    }
}
