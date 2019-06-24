using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Cesil
{
    [SuppressMessage("", "IDE0051", Justification = "Used via reflection")]
    internal static class DefaultTypeFormatters
    {
        internal static readonly char[] COMMA_AND_SPACE = new[] { ',', ' ' };

        internal static class DefaultFlagsEnumTypeFormatter<T>
            where T : struct, Enum
        {
            private static readonly string[] Names;

            internal static readonly Formatter TryParseFlagsEnumFormatter;
            internal static readonly Formatter TryParseNullableFlagsEnumFormatter;

            static DefaultFlagsEnumTypeFormatter()
            {
                var enumType = typeof(T).GetTypeInfo();
                Names = Enum.GetNames(enumType);

                var parsingClass = Types.DefaultFlagsEnumTypeFormatterType.MakeGenericType(enumType).GetTypeInfo();

                var enumParsingMtd = parsingClass.GetMethod(nameof(TryFormatFlagsEnum), BindingFlags.Static | BindingFlags.NonPublic);
                TryParseFlagsEnumFormatter = Formatter.ForMethod(enumParsingMtd);

                var nullableEnumParsingMtd = parsingClass.GetMethod(nameof(TryFormatNullableFlagsEnum), BindingFlags.Static | BindingFlags.NonPublic);
                TryParseNullableFlagsEnumFormatter = Formatter.ForMethod(nullableEnumParsingMtd);
            }

            private static bool TryFormatFlagsEnum(T e, in WriteContext _, IBufferWriter<char> writer)
            {
                // this will allocate, but we don't really have a choice?
                var valStr = e.ToString();

                // this will _really_ allocate... but again, we have to verify somehow
                var parts = valStr.Split(COMMA_AND_SPACE, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var isValid = false;
                    for (var j = 0; j < Names.Length; j++)
                    {
                        var name = Names[j];
                        if (name.Equals(part))
                        {
                            isValid = true;
                            break;
                        }
                    }

                    if (!isValid) return false;
                }

                var charSpan = writer.GetSpan(valStr.Length);
                if (charSpan.Length < valStr.Length) return false;

                valStr.AsSpan().CopyTo(charSpan);
                writer.Advance(valStr.Length);

                return true;
            }

            private static bool TryFormatNullableFlagsEnum(T? e, in WriteContext _, IBufferWriter<char> writer)
            {
                if (e == null) return true;

                return TryFormatFlagsEnum(e.Value, _, writer);
            }
        }

        internal static class DefaultEnumTypeFormatter<T>
            where T : struct, Enum
        {
            internal static readonly Formatter TryParseEnumFormatter;
            internal static readonly Formatter TryParseNullableEnumFormatter;

            static DefaultEnumTypeFormatter()
            {
                var enumType = typeof(T).GetTypeInfo();

                var parsingClass = Types.DefaultEnumTypeFormatterType.MakeGenericType(enumType).GetTypeInfo();

                var enumParsingMtd = parsingClass.GetMethod(nameof(TryFormatEnum), BindingFlags.Static | BindingFlags.NonPublic);
                TryParseEnumFormatter = Formatter.ForMethod(enumParsingMtd);

                var nullableEnumParsingMtd = parsingClass.GetMethod(nameof(TryFormatNullableEnum), BindingFlags.Static | BindingFlags.NonPublic);
                TryParseNullableEnumFormatter = Formatter.ForMethod(nullableEnumParsingMtd);
            }

            private static bool TryFormatEnum(T e, in WriteContext _, IBufferWriter<char> writer)
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

            private static bool TryFormatNullableEnum(T? e, in WriteContext _, IBufferWriter<char> writer)
            {
                if (e == null) return true;

                return TryFormatEnum(e.Value, _, writer);
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

            var charSpan = writer.GetSpan(MAX_CHARS);
            var ret = v.TryFormat(charSpan, out var chars);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatUri(Uri u, in WriteContext _, IBufferWriter<char> writer)
        {
            if(u == null)
            {
                return false;
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
    }
}
