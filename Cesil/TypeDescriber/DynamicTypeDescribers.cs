namespace Cesil
{
    /// <summary>
    /// Holds references to pre-allocated DynamicTypeConverters.
    /// </summary>
    public static class DynamicTypeConverters
    {
        /// <summary>
        /// An instance of DefaultDynamicTypeConverter.
        /// 
        /// This instance is used in cases where an IDynamicTypeConverter has not been
        ///   configured.
        /// </summary>
        public static readonly IDynamicTypeConverter Default = new DefaultDynamicTypeConverter();
    }
}
