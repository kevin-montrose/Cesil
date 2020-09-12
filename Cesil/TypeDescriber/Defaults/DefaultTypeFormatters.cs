using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

using static Cesil.BindingFlagsConstants;

namespace Cesil
{
    [SuppressMessage("", "IDE0051", Justification = "Used via reflection")]
    internal static class DefaultTypeFormatters
    {
        internal static readonly char[] COMMA_AND_SPACE = new[] { ',', ' ' };

        internal static class DefaultEnumTypeFormatter<T>
            where T : struct, Enum
        {
            private static readonly string[] Names = CreateNames();
            private static readonly ulong[] Values = CreateValues();
            private static readonly int MaxNameLength = GetMaxNameLength();

            internal static readonly Formatter TryEnumFormatter = CreateTryEnumFormatter();
            internal static readonly Formatter TryNullableEnumFormatter = CreateTryNullableEnumFormatter();

            private static TypeInfo GetFormattingClass()
            {
                var enumType = typeof(T).GetTypeInfo();

                var formattingClass = Types.DefaultEnumTypeFormatter.MakeGenericType(enumType).GetTypeInfo();

                return formattingClass;
            }

            private static string[] CreateNames()
            {
                var enumType = typeof(T).GetTypeInfo();
                if (enumType.IsFlagsEnum())
                {
                    // only need the actual names if we're in Flags mode
                    return Enum.GetNames(enumType);
                }

                return Array.Empty<string>();
            }

            private static int GetMaxNameLength()
            {
                // only need this for flags, so re-using CreateNames() works fine
                var names = CreateNames();

                var maxLen = -1;
                for(var i = 0; i < names.Length; i++)
                {
                    maxLen = Math.Max(maxLen, names[i].Length);
                }

                return maxLen;
            }

            private static ulong[] CreateValues()
            {
                var enumType = typeof(T).GetTypeInfo();
                if (enumType.IsFlagsEnum())
                {
                    // only need the actual values if we're in Flags mode
                    var values = Enum.GetValues(enumType);
                    var ret = new ulong[values.Length];
                    for (var i = 0; i < values.Length; i++)
                    {
                        var obj = values.GetValue(i);
                        var asT = (T)Utils.NonNull(obj);
                        ret[i] = Utils.EnumToULong(asT);
                    }

                    return ret;
                }

                return Array.Empty<ulong>();
            }

            private static Formatter CreateTryEnumFormatter()
            {
                var enumType = typeof(T).GetTypeInfo();
                if (enumType.IsFlagsEnum())
                {
                    return CreateTryFlagsEnumFormatter();
                }

                return CreateTryBasicEnumFormatter();
            }

            private static Formatter CreateTryNullableEnumFormatter()
            {
                var enumType = typeof(T).GetTypeInfo();
                if (enumType.IsFlagsEnum())
                {
                    return CreateTryNullableFlagsEnumFormatter();
                }

                return CreateTryNullableBasicEnumFormatter();
            }

            private static Formatter CreateTryBasicEnumFormatter()
            {
                var formattingClass = GetFormattingClass();

                var enumParsingMtd = formattingClass.GetMethodNonNull(nameof(TryFormatBasicEnum), InternalStatic);
                return Formatter.ForMethod(enumParsingMtd);
            }

            private static Formatter CreateTryFlagsEnumFormatter()
            {
                var formattingClass = GetFormattingClass();

                var enumParsingMtd = formattingClass.GetMethodNonNull(nameof(TryFormatFlagsEnum), InternalStatic);
                return Formatter.ForMethod(enumParsingMtd);
            }

            private static Formatter CreateTryNullableBasicEnumFormatter()
            {
                var formattingClass = GetFormattingClass();

                var nullableEnumParsingMtd = formattingClass.GetMethodNonNull(nameof(TryFormatNullableBasicEnum), InternalStatic);
                return Formatter.ForMethod(nullableEnumParsingMtd);
            }

            private static Formatter CreateTryNullableFlagsEnumFormatter()
            {
                var formattingClass = GetFormattingClass();

                var nullableEnumParsingMtd = formattingClass.GetMethodNonNull(nameof(TryFormatNullableFlagsEnum), InternalStatic);
                return Formatter.ForMethod(nullableEnumParsingMtd);
            }

            private static bool TryFormatBasicEnum(T e, in WriteContext _, IBufferWriter<char> writer)
            {
                if (!Enum.IsDefined(typeof(T), e)) return false;

                // this shouldn't allocate
                var valStr = e.ToString();

                var charSpan = writer.GetSpan(valStr.Length);
                if (charSpan.Length < valStr.Length) return false;

                valStr.AsSpan().CopyTo(charSpan);
                writer.Advance(valStr.Length);

                return true;
            }

            private static bool TryFormatNullableBasicEnum(T? e, in WriteContext _, IBufferWriter<char> writer)
            {
                if (e == null) return true;

                return TryFormatBasicEnum(e.Value, _, writer);
            }

            private static bool TryFormatFlagsEnum(T e, in WriteContext _, IBufferWriter<char> writer)
            {
                // assuming that most of the time only a single flag is set, so picking the biggest name is
                //   a solid guess
                var charSpan = writer.GetSpan(MaxNameLength);

                var formatRes = Utils.TryFormatFlagsEnum(e, Names, Values, charSpan);

                if (formatRes == 0)
                {
                    // malformed, fail
                    return false;
                }
                else if (formatRes > 0)
                {
                    // fit in the default span, so just take it
                    writer.Advance(formatRes);
                    return true;
                }
                else
                {
                    // buffer wasn't big enough, get a bigger buffer and try again
                    var neededLen = -formatRes;
                    charSpan = writer.GetSpan(neededLen);

                    var res = Utils.TryFormatFlagsEnum(e, Names, Values, charSpan);
                    if (res <= 0)
                    {
                        // couldn't fit, something is wildly wrong so fail
                        return false;
                    }

                    writer.Advance(neededLen);
                    return true;
                }
            }

            private static bool TryFormatNullableFlagsEnum(T? e, in WriteContext _, IBufferWriter<char> writer)
            {
                if (e == null) return true;

                return TryFormatFlagsEnum(e.Value, _, writer);
            }
        }

