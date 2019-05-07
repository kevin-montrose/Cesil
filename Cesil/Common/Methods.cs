using System.Reflection;

namespace Cesil
{
    internal static class Methods
    {
        internal static class DynamicCell
        {
            public static readonly MethodInfo GetDataSpan = Types.DynamicCell.GetMethod(nameof(Cesil.DynamicCell.GetDataSpan), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetReadContext = Types.DynamicCell.GetMethod(nameof(Cesil.DynamicCell.GetReadContext), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo CastTo = Types.DynamicCell.GetMethod(nameof(Cesil.DynamicCell.CastTo), BindingFlags.Instance | BindingFlags.NonPublic);
        }

        internal static class Throw
        {
            public static readonly MethodInfo InvalidOperationException = Types.Throw.GetMethod(nameof(Cesil.Throw.InvalidOperationException), BindingFlags.Static | BindingFlags.NonPublic);
        }

        internal static class DynamicRow
        {
            public static readonly MethodInfo GetIndex = Types.DynamicRow.GetMethod(nameof(Cesil.DynamicRow.GetIndex), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetIndexTyped = Types.DynamicRow.GetMethod(nameof(Cesil.DynamicRow.GetIndexTyped), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetValue = Types.DynamicRow.GetMethod(nameof(Cesil.DynamicRow.GetValue), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetReadContext = Types.DynamicRow.GetMethod(nameof(Cesil.DynamicRow.GetReadContext), BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
