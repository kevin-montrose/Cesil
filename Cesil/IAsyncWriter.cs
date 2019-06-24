using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    /// <summary>
    /// Interface for writing rows asynchronously.
    /// </summary>
    public interface IAsyncWriter<T> : IAsyncDisposable
    {
        /// <summary>
        /// Writes all rows enumerated by the given IAsyncEnumerable.
        /// 
        /// Will complete synchronously if possible, but will not block
        /// if all rows are not immediately available or if the underlying
        /// sink does not complete immediately.
        /// </summary>
        ValueTask WriteAllAsync(IAsyncEnumerable<T> rows, CancellationToken cancel = default);

        /// <summary>
        /// Writes all rows enumerated by the given IEnumerable.
        /// 
        /// Will complete synchronously if possible, but will not block
        /// if the underlying sink does not complete immediately.
        /// </summary>
        ValueTask WriteAllAsync(IEnumerable<T> rows, CancellationToken cancel = default);

        /// <summary>
        /// Write a single row.
        /// 
        /// Will complete synchronously if possible, but will not block
        /// if the underlying sink does not complete immediately.
        /// </summary>
        ValueTask WriteAsync(T row, CancellationToken cancel = default);

        /// <summary>
        /// Write a comment as a row.
        /// 
        /// Only supported if this IWriter's configuration has a way to indicate comments.
        /// 
        /// If the comment contains the row ending character sequence, it will be written as multiple
        /// comment lines.
        /// 
        /// Will complete synchronously if possible, but will not block
        /// if the underlying sink does not complete immediately.
        /// </summary>
        ValueTask WriteCommentAsync(string comment, CancellationToken cancel = default);
    }
}
