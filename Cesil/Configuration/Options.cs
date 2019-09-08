using System;
using System.Buffers;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// A set of options for reading and writing CSV files.
    /// 
    /// Combine with Configuration to bind to a particular
    /// type in a Configuration(T) which can create
    /// readers and writers.
    /// </summary>
    public sealed class Options : IEquatable<Options>
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
                .WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose)
                .Build();

        /// <summary>
        /// Default options for dynamic operations:
        ///   - separator = ,
        ///   - row endings = \r\n
        ///   - escaped columns start = "
        ///   - escape character = "
        ///   - assumes headers are present
        ///   - writes headers
        ///   - uses the default type describer
        ///   - uses MemoryPool.Shared
        ///   - uses the default write buffer size
        ///   - does not write a new line after the last row
        ///   - does not support comments
        ///   - uses the default read buffer size
        ///   - dynamic rows are disposed when the reader that returns them is disposed
        /// </summary>
        public static readonly Options DynamicDefault =
            NewEmptyBuilder()
                .WithValueSeparator(',')
                .WithRowEnding(RowEndings.CarriageReturnLineFeed)
                .WithEscapedValueStartAndEnd('"')
                .WithEscapedValueEscapeCharacter('"')
                .WithReadHeader(ReadHeaders.Always)
                .WithWriteHeader(WriteHeaders.Always)
                .WithTypeDescriber(TypeDescribers.Default)
                .WithWriteTrailingNewLine(WriteTrailingNewLines.Never)
                .WithMemoryPool(MemoryPool<char>.Shared)
                .WithWriteBufferSizeHint(null)
                .WithCommentCharacter(null)
                .WithReadBufferSizeHint(0)
                .WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose)
                .Build();

        /// <summary>
        /// Character used to separate two values in a row
        /// 
        /// Typically a comma.
        /// </summary>
        public char ValueSeparator { get; }
        /// <summary>
        /// Character used to start an escaped value.
        /// 
        /// Typically a double quote.
        /// </summary>
        public char EscapedValueStartAndEnd { get; }
        /// <summary>
        /// Character used to escape another character in an
        ///   escaped value.
        ///   
        /// Typically a double quote.
        /// </summary>
        public char EscapedValueEscapeCharacter { get; }
        /// <summary>
        /// The sequence of characters used to end a row.
        /// </summary>
        public RowEndings RowEnding { get; }
        /// <summary>
        /// Whether or not to read headers when reading a CSV.
        /// </summary>
        public ReadHeaders ReadHeader { get; }
        /// <summary>
        /// Whether or not to write headers when writing a CSV.
        /// </summary>
        public WriteHeaders WriteHeader { get; }
        /// <summary>
        /// The instance of ITypeDescriber that will be used to
        ///   discover which columns to read or write, as well
        ///   as the manner of their reading and writing.
        /// </summary>
        public ITypeDescriber TypeDescriber { get; }
        /// <summary>
        /// Whether or not to write a new line after the last row
        /// in a CSV.
        /// </summary>
        public WriteTrailingNewLines WriteTrailingNewLine { get; }
        /// <summary>
        /// Which MemoryPool to use when reading or writing a CSV.
        /// </summary>
        public MemoryPool<char> MemoryPool { get; }
        /// <summary>
        /// Which character, if any, is used to indicate the start
        /// of a comment.
        /// 
        /// Typically not set, but when set often the octothorpe.
        /// </summary>
        public char? CommentCharacter { get; }
        /// <summary>
        /// How big a buffer to request from the MemoryPool for
        ///   buffering write operations.
        ///   
        /// Set to 0 to disable buffering.
        /// 
        /// Set to null to use a default size.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate a size")]
        public int? WriteBufferSizeHint { get; }
        /// <summary>
        /// How big a buffer to request from the MemoryPool for
        ///   servicing read operations.
        ///   
        /// Set to 0 to use a default size.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate a size")]
        public int ReadBufferSizeHint { get; }
        /// <summary>
        /// When to dispose any dynamic rows returned by an IReader or IAsyncReader.
        /// </summary>
        public DynamicRowDisposal DynamicRowDisposal { get; }

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

        /// <summary>
        /// Returns true if this object equals the given Options.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is Options o)
            {
                return Equals(o);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given Options.
        /// </summary>
        public bool Equals(Options opts)
        {
            if (opts == null) return false;

            return
                opts.CommentCharacter == CommentCharacter &&
                opts.DynamicRowDisposal == DynamicRowDisposal &&
                opts.EscapedValueEscapeCharacter == EscapedValueEscapeCharacter &&
                opts.EscapedValueStartAndEnd == EscapedValueStartAndEnd &&
                opts.MemoryPool == MemoryPool &&
                opts.ReadBufferSizeHint == opts.ReadBufferSizeHint &&
                opts.ReadHeader == ReadHeader &&
                opts.RowEnding == opts.RowEnding &&
                opts.TypeDescriber == opts.TypeDescriber &&
                opts.ValueSeparator == opts.ValueSeparator &&
                opts.WriteBufferSizeHint == opts.WriteBufferSizeHint &&
                opts.WriteHeader == opts.WriteHeader &&
                opts.WriteTrailingNewLine == opts.WriteTrailingNewLine;
        }

        /// <summary>
        /// Returns a stable hash for this Options.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(
            CommentCharacter,
            DynamicRowDisposal,
            EscapedValueEscapeCharacter,
            EscapedValueStartAndEnd,
            MemoryPool,
            ReadBufferSizeHint,
            ReadHeader,
            HashCode.Combine(
                RowEnding,
                TypeDescriber,
                ValueSeparator,
                WriteBufferSizeHint,
                WriteHeader,
                WriteTrailingNewLine
            ));

        /// <summary>
        /// Returns a representation of this Options object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append($"{nameof(Options)} with ");
            ret.Append($"{nameof(CommentCharacter)}={CommentCharacter}");
            ret.Append($", {nameof(DynamicRowDisposal)}={DynamicRowDisposal}");
            ret.Append($", {nameof(EscapedValueEscapeCharacter)}={EscapedValueEscapeCharacter}");
            ret.Append($", {nameof(EscapedValueStartAndEnd)}={EscapedValueStartAndEnd}");
            ret.Append($", {nameof(MemoryPool)}={MemoryPool}");
            ret.Append($", {nameof(ReadBufferSizeHint)}={ReadBufferSizeHint}");
            ret.Append($", {nameof(ReadHeader)}={ReadHeader}");
            ret.Append($", {nameof(RowEnding)}={RowEnding}");
            ret.Append($", {nameof(TypeDescriber)}={TypeDescriber}");
            ret.Append($", {nameof(ValueSeparator)}={ValueSeparator}");
            ret.Append($", {nameof(WriteBufferSizeHint)}={WriteBufferSizeHint}");
            ret.Append($", {nameof(WriteHeader)}={WriteHeader}");
            ret.Append($", {nameof(WriteTrailingNewLine)}={WriteTrailingNewLine}");

            return ret.ToString();
        }

        /// <summary>
        /// Compare two Options for equality
        /// </summary>
        public static bool operator ==(Options a, Options b)
        => Utils.NullReferenceEquality(a, b);

        /// <summary>
        /// Compare two Options for inequality
        /// </summary>
        public static bool operator !=(Options a, Options b)
        => !(a == b);
    }
}