// todo: add a test that makes sure nothing implements this in RELEASE builds
namespace Cesil
{
    internal interface ITestableAsyncProvider
    {
        int GoAsyncAfter { set; }
        int AsyncCounter { get; }

        bool ShouldGoAsync();
    }
}
