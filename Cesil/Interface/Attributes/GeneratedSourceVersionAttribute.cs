using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Automatically attached to generated types to track compatibility.
    /// 
    /// You should not use this directly.
    /// </summary>
    public sealed class GeneratedSourceVersionAttribute: Attribute
    {
        /// <summary>
        /// The version of Cesil the associated source was generated for.
        /// 
        /// You should not use this directly.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// The type associated with generated code.
        /// 
        /// You should not use this directly.
        /// </summary>
        public TypeInfo ForType { get; }

        /// <summary>
        /// Construct a new GeneratedSourceVersionAttribute.
        /// </summary>
        public GeneratedSourceVersionAttribute(string version, Type forType)
        {
            Version = version;
            ForType = forType.GetTypeInfo();
        }
    }
}
