using System.Reflection;

namespace Cesil
{
    internal readonly struct PropertyPOCOResult
    {
        internal static readonly PropertyPOCOResult Empty = new PropertyPOCOResult();

        internal bool HasValue => Constructor.HasValue;
        internal readonly NonNull<ConstructorInfo> Constructor;
        internal readonly NonNull<Setter[]> Setters;
        internal readonly NonNull<ColumnIdentifier[]> Columns;

        internal PropertyPOCOResult(ConstructorInfo cons, Setter[] sets, ColumnIdentifier[] cols)
        {
            Constructor = default;
            Constructor.Value = cons;
            Setters = default;
            Setters.Value = sets;
            Columns = default;
            Columns.Value = cols;
        }
    }
}
