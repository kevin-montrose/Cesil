using System.Reflection;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    internal static class Fields
    {
        internal static class DynamicRow
        {
            internal static readonly FieldInfo Columns = Types.DynamicRow.GetFieldNonNull(nameof(Cesil.DynamicRow.Columns), InternalInstance);
            internal static readonly FieldInfo Context = Types.DynamicRow.GetFieldNonNull(nameof(Cesil.DynamicRow.Context), InternalInstance);
            internal static readonly FieldInfo Converter = Types.DynamicRow.GetFieldNonNull(nameof(Cesil.DynamicRow.Converter), InternalInstance);
            internal static readonly FieldInfo Owner = Types.DynamicRow.GetFieldNonNull(nameof(Cesil.DynamicRow.Owner), InternalInstance);
            internal static readonly FieldInfo RowNumber = Types.DynamicRow.GetFieldNonNull(nameof(Cesil.DynamicRow.RowNumber), InternalInstance);
        }

        internal static class DynamicCell
        {
            internal static readonly FieldInfo Row = Types.DynamicCell.GetFieldNonNull(nameof(Cesil.DynamicCell.Row), InternalInstance);
        }

        internal static class DynamicRowRange
        {
            internal static readonly FieldInfo Length = Types.DynamicRowRange.GetFieldNonNull(nameof(Cesil.DynamicRowRange.Length), InternalInstance);
            internal static readonly FieldInfo Offset = Types.DynamicRowRange.GetFieldNonNull(nameof(Cesil.DynamicRowRange.Offset), InternalInstance);
            internal static readonly FieldInfo Parent = Types.DynamicRowRange.GetFieldNonNull(nameof(Cesil.DynamicRowRange.Parent), InternalInstance);
        }
    }
}
