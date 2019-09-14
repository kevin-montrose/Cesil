namespace Cesil
{
    internal sealed class Column
    {
        internal static readonly Column Ignored = new Column("", delegate { return true; }, delegate { return true; }, false);

        internal string Name { get; }
        internal ColumnSetterDelegate Set { get; }
        internal ColumnWriterDelegate Write { get; }
        internal bool IsRequired { get; }

        internal Column(
            string name,
            ColumnSetterDelegate set,
            ColumnWriterDelegate write,
            bool isRequired
        )
        {
            Name = name;
            Set = set;
            Write = write;
            IsRequired = isRequired;
        }
    }
}
