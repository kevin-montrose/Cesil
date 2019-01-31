using System;
using System.Buffers;
using System.IO;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Represents and Options and Type pair.
    /// 
    /// Used to create readers and writers.
    /// </summary>
    public sealed class BoundConfiguration<T>
        where T : new()
    {
        internal readonly ConstructorInfo NewCons;
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

        internal BoundConfiguration(
            ConstructorInfo newCons,
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
                    RowEndingMemory = "\r".AsMemory();
                    break;
                case RowEndings.CarriageReturnLineFeed:
                    RowEndingMemory = "\r\n".AsMemory();
                    break;
                case RowEndings.LineFeed:
                    RowEndingMemory = "\n".AsMemory();
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

        /// <summary>
        /// Create a synchronous reader for the given reader.
        /// </summary>
        public IReader<T> CreateReader(TextReader inner)
        {
            if (DeserializeColumns.Length == 0)
            {
                Throw.InvalidOperation($"No columns configured to read for {typeof(T).FullName}");
            }

            if (inner == null)
            {
                Throw.ArgumentNullException(nameof(inner));
            }

            if (CommentChar != null)
            {
                return new Reader<T>(inner, this);
            }
            else
            {
                return new Reader<T>(inner, this);
            }
        }

        /// <summary>
        /// Create an asynchronous reader for the given reader.
        /// </summary>
        public IAsyncReader<T> CreateAsyncReader(TextReader inner)
        {
            if (DeserializeColumns.Length == 0)
            {
                Throw.InvalidOperation($"No columns configured to read for {typeof(T).FullName}");
            }

            if (inner == null)
            {
                Throw.ArgumentNullException(nameof(inner));
            }

            if (CommentChar != null)
            {
                return new AsyncReader<T>(inner, this);
            }
            else
            {
                return new AsyncReader<T>(inner, this);
            }
        }

        /// <summary>
        /// Create a synchronous writer for the given writer.
        /// </summary>
        public IWriter<T> CreateWriter(TextWriter inner)
        {
            if (SerializeColumns.Length == 0)
            {
                Throw.InvalidOperation($"No columns configured to write for {typeof(T).FullName}");
            }

            if (inner == null)
            {
                Throw.ArgumentNullException(nameof(inner));
            }

            return new Writer<T>(this, inner);
        }

        /// <summary>
        /// Create an asynchronous writer for the given writer.
        /// </summary>
        public IAsyncWriter<T> CreateAsyncWriter(TextWriter inner)
        {
            if (SerializeColumns.Length == 0)
            {
                Throw.InvalidOperation($"No columns configured to write for {typeof(T).FullName}");
            }

            if (inner == null)
            {
                Throw.ArgumentNullException(nameof(inner));
            }

            return new AsyncWriter<T>(this, inner);
        }
    }
}
