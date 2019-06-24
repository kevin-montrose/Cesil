namespace Cesil
{

    /// <summary>
    /// Indicates what a reader is doing when
    ///   a ReadContext is created.
    /// </summary>
    public enum ReadContextMode : byte
    {
        /// <summary>
        /// A single column is being read.
        /// </summary>
        ReadingColumn = 1,

        /// <summary>
        /// A single column is being converted,
        /// occurs only during dynamic deserialization.
        /// </summary>
        ConvertingColumn = 2,

        /// <summary>
        /// A whole row is being converted,
        /// occurs only during dynamic deserialization.
        /// </summary>
        ConvertingRow = 3
    }
}
