namespace Cesil
{
    /// <summary>
    /// Whether to expect a header row when
    ///   deserializing.
    ///   
    /// This can be determined automatically with
    ///   Detect.
    /// </summary>
    public enum ReadHeader : byte
    {
        /// <summary>
        /// Reading will fail if headers are present.
        /// </summary>
        Never = 1,
        /// <summary>
        /// Reading will fail if headers are _not_ present.
        /// </summary>
        Always = 2,
        /// <summary>
        /// Will probe for headers, but will continue if they
        ///   are not present.
        /// </summary>
        Detect = 3
    }
}
