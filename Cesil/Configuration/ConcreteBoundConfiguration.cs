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
            Options options
        ) :
            base(
                newCons,
                deserializeColumns,
                serializeColumns,
                serializeColumnsNeedEscape,
                options
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
            ret.Append($"{nameof(Options)} = ({Options})");
            return ret.ToString();
        }
    }
}
