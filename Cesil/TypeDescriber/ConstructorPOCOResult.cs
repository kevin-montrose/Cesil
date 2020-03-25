using System.Reflection;

namespace Cesil
{
    internal readonly struct ConstructorPOCOResult
    {
        internal static readonly ConstructorPOCOResult Empty = new ConstructorPOCOResult();

        internal bool HasValue => Constructor.HasValue;
        internal readonly NonNull<ConstructorInfo> Constructor;
        internal readonly NonNull<ColumnIdentifier[]> Columns;

        internal ConstructorPOCOResult(ConstructorInfo cons, ColumnIdentifier[] cols)
        {
            Constructor = default;
            Constructor.Value = cons;

            Columns = default;
            Columns.Value = cols;
        }
    }
}
