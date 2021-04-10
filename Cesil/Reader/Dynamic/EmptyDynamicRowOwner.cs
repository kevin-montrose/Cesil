using System.Diagnostics.CodeAnalysis;

namespace Cesil
{
    internal sealed class EmptyDynamicRowOwner : IDynamicRowOwner
    {
        internal static readonly IDynamicRowOwner Singleton = new EmptyDynamicRowOwner();

        public Options Options => Throw.ImpossibleException_Returns<Options>("Shouldn't be possible");

        public object? Context => Throw.ImpossibleException_Returns<object?>("Shouldn't be possible");

        private EmptyDynamicRowOwner() { }

        public void Remove(DynamicRow row)
        => Throw.ImpossibleException_Returns<object>("Shouldn't be possible");

        public NameLookup AcquireNameLookup()
        => Throw.ImpossibleException_Returns<NameLookup>("Shouldn't be possible");

        public void ReleaseNameLookup()
        => Throw.ImpossibleException_Returns<object>("Shouldn't be possible");

        bool IDelegateCache.TryGetDelegate<T, V>(T key, [MaybeNullWhen(returnValue: false)] out V del)
        {
#pragma warning disable CES0005 // this value isn't going to matter, since this method always explodes
            del = default!;
#pragma warning restore CES0005
            Throw.ImpossibleException("Shouldn't be possible");
            return default;
        }

        void IDelegateCache.AddDelegate<T, V>(T key, V cached)
        {
            Throw.ImpossibleException("Shouldn't be possible");
        }
    }
}
