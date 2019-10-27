using System;

namespace Cesil
{
    /// <summary>
    /// Identifies a particular column, either by index or 
    ///   index and name.
    /// </summary>
    public struct ColumnIdentifier : IEquatable<ColumnIdentifier>
    {
        /// <summary>
        /// Index of the column, base-0.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate an index, given that this is already wrapped up semantically")]
        public int Index { get; }

        /// <summary>
        /// Whether this column has a known name.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate something exists, it's fine")]
        public bool HasName => _Name != null;


        private string? _Name;
        /// <summary>
        /// The name of the column.
        /// </summary>
        public string Name
        {
            get
            {
                if (_Name == null)
                {
                    return Throw.InvalidOperationException<string>("Column does not have a name");
                }

                return _Name;
            }
        }

        private ColumnIdentifier(int ix, string? name)
        {
            Index = ix;
            _Name = name;
        }

        /// <summary>
        /// Create a ColumnIdentifier for the given index, optionally with a name.
        /// </summary>
        public static ColumnIdentifier Create(
            [IntentionallyExposedPrimitive("Best way to identifier an index")]int index,
            string? name = null
        )
        {
            if (index < 0)
            {
                return Throw.ArgumentException<ColumnIdentifier>($"Must be >= 0, found {index}", nameof(index));
            }

            return CreateInner(index, name);
        }

        // just for testing
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
        public bool Equals(ColumnIdentifier other)
        {
            if (Index != other.Index) return false;

            if (HasName != other.HasName) return false;

            if (HasName)
            {
                return Name.Equals(other.Name);
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
