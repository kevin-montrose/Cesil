namespace Cesil
{
    /// <summary>
    /// How to behave if a SurrogateTypeDescriber needs to
    ///   describe a type that isn't explicitly configured.
    /// </summary>
    public enum SurrogateTypeDescriberFallbackBehavior : byte
    {
        /// <summary>
        /// Throw if no type is configured.
        /// </summary>
        Throw = 1,
        /// <summary>
        /// Use a fallback ITypeDescriber if no type is configured.
        /// </summary>
        UseFallback = 2
    }
}