        private static bool TryFormatString(string s, in WriteContext _, IBufferWriter<char> writer)
        {
            if (string.IsNullOrEmpty(s))
            {
                return true;
            }

            var charSpan = writer.GetSpan(s.Length);
            if (charSpan.Length < s.Length) return false;

            s.AsSpan().CopyTo(charSpan);

            writer.Advance(s.Length);
            return true;
        }

        private static bool TryFormatVersion(Version v, in WriteContext _, IBufferWriter<char> writer)
        {
            const int MAX_CHARS =
                10 +    // major
                1 +     // dot
                10 +    // minor
                1 +     // dot
                10 +    // build
                1 +     // dot
                10;     // revision

            if (v == null)
            {
                return true;
            }

            var charSpan = writer.GetSpan(MAX_CHARS);
            var ret = v.TryFormat(charSpan, out var chars);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatUri(Uri u, in WriteContext _, IBufferWriter<char> writer)
        {
            if (u == null)
            {
                return true;
            }

            return TryFormatString(u.ToString(), in _, writer);
        }

        // non-nullable

        private static bool TryFormatBool(bool b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b)
            {
                var charSpan = writer.GetSpan(4);

                if (charSpan.Length < 4) return false;

                bool.TrueString.AsSpan().CopyTo(charSpan);
                writer.Advance(4);
            }
            else
            {
                var charSpan = writer.GetSpan(5);

                if (charSpan.Length < 5) return false;

                bool.FalseString.AsSpan().CopyTo(charSpan);
                writer.Advance(5);
            }

            return true;
        }

        private static bool TryFormatChar(char c, in WriteContext _, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(1);
            if (charSpan.Length < 1) return false;

            charSpan[0] = c;
            writer.Advance(1);

            return true;
        }

