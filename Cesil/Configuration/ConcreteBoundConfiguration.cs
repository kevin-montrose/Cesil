using System.Buffers;
using System.IO;
using System.Text;

namespace Cesil
{
    internal sealed class ConcreteBoundConfiguration<T> : BoundConfigurationBase<T>
    {
        internal ConcreteBoundConfiguration(
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
            int readBufferSizeHint) : base(
                    newCons,
                    deserializeColumns,
                    serializeColumns,
                    serializeColumnsNeedEscape,
                    valueSeparator,
                    escapedValueStartAndStop,
                    escapeValueEscapeChar,
                    rowEndings,
                    readHeader,
                    writeHeaders,
                    writeTrailingNewLine,
                    memoryPool,
                    commentChar,
                    writeBufferSizeHint,
                    readBufferSizeHint
            )
        { }

        public override IReader<T> CreateReader(TextReader inner, object context = null)
        {
            if (DeserializeColumns.Length == 0)
            {
                Throw.InvalidOperationException($"No columns configured to read for {typeof(T).FullName}");
            }

            if (inner == null)
            {
                Throw.ArgumentNullException(nameof(inner));
            }

            return new Reader<T>(inner, this, context);
        }

        public override IAsyncReader<T> CreateAsyncReader(TextReader inner, object context = null)
        {
            if (DeserializeColumns.Length == 0)
            {
                Throw.InvalidOperationException($"No columns configured to read for {typeof(T).FullName}");
            }

            if (inner == null)
            {
                Throw.ArgumentNullException(nameof(inner));
            }

            return new AsyncReader<T>(inner, this, context);
        }

        public override IWriter<T> CreateWriter(TextWriter inner, object context = null)
        {
            if (SerializeColumns.Length == 0)
            {
                Throw.InvalidOperationException($"No columns configured to write for {typeof(T).FullName}");
            }

            if (inner == null)
            {
                Throw.ArgumentNullException(nameof(inner));
            }

            return new Writer<T>(this, inner, context);
        }

        public override IAsyncWriter<T> CreateAsyncWriter(TextWriter inner, object context = null)
        {
            if (SerializeColumns.Length == 0)
            {
                Throw.InvalidOperationException($"No columns configured to write for {typeof(T).FullName}");
            }

            if (inner == null)
            {
                Throw.ArgumentNullException(nameof(inner));
            }

            return new AsyncWriter<T>(this, inner, context);
        }

        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append($"{nameof(ConcreteBoundConfiguration<T>)} with ");
            ret.Append($"{nameof(CommentChar)}={CommentChar}");
            // Dynamic* not included, since not relevant
            ret.Append($", {nameof(EscapedValueStartAndStop)}={EscapedValueStartAndStop}");
            ret.Append($", {nameof(EscapeValueEscapeChar)}={EscapeValueEscapeChar}");
            ret.Append($", {nameof(MemoryPool)}={MemoryPool}");
            ret.Append($", {nameof(NewCons)}={NewCons}");
            ret.Append($", {nameof(ReadBufferSizeHint)}={ReadBufferSizeHint}");
            ret.Append($", {nameof(ReadHeader)}={ReadHeader}");
            ret.Append($", {nameof(RowEnding)}={RowEnding}");
            // skipping RowEndingMemory
            ret.Append($", {nameof(ValueSeparator)}={ValueSeparator}");
            ret.Append($", {nameof(WriteBufferSizeHint)}={WriteBufferSizeHint}");
            ret.Append($", {nameof(WriteHeader)}={WriteHeader}");
            ret.Append($", {nameof(WriteTrailingNewLine)}={WriteTrailingNewLine}");

            return ret.ToString();
        }
    }
}
