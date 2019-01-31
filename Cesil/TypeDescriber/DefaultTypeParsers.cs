using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Cesil
{
    [SuppressMessage("", "IDE0051", Justification = "Used via reflection")]
    internal static class DefaultTypeParsers
    {
        internal static class DefaultFlagsEnumTypeParser<T>
            where T:struct, Enum
        {
            private static readonly string[] Names = Enum.GetNames(typeof(T));

            internal static bool TryParseFlagsEnum(ReadOnlySpan<char> data, out T val)
            {
                // no real choice but to make a copy
                // todo: get rid of this allocation
                //       once https://github.com/dotnet/corefx/issues/15453
                //       is fixed, introducing a ReadOnlySpan<char> taking TryParse
                var str = new string(data);

                if(!Enum.TryParse(str, out val))
                {
                    return false;
                }

                // have to check to see if it's valid
                var pieces = str.Split(DefaultTypeFormatters.COMMA_AND_SPACE, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < pieces.Length; i++)
                {
                    var piece = pieces[i];
                    var found = false;
                    for (var j = 0; j < Names.Length; j++)
                    {
                        var name = Names[j];
                        if(piece.Equals(name, StringComparison.InvariantCulture))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }

                return true;
            }

            internal static bool TryParseNullableFlagsEnum(ReadOnlySpan<char> data, out T? val)
            {
                if(data.Length == 0)
                {
                    val = null;
                    return true;
                }

                if(!TryParseFlagsEnum(data, out var pVal))
                {
                    val = null;
                    return false;
                }

                val = pVal;
                return true;
            }
        }

        internal static class DefaultEnumTypeParser<T>
            where T: struct, Enum
        {
            private static readonly T[] Values;
            private static readonly string[] Names;

            static DefaultEnumTypeParser()
            {
                Values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
                Names = Enum.GetNames(typeof(T));
            }

            internal static bool TryParseEnum(ReadOnlySpan<char> span, out T val)
            {
                // doing this instead of a .TryParse because we don't want to accept ints
                for (var i = 0; i < Names.Length; i++)
                {
                    var name = Names[i];
                    var cmp = span.CompareTo(name.AsSpan(), StringComparison.InvariantCulture);
                    if (cmp == 0)
                    {
                        val = Values[i];
                        return true;
                    }
                }

                val = default;
                return false;
            }

            internal static bool TryParseNullableEnum(ReadOnlySpan<char> data, out T? val)
            {
                if(data.Length == 0)
                {
                    val = null;
                    return true;
                }

                if(!TryParseEnum(data, out var pVal))
                {
                    val = null;
                    return false;
                }

                val = pVal;
                return true;
            }
        }

        private static bool TryParseString(ReadOnlySpan<char> span, out string val)
        {
            if (span.Length == 0)
            {
                val = "";
                return true;
            }

            val = new string(span);
            return true;
        }

        // non-null
        
        private static bool TryParseBool(ReadOnlySpan<char> span, out bool val)
        => bool.TryParse(span, out val);

        private static bool TryParseChar(ReadOnlySpan<char> span, out char val)
        {
            if (span.Length != 1)
            {
                val = default;
                return false;
            }

            val = span[0];
            return true;
        }

        private static bool TryParseDateTime(ReadOnlySpan<char> span, out DateTime val)
        => DateTime.TryParse(span, CultureInfo.InvariantCulture, DateTimeStyles.None, out val);

        
        private static bool TryParseDateTimeOffset(ReadOnlySpan<char> span, out DateTimeOffset val)
        => DateTimeOffset.TryParse(span, CultureInfo.InvariantCulture, DateTimeStyles.None, out val);

        
        private static bool TryParseByte(ReadOnlySpan<char> span, out byte val)
        => byte.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseSByte(ReadOnlySpan<char> span, out sbyte val)
        => sbyte.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseShort(ReadOnlySpan<char> span, out short val)
        => short.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseUShort(ReadOnlySpan<char> span, out ushort val)
        => ushort.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseInt(ReadOnlySpan<char> span, out int val)
        => int.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseUInt(ReadOnlySpan<char> span, out uint val)
        => uint.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseLong(ReadOnlySpan<char> span, out long val)
        => long.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseULong(ReadOnlySpan<char> span, out ulong val)
        => ulong.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseFloat(ReadOnlySpan<char> span, out float val)
        => float.TryParse(span, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseDouble(ReadOnlySpan<char> span, out double val)
        => double.TryParse(span, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out val);

        
        private static bool TryParseDecimal(ReadOnlySpan<char> span, out decimal val)
        => decimal.TryParse(span, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out val);

        private static bool TryParseGUID(ReadOnlySpan<char> span, out Guid val)
        => Guid.TryParse(span, out val);

        private static bool TryParseTimeSpan(ReadOnlySpan<char> span, out TimeSpan val)
        => TimeSpan.TryParse(span, out val);

        // nullable

        private static bool TryParseNullableBool(ReadOnlySpan<char> span, out bool? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseBool(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        private static bool TryParseNullableChar(ReadOnlySpan<char> span, out char? val)
        {
            if(span.Length == 0)
            {
                val = null;
                return true;
            }

            if (span.Length != 1)
            {
                val = default;
                return false;
            }

            val = span[0];
            return true;
        }

        private static bool TryParseNullableDateTime(ReadOnlySpan<char> span, out DateTime? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseDateTime(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableDateTimeOffset(ReadOnlySpan<char> span, out DateTimeOffset? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseDateTimeOffset(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableByte(ReadOnlySpan<char> span, out byte? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseByte(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableSByte(ReadOnlySpan<char> span, out sbyte? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseSByte(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableShort(ReadOnlySpan<char> span, out short? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseShort(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableUShort(ReadOnlySpan<char> span, out ushort? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseUShort(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableInt(ReadOnlySpan<char> span, out int? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseInt(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableUInt(ReadOnlySpan<char> span, out uint? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseUInt(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableLong(ReadOnlySpan<char> span, out long? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseLong(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableULong(ReadOnlySpan<char> span, out ulong? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseULong(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableFloat(ReadOnlySpan<char> span, out float? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseFloat(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableDouble(ReadOnlySpan<char> span, out double? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseDouble(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        
        private static bool TryParseNullableDecimal(ReadOnlySpan<char> span, out decimal? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseDecimal(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        private static bool TryParseNullableGUID(ReadOnlySpan<char> span, out Guid? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseGUID(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        private static bool TryParseNullableTimeSpan(ReadOnlySpan<char> span, out TimeSpan? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if(!TryParseTimeSpan(span, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }
    }
}
