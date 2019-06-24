﻿using System.Reflection;

namespace Cesil
{
    internal static class Fields
    {
        internal static class DynamicRow
        {
            public static readonly FieldInfo RowNumber = Types.DynamicRowType.GetField(nameof(Cesil.DynamicRow.RowNumber), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly FieldInfo Converter = Types.DynamicRowType.GetField(nameof(Cesil.DynamicRow.Converter), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly FieldInfo Columns = Types.DynamicRowType.GetField(nameof(Cesil.DynamicRow.Columns), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly FieldInfo Context = Types.DynamicRowType.GetField(nameof(Cesil.DynamicRow.Context), BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}