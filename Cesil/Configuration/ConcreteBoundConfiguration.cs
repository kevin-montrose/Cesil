using System.Buffers;
using System.Text;

namespace Cesil
{
    internal sealed class ConcreteBoundConfiguration<T> : BoundConfigurationBase<T>
    {
        internal ConcreteBoundConfiguration(
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
            WhitespaceTreatments whitespaceTreatment) : 
            base(
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
                readBufferSizeHint,
                whitespaceTreatment
            )
        { }

        internal override IReader<T> CreateReader(IReaderAdapter inner, object? context = null)
        {
            if (DeserializeColumns.Length == 0)
            {
                return Throw.InvalidOperationException<IReader<T>>($"No columns configured to read for {typeof(T).FullName}");
            }

            return new Reader<T>(inner, this, context);
        }

        internal override IAsyncReader<T> CreateAsyncReader(IAsyncReaderAdapter inner, object? context = null)
        {
            if (DeserializeColumns.Length == 0)
            {
                return Throw.InvalidOperationException<IAsyncReader<T>>($"No columns configured to read for {typeof(T).FullName}");
            }

            return new AsyncReader<T>(inner, this, context);
        }

        internal override IWriter<T> CreateWriter(IWriterAdapter inner, object? context = null)
        {
            if (SerializeColumns.Length == 0)
            {
                return Throw.InvalidOperationException<IWriter<T>>($"No columns configured to write for {typeof(T).FullName}");
            }

            return new Writer<T>(this, inner, context);
        }

        internal override IAsyncWriter<T> CreateAsyncWriter(IAsyncWriterAdapter inner, object? context = null)
        {
            if (SerializeColumns.Length == 0)
            {
                return Throw.InvalidOperationException<IAsyncWriter<T>>($"No columns configured to write for {typeof(T).FullName}");
            }

            return new AsyncWriter<T>(this, inner, context);
        }

        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append($"{nameof(ConcreteBoundConfiguration<T>)} with ");
            if (HasCommentChar)
            {
                ret.Append($"{nameof(CommentChar)}={CommentChar}");
            }
            // Dynamic* not included, since not relevant
            if (HasEscapedValueStartAndStop)
            {
                ret.Append($", {nameof(EscapedValueStartAndStop)}={EscapedValueStartAndStop}");
            }
            if (HasEscapeValueEscapeChar)
            {
                ret.Append($", {nameof(EscapeValueEscapeChar)}={EscapeValueEscapeChar}");
            }
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
