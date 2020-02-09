namespace Cesil
{
    internal readonly struct ShallowReadContext
    {
        public bool HasColumn => ColumnIndex != -1;

        public readonly ReadContextMode Mode;
        public readonly int RowNumber;
        public readonly int ColumnIndex;

        public ShallowReadContext(in ReadContext ctx)
        {
            Mode = ctx.Mode;
            RowNumber = ctx.RowNumber;
            ColumnIndex = ctx.HasColumn ? ctx.Column.Index : -1;
        }
    }
}
