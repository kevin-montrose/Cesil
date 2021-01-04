﻿namespace Cesil
{
    /// <summary>
    /// Whether or not a member is required during deserialization.
    /// </summary>
    public enum MemberRequired : byte
    {
        /// <summary>
        /// A member must be present, it is
        /// an error to omit it.
        /// </summary>
        Yes = 1,

        /// <summary>
        /// A member does not have to be present,
        /// it is not an error if it is omitted.
        /// </summary>
        No = 2
    }
}
