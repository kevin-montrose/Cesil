using System.Reflection;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    internal static class Methods
    {


        internal static class ReadContext
        {
            internal static readonly MethodInfo ConvertingRow = Types.ReadContext.GetMethodNonNull(nameof(Cesil.ReadContext.ConvertingRow), InternalStatic);
        }

        internal static class ITypeDescriber
        {
            internal static readonly MethodInfo GetDynamicCellParserFor = Types.ITypeDescriber.GetMethodNonNull(nameof(Cesil.ITypeDescriber.GetDynamicCellParserFor), PublicInstance);
            internal static readonly MethodInfo GetDynamicRowConverter = Types.ITypeDescriber.GetMethodNonNull(nameof(Cesil.ITypeDescriber.GetDynamicRowConverter), PublicInstance);
        }

        internal static class DynamicCell
        {
            internal static readonly MethodInfo Converter = Types.DynamicCell.GetPropertyNonNull(nameof(Cesil.DynamicCell.Converter), InternalInstance).GetGetMethodNonNull();
            internal static readonly MethodInfo GetDataSpan = Types.DynamicCell.GetMethodNonNull(nameof(Cesil.DynamicCell.GetDataSpan), InternalInstance);
            internal static readonly MethodInfo GetReadContext = Types.DynamicCell.GetMethodNonNull(nameof(Cesil.DynamicCell.GetReadContext), InternalInstance);
        }

        internal static class Throw
        {
            internal static readonly MethodInfo InvalidOperationException = Types.ThrowType.GetMethodNonNull(nameof(Cesil.Throw.InvalidOperationException), InternalStatic);
            internal static readonly MethodInfo InvalidOperationExceptionOfObject = Types.ThrowType.GetMethodNonNull(nameof(Cesil.Throw.InvalidOperationException), InternalStatic).MakeGenericMethod(Types.Object);
            internal static readonly MethodInfo ParseFailed = Types.ThrowType.GetMethodNonNull(nameof(Cesil.Throw.ParseFailed), InternalStatic);
        }

        internal static class DynamicRow
        {
            internal static readonly MethodInfo GetAt = Types.DynamicRow.GetMethodNonNull(nameof(Cesil.DynamicRow.GetAt), InternalInstance);
            internal static readonly MethodInfo GetAtTyped = Types.DynamicRow.GetMethodNonNull(nameof(Cesil.DynamicRow.GetAtTyped), InternalInstance);
            internal static readonly MethodInfo GetByName = Types.DynamicRow.GetMethodNonNull(nameof(Cesil.DynamicRow.GetByName), InternalInstance);
            internal static readonly MethodInfo GetByIndex = Types.DynamicRow.GetMethodNonNull(nameof(Cesil.DynamicRow.GetByIndex), InternalInstance);
            internal static readonly MethodInfo GetRange = Types.DynamicRow.GetMethodNonNull(nameof(Cesil.DynamicRow.GetRange), InternalInstance);
            internal static readonly MethodInfo GetReadContext = Types.DynamicRow.GetMethodNonNull(nameof(Cesil.DynamicRow.GetReadContext), InternalInstance);
            internal static readonly MethodInfo GetByIdentifier = Types.DynamicRow.GetMethodNonNull(nameof(Cesil.DynamicRow.GetByIdentifier), InternalInstance);
        }

        internal static class IDynamicRowOwner
        {
            internal static readonly MethodInfo Options = Types.IDynamicRowOwner.GetPropertyNonNull(nameof(Cesil.IDynamicRowOwner.Options), PublicInstance).GetGetMethodNonNull();
        }

        internal static class DisposableHelper
        {
            internal static readonly MethodInfo AssertNotDisposed = Types.DisposableHelper.GetMethodNonNull(nameof(Cesil.DisposableHelper.AssertNotDisposed), InternalStatic, null, new[] { Types.ITestableDisposable }, null);
        }
    }
}
