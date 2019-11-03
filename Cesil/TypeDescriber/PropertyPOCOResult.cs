using System.Reflection;

namespace Cesil
{
    internal readonly struct PropertyPOCOResult
    {
        public static readonly PropertyPOCOResult Empty = new PropertyPOCOResult();

        public bool HasValue => Constructor.HasValue;
        public readonly NonNull<ConstructorInfo> Constructor;
        public readonly NonNull<Setter[]> Setters;
        public readonly NonNull<ColumnIdentifier[]> Columns;

        public PropertyPOCOResult(ConstructorInfo cons, Setter[] sets, ColumnIdentifier[] cols)
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
