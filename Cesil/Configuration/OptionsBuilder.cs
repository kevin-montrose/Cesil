using System;
using System.Buffers;

namespace Cesil
{
    /// <summary>
    /// Builder for Options.
    /// 
    /// Options itself is immutable, but OptionsBuilder
    /// is chainable and mutable.
    /// </summary>
    public sealed class OptionsBuilder
    {
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

        internal OptionsBuilder() { }

        internal OptionsBuilder(Options copy)
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
        => new OptionsBuilder();

        /// <summary>
        /// Create the Options object that has been configured
        /// by this builder.
        /// </summary>
        public Options Build()
        {
            // can't distinguish between the start of a value and an empty value
            if(ValueSeparator == EscapedValueStartAndEnd)
            {
                Throw.InvalidOperationException($"{nameof(ValueSeparator)} cannot equal {nameof(EscapedValueStartAndEnd)}, both are '{ValueSeparator}'");
            }
            // can't distinguish between the start of a comment and an empty value
            if (ValueSeparator == CommentCharacter)
            {
                Throw.InvalidOperationException($"{nameof(ValueSeparator)} cannot equal {nameof(CommentCharacter)}, both are '{ValueSeparator}'");
            }
            // can't distinguish between the start of an escaped value and a comment
            if (EscapedValueStartAndEnd == CommentCharacter)
            {
                Throw.InvalidOperationException($"{nameof(EscapedValueStartAndEnd)} cannot equal {nameof(CommentCharacter)}, both are '{EscapedValueStartAndEnd}'");
            }
            // RowEnding not recognized
            if (RowEnding == RowEndings.None || !Enum.IsDefined(Types.RowEndingsType, RowEnding))
            {
                Throw.InvalidOperationException($"{nameof(RowEnding)} has an unexpected value, '{RowEnding}'");
            }
            // ReadHeader not recognized
            if (ReadHeader == ReadHeaders.None || !Enum.IsDefined(Types.ReadHeadersType, ReadHeader))
            {
                Throw.InvalidOperationException($"{nameof(ReadHeader)} has an unexpected value, '{ReadHeader}'");
            }
            // WriteHeader not recognized
            if (WriteHeader == WriteHeaders.None || !Enum.IsDefined(Types.WriteHeadersType, WriteHeader))
            {
                Throw.InvalidOperationException($"{nameof(WriteHeader)} has an unexpected value, '{WriteHeader}'");
            }
            // TypeDescriber not configured
            if(TypeDescriber == null)
            {
                Throw.InvalidOperationException($"{nameof(TypeDescriber)} has not been set");
            }
            // WriteTrailingNewLine not recognized
            if (WriteTrailingNewLine == WriteTrailingNewLines.None || !Enum.IsDefined(Types.WriteTrailingNewLinesType, WriteTrailingNewLine))
            {
                Throw.InvalidOperationException($"{nameof(WriteTrailingNewLine)} has an unexpected value, '{WriteTrailingNewLine}'");
            }
            // MemoryPool not configured
            if(MemoryPool == null)
            {
                Throw.InvalidOperationException($"{nameof(TypeDescriber)} has not been set");
            }
            // WriteBufferSizeHint < 0
            if(WriteBufferSizeHint.HasValue && WriteBufferSizeHint.Value < 0)
            {
                Throw.InvalidOperationException($"{nameof(WriteBufferSizeHint)} cannot be less than 0, is '{WriteBufferSizeHint}'");
            }
            // ReadBufferSizeHint < 0
            if (ReadBufferSizeHint < 0)
            {
                Throw.InvalidOperationException($"{nameof(ReadBufferSizeHint)} cannot be less than 0, is '{ReadBufferSizeHint}'");
            }
            // DynamicRowDisposal not recognized
            if (DynamicRowDisposal == DynamicRowDisposal.None || !Enum.IsDefined(Types.DynamicRowDisposalType, DynamicRowDisposal))
            {
                Throw.InvalidOperationException($"{nameof(DynamicRowDisposal)} has an unexpected value, '{DynamicRowDisposal}'");
            }

            return BuildInternal();
        }

        // sometimes we want to skip validation in tests
        internal Options BuildInternal()
        => new Options(this);

        /// <summary>
        /// Set the character used to separate two values in a row.
        /// </summary>
        public OptionsBuilder WithValueSeparator(char c)
        {
            ValueSeparator = c;
            return this;
        }

        /// <summary>
        /// Set the character used to start an escaped value.
        /// </summary>
        public OptionsBuilder WithEscapedValueStartAndEnd(char c)
        {
            EscapedValueStartAndEnd = c;
            return this;
        }

