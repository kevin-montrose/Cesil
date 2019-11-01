﻿using System.Reflection;

namespace Cesil
{
    internal static class Methods
    {
        internal static class ReadContext
        {
            public static readonly MethodInfo ConvertingRow = Types.ReadContextType.GetMethodNonNull(nameof(Cesil.ReadContext.ConvertingRow), BindingFlags.NonPublic | BindingFlags.Static);
        }

        internal static class ITypeDescriber
        {
            public static readonly MethodInfo GetDynamicCellParserFor = Types.ITypeDescriberType.GetMethodNonNull(nameof(Cesil.ITypeDescriber.GetDynamicCellParserFor));
            public static readonly MethodInfo GetDynamicRowConverter = Types.ITypeDescriberType.GetMethodNonNull(nameof(Cesil.ITypeDescriber.GetDynamicRowConverter));
        }

        internal static class DynamicCell
        {
            public static readonly MethodInfo Converter = Types.DynamicCellType.GetPropertyNonNull(nameof(Cesil.DynamicCell.Converter), BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethodNonNull();
            public static readonly MethodInfo GetDataSpan = Types.DynamicCellType.GetMethodNonNull(nameof(Cesil.DynamicCell.GetDataSpan), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetReadContext = Types.DynamicCellType.GetMethodNonNull(nameof(Cesil.DynamicCell.GetReadContext), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo CastTo = Types.DynamicCellType.GetMethodNonNull(nameof(Cesil.DynamicCell.CastTo), BindingFlags.Instance | BindingFlags.NonPublic);
        }

        internal static class Throw
        {
            public static readonly MethodInfo InvalidOperationException = Types.ThrowType.GetMethodNonNull(nameof(Cesil.Throw.InvalidOperationException), BindingFlags.Static | BindingFlags.NonPublic);
            public static readonly MethodInfo InvalidOperationExceptionOfObject = Types.ThrowType.GetMethodNonNull(nameof(Cesil.Throw.InvalidOperationException), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(Types.ObjectType);
        }

        internal static class DynamicRow
        {
            public static readonly MethodInfo GetAt = Types.DynamicRowType.GetMethodNonNull(nameof(Cesil.DynamicRow.GetAt), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetAtTyped = Types.DynamicRowType.GetMethodNonNull(nameof(Cesil.DynamicRow.GetAtTyped), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetByName = Types.DynamicRowType.GetMethodNonNull(nameof(Cesil.DynamicRow.GetByName), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetByIndex = Types.DynamicRowType.GetMethodNonNull(nameof(Cesil.DynamicRow.GetByIndex), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetRange = Types.DynamicRowType.GetMethodNonNull(nameof(Cesil.DynamicRow.GetRange), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetReadContext = Types.DynamicRowType.GetMethodNonNull(nameof(Cesil.DynamicRow.GetReadContext), BindingFlags.Instance | BindingFlags.NonPublic);
            public static readonly MethodInfo GetByIdentifier = Types.DynamicRowType.GetMethodNonNull(nameof(Cesil.DynamicRow.GetByIdentifier), BindingFlags.Instance | BindingFlags.NonPublic);
        }

        internal static class NonNull
        {
            internal static class OfITypeDescriber
            {
                public static readonly MethodInfo Value = Types.NonNullType.MakeGenericType(Types.ITypeDescriberType).GetTypeInfo().GetPropertyNonNull(nameof(NonNull<object>.Value), BindingFlags.Instance | BindingFlags.Public).GetGetMethodNonNull();
            }

            internal static class OfIReadOnlyListOfColumnIdentifier
            {
                public static readonly MethodInfo Value = Types.NonNullType.MakeGenericType(Types.IReadOnlyListType.MakeGenericType(Types.ColumnIdentifierType)).GetTypeInfo().GetPropertyNonNull(nameof(NonNull<object>.Value), BindingFlags.Instance | BindingFlags.Public).GetGetMethodNonNull();
            }
        }
    }
}
