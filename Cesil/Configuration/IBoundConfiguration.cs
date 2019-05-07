﻿using System.IO;

namespace Cesil
{
    /// <summary>
    /// Represents and Options and Type pair.
    /// 
    /// Used to create readers and writers.
    /// </summary>
    public interface IBoundConfiguration<T>
    {
        /// <summary>
        /// Create a synchronous reader for the given reader.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IReader<T> CreateReader(TextReader reader, object context = null);
        /// <summary>
        /// Create an asynchronous reader for the given reader.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IAsyncReader<T> CreateAsyncReader(TextReader reader, object context = null);
        /// <summary>
        /// Create a synchronous writer for the given writer.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IWriter<T> CreateWriter(TextWriter writer, object context = null);
        /// <summary>
        /// Create an asynchronous writer for the given writer.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IAsyncWriter<T> CreateAsyncWriter(TextWriter writer, object context = null);
    }
}
