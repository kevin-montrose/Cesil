using System;

namespace Cesil
{
    /// <summary>
    /// Holds references to pre-allocated TypeDescibers.
    /// </summary>
    public static class TypeDescribers
    {
        // if position isn't set, equal, otherwise sort positions to the front in ascending order
        internal static readonly Comparison<(DeserializableMember _, int? Position)> DeserializableComparer =
            (a, b) =>
            {
                if (a.Position == null && b.Position == null) return 0;

                if (a.Position == null) return 1;
                if (b.Position == null) return -1;

                return a.Position.Value.CompareTo(b.Position.Value);
            };

        // ditto
        internal static readonly Comparison<(SerializableMember _, int? Position)> SerializableComparer =
            (a, b) =>
            {
                if (a.Position == null && b.Position == null) return 0;

                if (a.Position == null) return 1;
                if (b.Position == null) return -1;

                return a.Position.Value.CompareTo(b.Position.Value);
            };

        /// <summary>
        /// An instance of DefaultTypeDescriber.
        /// 
        /// This instance is used in cases where an ITypeDescriber has not been
        ///   configured.
        /// </summary>
        public static readonly ITypeDescriber Default = new DefaultTypeDescriber();
    }
}
