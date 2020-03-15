namespace Cesil
{
    internal sealed class EmptyDynamicRowOwner : IDynamicRowOwner
    {
        public static readonly IDynamicRowOwner Singleton = new EmptyDynamicRowOwner();

        public Options Options => Throw.Exception<Options>("Shouldn't be possible");

        public object? Context => Throw.Exception<object?>("Shouldn't be possible");

        public int MinimumExpectedColumns => Throw.Exception<int>("Shouldn't be possible");

        private EmptyDynamicRowOwner() { }

        public void Remove(DynamicRow row)
        => Throw.Exception<object>("Shouldn't be possible");
    }
}
