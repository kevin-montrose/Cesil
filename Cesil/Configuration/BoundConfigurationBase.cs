using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;

namespace Cesil
{
    internal abstract class BoundConfigurationBase<T> : IBoundConfiguration<T>
    {
        private static readonly ReadOnlyMemory<char> CarriageReturn = "\r".AsMemory();
        private static readonly ReadOnlyMemory<char> LineFeed = "\n".AsMemory();
        private static readonly ReadOnlyMemory<char> CarriageReturnLineFeed = "\r\n".AsMemory();

        internal readonly NonNull<InstanceProviderDelegate<T>> NewCons;
        internal readonly Column[] DeserializeColumns;

        internal readonly Column[] SerializeColumns;
        internal readonly bool[] SerializeColumnsNeedEscape;

        // todo: maybe just replace this with an Options?
        // internal for testing purposes
        internal NonNull<ITypeDescriber> TypeDescriber;
        internal readonly char ValueSeparator;
        internal readonly RowEnding RowEnding;
        internal readonly ReadOnlyMemory<char> RowEndingMemory;
        internal readonly ReadHeader ReadHeader;
        internal readonly WriteHeader WriteHeader;
        internal readonly WriteTrailingNewLine WriteTrailingNewLine;
        internal readonly MemoryPool<char> MemoryPool;
        internal readonly int? WriteBufferSizeHint;
        internal readonly int ReadBufferSizeHint;
        internal readonly DynamicRowDisposal DynamicRowDisposal;
        internal readonly WhitespaceTreatments WhitespaceTreatment;

        private readonly char? _EscapedValueStartAndStop;
        internal bool HasEscapedValueStartAndStop => _EscapedValueStartAndStop != null;
        internal char EscapedValueStartAndStop
        {
            get
            {
                if(_EscapedValueStartAndStop == null)
                {
                    return Throw.Exception<char>($"{nameof(_EscapedValueStartAndStop)} has no value, this shouldn't be possible");
                }

                return _EscapedValueStartAndStop.Value;
            }
        }

        private readonly char? _EscapeValueEscapeChar;
        internal bool HasEscapeValueEscapeChar => _EscapeValueEscapeChar != null;
        internal char EscapeValueEscapeChar
        {
            get
            {
                if (_EscapeValueEscapeChar == null)
                {
                    return Throw.Exception<char>($"{nameof(_EscapeValueEscapeChar)} has no value, this shouldn't be possible");
                }

                return _EscapeValueEscapeChar.Value;
            }
        }

        private readonly char? _CommentChar;
        internal bool HasCommentChar => _CommentChar != null;
        internal char CommentChar
        {
            get
            {
                if (_CommentChar == null)
                {
                    return Throw.Exception<char>($"{nameof(_CommentChar)} has no value, this shouldn't be possible");
                }

                return _CommentChar.Value;
            }
        }

        internal readonly NeedsEncodeMode NeedsEncodeMode;

#pragma warning disable CS8618
        /// <summary>
        /// For some testing scenarios.
        /// 
        /// Created instance is nearly unusable.
        /// </summary>
        protected BoundConfigurationBase()
        {

        }
#pragma warning restore CS8618

