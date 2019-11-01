namespace Cesil
{
    internal sealed class Column
    {
        internal static readonly Column Ignored = new Column("", delegate { return true; }, delegate { return true; }, false);

        internal readonly NonNull<string> Name;
        internal readonly NonNull<ColumnSetterDelegate> Set;
        internal readonly NonNull<ColumnWriterDelegate> Write;
        internal bool IsRequired { get; }

        internal Column(
            string? name,
            ColumnSetterDelegate? set,
            ColumnWriterDelegate? write,
            bool isRequired
        )
        {
            Name.SetAllowNull(name);
            Set.SetAllowNull(set);
            Write.SetAllowNull(write);
            IsRequired = isRequired;
        }
    }
}
