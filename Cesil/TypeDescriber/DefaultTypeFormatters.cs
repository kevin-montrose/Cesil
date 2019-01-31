using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace Cesil
{
    [SuppressMessage("", "IDE0051", Justification = "Used via reflection")]
    internal static class DefaultTypeFormatters
    {
        internal static readonly char[] COMMA_AND_SPACE = new[] { ',', ' ' };

        internal static class DefaultFlagsEnumTypeFormatter<T>
            where T : struct, Enum
        {
            private static readonly string[] Names = Enum.GetNames(typeof(T));

            internal static bool TryFormatFlagsEnum(T e, IBufferWriter<char> writer)
            {
                // this will allocate, but we don't really have a choice?
                var valStr = e.ToString();

                // this will _really_ allocate... but again, we have to verify somehow
                var parts = valStr.Split(COMMA_AND_SPACE, StringSplitOptions.RemoveEmptyEntries);
                for(var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var isValid = false;
                    for(var j = 0; j<Names.Length; j++)
                    {
                        var name = Names[j];
                        if(name.Equals(part))
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
                writer.Advance(valStr.Length );

                return true;
            }

            internal static bool TryFormatNullableFlagsEnum(T? e, IBufferWriter<char> writer)
            {
                if (e == null) return true;

                return TryFormatFlagsEnum(e.Value, writer);
            }
        }

        internal static class DefaultEnumTypeFormatter<T>
            where T: struct, Enum
        {
            internal static bool TryFormatEnum(T e, IBufferWriter<char> writer)
            {
                if (!Enum.IsDefined(typeof(T), e)) return false;

                // this shouldn't allocate
                var valStr = e.ToString();

                var charSpan = writer.GetSpan(valStr.Length);
                if (charSpan.Length < valStr.Length) return false;

                valStr.AsSpan().CopyTo(charSpan);
                writer.Advance(valStr.Length );

                return true;
            }

            internal static bool TryFormatNullableEnum(T? e, IBufferWriter<char> writer)
            {
                if (e == null) return true;

                return TryFormatEnum(e.Value, writer);
            }
        }

        private static bool TryFormatString(string s, IBufferWriter<char> writer)
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

        // non-nullable

        private static bool TryFormatBool(bool b, IBufferWriter<char> writer)
        {
            if (b)
            {
                var charSpan = writer.GetSpan(4);

                if (charSpan.Length < 4) return false;

                bool.TrueString.AsSpan().CopyTo(charSpan);
                writer.Advance(4 );
            }
            else
            {
                var charSpan = writer.GetSpan(5 );

                if (charSpan.Length < 5) return false;

                bool.FalseString.AsSpan().CopyTo(charSpan);
                writer.Advance(5 );
            }

            return true;
        }

        private static bool TryFormatChar(char c, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(1);
            if (charSpan.Length < 1) return false;

            charSpan[0] = c;
            writer.Advance(1);

            return true;
        }

        private static bool TryFormatByte(byte b, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(3);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatSByte(sbyte b, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(4);

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatShort(short b, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(6 );

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatUShort(ushort b, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(5 );

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatInt(int b, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(11 );

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatUInt(uint b, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(10 );

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatLong(long b, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(20 );

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatULong(ulong b, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(20 );

            var ret = b.TryFormat(charSpan, out var chars, provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatFloat(float b, IBufferWriter<char> writer)
        {
            // based on https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#GFormatString
            const int MAX_CHARS =
                9 +     // 9 significant digits
                1 +     // negative sign
                1 +     // decimal point
                1 +     // e
                2 +     // magnitude digits
                1;      // magnitude sign bits
            var charSpan = writer.GetSpan(MAX_CHARS );

            var ret = b.TryFormat(charSpan, out var chars, "G9", provider: CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatDouble(double b, IBufferWriter<char> writer)
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

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatDecimal(decimal b, IBufferWriter<char> writer)
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

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatDateTime(DateTime dt, IBufferWriter<char> writer)
        {
            // yyyy-MM-dd HH:mm:ssZ
            const int MAX_CHARS = 20;

            // formatting will NOT convert (even though it'll write a Z)
            //   though DateTimeOffset will
            if (dt.Kind != DateTimeKind.Utc)
            {
                dt = dt.ToUniversalTime();
            }

            var charSpan = writer.GetSpan(MAX_CHARS );
            
            var ret = dt.TryFormat(charSpan, out var chars, "u", provider: CultureInfo.InstalledUICulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatDateTimeOffset(DateTimeOffset dt, IBufferWriter<char> writer)
        {
            // yyyy-MM-dd HH:mm:ssZ
            const int MAX_CHARS = 20;
            
            var charSpan = writer.GetSpan(MAX_CHARS );
            
            var ret = dt.TryFormat(charSpan, out var chars, "u", formatProvider: CultureInfo.InstalledUICulture);
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatGuid(Guid g, IBufferWriter<char> writer)
        {
            // 32 digits + 4 dashes
            const int MAX_CHARS = 36;

            var charSpan = writer.GetSpan(MAX_CHARS );
            
            var ret = g.TryFormat(charSpan, out var chars, "D");
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        private static bool TryFormatTimeSpan(TimeSpan ts, IBufferWriter<char> writer)
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

            var charSpan = writer.GetSpan(MAX_CHARS );
            
            var ret = ts.TryFormat(charSpan, out var chars, "c");
            if (!ret) return false;

            writer.Advance(chars );
            return true;
        }

        // nullable

        private static bool TryFormatNullableChar(char? c, IBufferWriter<char> writer)
        {
            if (c == null) return true;

            return TryFormatChar(c.Value, writer);
        }

        private static bool TryFormatNullableBool(bool? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatBool(b.Value, writer);
        }

        private static bool TryFormatNullableByte(byte? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatByte(b.Value, writer);
        }

        private static bool TryFormatNullableSByte(sbyte? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatSByte(b.Value, writer);
        }

        private static bool TryFormatNullableShort(short? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatShort(b.Value, writer);
        }

        private static bool TryFormatNullableUShort(ushort? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatUShort(b.Value, writer);
        }

        private static bool TryFormatNullableInt(int? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatInt(b.Value, writer);
        }

        private static bool TryFormatNullableUInt(uint? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatUInt(b.Value, writer);
        }

        private static bool TryFormatNullableLong(long? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatLong(b.Value, writer);
        }

        private static bool TryFormatNullableULong(ulong? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatULong(b.Value, writer);
        }

        private static bool TryFormatNullableFloat(float? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatFloat(b.Value, writer);
        }

        private static bool TryFormatNullableDouble(double? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatDouble(b.Value, writer);
        }

        private static bool TryFormatNullableDecimal(decimal? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatDecimal(b.Value, writer);
        }

        private static bool TryFormatNullableDateTime(DateTime? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatDateTime(b.Value, writer);
        }

        private static bool TryFormatNullableDateTimeOffset(DateTimeOffset? b, IBufferWriter<char> writer)
        {
            if (b == null) return true;

            return TryFormatDateTimeOffset(b.Value, writer);
        }

        private static bool TryFormatNullableGuid(Guid? g, IBufferWriter<char> writer)
        {
            if (g == null) return true;

            return TryFormatGuid(g.Value, writer);
        }

        private static bool TryFormatNullableTimeSpan(TimeSpan? ts, IBufferWriter<char> writer)
        {
            if (ts == null) return true;

            return TryFormatTimeSpan(ts.Value, writer);
        }
    }
}
