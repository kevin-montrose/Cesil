namespace Cesil
{
    internal interface IDynamicRowOwner
    {
        Options Options { get; }

        object? Context { get; }

        void Remove(DynamicRow row);
    }
}
