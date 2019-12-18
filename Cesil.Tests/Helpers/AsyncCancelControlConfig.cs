
using System;

namespace Cesil.Tests
{
    internal sealed class AsyncCancelControlConfig<T> : BoundConfigurationBase<T>, ITestableAsyncProvider
    {
        public object Inner => InnerConfig;

        public int DoCancelAfter { set; private get; }

        public int AsyncCounter => SingleAsync?.AsyncCounter ?? -1;

        public int CancelCounter => SingleCancel?.CancelCounter ?? -1;

        public int GoAsyncAfter { set => throw new NotImplementedException(); }

        public PoisonType? Poison => Single.Poison;

        private readonly BoundConfigurationBase<T> InnerConfig;

        private PoisonableBase Single;
        private ITestableAsyncProvider SingleAsync;
        private ITestableCancellableProvider SingleCancel;

        public AsyncCancelControlConfig(BoundConfigurationBase<T> inner) :
            base(
                inner.Options
            )
        {
            InnerConfig = inner;
            DoCancelAfter = -1;
        }

        internal override IAsyncReader<T> CreateAsyncReader(IAsyncReaderAdapter reader, object context = null)
        {
            var ret = InnerConfig.CreateAsyncReader(reader, context);
            Set(ret);

            return ret;
        }

        public bool ShouldGoAsync()
        => throw new NotImplementedException();

        internal override IAsyncWriter<T> CreateAsyncWriter(IAsyncWriterAdapter writer, object context = null)
        {
            var ret = InnerConfig.CreateAsyncWriter(writer, context);
            Set(ret);

            return ret;
        }

        public void Set(object providerObj)
        {
            Single = (PoisonableBase)providerObj;

            // force everything to go async
            if (providerObj is ITestableAsyncProvider providerAsync)
            {
                SingleAsync = providerAsync;
                SingleAsync.GoAsyncAfter = 0;
            }
            
            if (providerObj is ITestableCancellableProvider providerCancel)
            {
                SingleCancel = providerCancel;
                if(DoCancelAfter >= 0)
                {
                    SingleCancel.CancelAfter = DoCancelAfter;
                }
            }
        }

        internal override IReader<T> CreateReader(IReaderAdapter reader, object context = null)
        {
            throw new NotImplementedException("No sync reader");
        }

        internal override IWriter<T> CreateWriter(IWriterAdapter reader, object context = null)
        {
            throw new NotImplementedException("No sync writer");
        }
    }
}
