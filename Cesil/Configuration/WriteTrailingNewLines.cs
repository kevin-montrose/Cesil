namespace Cesil
{
    /// <summary>
    /// Whether or not their should be a trailing new line
    ///   after the last row when serializing.
    /// </summary>
    public enum WriteTrailingNewLines: byte
    {
        /// <summary>
        /// Default value, do not use.
        /// </summary>
        None = 0,

        /// <summary>
        /// After the last record is written, always append an additional new line / row ending.
        /// </summary>
        Always,

        /// <summary>
        /// Do not write an additional new line / row ending after the last record is written.
        /// </summary>
        Never
    }
}
