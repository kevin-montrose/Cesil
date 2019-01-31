namespace Cesil
{
    /// <summary>
    /// Which character sequence ends a row.
    /// 
    /// This can be determined automatically with
    ///   Detect.
    /// </summary>
    public enum RowEndings: byte
    {
        /// <summary>
        /// Default value, do not use.
        /// </summary>
        None = 0,

        /// <summary>
        /// The \r character.
        /// </summary>
        CarriageReturn,
        /// <summary>
        /// The \n character.
        /// </summary>
        LineFeed,
        /// <summary>
        /// \r\n character sequence.
        /// </summary>
        CarriageReturnLineFeed,
        /// <summary>
        /// Will probe the CSV and discover which sequence of characters
        ///    indicates the end of a record.
        /// </summary>
        Detect
    }
}
