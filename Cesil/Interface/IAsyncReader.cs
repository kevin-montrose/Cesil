﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    /// <summary>
    /// Interface for an asynchronous reader.
    /// </summary>
    public interface IAsyncReader<TRow> : IAsyncDisposable
    {
        /// <summary>
        /// Returns an async enumerable that will read and yield
        /// one row at a time.
        /// 
        /// The enumerable will attempt to complete synchronously,
        /// but will not block if results are not available.
        /// 
        /// The returned IAsyncEnumerable(TRow) may only be enumerated once.
        /// </summary>
        IAsyncEnumerable<TRow> EnumerateAllAsync();

        /// <summary>
        /// Asynchronously reads all rows, storing into the provided collection.
        /// 
        /// into must be non-null, and will be returned wrapped in a ValueTask.
        /// 
        /// The task will attempt to complete synchronously, 
        /// but will not block if results are not available.
        /// </summary>
        ValueTask<TCollection> ReadAllAsync<TCollection>(TCollection into, CancellationToken cancellationToken = default)
        where TCollection : class, ICollection<TRow>;

        /// <summary>
        /// Asynchronously reads all rows into a list, returning the entire set at once.
        /// 
        /// The task will attempt to complete synchronously, 
        /// but will not block if results are not available.
        /// </summary>
        ValueTask<List<TRow>> ReadAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Try to read a single row asynchronously, returning a ReadResult that 
        /// indicates success or failure.
        /// 
        /// The task will attempt to complete synchronously, 
        /// but will not block if results are not available.
        /// </summary>
        ValueTask<ReadResult<TRow>> TryReadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a single row into the existing instance of row,
        /// returning a ReadResult that indicates success or failure.
        /// 
        /// If need be, row will be initialized before this method returns - 
        /// it will remain in use until the returned ValueTask completes.
        /// 
        /// Note, it is possible for row to be initialized BUT for the ReadResult
        /// to indicate failure.  In that case row should be ignored / discarded.
        /// 
        /// The task will attempt to complete synchronously, 
        /// but will not block if results are not available.
        /// </summary>
        ValueTask<ReadResult<TRow>> TryReadWithReuseAsync(ref TRow row, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a single row or comment.
        /// 
        /// Distinguish between a row, comment, or nothing by inspecting 
        /// ReadWithCommentResult(T).ResultType.
        /// 
        /// Note, it is possible for row to be initialized BUT for this method
        /// to return a comment or no value.  In that case row should be ignored.
        /// 
        /// The task will attempt to complete synchronously, 
        /// but will not block if results are not available.
        /// </summary>
        ValueTask<ReadWithCommentResult<TRow>> TryReadWithCommentAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a single row (storing into an existing instance of a row
        /// if provided) or comment.
        ///
        /// Distinguish between a row, comment, or nothing by inspecting 
        /// ReadWithCommentResult(T).ResultType.
        /// 
        /// Row will be initialized if need be.
        /// 
        /// Note, it is possible for row to be initialized BUT for this method
        /// to return a comment or no value.  In that case row should be ignored.
        /// 
        /// The task will attempt to complete synchronously, 
        /// but will not block if results are not available.
        /// </summary>
        ValueTask<ReadWithCommentResult<TRow>> TryReadWithCommentReuseAsync(ref TRow row, CancellationToken cancellationToken = default);
    }
}
