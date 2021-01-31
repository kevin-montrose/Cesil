// be aware, this file is also included in Cesil.SourceGenerator
//   so it is _very_ particular in strucutre
//
// don't edit it all willy-nilly
using System;
using System.Linq;
using System.Reflection;

using static Cesil.BindingFlagsConstants;

// todo: analyzer to enforce all the conventions needed for Cesil.SourceGenerator to work

namespace Cesil
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("", "IDE0051", Justification = "Used via reflection")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("", "IDE0060", Justification = "Unused paramters are required")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("", "IDE0002", Justification = "Pattern is important for source generation")]
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
                for (var i = 0; i < values.Length; i++)
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

            private static bool TryParseEnum(ReadOnlySpan<char> span, in ReadContext ctx, out T val)
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

            private static bool TryParseNullableEnum(ReadOnlySpan<char> data, in ReadContext ctx, out T? val)
            {
                if (data.Length == 0)
                {
                    val = null;
                    return true;
                }

                if (!TryParseEnum(data, ctx, out var pVal))
                {
                    val = null;
                    return false;
                }

                val = pVal;
                return true;
            }

            private static bool TryParseFlagsEnum(ReadOnlySpan<char> data, in ReadContext ctx, out T val)
            {
                if (!ParseFlagsEnumImpl(data, Names, ULongValues, out val))
                {
#pragma warning disable CES0005     // T is generic, so we can't annotate it, but it needs a default value
                    val = default!;
#pragma warning restore CES0005     
                    return false;
                }

                return true;
            }

            private static bool TryParseNullableFlagsEnum(ReadOnlySpan<char> data, in ReadContext ctx, out T? val)
            {
                if (data.Length == 0)
                {
                    val = null;
                    return true;
                }

                if (!TryParseFlagsEnum(data, ctx, out var pVal))
                {
                    val = null;
                    return false;
                }

                val = pVal;
                return true;
            }

            /// <summary>
            /// This is _like_ calling TryParse(), but it doesn't allow values
            /// that aren't actually declared on the enum.
            /// </summary>
            internal static bool ParseFlagsEnumImpl(ReadOnlySpan<char> data, string[] enumNames, ulong[] enumValues, out T resultT)
            {
                // based on: https://referencesource.microsoft.com/#mscorlib/system/enum.cs,432

                ulong result = 0;

                while (!data.IsEmpty)
                {
                    var ix = MemoryExtensions.IndexOf(data, ',');
                    int startNextIx;

                    if (ix == -1)
                    {
                        ix = data.Length;
                        startNextIx = data.Length;
                    }
                    else
                    {
                        startNextIx = ix + 1;
                    }

                    var value = data[..ix];
                    value = TrimLeadingWhitespace(value);
                    value = TrimTrailingWhitespace(value);

                    var success = false;

                    for (int j = 0; j < enumNames.Length; j++)
                    {
                        var namesSpan = enumNames[j].AsSpan();

                        // have to use a comparer because different casing is legal!
                        var res = namesSpan.CompareTo(value, StringComparison.InvariantCultureIgnoreCase);
                        if (res != 0)
                        {
                            continue;
                        }

                        var item = enumValues[j];

                        result |= item;
                        success = true;
                        break;
                    }

                    if (!success)
                    {
                        resultT = default;
                        return false;
                    }

                    data = data[startNextIx..];
                }

                resultT = Utils.ULongToEnum<T>(result);
                return true;
            }

            static ReadOnlySpan<char> TrimLeadingWhitespace(ReadOnlySpan<char> span)
            {
                var skip = 0;
                var len = span.Length;

                while (skip < len)
                {
                    var c = span[skip];
                    if (!char.IsWhiteSpace(c)) break;

                    skip++;
                }

                if (skip == 0) return span;
                if (skip == len) return ReadOnlySpan<char>.Empty;

                return span[skip..];
            }

            static ReadOnlySpan<char> TrimTrailingWhitespace(ReadOnlySpan<char> span)
            {
                var len = span.Length;
                var start = len - 1;
                var skip = start;

                while (skip >= 0)
                {
                    var c = span[skip];
                    if (!char.IsWhiteSpace(c)) break;

                    skip--;
                }

                if (skip == start) return span;
                if (skip == -1) return ReadOnlySpan<char>.Empty;

                return span[0..(skip + 1)];
            }
        }

        private static bool TryParseString(ReadOnlySpan<char> span, in ReadContext ctx, out string val)
        {
            if (span.Length == 0)
            {
                val = "";
                return true;
            }

            val = new string(span);
            return true;
        }

        private static bool TryParseVersion(ReadOnlySpan<char> span, in ReadContext ctx, out Version? val)
        {
            return Version.TryParse(span, out val);
        }

        private static bool TryParseUri(ReadOnlySpan<char> span, in ReadContext ctx, out Uri? val)
        {
            if (DefaultTypeParsers.TryParseString(span, in ctx, out var asStr))
            {
                return Uri.TryCreate(asStr, UriKind.RelativeOrAbsolute, out val);
            }

            val = default;
            return false;
        }

        // non-null

        private static bool TryParseNInt(ReadOnlySpan<char> span, in ReadContext ctx, out nint val)
        {
            // nint is actually and IntPtr
            switch (IntPtr.Size)
            {
                // IntPtr is an int
                case 4:
                    if (DefaultTypeParsers.TryParseInt(span, ctx, out var intVal))
                    {
                        val = (nint)intVal;
                        return true;
                    }

                    val = default;
                    return false;

                // IntPtr is a long
                case 8:
                    if (DefaultTypeParsers.TryParseLong(span, ctx, out var longVal))
                    {
                        val = (nint)longVal;
                        return true;
                    }

                    val = default;
                    return false;

                // Shouldn't be possible
                default:
                    val = default;
                    return false;
            }
        }

        private static bool TryParseNUInt(ReadOnlySpan<char> span, in ReadContext ctx, out nuint val)
        {
            // nuint is actually and UIntPtr
            switch (UIntPtr.Size)
            {
                // UIntPtr is an int
                case 4:
                    if (DefaultTypeParsers.TryParseUInt(span, ctx, out var uintVal))
                    {
                        val = (nuint)uintVal;
                        return true;
                    }

                    val = default;
                    return false;

                // UIntPtr is a long
                case 8:
                    if (DefaultTypeParsers.TryParseULong(span, ctx, out var ulongVal))
                    {
                        val = (nuint)ulongVal;
                        return true;
                    }

                    val = default;
                    return false;

                // Shouldn't be possible
                default:
                    val = default;
                    return false;
            }
        }

        private static bool TryParseBool(ReadOnlySpan<char> span, in ReadContext ctx, out bool val)
        {
            return bool.TryParse(span, out val);
        }

        private static bool TryParseChar(ReadOnlySpan<char> span, in ReadContext ctx, out char val)
        {
            if (span.Length != 1)
            {
                val = default;
                return false;
            }

            val = span[0];
            return true;
        }

        private static bool TryParseDateTime(ReadOnlySpan<char> span, in ReadContext ctx, out DateTime val)
        {
            return DateTime.TryParse(span, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out val);
        }

        private static bool TryParseDateTimeOffset(ReadOnlySpan<char> span, in ReadContext ctx, out DateTimeOffset val)
        {
            return DateTimeOffset.TryParse(span, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out val);
        }

        private static bool TryParseByte(ReadOnlySpan<char> span, in ReadContext ctx, out byte val)
        {
            return byte.TryParse(span, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseSByte(ReadOnlySpan<char> span, in ReadContext ctx, out sbyte val)
        {
            return sbyte.TryParse(span, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseShort(ReadOnlySpan<char> span, in ReadContext ctx, out short val)
        {
            return short.TryParse(span, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseUShort(ReadOnlySpan<char> span, in ReadContext ctx, out ushort val)
        {
            return ushort.TryParse(span, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseInt(ReadOnlySpan<char> span, in ReadContext ctx, out int val)
        {
            return int.TryParse(span, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseUInt(ReadOnlySpan<char> span, in ReadContext ctx, out uint val)
        {
            return uint.TryParse(span, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseLong(ReadOnlySpan<char> span, in ReadContext ctx, out long val)
        {
            return long.TryParse(span, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseULong(ReadOnlySpan<char> span, in ReadContext ctx, out ulong val)
        {
            return ulong.TryParse(span, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseFloat(ReadOnlySpan<char> span, in ReadContext ctx, out float val)
        {
            const System.Globalization.NumberStyles STYLE = System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowExponent;

            return float.TryParse(span, STYLE, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseDouble(ReadOnlySpan<char> span, in ReadContext ctx, out double val)
        {
            const System.Globalization.NumberStyles STYLE = System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowExponent;

            return double.TryParse(span, STYLE, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseDecimal(ReadOnlySpan<char> span, in ReadContext ctx, out decimal val)
        {
            const System.Globalization.NumberStyles STYLE = System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowExponent;

            return decimal.TryParse(span, STYLE, System.Globalization.CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseGuid(ReadOnlySpan<char> span, in ReadContext ctx, out Guid val)
        {
            return Guid.TryParse(span, out val);
        }

        private static bool TryParseTimeSpan(ReadOnlySpan<char> span, in ReadContext ctx, out TimeSpan val)
        {
            return TimeSpan.TryParse(span, out val);
        }

        private static bool TryParseIndex(ReadOnlySpan<char> span, in ReadContext ctx, out Index val)
        {
            if (span.Length == 0)
            {
                val = default;
                return false;
            }

            var fromEnd = span[0] == '^';
            var ixSpan = fromEnd ? span.Slice(1) : span;

            if (!TryParseInt(ixSpan, in ctx, out var ix))
            {
                val = default;
                return false;
            }

            val = new Index(ix, fromEnd);
            return true;
        }

        private static bool TryParseRange(ReadOnlySpan<char> span, in ReadContext ctx, out Range val)
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

            if (!TryParseNullableIndex(startChars, in ctx, out var startNullable))
            {
                val = default;
                return false;
            }
            else
            {
                start = startNullable ?? Index.Start;
            }

            if (!TryParseNullableIndex(endChars, in ctx, out var endNullable))
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

        private static bool TryParseNullableNInt(ReadOnlySpan<char> span, in ReadContext ctx, out nint? val)
        {
            if(span.Length == 0)
            {
                val = null;
                return true;
            }

            if(DefaultTypeParsers.TryParseNInt(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableNUInt(ReadOnlySpan<char> span, in ReadContext ctx, out nuint? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseNUInt(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableBool(ReadOnlySpan<char> span, in ReadContext ctx, out bool? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseBool(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableChar(ReadOnlySpan<char> span, in ReadContext ctx, out char? val)
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

        private static bool TryParseNullableDateTime(ReadOnlySpan<char> span, in ReadContext ctx, out DateTime? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseDateTime(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableDateTimeOffset(ReadOnlySpan<char> span, in ReadContext ctx, out DateTimeOffset? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseDateTimeOffset(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableByte(ReadOnlySpan<char> span, in ReadContext ctx, out byte? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseByte(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableSByte(ReadOnlySpan<char> span, in ReadContext ctx, out sbyte? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseSByte(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableShort(ReadOnlySpan<char> span, in ReadContext ctx, out short? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseShort(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableUShort(ReadOnlySpan<char> span, in ReadContext ctx, out ushort? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseUShort(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableInt(ReadOnlySpan<char> span, in ReadContext ctx, out int? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseInt(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableUInt(ReadOnlySpan<char> span, in ReadContext ctx, out uint? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseUInt(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableLong(ReadOnlySpan<char> span, in ReadContext ctx, out long? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseLong(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableULong(ReadOnlySpan<char> span, in ReadContext ctx, out ulong? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseULong(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableFloat(ReadOnlySpan<char> span, in ReadContext ctx, out float? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseFloat(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableDouble(ReadOnlySpan<char> span, in ReadContext ctx, out double? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseDouble(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableDecimal(ReadOnlySpan<char> span, in ReadContext ctx, out decimal? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseDecimal(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableGuid(ReadOnlySpan<char> span, in ReadContext ctx, out Guid? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseGuid(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableTimeSpan(ReadOnlySpan<char> span, in ReadContext ctx, out TimeSpan? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseTimeSpan(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableIndex(ReadOnlySpan<char> span, in ReadContext ctx, out Index? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseIndex(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }

        private static bool TryParseNullableRange(ReadOnlySpan<char> span, in ReadContext ctx, out Range? val)
        {
            if (span.Length == 0)
            {
                val = null;
                return true;
            }

            if (DefaultTypeParsers.TryParseRange(span, in ctx, out var pVal))
            {
                val = pVal;
                return true;
            }

            val = null;
            return false;
        }
    }
}