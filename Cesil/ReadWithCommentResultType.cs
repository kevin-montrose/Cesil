namespace Cesil
{
    /// <summary>
    /// Represents the type of a ReadWithCommentResult,
    /// either no value (end of records), having a value (of type T),
    /// or having a comment (always a string).
    /// </summary>
    public enum ReadWithCommentResultType : byte
    {
        /// <summary>
        /// No value was read, the end of records has been reached.
        /// 
        /// Subsequent attempts to read will fail.
        /// 
        /// Equivalent to HasValue == false on ReadResult(T).
        /// </summary>
        NoValue = 1,
        /// <summary>
        /// A value was read.
        /// 
        /// Equivalent to HasValue == true on ReadResult(T).
        /// </summary>
        HasValue = 2,
        /// <summary>
        /// A comment was read.
        /// </summary>
        HasComment = 3
    }
}
