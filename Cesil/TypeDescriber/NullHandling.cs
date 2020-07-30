namespace Cesil
{
    /// <summary>
    /// Controls how null values encountered at runtime will be handled.
    /// </summary>
    public enum NullHandling: byte
    {
        /// <summary>
        /// Null is a legal value.
        /// 
        /// Note that non-nullable values types can never be null
        /// at runtime, and thus all values (including default) will be
        /// permitted.
        /// </summary>
        AllowNull = 1,

        /// <summary>
        /// Null is not a legal value.  If null is encountered at runtime,
        /// an exception will be raised.
        /// 
        /// Note that non-nullable values types can never be null
        /// at runtime, and thus all values (including default) will be
        /// permitted.
        /// </summary>
        ForbidNull = 2
    }
}
