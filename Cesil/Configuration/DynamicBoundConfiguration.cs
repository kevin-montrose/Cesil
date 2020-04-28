using System.Runtime.CompilerServices;
using System.Text;

namespace Cesil
{
    internal sealed class DynamicBoundConfiguration : BoundConfigurationBase<dynamic>
    {
        internal DynamicBoundConfiguration(Options options) : base(options) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertCanMakeWriter()
        {
            if (Options.RowEnding == RowEnding.Detect)
            {
                Throw.InvalidOperationException<object>($"Cannot write with a format that has {nameof(RowEnding)} option of {RowEnding.Detect}");
                return;
            }
        }

        internal override IAsyncWriter<dynamic> CreateAsyncWriter(IAsyncWriterAdapter writer, object? context = null)
        {
            AssertCanMakeWriter();

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
            AssertCanMakeWriter();

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
