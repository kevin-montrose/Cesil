namespace Cesil
{
    /// <summary>
    /// Whether or not to write a header row when
    ///   serializing.
    /// </summary>
    public enum WriteHeaders : byte
    {
        /// <summary>
        /// The first row written by the writer will be a header row.
        /// 
        /// Actually writing the row will be deferred until the first
        ///   row is of data is written, or the writer is disposed.
        /// </summary>
        Always = 1,

        /// <summary>
        /// No header row will be written the writer.
        /// </summary>
        Never = 2
    }
}