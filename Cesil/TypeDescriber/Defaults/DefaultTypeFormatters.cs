// be aware, this file is also included in Cesil.SourceGenerator
//   so it is _very_ particular in strucutre
//
// don't edit it all willy-nilly
using System;
using System.Buffers;
using System.Reflection;

using static Cesil.BindingFlagsConstants;

// todo: analyzer to enforce all the conventions needed for Cesil.SourceGenerator to work

namespace Cesil
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("", "IDE0051", Justification = "Used via reflection")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("", "IDE0060", Justification = "Unused paramters are required")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("", "IDE0002", Justification = "Pattern is important for source generation")]
    internal static class DefaultTypeFormatters
    {
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
                for (var i = 0; i < names.Length; i++)
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

            private static bool TryFormatBasicEnum(T value, in WriteContext ctx, IBufferWriter<char> writer)
            {
                if (!Enum.IsDefined(typeof(T), value)) return false;

                // this shouldn't allocate
                var valStr = value.ToString();

                var charSpan = writer.GetSpan(valStr.Length);
                if (charSpan.Length < valStr.Length) return false;

                valStr.AsSpan().CopyTo(charSpan);
                writer.Advance(valStr.Length);

                return true;
            }

            private static bool TryFormatNullableBasicEnum(T? value, in WriteContext ctx, IBufferWriter<char> writer)
            {
                if (value == null) return true;

                return DefaultEnumTypeFormatter<T>.TryFormatBasicEnum(value.Value, ctx, writer);
            }

            private static bool TryFormatFlagsEnum(T value, in WriteContext ctx, IBufferWriter<char> writer)
            {
                // assuming that most of the time only a single flag is set, so picking the biggest name is
                //   a solid guess
                var charSpan = writer.GetSpan(MaxNameLength);

                var formatRes = FormatFlagsEnumImpl(value, Names, Values, charSpan);

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

                    var res = FormatFlagsEnumImpl(value, Names, Values, charSpan);
                    if (res <= 0)
                    {
                        // couldn't fit, something is wildly wrong so fail
                        return false;
                    }

                    writer.Advance(neededLen);
                    return true;
                }
            }

            private static bool TryFormatNullableFlagsEnum(T? value, in WriteContext ctx, IBufferWriter<char> writer)
            {
                if (value == null) return true;

                return DefaultEnumTypeFormatter<T>.TryFormatFlagsEnum(value.Value, ctx, writer);
            }

            /// <summary>
            /// This is _like_ calling ToString(), but it doesn't allow values
            /// that aren't actually declared on the enum.
            /// 
            /// Since copyInto might not be big enough, can return the following values:
            ///  * 0, if flagsEnum was invalid
            ///  * greater than 0, length of the value that did fit into copyInto
            ///  * less than 0, the negated length of a value that didn't fit into copyInto
            /// </summary>
            internal static int FormatFlagsEnumImpl(
                T flagsEnum,
                string[] names,
                ulong[] values,
                Span<char> copyInto
            )
            {
                const int MALFORMED_VALUE = 0;

                // based on: https://referencesource.microsoft.com/#mscorlib/system/enum.cs,154

                const string ENUM_SEPERATOR = ", ";

                ulong result = Utils.EnumToULong(flagsEnum);

                if (result == 0)
                {
                    if (values.Length > 0 && values[0] == 0)
                    {
                        // it's 0 and 0 is a value
                        var zeroValue = names[0].AsSpan();
                        if (copyInto.Length < zeroValue.Length)
                        {
                            return -zeroValue.Length;
                        }

                        zeroValue.CopyTo(copyInto);
                        copyInto = copyInto[..zeroValue.Length];

                        return zeroValue.Length;
                    }
                    else
                    {
                        // it's 0 and 0 _isn't_ a value
                        return MALFORMED_VALUE;
                    }
                }

                var index = values.Length - 1;

                var len = 0;
                var firstTime = true;
                var saveResult = result;

                while (index >= 0)
                {
                    if ((index == 0) && (values[index] == 0))
                    {
                        break;
                    }

                    if ((result & values[index]) == values[index])
                    {
                        result -= values[index];
                        if (!firstTime)
                        {
                            Insert(copyInto, ref len, ENUM_SEPERATOR);
                        }

                        Insert(copyInto, ref len, names[index]);
                        firstTime = false;
                    }

                    index--;
                }

                // couldn't represent the value, so we fail
                if (result != 0)
                {
                    return MALFORMED_VALUE;
                }

                // couldn't fit, so ask for more space
                if (len > copyInto.Length)
                {
                    return -len;
                }

                // were able to represent the value
                copyInto[^len..].CopyTo(copyInto);  // move everything to the _front_, 'cause we'll need an Advance() call
                return len;

                // logicaclly, insert a string at the front of the "value"
                //
                // but, we actually append things from the _end_, so we don't have to
                //    copy anything around
                static void Insert(Span<char> span, ref int len, string value)
                {
                    var newLen = len + value.Length;
                    if (newLen > span.Length)
                    {
                        len = newLen;
                        return;
                    }

                    value.AsSpan().CopyTo(span[^newLen..]);

                    len = newLen;
                }
            }
        }

        private static bool TryFormatString(string? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            var charSpan = writer.GetSpan(value.Length);
            if (charSpan.Length < value.Length) return false;

            value.AsSpan().CopyTo(charSpan);

            writer.Advance(value.Length);
            return true;
        }

        private static bool TryFormatVersion(Version? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            const int MAX_CHARS =
                10 +    // major
                1 +     // dot
                10 +    // minor
                1 +     // dot
                10 +    // build
                1 +     // dot
                10;     // revision

            if (value == null)
            {
                return true;
            }

            var charSpan = writer.GetSpan(MAX_CHARS);
            var ret = value.TryFormat(charSpan, out var chars);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatUri(Uri? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null)
            {
                return true;
            }

            return DefaultTypeFormatters.TryFormatString(value.ToString(), ctx, writer);
        }

        // non-nullable

        private static bool TryFormatNInt(nint value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            // nint is _actually_ an IntPtr
            switch (IntPtr.Size)
            {
                // IntPtr is 32-bits
                case 4:
                    return DefaultTypeFormatters.TryFormatInt((int)value, ctx, writer);
                // IntPtr is 64-bits
                case 8:
                    return DefaultTypeFormatters.TryFormatLong((long)value, ctx, writer);
                default:
                    return false;
            }
        }

        private static bool TryFormatNUInt(nuint value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            // nuint is _actually_ a UIntPtr
            switch (UIntPtr.Size)
            {
                // UIntPtr is 32-bits
                case 4:
                    return DefaultTypeFormatters.TryFormatUInt((uint)value, ctx, writer);
                // UIntPtr is 64-bits
                case 8:
                    return DefaultTypeFormatters.TryFormatULong((ulong)value, ctx, writer);
                default:
                    return false;
            }
        }

        private static bool TryFormatBool(bool value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value)
            {
                var charSpan = writer.GetSpan(4);

                if (charSpan.Length < 4) return false;

                Boolean.TrueString.AsSpan().CopyTo(charSpan);
                writer.Advance(4);
            }
            else
            {
                var charSpan = writer.GetSpan(5);

                if (charSpan.Length < 5) return false;

                Boolean.FalseString.AsSpan().CopyTo(charSpan);
                writer.Advance(5);
            }

            return true;
        }

        private static bool TryFormatChar(char value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(1);
            if (charSpan.Length < 1) return false;

            charSpan[0] = value;
            writer.Advance(1);

            return true;
        }

        private static bool TryFormatByte(byte value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(3);

            var ret = value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatSByte(sbyte value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(4);

            var ret = value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatShort(short value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(6);

            var ret = value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatUShort(ushort value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(5);

            var ret = value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatInt(int value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(11);

            var ret = value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatUInt(uint value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(10);

            var ret = value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatLong(long value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(20);

            var ret = value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatULong(ulong value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            var charSpan = writer.GetSpan(20);

            var ret = value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatFloat(float value, in WriteContext ctx, IBufferWriter<char> writer)
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

            var ret = value.TryFormat(charSpan, out var chars, "G9", provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatDouble(double value, in WriteContext ctx, IBufferWriter<char> writer)
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

            var ret = value.TryFormat(charSpan, out var chars, "G17", provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatDecimal(decimal value, in WriteContext ctx, IBufferWriter<char> writer)
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

            var ret = value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatDateTime(DateTime value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            // yyyy-MM-dd HH:mm:ssZ
            const int MAX_CHARS = 20;

            // formatting will NOT convert (even though it'll write a Z)
            //   though DateTimeOffset will
            if (value.Kind != DateTimeKind.Utc)
            {
                value = value.ToUniversalTime();
            }

            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = value.TryFormat(charSpan, out var chars, "u", provider: System.Globalization.CultureInfo.InstalledUICulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatDateTimeOffset(DateTimeOffset value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            // yyyy-MM-dd HH:mm:ssZ
            const int MAX_CHARS = 20;

            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = value.TryFormat(charSpan, out var chars, "u", formatProvider: System.Globalization.CultureInfo.InstalledUICulture);
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatGuid(Guid value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            // 32 digits + 4 dashes
            const int MAX_CHARS = 36;

            var charSpan = writer.GetSpan(MAX_CHARS);

            var ret = value.TryFormat(charSpan, out var chars, "D");
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatTimeSpan(TimeSpan value, in WriteContext ctx, IBufferWriter<char> writer)
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

            var ret = value.TryFormat(charSpan, out var chars, "c");
            if (!ret) return false;

            writer.Advance(chars);
            return true;
        }

        private static bool TryFormatIndex(Index value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            const int MAX_CHARS =
                1 + // ^
                11; // int max length

            var charSpan = writer.GetSpan(MAX_CHARS);

            var written = 0;

            if (value.IsFromEnd)
            {
                if (charSpan.Length == 0) return false;

                charSpan[0] = '^';
                charSpan = charSpan.Slice(1);
                written++;
            }

            var ret = value.Value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            written += chars;

            writer.Advance(written);
            return true;
        }

        private static bool TryFormatRange(Range value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            const int MAX_CHARS =
                   1 +  // ^
                   11 + // int max length
                   2 +  // ..
                   1 +  // ^
                   11;  // int max length

            var charSpan = writer.GetSpan(MAX_CHARS);

            var written = 0;

            var start = value.Start;

            if (start.IsFromEnd)
            {
                if (charSpan.Length == 0) return false;

                charSpan[0] = '^';
                charSpan = charSpan.Slice(1);
                written++;
            }

            var ret = start.Value.TryFormat(charSpan, out var chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            written += chars;
            charSpan = charSpan.Slice(chars);

            if (charSpan.Length < 2) return false;

            charSpan[0] = '.';
            charSpan[1] = '.';

            charSpan = charSpan.Slice(2);

            written += 2;

            var end = value.End;

            if (end.IsFromEnd)
            {
                if (charSpan.Length == 0) return false;

                charSpan[0] = '^';
                charSpan = charSpan.Slice(1);
                written++;
            }

            ret = end.Value.TryFormat(charSpan, out chars, provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!ret) return false;

            written += chars;

            writer.Advance(written);
            return true;
        }

        // nullable

        private static bool TryFormatNullableNInt(nint? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatNInt(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableNUInt(nuint? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatNUInt(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableChar(char? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatChar(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableBool(bool? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatBool(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableByte(byte? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatByte(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableSByte(sbyte? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatSByte(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableShort(short? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatShort(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableUShort(ushort? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatUShort(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableInt(int? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatInt(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableUInt(uint? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatUInt(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableLong(long? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatLong(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableULong(ulong? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatULong(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableFloat(float? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatFloat(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableDouble(double? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatDouble(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableDecimal(decimal? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatDecimal(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableDateTime(DateTime? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatDateTime(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableDateTimeOffset(DateTimeOffset? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatDateTimeOffset(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableGuid(Guid? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatGuid(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableTimeSpan(TimeSpan? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatTimeSpan(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableIndex(Index? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatIndex(value.Value, ctx, writer);
        }

        private static bool TryFormatNullableRange(Range? value, in WriteContext ctx, IBufferWriter<char> writer)
        {
            if (value == null) return true;

            return DefaultTypeFormatters.TryFormatRange(value.Value, ctx, writer);
        }
    }
}
