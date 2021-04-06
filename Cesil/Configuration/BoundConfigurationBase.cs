﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;

namespace Cesil
{
    internal abstract class BoundConfigurationBase<T> : IBoundConfiguration<T>
    {
        private static readonly ReadOnlyMemory<char> CarriageReturn = "\r".AsMemory();
        private static readonly ReadOnlyMemory<char> LineFeed = "\n".AsMemory();
        private static readonly ReadOnlyMemory<char> CarriageReturnLineFeed = "\r\n".AsMemory();

        public Options Options { get; }

        internal readonly ReadOnlyMemory<char> RowEndingMemory;
        internal readonly ReadOnlyMemory<char> ValueSeparatorMemory;

        internal readonly DeserializableMember[] DeserializeColumns;

        internal readonly Column[] SerializeColumns;
        internal readonly bool[] SerializeColumnsNeedEscape;

        internal readonly NeedsEncodeHelper NeedsEncode;

        internal readonly MemoryPool<char> MemoryPool;
        internal readonly MemoryPool<DynamicCellValue> DynamicMemoryPool;

        /// <summary>
        /// For working with dynamic.
        /// </summary>
        protected BoundConfigurationBase(
            Options options
        )
        {
            DeserializeColumns = Array.Empty<DeserializableMember>();
            SerializeColumns = Array.Empty<Column>();
            SerializeColumnsNeedEscape = Array.Empty<bool>();

            Options = options;

            RowEndingMemory =
                Options.WriteRowEnding switch
                {
                    WriteRowEnding.CarriageReturn => CarriageReturn,
                    WriteRowEnding.CarriageReturnLineFeed => CarriageReturnLineFeed,
                    WriteRowEnding.LineFeed => LineFeed,
                    _ => Throw.ImpossibleException_Returns<ReadOnlyMemory<char>>($"Observed an unexpected {nameof(WriteRowEnding)}: {Options.WriteRowEnding}")
                };

            NeedsEncode = new NeedsEncodeHelper(options.ValueSeparator, options.EscapedValueStartAndEnd, options.CommentCharacter);

            ValueSeparatorMemory = options.ValueSeparator.AsMemory();

            MemoryPool = options.MemoryPoolProvider.GetMemoryPool<char>();
            DynamicMemoryPool = options.MemoryPoolProvider.GetMemoryPool<DynamicCellValue>();
        }

        /// <summary>
        /// For working with concrete types.
        /// </summary>
        protected BoundConfigurationBase(
            IEnumerable<DeserializableMember> deserializeColumns,
            Column[] serializeColumns,
            bool[] serializeColumnsNeedEscape,
            Options options
        )
        {
            DeserializeColumns = deserializeColumns.ToArray();
            SerializeColumns = serializeColumns;
            SerializeColumnsNeedEscape = serializeColumnsNeedEscape;

            Options = options;

            RowEndingMemory =
                Options.WriteRowEnding switch
                {
                    WriteRowEnding.CarriageReturn => CarriageReturn,
                    WriteRowEnding.CarriageReturnLineFeed => CarriageReturnLineFeed,
                    WriteRowEnding.LineFeed => LineFeed,
                    _ => Throw.ImpossibleException_Returns<ReadOnlyMemory<char>>($"Observed an unexpected {nameof(WriteRowEnding)}: {Options.WriteRowEnding}")
                };

            NeedsEncode = new NeedsEncodeHelper(options.ValueSeparator, options.EscapedValueStartAndEnd, options.CommentCharacter);

            ValueSeparatorMemory = options.ValueSeparator.AsMemory();

            MemoryPool = options.MemoryPoolProvider.GetMemoryPool<char>();
            DynamicMemoryPool = options.MemoryPoolProvider.GetMemoryPool<DynamicCellValue>();
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

            // context is legally null

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
