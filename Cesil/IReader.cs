using System;
using System.Collections.Generic;

namespace Cesil
{
    /// <summary>
    /// Interface for a synchronous reader.
    /// </summary>
    public interface IReader<T>: IDisposable
    {
        /// <summary>
        /// Reads all rows into the provided list, returning the entire set at once.
        /// 
        /// into must be non-null.
        /// </summary>
        List<T> ReadAll(List<T> into);

        /// <summary>
        /// Reads all rows, returning the entire set at once.
        /// </summary>
        List<T> ReadAll();

        /// <summary>
        /// Returns an enumerable that will read and yield
        /// one row at a time.
        /// </summary>
        IEnumerable<T> EnumerateAll();

        /// <summary>
        /// Reads a single row, populating row and returning true
        /// if a row was available and false otherwise.
        /// </summary>
        bool TryRead(out T row);

        /// <summary>
        /// Reads a single row into the existing instance of row,
        /// returning true if a row was available and false otherwise.
        /// 
        /// Row will be initialized if need be.
        /// 
        /// Note, it is possible for row to be initialized BUT for this method
        /// to return false.  In that case row should be ignored / discarded.
        /// </summary>
        bool TryReadWithReuse(ref T row);
    }
}
