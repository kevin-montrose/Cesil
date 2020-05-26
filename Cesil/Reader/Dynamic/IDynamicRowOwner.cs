namespace Cesil
{
    internal interface IDynamicRowOwner : IDelegateCache
    {
        Options Options { get; }

        object? Context { get; }

        void Remove(DynamicRow row);

        NameLookup AcquireNameLookup();
        void ReleaseNameLookup();
    }
}
