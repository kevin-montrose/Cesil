namespace Cesil
{
    internal interface ITestableAsyncProvider
    {
        int GoAsyncAfter { set; }
        int AsyncCounter { get; }

        bool ShouldGoAsync();
    }
}
