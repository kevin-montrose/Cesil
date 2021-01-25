using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class CancellationTests
    {
#if DEBUG
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

                            await AssertThrowsAsync<OperationCanceledException>(async () => await i.MoveNextAsync());
                        }
                    }
                }
            );

            static async Task AssertThrowsAsync<T>(Func<Task> task)
                where T : Exception
            {
                Exception exc;
                try
                {
                    await task();
                    exc = null;
                }
                catch (Exception e)
                {
                    exc = e;
                }

                Assert.NotNull(exc);

                if (exc is AggregateException ae)
                {
                    foreach (var a in ae.InnerExceptions)
                    {
                        if (a is T)
                        {
                            return;
                        }
                    }

                    // not the right exception
                    Assert.True(false);
                }
                else
                {
                    // exact match
                    Assert.IsType<T>(exc);
                }
            }
        }
#endif
    }
}
