using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// The interface used to discover the members to (de)serialize for a type.
    /// 
    /// DefaultTypeDescriber, ManualTypeDescriber, and SurrogateTypeDescriber all implement
    ///   this interface and handle the most common desired configurations.
    /// </summary>
    public interface ITypeDescriber
    {
        /// <summary>
        /// Enumerate all the members on forType to serialize.
        /// </summary>
        IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType);
        /// <summary>
        /// Enumerate all the members on forType to deserialize.
        /// </summary>
        IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType);
    }
}