        /// <summary>
        /// Set the character used to escape another character in
        /// an escaped value.
        /// </summary>
        public OptionsBuilder WithEscapedValueEscapeCharacter(char c)
        {
            EscapedValueEscapeCharacter = c;
            return this;
        }

        /// <summary>
        /// Set the sequence of characters that will end a row.
        /// </summary>
        public OptionsBuilder WithRowEnding(RowEndings l)
        {
            if (!Enum.IsDefined(Types.RowEndingsType, l))
            {
                Throw.ArgumentException($"Unexpected {nameof(RowEndings)} value: {l}", nameof(l));
            }

            return WithRowEndingInternal(l);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithRowEndingInternal(RowEndings l)
        {
            RowEnding = l;
            return this;
        }

        /// <summary>
        /// Set whether or not to read headers.
        /// </summary>
        public OptionsBuilder WithReadHeader(ReadHeaders r)
        {
            if (!Enum.IsDefined(Types.ReadHeadersType, r))
            {
                Throw.ArgumentException($"Unexpected {nameof(ReadHeaders)} value: {r}", nameof(r));
            }

            return WithReadHeaderInternal(r);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithReadHeaderInternal(ReadHeaders r)
        {
            ReadHeader = r;
            return this;
        }

        /// <summary>
        /// Set whether or not to write headers.
        /// </summary>
        public OptionsBuilder WithWriteHeader(WriteHeaders w)
        {
            if (!Enum.IsDefined(Types.WriteHeadersType, w))
            {
                Throw.ArgumentException($"Unexpected {nameof(WriteHeaders)} value: {w}", nameof(w));
            }

            return WithWriteHeaderInternal(w);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithWriteHeaderInternal(WriteHeaders w)
        {
            WriteHeader = w;
            return this;
        }

        /// <summary>
        /// Set the ITypeDescriber used to discover and configure the
        /// columns that are read and written.
        /// </summary>
        public OptionsBuilder WithTypeDescriber(ITypeDescriber describer)
        {
            describer = describer ?? TypeDescribers.Default;

            TypeDescriber = describer;
            return this;
        }

        /// <summary>
        /// Set the ITypeDescriber used to discover and configure the
        /// columns that are read and written.
        /// </summary>
        public OptionsBuilder WithDynamicTypeConverter(IDynamicTypeConverter converter)
        {
            converter = converter ?? DynamicTypeConverters.Default;

            DynamicTypeConverter = converter;
            return this;
        }

        /// <summary>
        /// Set whether or not to end the last row with a new line.
        /// </summary>
        public OptionsBuilder WithWriteTrailingNewLine(WriteTrailingNewLines w)
        {
            if (!Enum.IsDefined(Types.WriteTrailingNewLinesType, w))
            {
                Throw.ArgumentException($"Unexpected {nameof(WriteTrailingNewLines)} value: {w}", nameof(w));
            }

            return WithWriteTrailingNewLineInternal(w);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithWriteTrailingNewLineInternal(WriteTrailingNewLines w)
        {
            WriteTrailingNewLine = w;
            return this;
        }

        /// <summary>
        /// Set the MemoryPool used during reading and writing.
        /// </summary>
        public OptionsBuilder WithMemoryPool(MemoryPool<char> pool)
        {
            pool = pool ?? MemoryPool<char>.Shared;

            MemoryPool = pool;
            return this;
        }

        /// <summary>
        /// Set or clear the character that starts a row
        /// that is a comment.
        /// </summary>
        public OptionsBuilder WithCommentCharacter(char? c)
        {
            CommentCharacter = c;
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
        ///   the configured MemoryPool to satsify the request.
        /// </summary>
        public OptionsBuilder WithWriteBufferSizeHint(int? sizeHint)
        {
            if(sizeHint != null && sizeHint < 0)
            {
                Throw.ArgumentException($"Cannot be negative, was {sizeHint.Value}", nameof(sizeHint));
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
        ///   the configured MemoryPool to satsify the request.
        /// </summary>
        public OptionsBuilder WithReadBufferSizeHint(int sizeHint)
        {
            if(sizeHint < 0)
            {
                Throw.ArgumentException($"Cannot be negative, was {sizeHint}", nameof(sizeHint));
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
        public OptionsBuilder WithDynamicRowDisposal(DynamicRowDisposal d)
        {
            if(!Enum.IsDefined(typeof(DynamicRowDisposal), d))
            {
                Throw.ArgumentException($"Unexpected {nameof(DynamicRowDisposal)} value: {d}", nameof(d));
            }

            return WithDynamicRowDisposalInternal(d);
        }

        // sometimes we want to skip validation in tests
        internal OptionsBuilder WithDynamicRowDisposalInternal(DynamicRowDisposal d)
        {
            DynamicRowDisposal = d;
            return this;
        }
    }
}
