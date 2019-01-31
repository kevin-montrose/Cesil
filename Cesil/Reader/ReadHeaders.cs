namespace Cesil
{
    /// <summary>
    /// Whether to expect a header row when
    ///   deserializing.
    ///   
    /// This can be determined automatically with
    ///   Detect.
    /// </summary>
    public enum ReadHeaders: byte
    {
        /// <summary>
        /// Default value, do not use.
        /// </summary>
        None = 0,

        /// <summary>
        /// Reading will fail if headers are present.
        /// </summary>
        Never,
        /// <summary>
        /// Reading will fail if headers are _not_ present.
        /// </summary>
        Always,
        /// <summary>
        /// Will probe for headers, but will continue if they
        ///   are not present.
        /// </summary>
        Detect
    }
}
