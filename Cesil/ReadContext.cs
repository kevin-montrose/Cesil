namespace Cesil
{
    /// <summary>
    /// Context object provided during read operations.
    /// </summary>
    public readonly struct ReadContext
    {
        /// <summary>
        /// The index of the row being read (0-based).
        /// </summary>
        public int RowNumber { get; }
        /// <summary>
        /// The index of the column being read (0-based).
        /// </summary>
        public int ColumnNumber { get; }
        /// <summary>
        /// The name of the column being read, can be null
        ///   if no column names were available.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// The object, if any, provided to the call to CreateReader or
        ///   CreateAsyncReader that produced the reader which is
        ///   performing the read operation which is described
        ///   by this context.
        /// </summary>
        public object Context { get; }

        internal ReadContext(int r, int c, string n, object ctx)
        {
            RowNumber = r;
            ColumnNumber = c;
            ColumnName = n;
            Context = ctx;
        }

        /// <summary>
        /// Returns a string representation of this ReadContext.
        /// </summary>
        public override string ToString()
        => ColumnName != null ?
            $"{nameof(RowNumber)}={RowNumber}, {nameof(ColumnNumber)}={ColumnNumber}, {nameof(ColumnName)}={ColumnName}, {nameof(Context)}={Context}" :
            $"{nameof(RowNumber)}={RowNumber}, {nameof(ColumnNumber)}={ColumnNumber}, {nameof(Context)}={Context}";
    }
}
