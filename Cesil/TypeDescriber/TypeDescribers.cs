namespace Cesil
{
    /// <summary>
    /// Holds references to pre-allocated TypeDescibers.
    /// </summary>
    public static class TypeDescribers
    {
        /// <summary>
        /// An instance of DefaultTypeDescriber.
        /// 
        /// This instance is used in cases where an ITypeDescriber has not been
        ///   configured.
        /// </summary>
        public static readonly DefaultTypeDescriber Default = new DefaultTypeDescriber();

        /// <summary>
        /// An instance of AheadOfTimeTypeDescriber.
        /// 
        /// Use this with Cesil.SourceGenerator to avoid runtime code generation.
        /// </summary>
        public static readonly AheadOfTimeTypeDescriber AheadOfTime = new AheadOfTimeTypeDescriber();
    }
}
