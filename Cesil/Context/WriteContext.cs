using System;
using System.Runtime.CompilerServices;

namespace Cesil
{
    /// <summary>
    /// Context object provided during write operations.
    /// </summary>
    public readonly struct WriteContext : IEquatable<WriteContext>
    {
        /// <summary>
        /// Options used to create writer.  Useful for accessing
        ///   shared configurations, like MemoryPool(char).
        /// </summary>
        public Options Options { get; }

        /// <summary>
        /// What, precisely, a writer is doing.
        /// </summary>
        public WriteContextMode Mode { get; }

        private readonly int _RowNumber;

        /// <summary>
        /// Whether or not RowNumber is available.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose a presence, it's fine")]
        public bool HasRowNumber
        {
            get
            {
                switch (Mode)
                {
                    case WriteContextMode.WritingColumn:
                    case WriteContextMode.DiscoveringCells:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// The index of the row being written (0-based).
        /// 
        /// If HasRowNumber == false, or Mode is DiscoveringColumns this will throw.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose an index, it's fine")]
        public int RowNumber
        {
            get
            {
                switch (Mode)
                {
                    case WriteContextMode.WritingColumn:
                    case WriteContextMode.DiscoveringCells:
                        return _RowNumber;
                    case WriteContextMode.DiscoveringColumns:
                        return Throw.InvalidOperationException<int>($"No row number is available (we haven't started writing) when {nameof(Mode)} is {Mode}");
                    default:
                        return Throw.InvalidOperationException<int>($"Unexpected {nameof(WriteContextMode)}: {Mode}");
                }
            }
        }

        private readonly ColumnIdentifier _Column;


        /// <summary>
        /// Whether or not Column is available.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose an presence, it's fine")]
        public bool HasColumn => Mode == WriteContextMode.WritingColumn;

        /// <summary>
        /// Column being written.
        /// 
        /// If HasColumn == false, or Mode != WriteColumn this will throw.
        /// </summary>
        public ColumnIdentifier Column
        {
            get
            {
                switch (Mode)
                {
                    case WriteContextMode.WritingColumn:
                        return _Column;
                    case WriteContextMode.DiscoveringCells:
                    case WriteContextMode.DiscoveringColumns:
                        return Throw.InvalidOperationException<ColumnIdentifier>($"No column is available when {nameof(Mode)} is {Mode}");
                    default:
                        return Throw.InvalidOperationException<ColumnIdentifier>($"Unexpected {nameof(WriteContextMode)}: {Mode}");
                }
            }
        }

        /// <summary>
        /// The object, if any, provided to the call to CreateWriter or
        ///   CreateAsyncWriter that produced the writer which is
        ///   performing the writer operation which is described
        ///   by this context.
        /// </summary>
        [NullableExposed("The provided context is nullable, so the returned one must be")]
        public object? Context { get; }
        
        // for DiscoveringColumns
        private WriteContext(Options opts, object? ctx)
        {
            Options = opts;
            Mode = WriteContextMode.DiscoveringColumns;
            Context = ctx;
            _RowNumber = default;
            _Column = default;
        }

        // for DiscoveringCells
        private WriteContext(Options opts, int rowNumber, object? ctx)
        {
            Options = opts;
            Mode = WriteContextMode.DiscoveringCells;
            Context = ctx;
            _RowNumber = rowNumber;
            _Column = default;
        }

        // for WritingColumn
        private WriteContext(Options opts, int rowNumber, ColumnIdentifier col, object? ctx)
        {
            Options = opts;
            Mode = WriteContextMode.WritingColumn;
            Context = ctx;
            _RowNumber = rowNumber;
            _Column = col;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal WriteContext SetRowNumberForWriteColumn(int newRowNumber)
        => new WriteContext(Options, newRowNumber, _Column, Context);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static WriteContext WritingColumn(Options opts, int row, ColumnIdentifier col, object? ctx)
        => new WriteContext(opts, row, col, ctx);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static WriteContext DiscoveringCells(Options opts, int row, object? ctx)
        => new WriteContext(opts, row, ctx);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static WriteContext DiscoveringColumns(Options opts, object? ctx)
        => new WriteContext(opts, ctx);

        /// <summary>
        /// Returns true if this object equals the given WriteContext.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is WriteContext w)
            {
                return Equals(w);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given WriteContext.
        /// </summary>
        public bool Equals(WriteContext context)
        => context._Column == _Column &&
           context.Context == Context &&
           context.Mode == Mode &&
           context._RowNumber == _RowNumber &&
           context.Options == Options;

        /// <summary>
        /// Returns a stable hash for this WriteContext.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(WriteContext), _Column, Context, Mode, _RowNumber, Options);

        /// <summary>
        /// Returns a string representation of this WriteContext.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case WriteContextMode.DiscoveringCells:
                    return $"{nameof(WriteContext)} with {nameof(Mode)}={Mode}, {nameof(RowNumber)}={RowNumber}, {nameof(Options)}={Options}";
                case WriteContextMode.DiscoveringColumns:
                    return $"{nameof(WriteContext)} with {nameof(Mode)}={Mode}, {nameof(Options)}={Options}";
                case WriteContextMode.WritingColumn:
                    return $"{nameof(WriteContext)} with {nameof(Mode)}={Mode}, {nameof(RowNumber)}={RowNumber}, {nameof(Column)}={Column}, {nameof(Options)}={Options}";
                default:
                    return Throw.InvalidOperationException<string>($"Unexpected {nameof(WriteContextMode)}: {Mode}");
            }
        }

        /// <summary>
        /// Compare two WriteContexts for equality
        /// </summary>
        public static bool operator ==(WriteContext a, WriteContext b)
        => a.Equals(b);

        /// <summary>
        /// Compare two WriteContexts for inequality
        /// </summary>
        public static bool operator !=(WriteContext a, WriteContext b)
        => !(a == b);
    }
}
