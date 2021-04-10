namespace Cesil
{
    /// <summary>
    /// Which character sequence will end a row when writing rows.
    /// 
    /// Unlike with ReadRowEnding, this cannot be inferred.
    /// </summary>
    public enum WriteRowEnding : byte
    {
        /// <summary>
        /// \r\n character sequence.
        /// </summary>
        CarriageReturnLineFeed = 1,
        /// <summary>
        /// The \n character.
        /// </summary>
        LineFeed = 2,
        /// <summary>
        /// The \r character.
        /// </summary>
        CarriageReturn = 3
    }
}
