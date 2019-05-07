namespace Cesil
{
    /// <summary>
    /// Context object provided during write operations.
    /// </summary>
    public readonly struct WriteContext
    {
        /// <summary>
        /// The index of the row being written (0-based).
        /// </summary>
        public int RowNumber { get; }
        /// <summary>
        /// The index of the column being written (0-based).
        /// </summary>
        public int ColumnNumber { get; }
        /// <summary>
        /// The name of the column being written, can be null
        ///   if no column names are available.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// The object, if any, provided to the call to CreateWriter or
        ///   CreateAsyncWriter that produced the writer which is
        ///   performing the writer operation which is described
        ///   by this context.
        /// </summary>
        public object Context { get; }

        internal WriteContext(int r, int c, string n, object ctx)
        {
            RowNumber = r;
            ColumnNumber = c;
            ColumnName = n;
            Context = ctx;
        }

        /// <summary>
        /// Returns a string representation of this WriteContext.
        /// </summary>
        public override string ToString()
        => ColumnName != null ?
            $"{nameof(RowNumber)}={RowNumber}, {nameof(ColumnNumber)}={ColumnNumber}, {nameof(ColumnName)}={ColumnName}" :
            $"{nameof(RowNumber)}={RowNumber}, {nameof(ColumnNumber)}={ColumnNumber}";
    }
}
