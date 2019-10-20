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
        /// Get the provider for instances of forType.
        /// </summary>
        InstanceProvider GetInstanceProvider(TypeInfo forType);
        /// <summary>
        /// Enumerate all the members on forType to serialize.
        /// </summary>
        IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType);
        /// <summary>
        /// Enumerate all the members on forType to deserialize.
        /// </summary>
        IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType);

        /// <summary>
        /// Called to determine how to convert a dynamic cell.
        /// </summary>
        Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType);

        /// <summary>
        /// Called to determine how to convert an entire dynamic row into the given type, as identified by
        ///   it's number (base-0), into the given type.
        ///   
        /// Column names will be exposed on individual column identifiers only if they are set.
        /// </summary>
        DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType);

        /// <summary>
        /// Called to determine the cells that make up the given dynamic row.
        /// </summary>
        IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext ctx, object row);
    }
}
