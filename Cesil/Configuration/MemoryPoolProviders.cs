using System.Buffers;

namespace Cesil
{
    /// <summary>
    /// Holds references to pre-allocated IMemoryPoolProviders.
    /// </summary>
    public static class MemoryPoolProviders
    {
        // no reason to expose something this trivial
        internal sealed class DefaultMemoryPoolProvider : IMemoryPoolProvider
        {
            internal DefaultMemoryPoolProvider() { }

            public MemoryPool<T> GetMemoryPool<T>()
            => MemoryPool<T>.Shared;

            public override string ToString()
            => $"{nameof(DefaultMemoryPoolProvider)} Shared Instance";
        }

        /// <summary>
        /// Returns the default IMemoryPoolProvider instance, which obtains
        /// MemoryPools via MemoryPool(T).Shared.
        /// </summary>
        public static IMemoryPoolProvider Default { get; } = new DefaultMemoryPoolProvider();
    }
}
