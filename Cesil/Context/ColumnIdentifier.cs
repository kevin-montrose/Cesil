using System;

namespace Cesil
{
    /// <summary>
    /// Identifies a particular column, either by index or 
    ///   index and name.
    /// </summary>
    public readonly struct ColumnIdentifier : IEquatable<ColumnIdentifier>
    {
        /// <summary>
        /// Index of the column, base-0.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate an index, given that this is already wrapped up semantically")]
        public int Index { get; }

        private readonly NonNull<string> _Name;

        /// <summary>
        /// Whether this column has a known name.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate something exists, it's fine")]
        public bool HasName => _Name.HasValue;

        /// <summary>
        /// The name of the column.
        /// 
        /// If HasName is false, this will throw an exception.
        /// </summary>
        public string Name
        {
            get
            {
                // special to make the error "nicer", when most NonNull's indicate an internal error
                if (!_Name.HasValue)
                {
                    return Throw.InvalidOperationException<string>($"{nameof(Name)} is not set, check HasName before calling this");
                }

                return _Name.Value;
            }
        }

        private ColumnIdentifier(int ix, string? name)
        {
            Index = ix;
            _Name = default;
            _Name.SetAllowNull(name);
        }

        /// <summary>
        /// Create a ColumnIdentifier for the given index.
        /// </summary>
        public static ColumnIdentifier Create(
            [IntentionallyExposedPrimitive("Best way to identify an index")]
            int index
        )
        {
            if (index < 0)
            {
                return Throw.ArgumentException<ColumnIdentifier>($"Must be >= 0, found {index}", nameof(index));
            }

            return CreateInner(index, null);
        }

        /// <summary>
        /// Create a ColumnIdentifier for the given index and name.
        /// </summary>
        public static ColumnIdentifier Create(
            [IntentionallyExposedPrimitive("Best way to identify an index")]
            int index,
            string name
        )
        {
            if (index < 0)
            {
                return Throw.ArgumentException<ColumnIdentifier>($"Must be >= 0, found {index}", nameof(index));
            }

            Utils.CheckArgumentNull(name, nameof(name));

            return CreateInner(index, name);
        }

        internal static ColumnIdentifier CreateInner(int index, string? name)
        => new ColumnIdentifier(index, name);

        /// <summary>
        /// Returns true if the given object is equivalent to this one
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is ColumnIdentifier ci)
            {
                return Equals(ci);
            }

            return false;
        }

        /// <summary>
        /// Returns true if the given object is equivalent to this one
        /// </summary>
        public bool Equals(ColumnIdentifier column)
        {
            if (Index != column.Index) return false;

            if (HasName != column.HasName) return false;

            if (HasName)
            {
                return Name.Equals(column.Name);
            }

            return true;
        }

        /// <summary>
        /// Returns a stable hash for this ColumnIdentifier.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(ColumnIdentifier), Index, _Name);

        /// <summary>
        /// Describes this ColumnIdentifier.
        /// 
        /// This is provided for debugging purposes, and the format is not guaranteed to be stable between releases.
        /// </summary>
        public override string ToString()
        {
            if (HasName)
            {
                return $"{nameof(ColumnIdentifier)} with {nameof(Index)}={Index}, {nameof(Name)}={Name}";
            }

            return $"{nameof(ColumnIdentifier)} with {nameof(Index)}={Index}";
        }

        /// <summary>
        /// Compare two ColumnIdentifiers for equality
        /// </summary>
        public static bool operator ==(ColumnIdentifier a, ColumnIdentifier b)
        => a.Equals(b);

        /// <summary>
        /// Compare two ColumnIdentifiers for inequality
        /// </summary>
        public static bool operator !=(ColumnIdentifier a, ColumnIdentifier b)
        => !(a == b);

        /// <summary>
        /// Convenience operator for converting an index to a ColumnIdentifier.
        /// 
        /// Equivalent to calling Create.
        /// </summary>
        public static explicit operator ColumnIdentifier([IntentionallyExposedPrimitive("Best way to identify an index")]int index)
        => Create(index);

        /// <summary>
        /// Paired operator for the int-to-ColumnIdentifier cast.
        /// 
        /// Equivalent to Index property.
        /// </summary>
        [return: IntentionallyExposedPrimitive("Best way to identify an index")]
        public static explicit operator int(ColumnIdentifier col)
        => col.Index;
    }
}
