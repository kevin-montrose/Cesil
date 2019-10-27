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

        internal readonly InstanceProviderDelegate<T>? _NewCons;
        internal InstanceProviderDelegate<T> NewCons => Utils.NonNull(_NewCons);
        internal readonly Column[] DeserializeColumns;

        internal readonly Column[] SerializeColumns;
        internal readonly bool[] SerializeColumnsNeedEscape;

        // internal for testing purposes
        internal readonly ITypeDescriber? _TypeDescriber;
        internal ITypeDescriber TypeDescriber => Utils.NonNull(_TypeDescriber);
        internal readonly char ValueSeparator;
        internal readonly char EscapedValueStartAndStop;
        internal readonly char EscapeValueEscapeChar;
        internal readonly RowEndings RowEnding;
        internal readonly ReadOnlyMemory<char> RowEndingMemory;
        internal readonly ReadHeaders ReadHeader;
        internal readonly WriteHeaders WriteHeader;
        internal readonly WriteTrailingNewLines WriteTrailingNewLine;
        internal readonly MemoryPool<char> MemoryPool;
        internal readonly char? CommentChar;
        internal readonly int? WriteBufferSizeHint;
        internal readonly int ReadBufferSizeHint;
        internal readonly DynamicRowDisposal DynamicRowDisposal;

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
            char escapedValueStartAndStop,
            char escapeValueEscapeChar,
            RowEndings rowEndings,
            ReadHeaders readHeader,
            WriteHeaders writeHeaders,
            WriteTrailingNewLines writeTrailingNewLine,
            MemoryPool<char> memoryPool,
            char? commentChar,
            int? writeBufferSizeHint,
            int readBufferSizeHint,
            DynamicRowDisposal dynamicRowDisposal
        )
        {
            _TypeDescriber = describer;
            _NewCons = null;
            DeserializeColumns = Array.Empty<Column>();
            SerializeColumns = Array.Empty<Column>();
            SerializeColumnsNeedEscape = Array.Empty<bool>();
            ValueSeparator = valueSeparator;
            EscapedValueStartAndStop = escapedValueStartAndStop;
            EscapeValueEscapeChar = escapeValueEscapeChar;
            RowEnding = rowEndings;
            WriteBufferSizeHint = writeBufferSizeHint;
            ReadBufferSizeHint = readBufferSizeHint;

            switch (RowEnding)
            {
                case RowEndings.CarriageReturn:
                    RowEndingMemory = CarriageReturn;
                    break;
                case RowEndings.CarriageReturnLineFeed:
                    RowEndingMemory = CarriageReturnLineFeed;
                    break;
                case RowEndings.LineFeed:
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
            CommentChar = commentChar;
            DynamicRowDisposal = dynamicRowDisposal;
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
            char escapedValueStartAndStop,
            char escapeValueEscapeChar,
            RowEndings rowEndings,
            ReadHeaders readHeader,
            WriteHeaders writeHeaders,
            WriteTrailingNewLines writeTrailingNewLine,
            MemoryPool<char> memoryPool,
            char? commentChar,
            int? writeBufferSizeHint,
            int readBufferSizeHint
        )
        {
            _NewCons = newCons;
            DeserializeColumns = deserializeColumns;
            SerializeColumns = serializeColumns;
            SerializeColumnsNeedEscape = serializeColumnsNeedEscape;
            ValueSeparator = valueSeparator;
            EscapedValueStartAndStop = escapedValueStartAndStop;
            EscapeValueEscapeChar = escapeValueEscapeChar;
            RowEnding = rowEndings;
            WriteBufferSizeHint = writeBufferSizeHint;
            ReadBufferSizeHint = readBufferSizeHint;

            switch (RowEnding)
            {
                case RowEndings.CarriageReturn:
                    RowEndingMemory = CarriageReturn;
                    break;
                case RowEndings.CarriageReturnLineFeed:
                    RowEndingMemory = CarriageReturnLineFeed;
                    break;
                case RowEndings.LineFeed:
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
            CommentChar = commentChar;
        }

        public IAsyncReader<T> CreateAsyncReader(PipeReader reader, Encoding encoding, object? context = null)
        {
            if(reader == null)
            {
                return Throw.ArgumentNullException<IAsyncReader<T>>(nameof(reader));
            }

            if (encoding == null)
            {
                return Throw.ArgumentNullException<IAsyncReader<T>>(nameof(encoding));
            }

            // context is legally null

            var wrapper = new PipeReaderAdapter(reader, encoding);

            return CreateAsyncReader(wrapper, context);
        }

        public IAsyncReader<T> CreateAsyncReader(TextReader reader, object? context = null)
        {
            if (reader == null)
            {
                return Throw.ArgumentNullException<IAsyncReader<T>>(nameof(reader));
            }

            // context is legally null

            var wrapper = new AsyncTextReaderAdapter(reader);

            return CreateAsyncReader(wrapper, context);
        }

        public IAsyncWriter<T> CreateAsyncWriter(PipeWriter writer, Encoding encoding, object? context = null)
        {
            if(writer == null)
            {
                return Throw.ArgumentNullException<IAsyncWriter<T>>(nameof(writer));
            }

            if (encoding == null)
            {
                return Throw.ArgumentNullException<IAsyncWriter<T>>(nameof(encoding));
            }

            // context is legally null

            var wrapper = new PipeWriterAdapter(writer, encoding, MemoryPool);

            return CreateAsyncWriter(wrapper, context);
        }

        public IAsyncWriter<T> CreateAsyncWriter(TextWriter writer, object? context = null)
        {
            if(writer == null)
            {
                return Throw.ArgumentNullException<IAsyncWriter<T>>(nameof(writer));
            }

            // context is legally null

            var wrapper = new AsyncTextWriterAdapter(writer);

            return CreateAsyncWriter(wrapper, context);
        }

        public IReader<T> CreateReader(ReadOnlySequence<byte> sequence, Encoding encoding, object? context = null)
        {
            if(encoding == null)
            {
                return Throw.ArgumentNullException<IReader<T>>(nameof(encoding));
            }

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
            if(reader == null)
            {
                return Throw.ArgumentNullException<IReader<T>>(nameof(reader));
            }

            // context is legeally null

            var wrapper = new TextReaderAdapter(reader);

            return CreateReader(wrapper, context);
        }

        public IWriter<T> CreateWriter(IBufferWriter<byte> writer, Encoding encoding, object? context = null)
        {
            if (writer == null)
            {
                return Throw.ArgumentNullException<IWriter<T>>(nameof(writer));
            }

            if(encoding == null)
            {
                return Throw.ArgumentNullException<IWriter<T>>(nameof(encoding));
            }

            // context is legally null

            var wrapper = new BufferWriterByteAdapter(writer, encoding);

            return CreateWriter(wrapper, context);
        }

        public IWriter<T> CreateWriter(IBufferWriter<char> writer, object? context = null)
        {
            if(writer == null)
            {
                return Throw.ArgumentNullException<IWriter<T>>(nameof(writer));
            }

            // context is legally null

            var wrapper = new BufferWriterCharAdapter(writer);

            return CreateWriter(wrapper, context);
        }

        public IWriter<T> CreateWriter(TextWriter writer, object? context = null)
        {
            if(writer == null)
            {
                return Throw.ArgumentNullException<IWriter<T>>(nameof(writer));
            }

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
