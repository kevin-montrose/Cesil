using System;
using System.Buffers;
using System.IO;

namespace Cesil
{
    internal abstract class BoundConfigurationBase<T> : IBoundConfiguration<T>
    {
        private static readonly ReadOnlyMemory<char> CarriageReturn = "\r".AsMemory();
        private static readonly ReadOnlyMemory<char> LineFeed = "\n".AsMemory();
        private static readonly ReadOnlyMemory<char> CarriageReturnLineFeed = "\r\n".AsMemory();

        internal readonly InstanceBuilderDelegate<T> NewCons;
        internal readonly Column[] DeserializeColumns;

        internal readonly Column[] SerializeColumns;
        internal readonly bool[] SerializeColumnsNeedEscape;

        internal readonly char ValueSeparator;
        internal readonly ReadOnlyMemory<char> ValueSeparatorMemory;
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
        internal readonly IDynamicTypeConverter DynamicTypeConverter;
        internal readonly DynamicRowDisposal DynamicRowDisposal;

        /// <summary>
        /// For working with dynamic.
        /// </summary>
        protected BoundConfigurationBase(
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
            IDynamicTypeConverter dynamicTypeConverter,
            DynamicRowDisposal dynamicRowDisposal
        )
        {
            NewCons = null;
            DeserializeColumns = Array.Empty<Column>();
            SerializeColumns = Array.Empty<Column>();
            SerializeColumnsNeedEscape = Array.Empty<bool>();
            ValueSeparator = valueSeparator;
            ValueSeparatorMemory = ValueSeparator.ToString().AsMemory();
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
            DynamicTypeConverter = dynamicTypeConverter;
            DynamicRowDisposal = dynamicRowDisposal;
        }

        /// <summary>
        /// For working with concrete types.
        /// </summary>
        protected BoundConfigurationBase(
            InstanceBuilderDelegate<T> newCons,
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
            NewCons = newCons;
            DeserializeColumns = deserializeColumns;
            SerializeColumns = serializeColumns;
            SerializeColumnsNeedEscape = serializeColumnsNeedEscape;
            ValueSeparator = valueSeparator;
            ValueSeparatorMemory = ValueSeparator.ToString().AsMemory();
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
            DynamicTypeConverter = null;
        }

        public abstract IAsyncReader<T> CreateAsyncReader(TextReader reader, object context = null);

        public abstract IAsyncWriter<T> CreateAsyncWriter(TextWriter writer, object context = null);

        public abstract IReader<T> CreateReader(TextReader reader, object context = null);

        public abstract IWriter<T> CreateWriter(TextWriter writer, object context = null);
    }
}
