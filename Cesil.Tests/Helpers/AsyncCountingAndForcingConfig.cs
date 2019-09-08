using System;
using System.IO;

namespace Cesil.Tests
{
    internal sealed class AsyncCountingAndForcingConfig<T> : IBoundConfiguration<T>, ITestableAsyncProvider
    {
        public object Inner => InnerConfig;

        public int GoAsyncAfter { set; private get; }

        public int AsyncCounter => (Single as ITestableAsyncProvider)?.AsyncCounter ?? -1;

        private readonly IBoundConfiguration<T> InnerConfig;
        private IAsyncReader<T> Single;
        public AsyncCountingAndForcingConfig(IBoundConfiguration<T> inner)
        {
            InnerConfig = inner;
            GoAsyncAfter = -1;
        }

        public IAsyncReader<T> CreateAsyncReader(TextReader reader, object context = null)
        {
            Single = InnerConfig.CreateAsyncReader(reader, context);
            if(GoAsyncAfter >= 0)
            {
                (Single as ITestableAsyncProvider).GoAsyncAfter = GoAsyncAfter;
            }

            return Single;
        }

        public bool ShouldGoAsync()
        => throw new NotImplementedException();

        public IAsyncWriter<T> CreateAsyncWriter(TextWriter writer, object context = null)
        {
            throw new NotImplementedException();
        }

        public IReader<T> CreateReader(TextReader reader, object context = null)
        => throw new InvalidOperationException("No sync");

        public IWriter<T> CreateWriter(TextWriter writer, object context = null)
        => throw new InvalidOperationException("No sync");
    }
}
