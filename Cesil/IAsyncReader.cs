using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cesil
{
    /// <summary>
    /// Interface for an asynchronous reader.
    /// </summary>
    public interface IAsyncReader<T>: IAsyncDisposable
    {
        /// <summary>
        /// Returns an async enumerable that will read and yield
        /// one row at a time.
        /// 
        /// The enumerable will attempt to complete synchronously,
        /// but will not block if results are not available.
        /// </summary>
        IAsyncEnumerable<T> EnumerateAllAsync();

        /// <summary>
        /// Asynchronously reads all rows, storing into the provided list.
        /// 
        /// into must be non-null, and will be returned wrapped in a ValueTask.
        /// 
        /// The task will attempt to complete synchronously, 
        /// but will not block if results are not available.
        /// </summary>
        ValueTask<List<T>> ReadAllAsync(List<T> into, CancellationToken cancel = default);

        /// <summary>
        /// Asynchronously reads all rows, returning the entire set at once.
        /// 
        /// The task will attempt to complete synchronously, 
        /// but will not block if results are not available.
        /// </summary>
        ValueTask<List<T>> ReadAllAsync(CancellationToken cancel = default);

        /// <summary>
        /// Try to read a single row asynchronously, returning a ReadResult that 
        /// indicates success or failure.
        /// 
        /// The task will attempt to complete synchronously, 
        /// but will not block if results are not available.
        /// </summary>
        ValueTask<ReadResult<T>> TryReadAsync(CancellationToken cancel = default);

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
        ValueTask<ReadResult<T>> TryReadWithReuseAsync(ref T row, CancellationToken cancel = default);
    }
}