        private static bool TryFormatByte(byte b, in WriteContext _, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(3);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatSByte(sbyte b, in WriteContext _, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(4);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatShort(short b, in WriteContext _, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(6);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatUShort(ushort b, in WriteContext _, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(5);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatInt(int b, in WriteContext _, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(11);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatUInt(uint b, in WriteContext _, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(10);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatLong(long b, in WriteContext _, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(20);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatULong(ulong b, in WriteContext _, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(20);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatFloat(float b, in WriteContext _, IBufferWriter<char> writer)
        {
            // based on https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#GFormatString
            const int MAX_CHARS =
                9 +     // 9 significant digits
                1 +     // negative sign
                1 +     // decimal point
                1 +     // e
                2 +     // magnitude digits
                1;      // magnitude sign bits
            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = b.TryFormat(charSpan, out var chars, "G9", provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatDouble(double b, in WriteContext _, IBufferWriter<char> writer)
        {
            // based on https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#GFormatString
            const int MAX_CHARS =
                17 +    // 17 significant digits
                1 +     // negative sign
                1 +     // decimal point
                1 +     // e
                3 +     // magnitude digits
                1;      // magnitude sign bits
            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = b.TryFormat(charSpan, out var chars, "G17", provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatDecimal(decimal b, in WriteContext _, IBufferWriter<char> writer)
        {
            // based on https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#GFormatString
            const int MAX_CHARS =
                29 +    // 29 significant digits
                1 +     // negative sign
                1 +     // decimal point
                1 +     // e
                2 +     // magnitude digits
                1;      // magnitude sign bits
            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatDateTime(DateTime dt, in WriteContext _, IBufferWriter<char> writer)
        {
            // yyyy-MM-dd HH:mm:ssZ
            const int MAX_CHARS = 20;

            // formatting will NOT convert (even though it'll write a Z)
            //   though DateTimeOffset will
            if (dt.Kind != DateTimeKind.Utc)
            {
                dt = dt.ToUniversalTime();
            }

            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = dt.TryFormat(charSpan, out var chars, "u", provider: CultureInfo.InstalledUICulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatDateTimeOffset(DateTimeOffset dt, in WriteContext _, IBufferWriter<char> writer)
        {
            // yyyy-MM-dd HH:mm:ssZ
            const int MAX_CHARS = 20;

            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = dt.TryFormat(charSpan, out var chars, "u", formatProvider: CultureInfo.InstalledUICulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatGuid(Guid g, in WriteContext _, IBufferWriter<char> writer)
        {
            // 32 digits + 4 dashes
            const int MAX_CHARS = 36;

            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = g.TryFormat(charSpan, out var chars, "D");
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatTimeSpan(TimeSpan ts, in WriteContext _, IBufferWriter<char> writer)
        {
            // based on: https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-timespan-format-strings?view=netframework-4.7.2
            const int MAX_CHARS =
                1 +     // sign
                8 +     // days
                1 +     // separator
                2 +     // hours
                1 +     // separator
                2 +     // minutes
                1 +     // separator
                2 +     // seconds
                1 +     // separator
                7;      // fractional digits

            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = ts.TryFormat(charSpan, out var chars, "c");
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatIndex(Index i, in WriteContext _, IBufferWriter<char> writer)
        {
            const int MAX_CHARS =
                1 + // ^
                11; // int max length

            var charSpan = writer.GetSpan(MAX_CHARS);

            var written = 0;

            if (i.IsFromEnd)
            {
                if (charSpan.Length == 0) return false;

                charSpan[0] = '^';
                charSpan = charSpan.Slice(1);
                written++;
            }

            var ret = i.Value.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            written += chars;

            writer.Advance(written);
            return true;
        }

        private static bool TryFormatRange(Range r, in WriteContext _, IBufferWriter<char> writer)
        {
            const int MAX_CHARS =
                   1 +  // ^
                   11 + // int max length
                   2 +  // ..
                   1 +  // ^
                   11;  // int max length

            var charSpan = writer.GetSpan(MAX_CHARS);

            var written = 0;

            var start = r.Start;

            if (start.IsFromEnd)
            {
                if (charSpan.Length == 0) return false;

                charSpan[0] = '^';
                charSpan = charSpan.Slice(1);
                written++;
            }

            var ret = start.Value.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            written += chars;
            charSpan = charSpan.Slice(chars);

            if (charSpan.Length < 2) return false;

            charSpan[0] = '.';
            charSpan[1] = '.';

            charSpan = charSpan.Slice(2);

            written += 2;

            var end = r.End;

            if (end.IsFromEnd)
            {
                if (charSpan.Length == 0) return false;

                charSpan[0] = '^';
                charSpan = charSpan.Slice(1);
                written++;
            }

            ret = end.Value.TryFormat(charSpan, out chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            written += chars;

            writer.Advance(written);
            return true;
        }

        // nullable

        private static bool TryFormatNullableChar(char? c, in WriteContext _, IBufferWriter<char> writer)
        {
            if (c == null) return true;

            return TryFormatChar(c.Value, _, writer);
        }

        private static bool TryFormatNullableBool(bool? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatBool(b.Value, _, writer);
        }

        private static bool TryFormatNullableByte(byte? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatByte(b.Value, _, writer);
        }

        private static bool TryFormatNullableSByte(sbyte? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatSByte(b.Value, _, writer);
        }

        private static bool TryFormatNullableShort(short? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatShort(b.Value, _, writer);
        }

        private static bool TryFormatNullableUShort(ushort? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatUShort(b.Value, _, writer);
        }

        private static bool TryFormatNullableInt(int? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatInt(b.Value, _, writer);
        }

        private static bool TryFormatNullableUInt(uint? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatUInt(b.Value, _, writer);
        }

        private static bool TryFormatNullableLong(long? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatLong(b.Value, _, writer);
        }

        private static bool TryFormatNullableULong(ulong? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatULong(b.Value, _, writer);
        }

        private static bool TryFormatNullableFloat(float? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatFloat(b.Value, _, writer);
        }

        private static bool TryFormatNullableDouble(double? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatDouble(b.Value, _, writer);
        }

        private static bool TryFormatNullableDecimal(decimal? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatDecimal(b.Value, _, writer);
        }

        private static bool TryFormatNullableDateTime(DateTime? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatDateTime(b.Value, _, writer);
        }

        private static bool TryFormatNullableDateTimeOffset(DateTimeOffset? b, in WriteContext _, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatDateTimeOffset(b.Value, _, writer);
        }

        private static bool TryFormatNullableGuid(Guid? g, in WriteContext _, IBufferWriter<char> writer)
        {
            if (g == null) return true;

            return TryFormatGuid(g.Value, _, writer);
        }

        private static bool TryFormatNullableTimeSpan(TimeSpan? ts, in WriteContext _, IBufferWriter<char> writer)
        {
            if (ts == null) return true;

            return TryFormatTimeSpan(ts.Value, _, writer);
        }

        private static bool TryFormatNullableIndex(Index? i, in WriteContext _, IBufferWriter<char> writer)
        {
            if (i == null) return true;

            return TryFormatIndex(i.Value, _, writer);
        }

        private static bool TryFormatNullableRange(Range? r, in WriteContext _, IBufferWriter<char> writer)
        {
            if (r == null) return true;

            return TryFormatRange(r.Value, _, writer);
        }
    }
}
