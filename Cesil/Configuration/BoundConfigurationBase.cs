using System;
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

        internal readonly NonNull<InstanceProvider> InstanceProvider;
        internal readonly IEnumerable<DeserializableMember> DeserializeColumns;

        internal readonly Column[] SerializeColumns;
        internal readonly bool[] SerializeColumnsNeedEscape;

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
            Options options
        )
        {
            InstanceProvider.Clear();
            DeserializeColumns = Enumerable.Empty<DeserializableMember>();
            SerializeColumns = Array.Empty<Column>();
            SerializeColumnsNeedEscape = Array.Empty<bool>();

            Options = options;

            switch (Options.RowEnding)
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

            if (Options.EscapedValueStartAndEnd != null)
            {
                if (Options.CommentCharacter != null)
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
                if (Options.CommentCharacter != null)
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
            InstanceProvider? instanceProvider,
            IEnumerable<DeserializableMember> deserializeColumns,
            Column[] serializeColumns,
            bool[] serializeColumnsNeedEscape,
            Options options
        )
        {
            if (instanceProvider != null)
            {
                InstanceProvider.Value = instanceProvider;
            }
            else
            {
                InstanceProvider.Clear();
            }
            DeserializeColumns = deserializeColumns;
            SerializeColumns = serializeColumns;
            SerializeColumnsNeedEscape = serializeColumnsNeedEscape;

            Options = options;

            switch (Options.RowEnding)
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

            if (Options.EscapedValueStartAndEnd != null)
            {
                if (Options.CommentCharacter != null)
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
                if (Options.CommentCharacter != null)
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

            var wrapper = new PipeWriterAdapter(writer, encoding, Options.MemoryPool);

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
