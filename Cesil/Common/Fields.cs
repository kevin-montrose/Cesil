﻿using System.Reflection;

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
    }
}
