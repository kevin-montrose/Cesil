using System;
using System.Buffers;
using System.Linq;
using System.Text;

namespace Cesil
{
    /// <summary>
    /// Builder for Options.
    /// 
    /// Options itself is immutable, but OptionsBuilder
    /// is chainable and mutable.
    /// </summary>
    [NotEquatable("Mutable")]
    public sealed class OptionsBuilder
    {
        /// <summary>
        /// Character used to separate two values in a row
        /// 
        /// Typically a comma.
        /// </summary>
        public string ValueSeparator { get; private set; }

        /// <summary>
        /// Character used to start an escaped value.
        /// 
        /// Typically a double quote, but can be null for some formats.
        /// </summary>
        public char? EscapedValueStartAndEnd { get; private set; }

        /// <summary>
        /// Character used to escape another character in an
        ///   escaped value.
        ///   
        /// Typically a double quote, but can be null for some formats and
        ///   MUST be null for formats without an EscapedValueStartAndEnd.
        /// </summary>
        public char? EscapedValueEscapeCharacter { get; private set; }
        /// <summary>
        /// The sequence of characters used to end a row.
        /// </summary>
        public RowEnding RowEnding { get; private set; }
        /// <summary>
        /// Whether or not to read headers when reading a CSV.
        /// </summary>
        public ReadHeader ReadHeader { get; private set; }
        /// <summary>
        /// Whether or not to write headers when writing a CSV.
        /// </summary>
        public WriteHeader WriteHeader { get; private set; }
        /// <summary>
        /// The instance of ITypeDescriber that will be used to
        ///   discover which columns to read or write, as well
        ///   as the manner of their reading and writing.
        /// </summary>
        [NullableExposed("All properties on OptionsBuilder are mutable and nullable, Options handles making sure they're not null")]
        public ITypeDescriber? TypeDescriber { get; private set; }
        /// <summary>
        /// Whether or not to write a row ending after the last row
        /// in a CSV.
        /// </summary>
        public WriteTrailingRowEnding WriteTrailingRowEnding { get; private set; }
        /// <summary>
        /// Which MemoryPool to use when reading or writing a CSV.
        /// </summary>
        [NullableExposed("All properties on OptionsBuilder are mutable and nullable, Options handles making sure they're not null")]
        public MemoryPool<char>? MemoryPool { get; private set; }
        /// <summary>
        /// Which character, if any, is used to indicate the start
        /// of a comment.
        /// 
        /// Typically not set, but when set often '#'.
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
        [IntentionallyExposedPrimitive("Best way to indicate a size")]
        public int? WriteBufferSizeHint { get; private set; }
        /// <summary>
        /// How big a buffer to request from the MemoryPool for
        ///   servicing read operations.
        ///   
        /// Set to 0 to use a default size.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate a size")]
        public int ReadBufferSizeHint { get; private set; }
        /// <summary>
        /// When to dispose any dynamic rows returned by an IReader or IAsyncReader.
        /// </summary>
        public DynamicRowDisposal DynamicRowDisposal { get; private set; }
        /// <summary>
        /// How to handle whitespace when encountered during parsing.
        /// </summary>
        public WhitespaceTreatments WhitespaceTreatment { get; private set; }
        /// <summary>
        /// How to handle extra colums encountered when reading a CSV.
        /// </summary>
        public ExtraColumnTreatment ExtraColumnTreatment { get; private set; }
        /// <summary>
        /// Which ArrayPool to use when buffering DynamicCellValues when
        /// writing dynamic rows.
        /// </summary>
        [NullableExposed("All properties on OptionsBuilder are mutable and nullable, Options handles making sure they're not null")]
        public ArrayPool<DynamicCellValue>? ArrayPool { get; private set; }

        internal OptionsBuilder()
        {
            ValueSeparator = "";
        }

        internal OptionsBuilder(Options copy)
        {
            ValueSeparator = copy.ValueSeparator;
            EscapedValueStartAndEnd = copy.EscapedValueStartAndEnd;
            EscapedValueEscapeCharacter = copy.EscapedValueEscapeCharacter;
            RowEnding = copy.RowEnding;
            ReadHeader = copy.ReadHeader;
            WriteHeader = copy.WriteHeader;
            TypeDescriber = copy.TypeDescriber;
            WriteTrailingRowEnding = copy.WriteTrailingRowEnding;
            MemoryPool = copy.MemoryPool;
            CommentCharacter = copy.CommentCharacter;
            WriteBufferSizeHint = copy.WriteBufferSizeHint;
            ReadBufferSizeHint = copy.ReadBufferSizeHint;
            DynamicRowDisposal = copy.DynamicRowDisposal;
            WhitespaceTreatment = copy.WhitespaceTreatment;
            ExtraColumnTreatment = copy.ExtraColumnTreatment;
            ArrayPool = copy.ArrayPool;
        }

