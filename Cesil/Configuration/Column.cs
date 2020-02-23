namespace Cesil
{
    internal sealed class Column
    {
        internal readonly string Name;
        internal readonly ColumnWriterDelegate Write;

        internal Column(
            string name,
            ColumnWriterDelegate write
        )
        {
            Name = name;
            Write = write;
        }
    }
}
