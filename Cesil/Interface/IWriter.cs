using System;
using System.Collections.Generic;

namespace Cesil
{
    /// <summary>
    /// Interface for synchronously writing rows.
    /// </summary>
    public interface IWriter<TRow> : IDisposable
    {
        /// <summary>
        /// Write all rows in the provided enumerable.
        /// </summary>
        void WriteAll(IEnumerable<TRow> rows);

        /// <summary>
        /// Write a single row.
        /// </summary>
        void Write(TRow row);

        /// <summary>
        /// Write a comment as a row.
        /// 
        /// Only supported if this IWriter's configuration has a way to indicate comments.
        /// 
        /// If the comment contains the row ending character sequence, it will be written as multiple
        /// comment lines.
        /// </summary>
        void WriteComment(string comment);

        /// <summary>
        /// Write a comment as a row.
        /// 
        /// Only supported if this IWriter's configuration has a way to indicate comments.
        /// 
        /// If the comment contains the row ending character sequence, it will be written as multiple
        /// comment lines.
        /// </summary>
        void WriteComment(ReadOnlySpan<char> comment);
    }
}
