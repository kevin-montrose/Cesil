﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    [SuppressMessage("", "IDE0051", Justification = "Used via reflection")]
    internal static class DefaultTypeParsers
    {

        internal static class DefaultEnumTypeParser<T>
            where T : struct, Enum
        {
            private static readonly T[] Values = CreateValues();
            private static readonly ulong[] ULongValues = CreateULongValues();
            private static readonly string[] Names = CreateNames();

            internal static readonly Parser TryParseEnumParser = CreateTryParseEnumParser();
            internal static readonly Parser TryParseNullableEnumParser = CreateTryParseNullableEnumParser();

            private static T[] CreateValues()
            {
                var enumType = typeof(T).GetTypeInfo();

                if (enumType.IsFlagsEnum())
                {
                    // we don't use Values for [Flags] enums
                    return Array.Empty<T>();
                }

                return Enum.GetValues(enumType).Cast<T>().ToArray();
            }

            private static ulong[] CreateULongValues()
            {
                var enumType = typeof(T).GetTypeInfo();

                // note that this is different from CreateValues()
                //   which means these don't match like they do in formatting
                if (!enumType.IsFlagsEnum())
                {
                    // only need ULongValues for [Flags] enums
                    return Array.Empty<ulong>();
                }

                var values = Enum.GetValues(enumType);
                var ret = new ulong[values.Length];
                for(var i = 0; i < values.Length; i++)
                {
                    var obj = values.GetValue(i);
                    obj = Utils.NonNull(obj);
                    ret[i] = Utils.EnumToULong((T)obj);
                }

                return ret;
            }

            private static string[] CreateNames()
            {
                var enumType = typeof(T).GetTypeInfo();

                return Enum.GetNames(enumType);
            }

            private static TypeInfo GetParsingClass()
            {
                var enumType = typeof(T).GetTypeInfo();

                var parsingClass = Types.DefaultEnumTypeParser.MakeGenericType(enumType).GetTypeInfo();

                return parsingClass;
            }

            private static Parser CreateTryParseEnumParser()
            {
                var enumType = typeof(T).GetTypeInfo();

                if (enumType.IsFlagsEnum())
                {
                    return CreateTryParseFlagsEnumParser();
                }

                return CreateTryParseBasicEnumParser();
            }

            private static Parser CreateTryParseNullableEnumParser()
            {
                var enumType = typeof(T).GetTypeInfo();

                if (enumType.IsFlagsEnum())
                {
                    return CreateTryParseNullableFlagsEnumParser();
                }

                return CreateTryParseNullableBasicEnumParser();
            }

            private static Parser CreateTryParseBasicEnumParser()
            {
                var parsingClass = GetParsingClass();

                var enumParsingMtd = parsingClass.GetMethodNonNull(nameof(TryParseEnum), InternalStatic);
                return Parser.ForMethod(enumParsingMtd);
            }

            private static Parser CreateTryParseNullableBasicEnumParser()
            {
                var parsingClass = GetParsingClass();

                var nullableEnumParsingMtd = parsingClass.GetMethodNonNull(nameof(TryParseNullableEnum), InternalStatic);
                return Parser.ForMethod(nullableEnumParsingMtd);
            }

            private static Parser CreateTryParseFlagsEnumParser()
            {
                var parsingClass = GetParsingClass();

                var enumParsingMtd = parsingClass.GetMethodNonNull(nameof(TryParseFlagsEnum), InternalStatic);
                return Parser.ForMethod(enumParsingMtd);
            }

            private static Parser CreateTryParseNullableFlagsEnumParser()
            {
                var parsingClass = GetParsingClass();

                var enumParsingMtd = parsingClass.GetMethodNonNull(nameof(TryParseNullableFlagsEnum), InternalStatic);
                return Parser.ForMethod(enumParsingMtd);
            }

            private static bool TryParseEnum(ReadOnlySpan<char> span, in ReadContext _, out T val)
            {
                // todo: use a better method when one is available (tracking issue: https://github.com/kevin-montrose/Cesil/issues/7)
                //       maybe after https://github.com/dotnet/corefx/issues/15453 lands?

                // doing this instead of a .TryParse because we don't want to accept ints
                for (var i = 0; i < Names.Length; i++)
                {
                    var name = Names[i];
                    // use CompareTo because we need to allow different casings
                    var cmp = span.CompareTo(name.AsSpan(), StringComparison.InvariantCultureIgnoreCase);
                    if (cmp == 0)
                    {
                        val = Values[i];
                        return true;
                    }
                }

                val = default;
                return false;
            }

            private static bool TryParseNullableEnum(ReadOnlySpan<char> data, in ReadContext _, out T? val)
            {
                if (data.Length == 0)
                {
                    val = null;
                    return true;
                }

                if (!TryParseEnum(data, _, out var pVal))
                {
                    val = null;
                    return false;
                }

                val = pVal;
                return true;
            }

            private static bool TryParseFlagsEnum(ReadOnlySpan<char> data, in ReadContext _, out T val)
            {
                if(!Utils.TryParseFlagsEnum(data, Names, ULongValues, out val))
                {
#pragma warning disable CES0005     // T is generic, so we can't annotate it, but it needs a default value
                    val = default!;
#pragma warning restore CES0005     
                    return false;
                }

                return true;
            }

            private static bool TryParseNullableFlagsEnum(ReadOnlySpan<char> data, in ReadContext _, out T? val)
            {
                if (data.Length == 0)
                {
                    val = null;
                    return true;
                }

                if (!TryParseFlagsEnum(data, _, out var pVal))
                {
                    val = null;
                    return false;
                }

                val = pVal;
                return true;
            }
        }

        private static bool TryParseString(ReadOnlySpan<char> span, in ReadContext _, out string val)
        {
            if (span.Length == 0)
            {
                val = "";
                return true;
            }

            val = new string(span);
            return true;
        }

        private static bool TryParseVersion(ReadOnlySpan<char> span, in ReadContext _, out Version? val)
        => Version.TryParse(span, out val);

        private static bool TryParseUri(ReadOnlySpan<char> span, in ReadContext _, out Uri? val)
        {
            if (!TryParseString(span, in _, out var asStr))
            {
                val = default;
                return false;
            }

            return Uri.TryCreate(asStr, UriKind.RelativeOrAbsolute, out val);
        }

        // non-null

        private static bool TryParseBool(ReadOnlySpan<char> span, in ReadContext _, out bool val)
        => bool.TryParse(span, out val);

        private static bool TryParseChar(ReadOnlySpan<char> span, in ReadContext _, out char val)
        {
            if (span.Length != 1)
            {
                val = default;
                return false;
            }

            val = span[0];
            return true;
        }

        private static bool TryParseDateTime(ReadOnlySpan<char> span, in ReadContext _, out DateTime val)
        => DateTime.TryParse(span, CultureInfo.InvariantCulture, DateTimeStyles.None, out val);

        private static bool TryParseDateTimeOffset(ReadOnlySpan<char> span, in ReadContext _, out DateTimeOffset val)
        => DateTimeOffset.TryParse(span, CultureInfo.InvariantCulture, DateTimeStyles.None, out val);


        private static bool TryParseByte(ReadOnlySpan<char> span, in ReadContext _, out byte val)
        => byte.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);


        private static bool TryParseSByte(ReadOnlySpan<char> span, in ReadContext _, out sbyte val)
        => sbyte.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);


        private static bool TryParseShort(ReadOnlySpan<char> span, in ReadContext _, out short val)
        => short.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);


        private static bool TryParseUShort(ReadOnlySpan<char> span, in ReadContext _, out ushort val)
        => ushort.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out val);


        private static bool TryParseInt(ReadOnlySpan<char> span, in ReadContext _, out int val)
        => int.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);


        private static bool TryParseUInt(ReadOnlySpan<char> span, in ReadContext _, out uint val)
        => uint.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out val);


        private static bool TryParseLong(ReadOnlySpan<char> span, in ReadContext _, out long val)
        => long.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);


        private static bool TryParseULong(ReadOnlySpan<char> span, in ReadContext _, out ulong val)
        => ulong.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out val);


        private static bool TryParseFloat(ReadOnlySpan<char> span, in ReadContext _, out float val)
        => float.TryParse(span, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out val);


        private static bool TryParseDouble(ReadOnlySpan<char> span, in ReadContext _, out double val)
        => double.TryParse(span, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out val);


        private static bool TryParseDecimal(ReadOnlySpan<char> span, in ReadContext _, out decimal val)
        => decimal.TryParse(span, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out val);

        private static bool TryParseGUID(ReadOnlySpan<char> span, in ReadContext _, out Guid val)
        => Guid.TryParse(span, out val);

        private static bool TryParseTimeSpan(ReadOnlySpan<char> span, in ReadContext _, out TimeSpan val)
        => TimeSpan.TryParse(span, out val);

        private static bool TryParseIndex(ReadOnlySpan<char> span, in ReadContext _, out Index val)
        {
            if (span.Length == 0)
            {
                val = default;
                return false;
            }

            var fromEnd = span[0] == '^';
            var ixSpan = fromEnd ? span.Slice(1) : span;

            if (!TryParseInt(ixSpan, in _, out var ix))
            {
                val = default;
                return false;
            }

            val = new Index(ix, fromEnd);
            return true;
        }

        private static bool TryParseRange(ReadOnlySpan<char> span, in ReadContext _, out Range val)
        {
            if (span.Length == 0)
            {
                val = default;
                return false;
            }

            var dotIndex = Utils.FindChar(span, 0, '.');
            if (dotIndex == -1)
            {
                val = default;
                return false;
            }

            var secondDotIndex = dotIndex + 1;
            if (secondDotIndex >= span.Length)
            {
                val = default;
                return false;
            }

            if (span[secondDotIndex] != '.')
            {
                val = default;
                return false;
            }

            var startChars = span.Slice(0, dotIndex);
            var endChars = span.Slice(secondDotIndex + 1);

            Index start, end;

            if (!TryParseNullableIndex(startChars, in _, out var startNullable))
            {
                val = default;
                return false;
            }
            else
            {
                start = startNullable ?? Index.Start;
            }

            if (!TryParseNullableIndex(endChars, in _, out var endNullable))
            {
                val = default;
                return false;
            }
            else
            {
                end = endNullable ?? Index.End;
            }

            val = new Range(start, end);
            return true;
        }

        // nullable

        private static bool TryParseNullableBool(ReadOnlySpan<char> span, in ReadContext _, out bool? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseBool(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        private static bool TryParseNullableChar(ReadOnlySpan<char> span, in ReadContext _, out char? val)
        {
            if (span.Length == 0)
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

        private static bool TryParseNullableDateTime(ReadOnlySpan<char> span, in ReadContext _, out DateTime? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseDateTime(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableDateTimeOffset(ReadOnlySpan<char> span, in ReadContext _, out DateTimeOffset? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseDateTimeOffset(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableByte(ReadOnlySpan<char> span, in ReadContext _, out byte? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseByte(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableSByte(ReadOnlySpan<char> span, in ReadContext _, out sbyte? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseSByte(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableShort(ReadOnlySpan<char> span, in ReadContext _, out short? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseShort(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableUShort(ReadOnlySpan<char> span, in ReadContext _, out ushort? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseUShort(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableInt(ReadOnlySpan<char> span, in ReadContext _, out int? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseInt(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableUInt(ReadOnlySpan<char> span, in ReadContext _, out uint? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseUInt(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableLong(ReadOnlySpan<char> span, in ReadContext _, out long? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseLong(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableULong(ReadOnlySpan<char> span, in ReadContext _, out ulong? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseULong(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableFloat(ReadOnlySpan<char> span, in ReadContext _, out float? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseFloat(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableDouble(ReadOnlySpan<char> span, in ReadContext _, out double? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseDouble(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }


        private static bool TryParseNullableDecimal(ReadOnlySpan<char> span, in ReadContext _, out decimal? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseDecimal(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        private static bool TryParseNullableGUID(ReadOnlySpan<char> span, in ReadContext _, out Guid? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseGUID(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        private static bool TryParseNullableTimeSpan(ReadOnlySpan<char> span, in ReadContext _, out TimeSpan? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseTimeSpan(span, _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        private static bool TryParseNullableIndex(ReadOnlySpan<char> span, in ReadContext _, out Index? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseIndex(span, in _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }

        private static bool TryParseNullableRange(ReadOnlySpan<char> span, in ReadContext _, out Range? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (!TryParseRange(span, in _, out var pVal))
            {
                val = null;
                return false;
            }

            val = pVal;
            return true;
        }
    }
}