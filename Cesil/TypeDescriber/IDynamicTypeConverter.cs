using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// The interface used to discover how to convert dynamically typed values
    ///   to concrete types.
    /// </summary>
    public interface IDynamicTypeConverter
    {
        /// <summary>
        /// Called to determine how to convert a cell in the given column, as identified by
        ///    it's number (base-0) and column name (which may be null), into the given type.
        ///    
        /// Column name will be null if headers were not configured or discovered during reading.
        /// </summary>
        DynamicCellConverter GetCellConverter(int columnNumber, string columnName, TypeInfo targetType);

        /// <summary>
        /// Called to determine how to convert an entire row into the given type, as identified by
        ///   it's number (base-0), into the given type.
        ///   
        /// Column names will be null if headers were not configured or discovered during reading.
        /// </summary>
        DynamicRowConverter GetRowConverter(int rowNumber, int columnCount, string[] columnNames, TypeInfo targetType);
    }
}
