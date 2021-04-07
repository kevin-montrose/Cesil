using System;
using System.Reflection;

namespace Cesil
{
    /// <summary>
    /// Wrapper around a dynamic cell which is to be written
    ///   by an I(Async)Writer.
    /// </summary>
    public readonly struct DynamicCellValue : IEquatable<DynamicCellValue>
    {
        /// <summary>
        /// Returns true if Name is set.
        /// </summary>
        [IntentionallyExposedPrimitive("Indicating presence, it's fine")]
        public bool HasName => _Name.HasValue;

        private readonly NonNull<string> _Name;
        /// <summary>
        /// Name of the column the cell belongs to, throws if Name is not set.
        /// </summary>
        public string Name => _Name.Value;
        /// <summary>
        /// Cell value
        /// </summary>
        [NullableExposed("User may want to provide a null")]
        public dynamic? Value { get; }
        /// <summary>
        /// Instance of Formatter to use when formatting the associated value
        ///   for writing.
        /// </summary>
        public Formatter Formatter { get; }

        private DynamicCellValue(string? n, dynamic? v, Formatter f)
        {
            _Name = default;
            _Name.SetAllowNull(n);
            Value = v;
            Formatter = f;
        }

        // note, not doing overrides without name (so it can be non-nullable) for Create because
        //       it is BAD NEWS to have overloads where one of the terms is dynamic

        /// <summary>
        /// Create a DynamicCellValue to format the given value of the given column.
        /// 
        /// It's permissible for both name and value to be null.
        /// </summary>
        public static DynamicCellValue Create(
            [NullableExposed("May be purely positional, in which case it has no name")]
            string? name,
            [NullableExposed("User may want to provide a null")]
            dynamic? value,
            Formatter formatter
        )
        {
            // name can be null, that's fine

            // value can be null, that's fine

            Utils.CheckArgumentNull(formatter, nameof(formatter));

            if (value is object valObj)
            {
                var valType = valObj.GetType().GetTypeInfo();

                if (!formatter.Takes.IsAssignableFrom(valType))
                {
                    Throw.ArgumentException($"Formatter must accept an object assignable from {valType}", nameof(formatter));
                }
            }

            return new DynamicCellValue(name, value, formatter);
        }

        /// <summary>
        /// Returns true if this object equals the given DynamicCellValue.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is DynamicCellValue d)
            {
                return Equals(d);
            }

            return false;
        }

        /// <summary>
        /// Returns true if this object equals the given DynamicCellValue.
        /// </summary>
        public bool Equals(DynamicCellValue value)
        {
            if (value.Formatter != Formatter) return false;

            if (HasName)
            {
                if (!value.HasName) return false;

                if (Name != value.Name) return false;
            }
            else
            {
                if (value.HasName) return false;
            }

            var dAsObj = value.Value as object;

            if (!(Value is object selfAsObj))
            {
                return dAsObj == null;
            }

            return selfAsObj.Equals(dAsObj);
        }

        /// <summary>
        /// Returns a stable hash for this DynamicCellValue.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(DynamicCellValue), Formatter, _Name, Value as object);

        /// <summary>
        /// Returns a representation of this DynamicCellValue struct.
        /// 
        /// Only for debugging, this value is not guaranteed to be stable.
        /// </summary>
        public override string ToString()
        => $"{nameof(DynamicCellValue)} with {nameof(Name)}={Name}, {nameof(Value)}={Value}, {nameof(Formatter)}={Formatter}";

        /// <summary>
        /// Compare two DynamicCellValues for equality
        /// </summary>
        public static bool operator ==(DynamicCellValue a, DynamicCellValue b)
        => a.Equals(b);

        /// <summary>
        /// Compare two DynamicCellValues for inequality
        /// </summary>
        public static bool operator !=(DynamicCellValue a, DynamicCellValue b)
        => !(a == b);
    }
}
