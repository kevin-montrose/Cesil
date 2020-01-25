namespace Cesil
{
    internal sealed class Column
    {
        internal static readonly Column Ignored = new Column("", delegate { return true; }, false);

        internal readonly NonNull<string> Name;
        internal readonly NonNull<ColumnWriterDelegate> Write;
        internal bool IsRequired { get; }

        internal Column(
            string? name,
            ColumnWriterDelegate? write,
            bool isRequired
        )
        {
            Name.SetAllowNull(name);
            Write.SetAllowNull(write);
            IsRequired = isRequired;
        }
    }
}
