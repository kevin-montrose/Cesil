using System;
using System.Buffers;
using System.IO;

namespace Cesil
{
    internal sealed class DynamicBoundConfiguration : BoundConfigurationBase<dynamic>
    {
        internal DynamicBoundConfiguration(
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
            ) :
            base(
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
                dynamicTypeConverter,
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
            if(reader == null)
            {
                Throw.ArgumentNullException(nameof(reader));
            }

            return new AsyncDynamicReader(reader, this, context);
        }

        public override IWriter<dynamic> CreateWriter(TextWriter writer, object context = null)
        {
            // todo
            throw new NotImplementedException();
        }
    }
}
