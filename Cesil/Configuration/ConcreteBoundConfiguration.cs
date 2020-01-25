using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cesil
{
    internal sealed class ConcreteBoundConfiguration<T> : BoundConfigurationBase<T>
    {
        internal ConcreteBoundConfiguration(
            InstanceProvider? instanceProvider,
            IEnumerable<DeserializableMember> deserializeColumns,
            Column[] serializeColumns,
            bool[] serializeColumnsNeedEscape,
            Options options
        ) :
            base(
                instanceProvider,
                deserializeColumns,
                serializeColumns,
                serializeColumnsNeedEscape,
                options
            )
        { }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertCanMakeReader()
        {
            if (!InstanceProvider.HasValue)
            {
                Throw.InvalidOperationException<object>($"Cannot make a reader for {typeof(T).Name}, no {nameof(InstanceProvider)} was discovered using {nameof(Options)} provided to {nameof(Configuration)}.");
            }

            if (!DeserializeColumns.Any())
            {
                Throw.InvalidOperationException<IReader<T>>($"No columns configured to read for {typeof(T).FullName}");
            }
        }

        internal override IReader<T> CreateReader(IReaderAdapter inner, object? context = null)
        {
            AssertCanMakeReader();

            return new Reader<T>(inner, this, context, CreateRowConstructor());
        }

        internal override IAsyncReader<T> CreateAsyncReader(IAsyncReaderAdapter inner, object? context = null)
        {
            AssertCanMakeReader();

            return new AsyncReader<T>(inner, this, context, CreateRowConstructor());
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

        private IRowConstructor<T> CreateRowConstructor()
        => RowConstructor.Create<T>(Options.MemoryPool, InstanceProvider.Value, DeserializeColumns);

        public override string ToString()
        {
            var ret = new StringBuilder();
            ret.Append($"{nameof(ConcreteBoundConfiguration<T>)} with ");
            ret.Append($"{nameof(Options)} = ({Options})");
            return ret.ToString();
        }
    }
}
