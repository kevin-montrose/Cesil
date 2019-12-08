namespace Cesil
{
    /// <summary>
    /// Whether or not their should be a trailing row ending
    ///   after the last row when serializing.
    /// </summary>
    public enum WriteTrailingRowEnding : byte
    {
        /// <summary>
        /// After the last row is written, always append an additional row ending.
        /// </summary>
        Always = 1,

        /// <summary>
        /// Do not write an additional row ending after the last row is written.
        /// </summary>
        Never = 2
    }
}
