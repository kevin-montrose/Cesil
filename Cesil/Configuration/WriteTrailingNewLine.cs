namespace Cesil
{
    /// <summary>
    /// Whether or not their should be a trailing new line
    ///   after the last row when serializing.
    /// </summary>
    public enum WriteTrailingNewLine : byte
    {
        /// <summary>
        /// After the last record is written, always append an additional new line / row ending.
        /// </summary>
        Always = 1,

        /// <summary>
        /// Do not write an additional new line / row ending after the last record is written.
        /// </summary>
        Never = 2
    }
}
