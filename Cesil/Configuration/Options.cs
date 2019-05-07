using System.Buffers;

namespace Cesil
{
    /// <summary>
    /// A set of options for reading and writing CSV files.
    /// 
    /// Combine with Configuration to bind to a particular
    /// type in a Configuration(T) which can create
    /// readers and writers.
    /// </summary>
    public sealed class Options
    {
        /// <summary>
        /// Default options:
        ///   - separator = ,
        ///   - row endings = \r\n
        ///   - escaped columns start = "
        ///   - escape character = "
        ///   - detects headers when reading
        ///   - writes headers
        ///   - uses the default type describer
        ///   - uses MemoryPool.Shared
        ///   - uses the default write buffer size
        ///   - does not write a new line after the last row
        ///   - does not support comments
        ///   - uses the default read buffer size
        ///   - dynamic rows are disposed when the reader that returns them is disposed
        /// </summary>
        public static readonly Options Default =
            NewEmptyBuilder()
                .WithValueSeparator(',')
                .WithRowEnding(RowEndings.CarriageReturnLineFeed)
                .WithEscapedValueStartAndEnd('"')
                .WithEscapedValueEscapeCharacter('"')
                .WithReadHeader(ReadHeaders.Detect)
                .WithWriteHeader(WriteHeaders.Always)
                .WithTypeDescriber(TypeDescribers.Default)
                .WithWriteTrailingNewLine(WriteTrailingNewLines.Never)
                .WithMemoryPool(MemoryPool<char>.Shared)
                .WithWriteBufferSizeHint(null)
                .WithCommentCharacter(null)
                .WithReadBufferSizeHint(0)
                .WithDynamicTypeConverter(DynamicTypeConverters.Default)
                .WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose)
                .Build();

        /// <summary>
        /// Character used to separate two values in a row
        /// 
        /// Typically a comma.
        /// </summary>
        public char ValueSeparator { get; private set; }
        /// <summary>
        /// Character used to start an escaped value.
        /// 
        /// Typically a double quote.
        /// </summary>
        public char EscapedValueStartAndEnd { get; private set; }
        /// <summary>
        /// Character used to escape another character in an
        ///   escaped value.
        ///   
        /// Typically a double quote.
        /// </summary>
        public char EscapedValueEscapeCharacter { get; private set; }
        /// <summary>
        /// The sequence of characters used to end a row.
        /// </summary>
        public RowEndings RowEnding { get; private set; }
        /// <summary>
        /// Whether or not to read headers when reading a CSV.
        /// </summary>
        public ReadHeaders ReadHeader { get; private set; }
        /// <summary>
        /// Whether or not to write headers when writing a CSV.
        /// </summary>
        public WriteHeaders WriteHeader { get; private set; }
        /// <summary>
        /// The instance of ITypeDescriber that will be used to
        ///   discover which columns to read or write, as well
        ///   as the manner of their reading and writing.
        /// </summary>
        public ITypeDescriber TypeDescriber { get; private set; }
        /// <summary>
        /// Whether or not to write a new line after the last row
        /// in a CSV.
        /// </summary>
        public WriteTrailingNewLines WriteTrailingNewLine { get; private set; }
        /// <summary>
        /// Which MemoryPool to use when reading or writing a CSV.
        /// </summary>
        public MemoryPool<char> MemoryPool { get; private set; }
        /// <summary>
        /// Which character, if any, is used to indicate the start
        /// of a comment.
        /// 
        /// Typically not set, but when set often the octothorpe.
        /// </summary>
        public char? CommentCharacter { get; private set; }
        /// <summary>
        /// How big a buffer to request from the MemoryPool for
        ///   buffering write operations.
        ///   
        /// Set to 0 to disable buffering.
        /// 
        /// Set to null to use a default size.
        /// </summary>
        public int? WriteBufferSizeHint { get; private set; }
        /// <summary>
        /// How big a buffer to request from the MemoryPool for
        ///   servicing read operations.
        ///   
        /// Set to 0 to use a default size.
        /// </summary>
        public int ReadBufferSizeHint { get; private set; }
        /// <summary>
        /// The instance of IDynamicTypeConverter that will be used to
        ///   determine how to convert dynamic rows and cells into
        ///   concrete types.
        /// </summary>
        public IDynamicTypeConverter DynamicTypeConverter { get; private set; }
        /// <summary>
        /// When to dispose any dynamic rows returned by an IReader or IAsyncReader.
        /// </summary>
        public DynamicRowDisposal DynamicRowDisposal { get; private set; }

        internal Options(OptionsBuilder copy)
        {
            ValueSeparator = copy.ValueSeparator;
            EscapedValueStartAndEnd = copy.EscapedValueStartAndEnd;
            EscapedValueEscapeCharacter = copy.EscapedValueEscapeCharacter;
            RowEnding = copy.RowEnding;
            ReadHeader = copy.ReadHeader;
            WriteHeader = copy.WriteHeader;
            TypeDescriber = copy.TypeDescriber;
            WriteTrailingNewLine = copy.WriteTrailingNewLine;
            MemoryPool = copy.MemoryPool;
            CommentCharacter = copy.CommentCharacter;
            WriteBufferSizeHint = copy.WriteBufferSizeHint;
            ReadBufferSizeHint = copy.ReadBufferSizeHint;
            DynamicTypeConverter = copy.DynamicTypeConverter;
            DynamicRowDisposal = copy.DynamicRowDisposal;
        }

        /// <summary>
        /// Create a new, empty, OptionsBuilder.
        /// </summary>
        public static OptionsBuilder NewEmptyBuilder()
        => OptionsBuilder.NewEmptyBuilder();

        /// <summary>
        /// Create a new OptionsBuilder that copies its initial values
        /// from this Options.
        /// </summary>
        public OptionsBuilder NewBuilder() 
        => new OptionsBuilder(this);
    }
}