namespace Cesil
{
    /// <summary>
    /// Indicates what a writer is doing when
    ///   a WriteContext is created.
    /// </summary>
    public enum WriteContextMode : byte
    {
        /// <summary>
        /// A writer is discovering columns for a table, used during
        ///   dynamic serialization to determine headers.
        ///   
        /// Neither columns nor rows are specified during this operation.
        /// </summary>
        DiscoveringColumns = 1,

        /// <summary>
        /// A writer is discovering cells in a row, used during
        ///   dynamic serialization to determine per-row values.
        ///   
        /// Only a row is specified during this operation.
        /// </summary>
        DiscoveringCells = 2,

        /// <summary>
        /// A writer is writing a single column.
        /// 
        /// Both a row and a column are specified during this operation.
        /// </summary>
        WritingColumn = 3
    }
}
