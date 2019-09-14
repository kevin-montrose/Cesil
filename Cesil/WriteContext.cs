﻿using System;

namespace Cesil
{
    /// <summary>
    /// Context object provided during write operations.
    /// </summary>
    public readonly struct WriteContext : IEquatable<WriteContext>
    {
        /// <summary>
        /// What, precisely, a writer is doing.
        /// </summary>
        public WriteContextMode Mode { get; }

        private readonly int _RowNumber;

        /// <summary>
        /// Whether or not RowNumber is available.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to expose a presense, it's fine")]
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
        [IntentionallyExposedPrimitive("Best way to expose an presense, it's fine")]
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
        public object Context { get; }

        private WriteContext(WriteContextMode m, int? r, ColumnIdentifier? ci, object ctx)
        {
            Mode = m;
            _RowNumber = r ?? default;
            _Column = ci ?? default;
            Context = ctx;
        }

        internal static WriteContext WritingColumn(int row, ColumnIdentifier col, object ctx)
        => new WriteContext(WriteContextMode.WritingColumn, row, col, ctx);

        internal static WriteContext DiscoveringCells(int row, object ctx)
        => new WriteContext(WriteContextMode.DiscoveringCells, row, null, ctx);

        internal static WriteContext DiscoveringColumns(object ctx)
        => new WriteContext(WriteContextMode.DiscoveringColumns, null, null, ctx);

        /// <summary>
        /// Returns true if this object equals the given WriteContext.
        /// </summary>
        public override bool Equals(object obj)
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
        public bool Equals(WriteContext w)
        => w._Column == _Column &&
           w.Context == Context &&
           w.Mode == Mode &&
           w._RowNumber == _RowNumber;

        /// <summary>
        /// Returns a stable hash for this WriteContext.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(WriteContext), _Column, Context, Mode, _RowNumber);

        /// <summary>
        /// Returns a string representation of this WriteContext.
        /// </summary>
        public override string ToString()
        {
            switch (Mode)
            {
                case WriteContextMode.DiscoveringCells:
                    return $"{nameof(WriteContext)} with {nameof(Mode)}={Mode}, {nameof(RowNumber)}={RowNumber}";
                case WriteContextMode.DiscoveringColumns:
                    return $"{nameof(WriteContext)} with {nameof(Mode)}={Mode}";
                case WriteContextMode.WritingColumn:
                    return $"{nameof(WriteContext)} with {nameof(Mode)}={Mode}, {nameof(RowNumber)}={RowNumber}, {nameof(Column)}={Column}";
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
