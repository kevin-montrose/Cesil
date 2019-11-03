namespace Cesil
{
    internal readonly struct CachedDelegate<T>
        where T : class
    {
        public static readonly CachedDelegate<T> Empty = new CachedDelegate<T>();

        public readonly NonNull<T> Value;

        public CachedDelegate(T? value)
        {
            Value = default;
            Value.SetAllowNull(value);
        }
    }
}