        /// <summary>
        /// For working with dynamic.
        /// </summary>
        protected BoundConfigurationBase(
            ITypeDescriber? describer,
            char valueSeparator,
            char? escapedValueStartAndStop,
            char? escapeValueEscapeChar,
            RowEnding rowEndings,
            ReadHeader readHeader,
            WriteHeader writeHeaders,
            WriteTrailingNewLine writeTrailingNewLine,
            MemoryPool<char> memoryPool,
            char? commentChar,
            int? writeBufferSizeHint,
            int readBufferSizeHint,
            DynamicRowDisposal dynamicRowDisposal,
            WhitespaceTreatments whitespaceTreatment
        )
        {
            if (describer != null)
            {
                TypeDescriber.Value = describer;
            }
            DeserializeColumns = Array.Empty<Column>();
            SerializeColumns = Array.Empty<Column>();
            SerializeColumnsNeedEscape = Array.Empty<bool>();
            ValueSeparator = valueSeparator;
            _EscapedValueStartAndStop = escapedValueStartAndStop;
            _EscapeValueEscapeChar = escapeValueEscapeChar;
            RowEnding = rowEndings;
            WriteBufferSizeHint = writeBufferSizeHint;
            ReadBufferSizeHint = readBufferSizeHint;

            switch (RowEnding)
            {
                case RowEnding.CarriageReturn:
                    RowEndingMemory = CarriageReturn;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    RowEndingMemory = CarriageReturnLineFeed;
                    break;
                case RowEnding.LineFeed:
                    RowEndingMemory = LineFeed;
                    break;
                default:
                    // for cases like detecting headers, actually trying to write is NO GOOD...
                    //     but construction is fine
                    RowEndingMemory = default;
                    break;
            }

            ReadHeader = readHeader;
            WriteHeader = writeHeaders;
            WriteTrailingNewLine = writeTrailingNewLine;
            MemoryPool = memoryPool;
            _CommentChar = commentChar;
            DynamicRowDisposal = dynamicRowDisposal;
            WhitespaceTreatment = whitespaceTreatment;

            if (HasEscapedValueStartAndStop)
            {
                if (HasCommentChar)
                {
                    NeedsEncodeMode = NeedsEncodeMode.SeparatorLineEndingsEscapeStartComment;
                }
                else
                {
                    NeedsEncodeMode = NeedsEncodeMode.SeparatorLineEndingsEscapeStart;
                }
            }
            else
            {
                if (HasCommentChar)
                {
                    NeedsEncodeMode = NeedsEncodeMode.SeparatorLineEndingsComment;
                }
                else
                {
                    NeedsEncodeMode = NeedsEncodeMode.SeparatorAndLineEndings;
                }
            }
        }

        /// <summary>
        /// For working with concrete types.
        /// </summary>
        protected BoundConfigurationBase(
            InstanceProviderDelegate<T> newCons,
            Column[] deserializeColumns,
            Column[] serializeColumns,
            bool[] serializeColumnsNeedEscape,
            char valueSeparator,
            char? escapedValueStartAndStop,
            char? escapeValueEscapeChar,
            RowEnding rowEndings,
            ReadHeader readHeader,
            WriteHeader writeHeaders,
            WriteTrailingNewLine writeTrailingNewLine,
            MemoryPool<char> memoryPool,
            char? commentChar,
            int? writeBufferSizeHint,
            int readBufferSizeHint,
            WhitespaceTreatments whitespaceTreatment
        )
        {
            NewCons.Value = newCons;
            DeserializeColumns = deserializeColumns;
            SerializeColumns = serializeColumns;
            SerializeColumnsNeedEscape = serializeColumnsNeedEscape;
            ValueSeparator = valueSeparator;
            _EscapedValueStartAndStop = escapedValueStartAndStop;
            _EscapeValueEscapeChar = escapeValueEscapeChar;
            RowEnding = rowEndings;
            WriteBufferSizeHint = writeBufferSizeHint;
            ReadBufferSizeHint = readBufferSizeHint;

            switch (RowEnding)
            {
                case RowEnding.CarriageReturn:
                    RowEndingMemory = CarriageReturn;
                    break;
                case RowEnding.CarriageReturnLineFeed:
                    RowEndingMemory = CarriageReturnLineFeed;
                    break;
                case RowEnding.LineFeed:
                    RowEndingMemory = LineFeed;
                    break;
                default:
                    // for cases like detecting headers, actually trying to write is NO GOOD...
                    //     but construction is fine
                    RowEndingMemory = default;
                    break;
            }

            ReadHeader = readHeader;
            WriteHeader = writeHeaders;
            WriteTrailingNewLine = writeTrailingNewLine;
            MemoryPool = memoryPool;
            _CommentChar = commentChar;
            WhitespaceTreatment = whitespaceTreatment;

            if (HasEscapedValueStartAndStop)
            {
                if (HasCommentChar)
                {
                    NeedsEncodeMode = NeedsEncodeMode.SeparatorLineEndingsEscapeStartComment;
                }
                else
                {
                    NeedsEncodeMode = NeedsEncodeMode.SeparatorLineEndingsEscapeStart;
                }
            }
            else
            {
                if (HasCommentChar)
                {
                    NeedsEncodeMode = NeedsEncodeMode.SeparatorLineEndingsComment;
                }
                else
                {
                    NeedsEncodeMode = NeedsEncodeMode.SeparatorAndLineEndings;
                }
            }
        }

