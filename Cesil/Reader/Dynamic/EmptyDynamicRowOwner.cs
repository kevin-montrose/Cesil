namespace Cesil
{
    internal sealed class EmptyDynamicRowOwner : IDynamicRowOwner
    {
        internal static readonly IDynamicRowOwner Singleton = new EmptyDynamicRowOwner();

        public Options Options => Throw.ImpossibleException<Options>("Shouldn't be possible");

        public object? Context => Throw.ImpossibleException<object?>("Shouldn't be possible");

        public int MinimumExpectedColumns => Throw.ImpossibleException<int>("Shouldn't be possible");

        private EmptyDynamicRowOwner() { }

        public void Remove(DynamicRow row)
        => Throw.ImpossibleException<object>("Shouldn't be possible");

        public NameLookup AcquireNameLookup()
        => Throw.ImpossibleException<NameLookup>("Shouldn't be possible");

        public void ReleaseNameLookup()
        => Throw.ImpossibleException<object>("Shouldn't be possible");
    }
}
