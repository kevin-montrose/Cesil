using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cesil
{
    internal sealed class ConcreteBoundConfiguration<T> : BoundConfigurationBase<T>
    {
        // internal for testing purposes
        internal readonly NonNull<IRowConstructor<T>> RowBuilder;

        internal ConcreteBoundConfiguration(
            InstanceProvider? instanceProvider,
            IEnumerable<DeserializableMember> deserializeColumns,
            Column[] serializeColumns,
            bool[] serializeColumnsNeedEscape,
            Options options
        ) :
            base(
                deserializeColumns,
                serializeColumns,
                serializeColumnsNeedEscape,
                options
            )
        {
            if (instanceProvider != null && deserializeColumns.Any())
            {
                var builder = RowConstructor.Create<T>(MemoryPool, instanceProvider, deserializeColumns);
                RowBuilder.Value = builder;
            }
            else
            {
                RowBuilder.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertCanMakeReader()
        {
            if (!RowBuilder.HasValue)
            {
                Throw.InvalidOperationException<object>($"Cannot make a reader for {typeof(T).Name}, returned {nameof(InstanceProvider)} and {nameof(DeserializableMember)}s were not sufficient.");
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertCanMakeWriter()
        {
            if (SerializeColumns.Length == 0)
            {
                Throw.InvalidOperationException<object>($"No columns configured to write for {typeof(T).FullName}");
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IRowConstructor<T> GetMutableRowBuilder()
        => RowBuilder.Value.Clone(Options);

        internal override IReader<T> CreateReader(IReaderAdapter inner, object? context = null)
        {
            AssertCanMakeReader();

            return new Reader<T>(inner, this, context, GetMutableRowBuilder());
        }

        internal override IAsyncReader<T> CreateAsyncReader(IAsyncReaderAdapter inner, object? context = null)
        {
            AssertCanMakeReader();

            return new AsyncReader<T>(inner, this, context, GetMutableRowBuilder());
        }

        internal override IWriter<T> CreateWriter(IWriterAdapter inner, object? context = null)
        {
            AssertCanMakeWriter();

            return new Writer<T>(this, inner, context);
        }

        internal override IAsyncWriter<T> CreateAsyncWriter(IAsyncWriterAdapter inner, object? context = null)
        {
            AssertCanMakeWriter();

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
