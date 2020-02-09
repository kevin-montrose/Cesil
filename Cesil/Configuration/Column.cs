namespace Cesil
{
    internal sealed class Column
    {
        internal readonly NonNull<string> Name;
        internal readonly NonNull<ColumnWriterDelegate> Write;

        internal Column(
            string? name,
            ColumnWriterDelegate? write
        )
        {
            Name.SetAllowNull(name);
            Write.SetAllowNull(write);
        }
    }
}
