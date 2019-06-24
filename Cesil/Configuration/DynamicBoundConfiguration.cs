using System;
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

        public override IAsyncWriter<dynamic> CreateAsyncWriter(TextWriter writer, object context = null)
        {
            // todo
            throw new NotImplementedException();
        }

        public override IReader<dynamic> CreateReader(TextReader reader, object context = null)
        {
            if (reader == null)
            {
                Throw.ArgumentNullException(nameof(reader));
            }

            return new DynamicReader(reader, this, context);
        }

        public override IAsyncReader<dynamic> CreateAsyncReader(TextReader reader, object context = null)
        {
            if (reader == null)
            {
                Throw.ArgumentNullException(nameof(reader));
            }

            return new AsyncDynamicReader(reader, this, context);
        }

        public override IWriter<dynamic> CreateWriter(TextWriter writer, object context = null)
        {
            if (writer == null)
            {
                Throw.ArgumentNullException(nameof(writer));
            }

            return new DynamicWriter(this, writer, context);
        }

        public override string ToString()
        {
            var ret = new StringBuilder();
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
