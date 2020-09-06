using System;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// A set of options for reading and writing CSV files.
    /// 
    /// Combine with Configuration to bind to a particular
    /// type in a IBoundConfiguration(T) which can create
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
        ///   - uses MemoryPoolProviders.Default
        ///   - uses the default write buffer size
        ///   - does not write a new line after the last row
        ///   - does not support comments
        ///   - uses the default read buffer size
        ///   - dynamic rows are disposed when the reader that returns them is disposed
        ///   - whitespace is preserved
        ///   - extra columns are ignored
        ///   - uses ArrayPool.Shared
        /// </summary>
        public static readonly Options Default =
            CreateBuilder()
                .WithValueSeparator(",")
                .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                .WithEscapedValueStartAndEnd('"')
                .WithEscapedValueEscapeCharacter('"')
                .WithReadHeader(ReadHeader.Detect)
                .WithWriteHeader(WriteHeader.Always)
                .WithTypeDescriber(TypeDescribers.Default)
                .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Never)
                .WithMemoryPoolProvider(MemoryPoolProviders.Default)
                .WithWriteBufferSizeHint(null)
                .WithCommentCharacter(null)
                .WithReadBufferSizeHint(0)
                .WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose)
                .WithWhitespaceTreatment(WhitespaceTreatments.Preserve)
                .WithExtraColumnTreatment(ExtraColumnTreatment.Ignore)
                .ToOptions();

        /// <summary>
        /// Default options for dynamic operations:
        ///   - separator = ,
        ///   - row endings = \r\n
        ///   - escaped columns start = "
        ///   - escape character = "
        ///   - assumes headers are present
        ///   - writes headers
        ///   - uses the default type describer
        ///   - uses MemoryPoolProviders.Default
        ///   - uses the default write buffer size
        ///   - does not write a new line after the last row
        ///   - does not support comments
        ///   - uses the default read buffer size
        ///   - dynamic rows are disposed when the reader that returns them is disposed
        ///   - extra columns are included, accessible via index
        /// </summary>
        public static readonly Options DynamicDefault =
            CreateBuilder()
                .WithValueSeparator(",")
                .WithRowEnding(RowEnding.CarriageReturnLineFeed)
                .WithEscapedValueStartAndEnd('"')
                .WithEscapedValueEscapeCharacter('"')
                .WithReadHeader(ReadHeader.Always)
                .WithWriteHeader(WriteHeader.Always)
                .WithTypeDescriber(TypeDescribers.Default)
                .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Never)
                .WithMemoryPoolProvider(MemoryPoolProviders.Default)
                .WithWriteBufferSizeHint(null)
                .WithCommentCharacter(null)
                .WithReadBufferSizeHint(0)
                .WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose)
                .WithWhitespaceTreatment(WhitespaceTreatments.Preserve)
                .WithExtraColumnTreatment(ExtraColumnTreatment.IncludeDynamic)
                .ToOptions();

        /// <summary>
        /// Character used to separate two values in a row
        /// 
        /// Typically a comma.
        /// </summary>
        public string ValueSeparator { get; }
        /// <summary>
        /// Character used to start an escaped value.
        /// 
        /// Typically a double quote, but can be null for some formats.
        /// </summary>
        public char? EscapedValueStartAndEnd { get; }
        /// <summary>
        /// Character used to escape another character in an
        ///   escaped value.
        ///   
        /// Typically a double quote, but can be null for some formats and
        ///   will be null for formats without an EscapedValueStartAndEnd.
        /// </summary>
        public char? EscapedValueEscapeCharacter { get; }
        /// <summary>
        /// The sequence of characters used to end a row.
        /// </summary>
        public RowEnding RowEnding { get; }
        /// <summary>
        /// Whether or not to read headers when reading a CSV.
        /// </summary>
        public ReadHeader ReadHeader { get; }
        /// <summary>
        /// Whether or not to write headers when writing a CSV.
        /// </summary>
        public WriteHeader WriteHeader { get; }
        /// <summary>
        /// The instance of ITypeDescriber that will be used to
        ///   discover which columns to read or write, as well
        ///   as the manner of their reading and writing.
        /// </summary>
        public ITypeDescriber TypeDescriber { get; }
        /// <summary>
        /// Whether or not to write a row ending after the last row
        /// in a CSV.
        /// </summary>
        public WriteTrailingRowEnding WriteTrailingRowEnding { get; }
        /// <summary>
        /// Provider for MemoryPools needed during reading or writing CSVs.
        /// </summary>
        public IMemoryPoolProvider MemoryPoolProvider { get; }
        /// <summary>
        /// Which character, if any, is used to indicate the start
        /// of a comment.
        /// 
        /// Typically not set, but when set often '#'.
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
        /// <summary>
        /// How to handle whitespace when encountered during parsing.
        /// </summary>
        public WhitespaceTreatments WhitespaceTreatment { get; private set; }
        /// <summary>
        /// How to handle extra colums encountered when reading a CSV.
        /// </summary>
        public ExtraColumnTreatment ExtraColumnTreatment { get; private set; }

        internal Options(OptionsBuilder copy)
        {
            ValueSeparator = copy.ValueSeparator;
            EscapedValueStartAndEnd = copy.EscapedValueStartAndEnd;
            EscapedValueEscapeCharacter = copy.EscapedValueEscapeCharacter;
            RowEnding = copy.RowEnding;
            ReadHeader = copy.ReadHeader;
            WriteHeader = copy.WriteHeader;
            TypeDescriber = Utils.NonNull(copy.TypeDescriber);
            WriteTrailingRowEnding = copy.WriteTrailingRowEnding;
            MemoryPoolProvider = Utils.NonNull(copy.MemoryPoolProvider);
            CommentCharacter = copy.CommentCharacter;
            WriteBufferSizeHint = copy.WriteBufferSizeHint;
            ReadBufferSizeHint = copy.ReadBufferSizeHint;
            DynamicRowDisposal = copy.DynamicRowDisposal;
            WhitespaceTreatment = copy.WhitespaceTreatment;
            ExtraColumnTreatment = copy.ExtraColumnTreatment;
        }

        /// <summary>
        /// Create a new, empty, OptionsBuilder.
        /// </summary>
        public static OptionsBuilder CreateBuilder()
        => OptionsBuilder.CreateBuilder();

        /// <summary>
        /// Create a new OptionsBuilder that copies its initial values
        /// from the given Options.
        /// </summary>
        public static OptionsBuilder CreateBuilder(Options options)
        => OptionsBuilder.CreateBuilder(options);

        /// <summary>
        /// Returns true if this object equals the given Options.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is Options o)
            {
                return Equals(o);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this Options equals the given Options.
        /// </summary>
        public bool Equals(Options? options)
        {
            if (ReferenceEquals(options, null)) return false;

            return
                options.CommentCharacter == CommentCharacter &&
                options.DynamicRowDisposal == DynamicRowDisposal &&
                options.EscapedValueEscapeCharacter == EscapedValueEscapeCharacter &&
                options.EscapedValueStartAndEnd == EscapedValueStartAndEnd &&
                options.MemoryPoolProvider == MemoryPoolProvider &&
                options.ReadBufferSizeHint == ReadBufferSizeHint &&
                options.ReadHeader == ReadHeader &&
                options.RowEnding == RowEnding &&
                options.TypeDescriber == TypeDescriber &&
                options.ValueSeparator == ValueSeparator &&
                options.WriteBufferSizeHint == WriteBufferSizeHint &&
                options.WriteHeader == WriteHeader &&
                options.WriteTrailingRowEnding == WriteTrailingRowEnding &&
                options.WhitespaceTreatment == WhitespaceTreatment &&
                options.ExtraColumnTreatment == ExtraColumnTreatment;
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
                MemoryPoolProvider,
                ReadBufferSizeHint,
                ReadHeader,
                HashCode.Combine(
                    RowEnding,
                    TypeDescriber,
                    ValueSeparator,
                    WriteBufferSizeHint,
                    WriteHeader,
                    WriteTrailingRowEnding,
                    WhitespaceTreatment,
                    ExtraColumnTreatment
                )
            );

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
            ret.Append($", {nameof(MemoryPoolProvider)}={MemoryPoolProvider}");
            ret.Append($", {nameof(ReadBufferSizeHint)}={ReadBufferSizeHint}");
            ret.Append($", {nameof(ReadHeader)}={ReadHeader}");
            ret.Append($", {nameof(RowEnding)}={RowEnding}");
            ret.Append($", {nameof(TypeDescriber)}={TypeDescriber}");
            ret.Append($", {nameof(ValueSeparator)}={ValueSeparator}");
            ret.Append($", {nameof(WriteBufferSizeHint)}={WriteBufferSizeHint}");
            ret.Append($", {nameof(WriteHeader)}={WriteHeader}");
            ret.Append($", {nameof(WriteTrailingRowEnding)}={WriteTrailingRowEnding}");
            ret.Append($", {nameof(WhitespaceTreatment)}={WhitespaceTreatment}");
            ret.Append($", {nameof(ExtraColumnTreatment)}={ExtraColumnTreatment}");

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