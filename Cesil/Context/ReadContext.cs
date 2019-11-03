using System;

namespace Cesil
{
    /// <summary>
    /// Context object provided during read operations.
    /// </summary>
    public readonly struct ReadContext : IEquatable<ReadContext>
    {
        /// <summary>
        /// What, precisely, a reader is doing.
        /// </summary>
        public ReadContextMode Mode { get; }

        /// <summary>
        /// The index of the row being read (0-based).
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose an index, it's fine")]
        public int RowNumber { get; }

        private readonly ColumnIdentifier _Column;

        /// <summary>
        /// Whether or not Column is available.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose a presense, it's fine")]
        public bool HasColumn
        {
            get
            {
                switch (Mode)
                {
                    case ReadContextMode.ConvertingColumn:
                    case ReadContextMode.ReadingColumn:
                        return true;
                    case ReadContextMode.ConvertingRow:
                        return false;
                    default:
                        return Throw.InvalidOperationException<bool>($"Unexpected {nameof(ReadContextMode)}: {Mode}");

                }
            }
        }

        /// <summary>
        /// The column being read.
        /// 
        /// Will throw if HasColumn == false, or Mode != ReadingColumn.
        /// </summary>
        public ColumnIdentifier Column
        {
            get
            {
                if (!HasColumn)
                {
                    return Throw.InvalidOperationException<ColumnIdentifier>($"No column is available when {nameof(Mode)} is {Mode}");
                }

                return _Column;
            }
        }

        /// <summary>
        /// The object, if any, provided to the call to CreateReader or
        ///   CreateAsyncReader that produced the reader which is
        ///   performing the read operation which is described
        ///   by this context.
        /// </summary>
        [NullableExposed("The provided context is nullable, so the returned one must be")]
        public object? Context { get; }

        private ReadContext(ReadContextMode m, int r, ColumnIdentifier? ci, object? ctx)
        {
            Mode = m;
            RowNumber = r;
            _Column = ci ?? default;
            Context = ctx;
        }

        internal static ReadContext ReadingColumn(int r, ColumnIdentifier col, object? ctx)
        => new ReadContext(ReadContextMode.ReadingColumn, r, col, ctx);

        internal static ReadContext ConvertingColumn(int r, ColumnIdentifier col, object? ctx)
        => new ReadContext(ReadContextMode.ConvertingColumn, r, col, ctx);

        internal static ReadContext ConvertingRow(int r, object? ctx)
        => new ReadContext(ReadContextMode.ConvertingRow, r, null, ctx);

        /// <summary>
        /// Returns true if this object equals the given ReadContext.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is ReadContext r)
            {
                return Equals(r);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given ReadContext.
        /// </summary>
        public bool Equals(ReadContext context)
        => context.Mode == Mode &&
           context._Column == _Column &&
           context.Context == Context &&
           context.RowNumber == RowNumber;

        /// <summary>
        /// Returns a stable hash for this ReadContext.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(ReadContext), Mode, _Column, Context, RowNumber);

        /// <summary>
        /// Returns a string representation of this ReadContext.
        /// </summary>
        public override string ToString()
        => HasColumn ?
            $"{nameof(ReadContext)} of {Mode} with {nameof(RowNumber)}={RowNumber}, {nameof(Column)}={Column}, {nameof(Context)}={Context}" :
            $"{nameof(ReadContext)} of {Mode} with {nameof(RowNumber)}={RowNumber}, {nameof(Context)}={Context}";

        /// <summary>
        /// Compare two ReadContexts for equality
        /// </summary>
        public static bool operator ==(ReadContext a, ReadContext b)
        => a.Equals(b);

        /// <summary>
        /// Compare two ReadContexts for inequality
        /// </summary>
        public static bool operator !=(ReadContext a, ReadContext b)
        => !(a == b);
    }
}
