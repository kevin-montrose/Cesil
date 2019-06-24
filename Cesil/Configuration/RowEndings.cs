namespace Cesil
{
    /// <summary>
    /// Which character sequence ends a row.
    /// 
    /// This can be determined automatically with
    ///   Detect.
    /// </summary>
    public enum RowEndings : byte
    {
        /// <summary>
        /// The \r character.
        /// </summary>
        CarriageReturn = 1,
        /// <summary>
        /// The \n character.
        /// </summary>
        LineFeed = 2,
        /// <summary>
        /// \r\n character sequence.
        /// </summary>
        CarriageReturnLineFeed = 3,
        /// <summary>
        /// Will probe the CSV and discover which sequence of characters
        ///    indicates the end of a record.
        /// </summary>
        Detect = 4
    }
}
