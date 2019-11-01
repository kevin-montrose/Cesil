using System;
using System.IO;

namespace Cesil.Tests
{
    internal sealed class AsyncCountingAndForcingConfig<T> : BoundConfigurationBase<T>, ITestableAsyncProvider
    {
        public object Inner => InnerConfig;

        public int GoAsyncAfter { set; private get; }

        public int AsyncCounter => Single?.AsyncCounter ?? -1;

        private readonly BoundConfigurationBase<T> InnerConfig;

        private ITestableAsyncProvider Single;

        public AsyncCountingAndForcingConfig(BoundConfigurationBase<T> inner) : 
            base(inner.TypeDescriber.HasValue ? inner.TypeDescriber.Value : null, inner.ValueSeparator, inner.EscapedValueStartAndStop, inner.EscapeValueEscapeChar, inner.RowEnding, inner.ReadHeader, inner.WriteHeader, inner.WriteTrailingNewLine, inner.MemoryPool, inner.CommentChar, inner.WriteBufferSizeHint, inner.ReadBufferSizeHint, inner.DynamicRowDisposal)
        {
            InnerConfig = inner;
            GoAsyncAfter = -1;
        }

        internal override IAsyncReader<T> CreateAsyncReader(IAsyncReaderAdapter reader, object context = null)
        {
            var ret = InnerConfig.CreateAsyncReader(reader, context);
            Set(ret as ITestableAsyncProvider);

            return ret;
        }

        public bool ShouldGoAsync()
        => throw new NotImplementedException();

        internal override IAsyncWriter<T> CreateAsyncWriter(IAsyncWriterAdapter writer, object context = null)
        {
            var ret = InnerConfig.CreateAsyncWriter(writer, context);
            Set(ret as ITestableAsyncProvider);

            return ret;
        }

        public void Set(object providerObj)
        {
            var provider = providerObj as ITestableAsyncProvider;
            if (provider == null) return;

            Single = provider;
            if (GoAsyncAfter >= 0)
            {
                provider.GoAsyncAfter = GoAsyncAfter;
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
