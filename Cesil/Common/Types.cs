﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    internal static class Types
    {
        internal delegate bool TypeParserDelegate<T>(ReadOnlySpan<char> text, out T val);
        internal delegate bool InstanceBuildThunkDelegate<T>(object thunk, out T val);
        
        internal static readonly TypeInfo TypeParserDelegateType = typeof(TypeParserDelegate<>).GetTypeInfo();

        internal static readonly TypeInfo VoidType = typeof(void).GetTypeInfo();
        internal static readonly TypeInfo BoolType = typeof(bool).GetTypeInfo();
        internal static readonly TypeInfo IntType = typeof(int).GetTypeInfo();
        internal static readonly TypeInfo ObjectType = typeof(object).GetTypeInfo();
        internal static readonly TypeInfo StringType = typeof(string).GetTypeInfo();

        internal static readonly TypeInfo[] TupleTypes =
            new TypeInfo[]
            {
                typeof(Tuple<>).GetTypeInfo(),
                typeof(Tuple<,>).GetTypeInfo(),
                typeof(Tuple<,,>).GetTypeInfo(),
                typeof(Tuple<,,,>).GetTypeInfo(),
                typeof(Tuple<,,,,>).GetTypeInfo(),
                typeof(Tuple<,,,,,>).GetTypeInfo(),
                typeof(Tuple<,,,,,,>).GetTypeInfo(),
                typeof(Tuple<,,,,,,,>).GetTypeInfo()
            };

        internal static readonly TypeInfo[] ValueTupleTypes =
           new TypeInfo[]
           {
                typeof(ValueTuple<>).GetTypeInfo(),
                typeof(ValueTuple<,>).GetTypeInfo(),
                typeof(ValueTuple<,,>).GetTypeInfo(),
                typeof(ValueTuple<,,,>).GetTypeInfo(),
                typeof(ValueTuple<,,,,>).GetTypeInfo(),
                typeof(ValueTuple<,,,,,>).GetTypeInfo(),
                typeof(ValueTuple<,,,,,,>).GetTypeInfo(),
                typeof(ValueTuple<,,,,,,,>).GetTypeInfo()
           };

        internal static readonly TypeInfo ReadOnlySpanOfCharType = typeof(ReadOnlySpan<char>).GetTypeInfo();
        internal static readonly TypeInfo IBufferWriterOfCharType = typeof(IBufferWriter<char>).GetTypeInfo();
        internal static readonly TypeInfo IEnumerableType = typeof(System.Collections.IEnumerable).GetTypeInfo();
        internal static readonly TypeInfo IEnumerableOfTType = typeof(IEnumerable<>).GetTypeInfo();
        internal static readonly TypeInfo IEquatableType = typeof(IEquatable<>).GetTypeInfo();

        internal static readonly TypeInfo[] ReadOnlySpanOfCharType_Array = new[] { typeof(ReadOnlySpan<char>).GetTypeInfo() };

        internal static readonly TypeInfo RowEndingsType = typeof(RowEndings).GetTypeInfo();
        internal static readonly TypeInfo ReadHeadersType = typeof(ReadHeaders).GetTypeInfo();
        internal static readonly TypeInfo WriteHeadersType = typeof(WriteHeaders).GetTypeInfo();
        internal static readonly TypeInfo WriteTrailingNewLinesType = typeof(WriteTrailingNewLines).GetTypeInfo();
        internal static readonly TypeInfo ReadContextType = typeof(ReadContext).GetTypeInfo();
        internal static readonly TypeInfo WriteContext = typeof(WriteContext).GetTypeInfo();
        internal static readonly TypeInfo DynamicRowDisposalType = typeof(DynamicRowDisposal).GetTypeInfo();
        internal static readonly TypeInfo InstanceBuilderDelegateType = typeof(InstanceBuilderDelegate<>).GetTypeInfo();

        internal static readonly TypeInfo DefaultTypeParsersType = typeof(DefaultTypeParsers).GetTypeInfo();
        internal static readonly TypeInfo DefaultEnumTypeParserType = typeof(DefaultTypeParsers.DefaultEnumTypeParser<>).GetTypeInfo();
        internal static readonly TypeInfo DefaultFlagsEnumTypeParserType = typeof(DefaultTypeParsers.DefaultFlagsEnumTypeParser<>).GetTypeInfo();

        internal static readonly TypeInfo DefaultTypeFormattersType = typeof(DefaultTypeFormatters).GetTypeInfo();
        internal static readonly TypeInfo DefaultEnumTypeFormatterType = typeof(DefaultTypeFormatters.DefaultEnumTypeFormatter<>).GetTypeInfo();
        internal static readonly TypeInfo DefaultFlagsEnumTypeFormatterType = typeof(DefaultTypeFormatters.DefaultFlagsEnumTypeFormatter<>).GetTypeInfo();

        internal static readonly TypeInfo DynamicCell = typeof(DynamicCell).GetTypeInfo();
        internal static readonly TypeInfo DynamicRow = typeof(DynamicRow).GetTypeInfo();

        internal static readonly TypeInfo DynamicRowEnumerableType = typeof(DynamicRowEnumerable<>).GetTypeInfo();
        internal static readonly TypeInfo DynamicRowEnumerableNonGenericType = typeof(DynamicRowEnumerableNonGeneric).GetTypeInfo();

        internal static readonly TypeInfo Throw = typeof(Throw).GetTypeInfo();

        internal static readonly TypeInfo TupleDynamicParser = typeof(TupleDynamicParsers<>).GetTypeInfo();
    }
}
