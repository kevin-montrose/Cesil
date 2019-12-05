using System.Reflection;

namespace Cesil
{
    internal static class Fields
    {
        internal static class DynamicRow
        {
            public static readonly FieldInfo Columns = Types.DynamicRowType.GetFieldNonNull(nameof(Cesil.DynamicRow.Columns), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly FieldInfo Context = Types.DynamicRowType.GetFieldNonNull(nameof(Cesil.DynamicRow.Context), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly FieldInfo Converter = Types.DynamicRowType.GetFieldNonNull(nameof(Cesil.DynamicRow.Converter), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly FieldInfo Owner = Types.DynamicRowType.GetFieldNonNull(nameof(Cesil.DynamicRow.Owner), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly FieldInfo RowNumber = Types.DynamicRowType.GetFieldNonNull(nameof(Cesil.DynamicRow.RowNumber), BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
