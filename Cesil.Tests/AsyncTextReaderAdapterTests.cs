using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Cesil.Tests
{
    public class AsyncTextReaderAdapterTests
    {
        private sealed class _NonAsyncDisposalAsync : TextReader
        {
            public bool DisposeCalled { get;private set;}

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    DisposeCalled = true;
                }
            }
        }

        [Fact]
        public async Task NonAsyncDisposalAsync()
        {
            var d = new _NonAsyncDisposalAsync();
            var dAsObj = (object)d;
            Assert.False(dAsObj is IAsyncDisposable);

            await using (var wrapper = new AsyncTextReaderAdapter(d)) { }

            Assert.True(d.DisposeCalled);
        }

        private sealed class _AsyncDisposalAsync : TextReader, IAsyncDisposable
        {
            public bool DisposeCalled { get; private set; }
            public bool DisposeAsyncCalled { get; private set; }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    DisposeCalled = true;
                }
            }

            public ValueTask DisposeAsync()
            {
                DisposeAsyncCalled = true;

                return default;
            }
        }

        [Fact]
        public async Task AsyncDisposalAsync()
        {
            var d = new _AsyncDisposalAsync();
            var dAsObj = (object)d;
            Assert.True(dAsObj is IAsyncDisposable);

            await using (var wrapper = new AsyncTextReaderAdapter(d)) { }

            Assert.False(d.DisposeCalled);
            Assert.True(d.DisposeAsyncCalled);
        }
    }
}
