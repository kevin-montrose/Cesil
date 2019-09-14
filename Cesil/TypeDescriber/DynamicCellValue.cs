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
        /// Name of the column (if any) the cell belongs to
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Cell value
        /// </summary>
        public dynamic Value { get; }
        /// <summary>
        /// Instance of Formatter to use when formatting the associated value
        ///   for writing.
        /// </summary>
        public Formatter Formatter { get; }

        private DynamicCellValue(string n, dynamic v, Formatter f)
        {
            Name = n;
            Value = v;
            Formatter = f;
        }

        /// <summary>
        /// Create a DynamicCellValue to format the given value of the given column.
        /// 
        /// It's permissable for both name and val to be null.
        /// </summary>
        public static DynamicCellValue Create(string name, dynamic val, Formatter formatter)
        {
            // name can be null, that's fine

            // val can be null, that's fine

            if (formatter == null)
            {
                return Throw.ArgumentNullException<DynamicCellValue>(nameof(formatter));
            }

            var valType = (val as object)?.GetType().GetTypeInfo();

            if (valType != null && !formatter.Takes.IsAssignableFrom(valType))
            {
                return Throw.ArgumentException<DynamicCellValue>($"Formatter must accept an object assignable from {valType}", nameof(formatter));
            }

            return new DynamicCellValue(name, val, formatter);
        }

        /// <summary>
        /// Returns true if this object equals the given DynamicCellValue.
        /// </summary>
        public override bool Equals(object obj)
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
        public bool Equals(DynamicCellValue d)
        {
            if (d.Formatter != Formatter) return false;
            if (d.Name != Name) return false;

            var selfAsObj = Value as object;
            var dAsObj = d.Value as object;

            if(selfAsObj == null)
            {
                return dAsObj == null;
            }

            return selfAsObj.Equals(dAsObj);
        }

        /// <summary>
        /// Returns a stable hash for this DynamicCellValue.
        /// </summary>
        public override int GetHashCode()
        => HashCode.Combine(nameof(DynamicCellValue), Formatter, Name, Value as object);

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
