using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// Represents and Options and Type pair.
    /// 
    /// Used to create readers and writers.
    /// </summary>
    public interface IBoundConfiguration<TRow>
    {
        /// <summary>
        /// The Options used to create this configuration.
        /// 
        /// If any settings are used that defer decisions (like ReadHeader.Detect)
        ///   this Options object will remain unchanged when a decision is made.  In other words,
        ///   this Options object will always match what was used to create the configuration - it
        ///   is not updated.
        /// </summary>
        Options Options { get; }

        /// <summary>
        /// Create a synchronous reader for the given sequence, converting bytes to characters using the provided encoding.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IReader<TRow> CreateReader(
            [IntentionallyExposedPrimitive("Bytes are the whole point here")]
            ReadOnlySequence<byte> sequence,
            Encoding encoding,
            [NullableExposed("context is truly optional")]
            object? context = null
        );

        /// <summary>
        /// Create a synchronous reader for the given sequence.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IReader<TRow> CreateReader(
            ReadOnlySequence<char> sequence,
            [NullableExposed("context is truly optional")]
            object? context = null
        );

        /// <summary>
        /// Create a synchronous reader for the given reader.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IReader<TRow> CreateReader(
            TextReader reader,
            [NullableExposed("context is truly optional")]
            object? context = null
        );

        /// <summary>
        /// Create an asynchronous reader for the given reader.
        /// 
        /// The provided encoding is used to convert the bytes provided by the reader
        ///   into characters for parsing.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IAsyncReader<TRow> CreateAsyncReader(
            PipeReader reader,
            Encoding encoding,
            [NullableExposed("context is truly optional")]
            object? context = null
        );

        /// <summary>
        /// Create an asynchronous reader for the given reader.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IAsyncReader<TRow> CreateAsyncReader(
            TextReader reader,
            [NullableExposed("context is truly optional")]
            object? context = null
        );

        /// <summary>
        /// Create a synchronous writer for the given writer, convering chars to bytes using the given encoding.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IWriter<TRow> CreateWriter(
            [IntentionallyExposedPrimitive("Bytes are the whole point here")]
            IBufferWriter<byte> writer,
            Encoding encoding,
            [NullableExposed("context is truly optional")]
            object? context = null
        );

        /// <summary>
        /// Create a synchronous writer for the given writer.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IWriter<TRow> CreateWriter(
            IBufferWriter<char> writer,
            [NullableExposed("context is truly optional")]
            object? context = null
        );

        /// <summary>
        /// Create a synchronous writer for the given writer.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IWriter<TRow> CreateWriter(
            TextWriter writer,
            [NullableExposed("context is truly optional")]
            object? context = null
        );

        /// <summary>
        /// Create an asynchronous writer for the given writer.
        /// 
        /// The provided encoding is used to convert characers into 
        ///   bytes.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IAsyncWriter<TRow> CreateAsyncWriter(
            PipeWriter writer,
            Encoding encoding,
            [NullableExposed("context is truly optional")]
            object? context = null
        );

        /// <summary>
        /// Create an asynchronous writer for the given writer.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IAsyncWriter<TRow> CreateAsyncWriter(
            TextWriter writer,
            [NullableExposed("context is truly optional")]
            object? context = null
        );
    }
}
