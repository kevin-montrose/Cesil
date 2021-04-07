using System.Runtime.CompilerServices;
using System.Text;

namespace Cesil
{
    internal sealed class DynamicBoundConfiguration : BoundConfigurationBase<dynamic>
    {
        internal DynamicBoundConfiguration(Options options) : base(options) { }

        internal override AsyncDynamicWriter CreateAsyncWriter(IAsyncWriterAdapter writer, object? context = null)
        {
            return new AsyncDynamicWriter(this, writer, context);
        }

        internal override DynamicReader CreateReader(IReaderAdapter reader, object? context = null)
        {
            return new DynamicReader(reader, this, context);
        }

        internal override AsyncDynamicReader CreateAsyncReader(IAsyncReaderAdapter reader, object? context = null)
        {
            return new AsyncDynamicReader(reader, this, context);
        }

        internal override DynamicWriter CreateWriter(IWriterAdapter writer, object? context = null)
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
