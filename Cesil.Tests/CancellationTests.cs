using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class CancellationTests
    {
        // todo: need to test all the cancellation points... somehow?

        [Fact]
        public async Task AsyncEnumeratorAsync()
        {
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("hello,world\r\n1,foo\r\n2,bar"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var token = new CancellationTokenSource();

                        var e = csv.EnumerateAllAsync();
                        await using (var i = e.GetAsyncEnumerator(token.Token))
                        {
                            Assert.True(await i.MoveNextAsync());

                            var r1 = i.Current;
                            Assert.Equal(1, (int)r1.hello);
                            Assert.Equal("foo", (string)r1.world);

                            token.Cancel();

                            await Assert.ThrowsAsync<OperationCanceledException>(async () => await i.MoveNextAsync());
                        }
                    }
                }
            );
        }
    }
}
