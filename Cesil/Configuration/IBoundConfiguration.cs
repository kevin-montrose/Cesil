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
    public interface IBoundConfiguration<T>
    {
        /// <summary>
        /// Create a synchronous reader for the given sequence, converting bytes to characters using the provided encoding.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IReader<T> CreateReader(
            [IntentionallyExposedPrimitive("Bytes are the whole point here")] 
            ReadOnlySequence<byte> sequence, 
            Encoding encoding,
            object context = null
        );
        /// <summary>
        /// Create a synchronous reader for the given sequence.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IReader<T> CreateReader(ReadOnlySequence<char> sequence, object context = null);
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
        /// The provided encoding is used to convert the bytes provided by the reader
        ///   into characters for parsing.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IAsyncReader<T> CreateAsyncReader(PipeReader reader, Encoding encoding, object context = null);
        /// <summary>
        /// Create an asynchronous reader for the given reader.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on ReadContext.
        /// </summary>
        IAsyncReader<T> CreateAsyncReader(TextReader reader, object context = null);

        /// <summary>
        /// Create a synchronous writer for the given writer, convering chars to bytes using the given encoding.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IWriter<T> CreateWriter(
            [IntentionallyExposedPrimitive("Bytes are the whole point here")] 
            IBufferWriter<byte> writer,
            Encoding encoding, 
            object context = null
        );
        /// <summary>
        /// Create a synchronous writer for the given writer.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IWriter<T> CreateWriter(IBufferWriter<char> writer, object context = null);
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
        /// The provided encoding is used to convert characers into 
        ///   bytes.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IAsyncWriter<T> CreateAsyncWriter(PipeWriter writer, Encoding encoding, object context = null);
        /// <summary>
        /// Create an asynchronous writer for the given writer.
        /// 
        /// Takes an optional context object which is made available
        ///   during certain operations as a member on WriteContext.
        /// </summary>
        IAsyncWriter<T> CreateAsyncWriter(TextWriter writer, object context = null);
    }
}
