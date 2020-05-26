using System.Diagnostics.CodeAnalysis;

namespace Cesil
{
    internal sealed class EmptyDynamicRowOwner : IDynamicRowOwner
    {
        internal static readonly IDynamicRowOwner Singleton = new EmptyDynamicRowOwner();

        public Options Options => Throw.ImpossibleException<Options>("Shouldn't be possible");

        public object? Context => Throw.ImpossibleException<object?>("Shouldn't be possible");

        private EmptyDynamicRowOwner() { }

        public void Remove(DynamicRow row)
        => Throw.ImpossibleException<object>("Shouldn't be possible");

        public NameLookup AcquireNameLookup()
        => Throw.ImpossibleException<NameLookup>("Shouldn't be possible");

        public void ReleaseNameLookup()
        => Throw.ImpossibleException<object>("Shouldn't be possible");

        bool IDelegateCache.TryGetDelegate<T, V>(T key, [MaybeNullWhen(returnValue:false)]out V del)
        {
            del = default;
            return Throw.ImpossibleException<bool>("Shouldn't be possible");
        }

        void IDelegateCache.AddDelegate<T, V>(T key, V cached)
        => Throw.ImpossibleException<object>("Shouldn't be possible");
    }
}
