using System.Buffers;
using System.IO;
using System.Text;

namespace Cesil
{
    internal sealed class DynamicBoundConfiguration : BoundConfigurationBase<dynamic>
    {
        internal DynamicBoundConfiguration(
            ITypeDescriber describer,
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
            ) :
            base(
                describer,
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
                dynamicRowDisposal
            )
        { }

        internal override IAsyncWriter<dynamic> CreateAsyncWriter(IAsyncWriterAdapter writer, object context = null)
        {
            return new AsyncDynamicWriter(this, writer, context);
        }

        internal override IReader<dynamic> CreateReader(IReaderAdapter reader, object context = null)
        {
            return new DynamicReader(reader, this, context);
        }

        internal override IAsyncReader<dynamic> CreateAsyncReader(IAsyncReaderAdapter reader, object context = null)
        {
            return new AsyncDynamicReader(reader, this, context);
        }

        internal override IWriter<dynamic> CreateWriter(IWriterAdapter writer, object context = null)
        {
            return new DynamicWriter(this, writer, context);
        }

        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append($"{nameof(DynamicBoundConfiguration)} with ");
            ret.Append($"{nameof(CommentChar)}={CommentChar}");
            ret.Append($", {nameof(DynamicRowDisposal)}={DynamicRowDisposal}");
            ret.Append($", {nameof(TypeDescriber)}={TypeDescriber}");
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
