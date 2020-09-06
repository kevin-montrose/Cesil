using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// The interface used to discover the members to (de)serialize for a type.
    /// 
    /// DefaultTypeDescriber, ManualTypeDescriber, and SurrogateTypeDescriber all implement
    ///   this interface and handle the most common desired configurations.
    ///   
    /// Note to implementors: All ITypeDescriber methods must be thread safe, as they are invoked 
    ///   as needed during operation with no guarantee about the calling thread(s).
    /// </summary>
    public interface ITypeDescriber
    {
        // for static typing scenarios

        /// <summary>
        /// Get the provider for instances of forType.
        /// 
        /// Returns null if no InstanceProvider could be found.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        InstanceProvider? GetInstanceProvider(TypeInfo forType);
        /// <summary>
        /// Enumerate all the members on forType to serialize.
        /// </summary>
        IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType);
        /// <summary>
        /// Enumerate all the members on forType to deserialize.
        /// </summary>
        IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType);

        // for dynamic typing scenarios

        /// <summary>
        /// Called to determine how to convert a dynamic cell.
        /// 
        /// Returns null if no Parser could be found.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        Parser? GetDynamicCellParserFor(in ReadContext context, TypeInfo targetType);

        /// <summary>
        /// Called to determine how to convert an entire dynamic row into the given type.
        /// 
        /// Returns null if no DynamicRowConverter could be found.
        /// </summary>
        [return: NullableExposed("May not be known, null is cleanest way to handle it")]
        DynamicRowConverter? GetDynamicRowConverter(in ReadContext context, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType);

        /// <summary>
        /// Called to determine the cells that make up the given dynamic row.
        /// 
        /// Returns the number of cells extracted from the row.  If the given span is 
        /// too small, its contents are ignored and GetCellsForDynamicRow is called again with one 
        /// of at least the indicated size.
        /// 
        /// The content of the given span is undefined, in particular no values are guaranteed
        /// to be carried forward between calls.
        /// </summary>
        [return: IntentionallyExposedPrimitive("Count, int is the best option")]
        int GetCellsForDynamicRow(in WriteContext context, object row, Span<DynamicCellValue> cells);
    }
}
