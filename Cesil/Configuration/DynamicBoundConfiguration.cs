using System.Text;

namespace Cesil
{
    internal sealed class DynamicBoundConfiguration : BoundConfigurationBase<dynamic>
    {
        internal DynamicBoundConfiguration(Options options) : base(options) { }

        internal override IAsyncWriter<dynamic> CreateAsyncWriter(IAsyncWriterAdapter writer, object? context = null)
        {
            return new AsyncDynamicWriter(this, writer, context);
        }

        internal override IReader<dynamic> CreateReader(IReaderAdapter reader, object? context = null)
        {
            return new DynamicReader(reader, this, context);
        }

        internal override IAsyncReader<dynamic> CreateAsyncReader(IAsyncReaderAdapter reader, object? context = null)
        {
            return new AsyncDynamicReader(reader, this, context);
        }

        internal override IWriter<dynamic> CreateWriter(IWriterAdapter writer, object? context = null)
        {
            return new DynamicWriter(this, writer, context);
        }

        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append($"{nameof(DynamicBoundConfiguration)} with ");
            ret.Append($"{nameof(Options)} = ({Options})");
            return ret.ToString();
        }
    }
}
