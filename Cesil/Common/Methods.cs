using System.Reflection;

namespace Cesil
{
    internal static class Methods
    {
        internal static class ReadContext
        {
            public static readonly MethodInfo ConvertingRow = Types.ReadContextType.GetMethod(nameof(Cesil.ReadContext.ConvertingRow), BindingFlags.NonPublic | BindingFlags.Static);
        }

        internal static class ITypeDescriber
        {
            public static readonly MethodInfo GetCellParserFor = Types.ITypeDescriberType.GetMethod(nameof(Cesil.ITypeDescriber.GetDynamicCellParserFor));
            public static readonly MethodInfo GetRowConverter = Types.ITypeDescriberType.GetMethod(nameof(Cesil.ITypeDescriber.GetDynamicRowConverter));
        }

        internal static class DynamicCell
        {
            public static readonly MethodInfo GetConverter = Types.DynamicCellType.GetProperty(nameof(Cesil.DynamicCell.Converter), BindingFlags.Instance | BindingFlags.NonPublic).GetMethod;
            public static readonly MethodInfo GetDataSpan = Types.DynamicCellType.GetMethod(nameof(Cesil.DynamicCell.GetDataSpan), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetReadContext = Types.DynamicCellType.GetMethod(nameof(Cesil.DynamicCell.GetReadContext), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo CastTo = Types.DynamicCellType.GetMethod(nameof(Cesil.DynamicCell.CastTo), BindingFlags.Instance | BindingFlags.NonPublic);
        }

        internal static class Throw
        {
            public static readonly MethodInfo InvalidOperationException = Types.ThrowType.GetMethod(nameof(Cesil.Throw.InvalidOperationException), BindingFlags.Static | BindingFlags.NonPublic);
        }

        internal static class DynamicRow
        {
            public static readonly MethodInfo GetAt = Types.DynamicRowType.GetMethod(nameof(Cesil.DynamicRow.GetAt), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetAtTyped = Types.DynamicRowType.GetMethod(nameof(Cesil.DynamicRow.GetAtTyped), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetByName = Types.DynamicRowType.GetMethod(nameof(Cesil.DynamicRow.GetByName), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetByIndex = Types.DynamicRowType.GetMethod(nameof(Cesil.DynamicRow.GetByIndex), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetRange = Types.DynamicRowType.GetMethod(nameof(Cesil.DynamicRow.GetRange), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetReadContext = Types.DynamicRowType.GetMethod(nameof(Cesil.DynamicRow.GetReadContext), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetByIdentifier = Types.DynamicRowType.GetMethod(nameof(Cesil.DynamicRow.GetByIdentifier), BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
