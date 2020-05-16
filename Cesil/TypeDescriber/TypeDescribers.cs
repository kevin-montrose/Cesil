using System;

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
        public static readonly ITypeDescriber Default = new DefaultTypeDescriber();
    }
}
