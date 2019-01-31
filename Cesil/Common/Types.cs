using System;
using System.Buffers;
using System.Reflection;

namespace Cesil
{
    internal static class Types
    {
        internal static readonly TypeInfo VoidType = typeof(void).GetTypeInfo();
        internal static readonly TypeInfo BoolType = typeof(bool).GetTypeInfo();
        internal static readonly TypeInfo ObjectType = typeof(object).GetTypeInfo();

        internal static readonly TypeInfo ReadOnlySpanOfCharType = typeof(ReadOnlySpan<char>).GetTypeInfo();
        internal static readonly TypeInfo IBufferWriterOfCharType = typeof(IBufferWriter<char>).GetTypeInfo();

        internal static readonly TypeInfo RowEndingsType = typeof(RowEndings).GetTypeInfo();
        internal static readonly TypeInfo ReadHeadersType = typeof(ReadHeaders).GetTypeInfo();
        internal static readonly TypeInfo WriteHeadersType = typeof(WriteHeaders).GetTypeInfo();
        internal static readonly TypeInfo WriteTrailingNewLinesType = typeof(WriteTrailingNewLines).GetTypeInfo();

        internal static readonly TypeInfo DefaultTypeParsersType = typeof(DefaultTypeParsers).GetTypeInfo();
        internal static readonly TypeInfo DefaultEnumTypeParserType = typeof(DefaultTypeParsers.DefaultEnumTypeParser<>).GetTypeInfo();
        internal static readonly TypeInfo DefaultFlagsEnumTypeParserType = typeof(DefaultTypeParsers.DefaultFlagsEnumTypeParser<>).GetTypeInfo();

        internal static readonly TypeInfo DefaultTypeFormattersType = typeof(DefaultTypeFormatters).GetTypeInfo();
        internal static readonly TypeInfo DefaultEnumTypeFormatterType = typeof(DefaultTypeFormatters.DefaultEnumTypeFormatter<>).GetTypeInfo();
        internal static readonly TypeInfo DefaultFlagsEnumTypeFormatterType = typeof(DefaultTypeFormatters.DefaultFlagsEnumTypeFormatter<>).GetTypeInfo();
    }
}
