using System;

namespace Cesil
{
    /// <summary>
    /// Identifies a particular column, either by index or 
    ///   index and name.
    ///   
    /// A ColumnIdentifier is only valid for as long as the 
    ///   values used to construct it.  Accordingly, you should
    ///   be careful when storing ColumnIdentifiers that you did
    ///   not create yourself as they may have been created with
    ///   ReadOnlyMemory(char)s whose lifetime you do not control.
    /// </summary>
    public struct ColumnIdentifier : IEquatable<ColumnIdentifier>
    {
        /// <summary>
        /// Index of the column, base-0.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate an index, given that this is already wrapped up semantically")]
        public int Index { get; }

        private readonly NonNull<string> _Name;

        private string? MemoizedName;
        private ReadOnlyMemory<char>? _NameMemory;

        /// <summary>
        /// Whether this column has a known name.
        /// </summary>
        [IntentionallyExposedPrimitive("Best way to indicate something exists, it's fine")]
        public bool HasName => _Name.HasValue || _NameMemory != null;

        /// <summary>
        /// The name of the column, as a string.
        /// 
        /// Easier to work with than NameMemory, but this may allocate.
        /// 
        /// If HasName is false, this will throw an exception.
        /// </summary>
        public string Name
        {
            get
            {
                if (_Name.HasValue)
                {
                    return _Name.Value;
                }

                if (_NameMemory != null)
                {
                    return (MemoizedName ??= new string(_NameMemory.Value.Span));
                }

                Throw.InvalidOperationException($"{nameof(Name)} is not set, check HasName before calling this");
                return default;
            }
        }

        /// <summary>
        /// The name of the column, as a block of memory.
        /// 
        /// More awkward to work with than Name, but will not allocate.
        /// 
        /// If HasName is false, this will throw an exception.
        /// </summary>
        public ReadOnlyMemory<char> NameMemory
        {
            get
            {
                if (_NameMemory != null)
                {
                    return _NameMemory.Value;
                }

                if (_Name.HasValue)
                {
                    _NameMemory = _Name.Value.AsMemory();

                    return _NameMemory.Value;
                }

                Throw.InvalidOperationException($"{nameof(NameMemory)} is not set, check HasName before calling this");
                return default;
            }
        }

        private ColumnIdentifier(int ix, string? name, ReadOnlyMemory<char>? nameMem)
        {
            Index = ix;
            _Name = default;
            _Name.SetAllowNull(name);

            _NameMemory = nameMem;

            MemoizedName = name;
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
                Throw.ArgumentException($"Must be >= 0, found {index}", nameof(index));
            }

            return CreateInner(index, null, null);
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
                Throw.ArgumentException($"Must be >= 0, found {index}", nameof(index));
            }

            Utils.CheckArgumentNull(name, nameof(name));

            return CreateInner(index, name, null);
        }

        /// <summary>
        /// Create a ColumnIdentifier for the given index and name.
        /// </summary>
        public static ColumnIdentifier Create(
            [IntentionallyExposedPrimitive("Best way to identify an index")]
            int index,
            ReadOnlyMemory<char> name
        )
        {
            if (index < 0)
            {
                Throw.ArgumentException($"Must be >= 0, found {index}", nameof(index));
            }

            return CreateInner(index, null, name);
        }

        internal static ColumnIdentifier CreateInner(int index, string? name, ReadOnlyMemory<char>? nameMemory)
        => new ColumnIdentifier(index, name, nameMemory);

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
        {
            var ret = HashCode.Combine(nameof(ColumnIdentifier), Index);

            ReadOnlyMemory<char>? mem = null;

            if (_NameMemory.HasValue)
            {
                mem = _NameMemory;
            }
            else if (_Name.HasValue)
            {
                mem = _Name.Value.AsMemory();
            }

            if (mem != null)
            {
                var memVal = mem.Value.Span;
                for (var i = 0; i < memVal.Length; i++)
                {
                    ret = HashCode.Combine(ret, memVal[i]);
                }
            }

            return ret;
        }

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
        public static explicit operator ColumnIdentifier([IntentionallyExposedPrimitive("Best way to identify an index")] int index)
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
