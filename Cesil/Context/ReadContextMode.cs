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
        /// A row is being read, but a particular 
        /// column hasn't been encountered yet.
        /// </summary>
        ReadingRow = 2,

        /// <summary>
        /// A single column is being converted,
        /// occurs only during dynamic deserialization.
        /// </summary>
        ConvertingColumn = 3,

        /// <summary>
        /// A whole row is being converted,
        /// occurs only during dynamic deserialization.
        /// </summary>
        ConvertingRow = 4
    }
}
