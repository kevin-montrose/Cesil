namespace Cesil
{
    internal sealed class Column
    {
        internal static readonly Column Ignored = new Column("", delegate { return true; }, delegate { return true; }, false);

        private readonly string? _Name;
        internal bool HasName => _Name != null;
        internal string Name => Utils.NonNull(_Name);

        private readonly ColumnSetterDelegate? _Set;
        internal ColumnSetterDelegate Set => Utils.NonNull(_Set);
        private readonly ColumnWriterDelegate? _Write;
        internal ColumnWriterDelegate Write => Utils.NonNull(_Write);
        internal bool IsRequired { get; }

        internal Column(
            string? name,
            ColumnSetterDelegate? set,
            ColumnWriterDelegate? write,
            bool isRequired
        )
        {
            _Name = name;
            _Set = set;
            _Write = write;
            IsRequired = isRequired;
        }
    }
}
