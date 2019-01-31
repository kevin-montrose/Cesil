using System;
using System.Collections.Generic;

namespace Cesil
{
    /// <summary>
    /// Interface for synchronously writing rows.
    /// </summary>
    public interface IWriter<T>: IDisposable
    {
        /// <summary>
        /// Write all rows in the provided enumerable.
        /// </summary>
        void WriteAll(IEnumerable<T> rows);

        /// <summary>
        /// Write a single row.
        /// </summary>
        void Write(T row);
    }
}
