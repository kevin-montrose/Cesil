namespace Cesil
{
    internal readonly struct CachedDelegate<T>
        where T : class
    {
        internal static readonly CachedDelegate<T> Empty = new CachedDelegate<T>();

        internal readonly NonNull<T> Value;

        internal CachedDelegate(T? value)
        {
            Value = default;
            Value.SetAllowNull(value);
        }
    }
}
