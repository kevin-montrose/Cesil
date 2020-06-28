using System.Buffers;

namespace Cesil
{
    /// <summary>
    /// Interface used to obtain MemoryPools.
    /// 
    /// Cesil uses this during creation of IBoundConfigurations to determine
    ///   where necessary allocations during read/writing will be placed.
    /// </summary>
    public interface IMemoryPoolProvider
    {
        /// <summary>
        /// Returns a MemoryPool for the given type.
        /// </summary>
        MemoryPool<TElement> GetMemoryPool<TElement>();
    }
}
