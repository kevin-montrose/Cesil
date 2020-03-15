namespace Cesil
{
    internal interface IDynamicRowOwner
    {
        // todo: check that this still all... actually works if we're not tracking rows for automatic disposal?

        Options Options { get; }

        object? Context { get; }

        int MinimumExpectedColumns { get; }

        void Remove(DynamicRow row);
    }
}