        /// <summary>
        /// Create a new, empty, OptionsBuilder.
        /// </summary>
        public static OptionsBuilder CreateBuilder()
        => new OptionsBuilder();

        /// <summary>
        /// Create a new OptionsBuilder, copying defaults
        /// from the given Options.
        /// </summary>
        public static OptionsBuilder CreateBuilder(Options options)
        {
            Utils.CheckArgumentNull(options, nameof(options));

            return new OptionsBuilder(options);
        }

        /// <summary>
        /// Create the Options object that has been configured
        /// by this builder.
        /// </summary>
        public Options ToOptions()
        {
            if (string.IsNullOrEmpty(ValueSeparator))
            {
                return Throw.InvalidOperationException<Options>($"{nameof(ValueSeparator)} cannot be empty");
            }

            // can't distinguish between the start of a value and an empty value
            if (ValueSeparator[0] == EscapedValueStartAndEnd)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(ValueSeparator)} cannot equal {nameof(EscapedValueStartAndEnd)}, both are '{ValueSeparator}'");
            }
            // can't distinguish between the start of a comment and an empty value
            if (ValueSeparator[0] == CommentCharacter)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(ValueSeparator)} cannot equal {nameof(CommentCharacter)}, both are '{ValueSeparator}'");
            }
            // can't distinguish between the start of an escaped value and a comment
            if (EscapedValueStartAndEnd != null && EscapedValueStartAndEnd == CommentCharacter)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(EscapedValueStartAndEnd)} cannot equal {nameof(CommentCharacter)}, both are '{EscapedValueStartAndEnd}'");
            }
            // can't have an escape char if you can't start an escape sequence
            if (EscapedValueStartAndEnd == null && EscapedValueEscapeCharacter != null)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(EscapedValueEscapeCharacter)} cannot be set if, {nameof(EscapedValueStartAndEnd)} isn't set");
            }
            // RowEnding not recognized
            if (!Enum.IsDefined(Types.RowEnding, RowEnding))
            {
                return Throw.InvalidOperationException<Options>($"{nameof(RowEnding)} has an unexpected value, '{RowEnding}'");
            }
            // ReadHeader not recognized
            if (!Enum.IsDefined(Types.ReadHeader, ReadHeader))
            {
                return Throw.InvalidOperationException<Options>($"{nameof(ReadHeader)} has an unexpected value, '{ReadHeader}'");
            }
            // WriteHeader not recognized
            if (!Enum.IsDefined(Types.WriteHeader, WriteHeader))
            {
                return Throw.InvalidOperationException<Options>($"{nameof(WriteHeader)} has an unexpected value, '{WriteHeader}'");
            }
            // TypeDescriber not configured
            if (TypeDescriber == null)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(TypeDescriber)} has not been set");
            }
            // WriteTrailingNewLine not recognized
            if (!Enum.IsDefined(Types.WriteTrailingRowEnding, WriteTrailingRowEnding))
            {
                return Throw.InvalidOperationException<Options>($"{nameof(WriteTrailingRowEnding)} has an unexpected value, '{WriteTrailingRowEnding}'");
            }
            // MemoryPool not configured
            if (MemoryPool == null)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(MemoryPool)} has not been set");
            }
            // ArrayPool not configured
            if (ArrayPool == null)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(ArrayPool)} has not been set");
            }
            // WriteBufferSizeHint < 0
            if (WriteBufferSizeHint.HasValue && WriteBufferSizeHint.Value < 0)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(WriteBufferSizeHint)} cannot be less than 0, is '{WriteBufferSizeHint}'");
            }
            // ReadBufferSizeHint < 0
            if (ReadBufferSizeHint < 0)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(ReadBufferSizeHint)} cannot be less than 0, is '{ReadBufferSizeHint}'");
            }
            // DynamicRowDisposal not recognized
            if (!Enum.IsDefined(Types.DynamicRowDisposal, DynamicRowDisposal))
            {
                return Throw.InvalidOperationException<Options>($"{nameof(DynamicRowDisposal)} has an unexpected value, '{DynamicRowDisposal}'");
            }
            // WhitespaceTreatment not recognized
            if (!Utils.IsLegalFlagEnum(WhitespaceTreatment))
            {
                return Throw.InvalidOperationException<Options>($"{nameof(WhitespaceTreatment)} has an unexpected value, '{WhitespaceTreatment}'");
            }
            // ExtraColumnTreatment not recognized
            if (!Enum.IsDefined(Types.ExtraColumnTreatment, ExtraColumnTreatment))
            {
                return Throw.InvalidOperationException<Options>($"{nameof(ExtraColumnTreatment)} has an unexpected value, '{ExtraColumnTreatment}'");
            }

            // if any of the special characters are whitespace, we can't use them with any of the trimming... parse is ambiguous
            if (WhitespaceTreatment != WhitespaceTreatments.Preserve)
            {
                if (CommentCharacter.HasValue && char.IsWhiteSpace(CommentCharacter.Value))
                {
                    return Throw.InvalidOperationException<Options>($"Cannot use a whitespace removing {nameof(WhitespaceTreatment)} ({WhitespaceTreatment}) with a whitespace {nameof(CommentCharacter)} ('{CommentCharacter}')");
                }

                if (EscapedValueEscapeCharacter.HasValue && char.IsWhiteSpace(EscapedValueEscapeCharacter.Value))
                {
                    return Throw.InvalidOperationException<Options>($"Cannot use a whitespace removing {nameof(WhitespaceTreatment)} ({WhitespaceTreatment}) with a whitespace {nameof(EscapedValueEscapeCharacter)} ('{EscapedValueEscapeCharacter}')");
                }

                if (EscapedValueStartAndEnd.HasValue && char.IsWhiteSpace(EscapedValueStartAndEnd.Value))
                {
                    return Throw.InvalidOperationException<Options>($"Cannot use a whitespace removing {nameof(WhitespaceTreatment)} ({WhitespaceTreatment}) with a whitespace {nameof(EscapedValueStartAndEnd)} ('{EscapedValueStartAndEnd}')");
                }

                if (ValueSeparator.Any(c => char.IsWhiteSpace(c)))
                {
                    return Throw.InvalidOperationException<Options>($"Cannot use a whitespace removing {nameof(WhitespaceTreatment)} ({WhitespaceTreatment}) with a whitespace {nameof(ValueSeparator)} ('{ValueSeparator}')");
                }
            }

            // ValueSeparator can't be the same as the expected row ending
            var valSepFirstChar = ValueSeparator[0];
            var valSepIsCR = valSepFirstChar == '\r';
            var valSepIsLF = valSepFirstChar == '\n';
            var valSepIsRowEnding = valSepIsCR || valSepIsLF;

            var valueSeparatorMatchesRowEnding =
                (RowEnding == RowEnding.CarriageReturn && valSepIsCR) ||
                (RowEnding == RowEnding.CarriageReturnLineFeed && valSepIsCR) ||    // only the first character matters, so valSepIsCR is what we want here
                (RowEnding == RowEnding.LineFeed && valSepIsLF) ||
                (RowEnding == RowEnding.Detect && valSepIsRowEnding);
            if (valueSeparatorMatchesRowEnding)
            {
                return Throw.InvalidOperationException<Options>($"{nameof(ValueSeparator)} cannot match the expected (or potential for {nameof(RowEnding.Detect)}) {nameof(RowEnding)}");
            }

            return BuildInternal();
        }

        // sometimes we want to skip validation in tests
        internal Options BuildInternal()
        => new Options(this);

        /// <summary>
        /// Set the character used to separate two values in a row.
        /// </summary>
        public OptionsBuilder WithValueSeparator(string valueSeparator)
        {
            Utils.CheckArgumentNull(valueSeparator, nameof(valueSeparator));
            if (valueSeparator.Length == 0)
            {
                return Throw.ArgumentException<OptionsBuilder>($"{nameof(valueSeparator)} cannot be empty", nameof(valueSeparator));
            }

            return WithValueSeparatorInternal(valueSeparator);
        }

        internal OptionsBuilder WithValueSeparatorInternal(string valueSeparator)
        {
            ValueSeparator = valueSeparator;
            return this;
        }

        /// <summary>
        /// Set the character used to start an escaped value.
        /// 
        /// If this is null, EscapedValueEscapeCharacter must also be null.
        /// </summary>
        public OptionsBuilder WithEscapedValueStartAndEnd(char? escapeStart)
        {
            EscapedValueStartAndEnd = escapeStart;
            return this;
        }

        /// <summary>
        /// Set the character used to escape another character in
        /// an escaped value.
        /// 
        /// If this is non-null, EscapedValueStartAndEnd must also be non-null.
        /// </summary>
        public OptionsBuilder WithEscapedValueEscapeCharacter(char? escape)
        {
            EscapedValueEscapeCharacter = escape;
            return this;
        }

        /// <summary>
        /// Set the sequence of characters that will end a row.
        /// </summary>
        public OptionsBuilder WithRowEnding(RowEnding rowEnding)
        {
            if (!Enum.IsDefined(Types.RowEnding, rowEnding))
            {
                return Throw.ArgumentException<OptionsBuilder>($"Unexpected {nameof(Cesil.RowEnding)} value: {rowEnding}", nameof(rowEnding));
            }

            return WithRowEndingInternal(rowEnding);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithRowEndingInternal(RowEnding l)
        {
            RowEnding = l;
            return this;
        }

        /// <summary>
        /// Set whether or not to read headers.
        /// </summary>
        public OptionsBuilder WithReadHeader(ReadHeader readHeader)
        {
            if (!Enum.IsDefined(Types.ReadHeader, readHeader))
            {
                return Throw.ArgumentException<OptionsBuilder>($"Unexpected {nameof(Cesil.ReadHeader)} value: {readHeader}", nameof(readHeader));
            }

            return WithReadHeaderInternal(readHeader);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithReadHeaderInternal(ReadHeader r)
        {
            ReadHeader = r;
            return this;
        }

        /// <summary>
        /// Set whether or not to write headers.
        /// </summary>
        public OptionsBuilder WithWriteHeader(WriteHeader writeHeader)
        {
            if (!Enum.IsDefined(Types.WriteHeader, writeHeader))
            {
                return Throw.ArgumentException<OptionsBuilder>($"Unexpected {nameof(Cesil.WriteHeader)} value: {writeHeader}", nameof(writeHeader));
            }

            return WithWriteHeaderInternal(writeHeader);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithWriteHeaderInternal(WriteHeader w)
        {
            WriteHeader = w;
            return this;
        }

        /// <summary>
        /// Set the ITypeDescriber used to discover and configure the
        /// columns that are read and written.
        /// </summary>
        public OptionsBuilder WithTypeDescriber(ITypeDescriber typeDescriber)
        {
            typeDescriber ??= TypeDescribers.Default;

            TypeDescriber = typeDescriber;
            return this;
        }

        /// <summary>
        /// Set whether or not to end the last row with a new line.
        /// </summary>
        public OptionsBuilder WithWriteTrailingRowEnding(WriteTrailingRowEnding writeTrailingNewLine)
        {
            if (!Enum.IsDefined(Types.WriteTrailingRowEnding, writeTrailingNewLine))
            {
                return Throw.ArgumentException<OptionsBuilder>($"Unexpected {nameof(Cesil.WriteTrailingRowEnding)} value: {writeTrailingNewLine}", nameof(writeTrailingNewLine));
            }

            return WithWriteTrailingRowEndingInternal(writeTrailingNewLine);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithWriteTrailingRowEndingInternal(WriteTrailingRowEnding w)
        {
            WriteTrailingRowEnding = w;
            return this;
        }

        /// <summary>
        /// Set the MemoryPool used during reading and writing.
        /// </summary>
        public OptionsBuilder WithMemoryPool(MemoryPool<char> memoryPool)
        {
            memoryPool ??= MemoryPool<char>.Shared;

            MemoryPool = memoryPool;
            return this;
        }

        /// <summary>
        /// Set the ArrayPool used for buffering DynamicCellValues when writing
        /// dynamic rows.
        /// </summary>
        public OptionsBuilder WithArrayPool(ArrayPool<DynamicCellValue> arrayPool)
        {
            arrayPool ??= ArrayPool<DynamicCellValue>.Shared;

            ArrayPool = arrayPool;
            return this;
        }

        /// <summary>
        /// Set or clear the character that starts a row
        /// that is a comment.
        /// </summary>
        public OptionsBuilder WithCommentCharacter(char? commentStart)
        {
            CommentCharacter = commentStart;
            return this;
        }

        /// <summary>
        /// Set or clear the buffer size hint for write operations.
        /// 
        /// Setting it to null will cause a default "best guess" buffer to
        ///   be requested from the configured MemoryPool.
        ///   
        /// Setting it to 0 will disable buffering.
        /// 
        /// All values are treated as hints, it's up to
        ///   the configured MemoryPool to satisfy the request.
        /// </summary>
        public OptionsBuilder WithWriteBufferSizeHint([IntentionallyExposedPrimitive("Best way to indicate a size")] int? sizeHint)
        {
            if (sizeHint != null && sizeHint < 0)
            {
                return Throw.ArgumentException<OptionsBuilder>($"Cannot be negative, was {sizeHint.Value}", nameof(sizeHint));
            }

            return WithWriteBufferSizeHintInternal(sizeHint);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithWriteBufferSizeHintInternal(int? sizeHint)
        {
            WriteBufferSizeHint = sizeHint;
            return this;
        }

        /// <summary>
        /// Set the buffer size hint for read operations.
        /// 
        /// Setting to 0 will cause a default "best guess" size to be requested
        ///   from the configured MemoryPool.
        ///   
        /// All values are treated as hints, it's up to
        ///   the configured MemoryPool to satisfy the request.
        /// </summary>
        public OptionsBuilder WithReadBufferSizeHint([IntentionallyExposedPrimitive("Best way to indicate a size")] int sizeHint)
        {
            if (sizeHint < 0)
            {
                return Throw.ArgumentException<OptionsBuilder>($"Cannot be negative, was {sizeHint}", nameof(sizeHint));
            }

            return WithReadBufferSizeHintInternal(sizeHint);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithReadBufferSizeHintInternal(int sizeHint)
        {
            ReadBufferSizeHint = sizeHint;
            return this;
        }

        /// <summary>
        /// Set when dynamic rows returned by a reader are disposed.
        /// 
        /// The options are either when the reader is disposed (the default) or
        ///   when the row is explicitly disposed.
        /// </summary>
        public OptionsBuilder WithDynamicRowDisposal(DynamicRowDisposal dynamicRowDisposal)
        {
            if (!Enum.IsDefined(Types.DynamicRowDisposal, dynamicRowDisposal))
            {
                return Throw.ArgumentException<OptionsBuilder>($"Unexpected {nameof(DynamicRowDisposal)} value: {dynamicRowDisposal}", nameof(dynamicRowDisposal));
            }

            return WithDynamicRowDisposalInternal(dynamicRowDisposal);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithDynamicRowDisposalInternal(DynamicRowDisposal d)
        {
            DynamicRowDisposal = d;
            return this;
        }

        /// <summary>
        /// Set how to treat whitespace when parsing.
        /// </summary>
        public OptionsBuilder WithWhitespaceTreatment(WhitespaceTreatments whitespaceTreatment)
        {
            // WhitespaceTreatment not recognized
            if (!Utils.IsLegalFlagEnum(whitespaceTreatment))
            {
                return Throw.ArgumentException<OptionsBuilder>($"{nameof(WhitespaceTreatment)} has an unexpected value, '{WhitespaceTreatment}'", nameof(whitespaceTreatment));
            }

            return WithWhitespaceTreatmentInternal(whitespaceTreatment);
        }

        internal OptionsBuilder WithWhitespaceTreatmentInternal(WhitespaceTreatments whitespaceTreatment)
        {
            WhitespaceTreatment = whitespaceTreatment;
            return this;
        }

        /// <summary>
        /// Set how to treat extra columns when parsing.
        /// </summary>
        public OptionsBuilder WithExtraColumnTreatment(ExtraColumnTreatment extraColumnTreatment)
        {
            // ExtraColumnTreatment not recognized
            if (!Enum.IsDefined(Types.ExtraColumnTreatment, extraColumnTreatment))
            {
                return Throw.ArgumentException<OptionsBuilder>($"Unexpected {nameof(ExtraColumnTreatment)} value: {extraColumnTreatment}", nameof(extraColumnTreatment));
            }

            return WithExtraColumnTreatmentInternal(extraColumnTreatment);
        }

        internal OptionsBuilder WithExtraColumnTreatmentInternal(ExtraColumnTreatment extraColumnTreatment)
        {
            ExtraColumnTreatment = extraColumnTreatment;
            return this;
        }

        /// <summary>
        /// Returns a representation of this OptionsBuilder object.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append($"{nameof(OptionsBuilder)} with ");
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
            ret.Append($", {nameof(WriteTrailingRowEnding)}={WriteTrailingRowEnding}");
            ret.Append($", {nameof(WhitespaceTreatment)}={WhitespaceTreatment}");
            ret.Append($", {nameof(ExtraColumnTreatment)}={ExtraColumnTreatment}");
            ret.Append($", {nameof(ArrayPool)}={ArrayPool}");

            return ret.ToString();
        }
    }
}