        public IAsyncReader<T> CreateAsyncReader(PipeReader reader, Encoding encoding, object? context = null)
        {
            Utils.CheckArgumentNull(reader, nameof(reader));
            Utils.CheckArgumentNull(encoding, nameof(encoding));

            // context is legally null

            var wrapper = new PipeReaderAdapter(reader, encoding);

            return CreateAsyncReader(wrapper, context);
        }

        public IAsyncReader<T> CreateAsyncReader(TextReader reader, object? context = null)
        {
            Utils.CheckArgumentNull(reader, nameof(reader));

            // context is legally null

            var wrapper = new AsyncTextReaderAdapter(reader);

            return CreateAsyncReader(wrapper, context);
        }

        public IAsyncWriter<T> CreateAsyncWriter(PipeWriter writer, Encoding encoding, object? context = null)
        {
            Utils.CheckArgumentNull(writer, nameof(writer));
            Utils.CheckArgumentNull(encoding, nameof(encoding));

            // context is legally null

            var wrapper = new PipeWriterAdapter(writer, encoding, MemoryPool);

            return CreateAsyncWriter(wrapper, context);
        }

        public IAsyncWriter<T> CreateAsyncWriter(TextWriter writer, object? context = null)
        {
            Utils.CheckArgumentNull(writer, nameof(writer));

            // context is legally null

            var wrapper = new AsyncTextWriterAdapter(writer);

            return CreateAsyncWriter(wrapper, context);
        }

        public IReader<T> CreateReader(ReadOnlySequence<byte> sequence, Encoding encoding, object? context = null)
        {
            Utils.CheckArgumentNull(encoding, nameof(encoding));

            // context is legally null

            var wrapper = new ReadOnlyByteSequenceAdapter(sequence, encoding);

            return CreateReader(wrapper, context);
        }

        public IReader<T> CreateReader(ReadOnlySequence<char> sequence, object? context = null)
        {
            // context is legally null

            var wrapper = new ReadOnlyCharSequenceAdapter(sequence);

            return CreateReader(wrapper, context);
        }

        public IReader<T> CreateReader(TextReader reader, object? context = null)
        {
            Utils.CheckArgumentNull(reader, nameof(reader));

            // context is legeally null

            var wrapper = new TextReaderAdapter(reader);

            return CreateReader(wrapper, context);
        }

        public IWriter<T> CreateWriter(IBufferWriter<byte> writer, Encoding encoding, object? context = null)
        {
            Utils.CheckArgumentNull(writer, nameof(writer));
            Utils.CheckArgumentNull(encoding, nameof(encoding));

            // context is legally null

            var wrapper = new BufferWriterByteAdapter(writer, encoding);

            return CreateWriter(wrapper, context);
        }

        public IWriter<T> CreateWriter(IBufferWriter<char> writer, object? context = null)
        {
            Utils.CheckArgumentNull(writer, nameof(writer));

            // context is legally null

            var wrapper = new BufferWriterCharAdapter(writer);

            return CreateWriter(wrapper, context);
        }

        public IWriter<T> CreateWriter(TextWriter writer, object? context = null)
        {
            Utils.CheckArgumentNull(writer, nameof(writer));

            // context is legally null

            var wrapper = new TextWriterAdapter(writer);

            return CreateWriter(wrapper, context);
        }

        internal abstract IReader<T> CreateReader(IReaderAdapter reader, object? context = null);
        internal abstract IWriter<T> CreateWriter(IWriterAdapter writer, object? context = null);

        internal abstract IAsyncWriter<T> CreateAsyncWriter(IAsyncWriterAdapter writer, object? context = null);
        internal abstract IAsyncReader<T> CreateAsyncReader(IAsyncReaderAdapter reader, object? context = null);
    }
}
