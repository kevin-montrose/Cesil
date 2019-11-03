using System.Reflection;

namespace Cesil
{
    internal readonly struct ConstructorPOCOResult
    {
        public static readonly ConstructorPOCOResult Empty = new ConstructorPOCOResult();

        public bool HasValue => Constructor.HasValue;
        public readonly NonNull<ConstructorInfo> Constructor;
        public readonly NonNull<ColumnIdentifier[]> Columns;

        public ConstructorPOCOResult(ConstructorInfo cons, ColumnIdentifier[] cols)
        {
            Constructor = default;
            Constructor.Value = cons;

            Columns = default;
            Columns.Value = cols;
        }
    }
}
