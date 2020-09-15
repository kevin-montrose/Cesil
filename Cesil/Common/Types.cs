using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;

namespace Cesil
{
    internal static class Types
    {
        // built-in primitives
        internal static readonly TypeInfo Char = typeof(char).GetTypeInfo();
        internal static readonly TypeInfo Bool = typeof(bool).GetTypeInfo();
        internal static readonly TypeInfo Byte = typeof(byte).GetTypeInfo();
        internal static readonly TypeInfo SByte = typeof(sbyte).GetTypeInfo();
        internal static readonly TypeInfo Short = typeof(short).GetTypeInfo();
        internal static readonly TypeInfo UShort = typeof(ushort).GetTypeInfo();
        internal static readonly TypeInfo Int = typeof(int).GetTypeInfo();
        internal static readonly TypeInfo UInt = typeof(uint).GetTypeInfo();
        internal static readonly TypeInfo Long = typeof(long).GetTypeInfo();
        internal static readonly TypeInfo ULong = typeof(ulong).GetTypeInfo();
        internal static readonly TypeInfo Float = typeof(float).GetTypeInfo();
        internal static readonly TypeInfo Double = typeof(double).GetTypeInfo();
        internal static readonly TypeInfo Decimal = typeof(decimal).GetTypeInfo();

        // System structs
        internal static readonly TypeInfo Void = typeof(void).GetTypeInfo();
        internal static readonly TypeInfo DateTime = typeof(DateTime).GetTypeInfo();
        internal static readonly TypeInfo DateTimeOffset = typeof(DateTimeOffset).GetTypeInfo();
        internal static readonly TypeInfo TimeSpan = typeof(TimeSpan).GetTypeInfo();
        internal static readonly TypeInfo Index = typeof(Index).GetTypeInfo();
        internal static readonly TypeInfo Range = typeof(Range).GetTypeInfo();
        internal static readonly TypeInfo Guid = typeof(Guid).GetTypeInfo();
        internal static readonly TypeInfo Nullable = typeof(Nullable<>).GetTypeInfo();
        internal static readonly TypeInfo[] ValueTuple_Array =
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
        internal static readonly TypeInfo ReadOnlySpanOfChar = typeof(ReadOnlySpan<char>).GetTypeInfo();
        internal static readonly TypeInfo NullableInt = typeof(int?).GetTypeInfo();

        // System classes
        internal static readonly TypeInfo Object = typeof(object).GetTypeInfo();
        internal static readonly TypeInfo String = typeof(string).GetTypeInfo();
        internal static readonly TypeInfo Uri = typeof(Uri).GetTypeInfo();
        internal static readonly TypeInfo Version = typeof(Version).GetTypeInfo();
        internal static readonly TypeInfo[] Tuple_Array =
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

        // Arrays of system types
        internal static readonly TypeInfo ByteArray = typeof(byte[]).GetTypeInfo();

        // System interfaces
        internal static readonly TypeInfo IDisposable = typeof(IDisposable).GetTypeInfo();
        internal static readonly TypeInfo IConvertible = typeof(IConvertible).GetTypeInfo();

        // System.Collections.Generic interfaces
        internal static readonly TypeInfo IReadOnlyList = typeof(IReadOnlyList<>).GetTypeInfo();
        internal static readonly TypeInfo IEnumerableOfT = typeof(IEnumerable<>).GetTypeInfo();
        internal static readonly TypeInfo IEquatable = typeof(IEquatable<>).GetTypeInfo();

        // System.Collections interfaces
        internal static readonly TypeInfo IEnumerable = typeof(System.Collections.IEnumerable).GetTypeInfo();

        // System.Buffers interfaces
        internal static readonly TypeInfo IBufferWriterOfChar = typeof(IBufferWriter<char>).GetTypeInfo();

        // System types that we consider "Well Known"
        internal static readonly TypeInfo[] WellKnownTypes_Array =
            new[]
            {
                typeof(bool).GetTypeInfo(),
                typeof(char).GetTypeInfo(),
                typeof(byte).GetTypeInfo(),
                typeof(sbyte).GetTypeInfo(),
                typeof(short).GetTypeInfo(),
                typeof(ushort).GetTypeInfo(),
                typeof(int).GetTypeInfo(),
                typeof(uint).GetTypeInfo(),
                typeof(long).GetTypeInfo(),
                typeof(ulong).GetTypeInfo(),
                typeof(float).GetTypeInfo(),
                typeof(double).GetTypeInfo(),
                typeof(decimal).GetTypeInfo(),
                typeof(DateTime).GetTypeInfo(),
                typeof(DateTimeOffset).GetTypeInfo(),
                typeof(TimeSpan).GetTypeInfo(),
                typeof(string).GetTypeInfo(),
                typeof(Uri).GetTypeInfo(),
                typeof(Version).GetTypeInfo(),
                typeof(Guid).GetTypeInfo(),
                typeof(Index).GetTypeInfo(),
                typeof(Range).GetTypeInfo()
            };

        // Cesil delegates
        internal static readonly TypeInfo ParserDelegate = typeof(ParserDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo SetterDelegate = typeof(SetterDelegate<,>).GetTypeInfo();
        internal static readonly TypeInfo SetterByRefDelegate = typeof(SetterByRefDelegate<,>).GetTypeInfo();
        internal static readonly TypeInfo StaticSetterDelegate = typeof(StaticSetterDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo ResetDelegate = typeof(ResetDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo ResetByRefDelegate = typeof(ResetByRefDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo StaticResetDelegate = typeof(StaticResetDelegate).GetTypeInfo();
        internal static readonly TypeInfo GetterDelegate = typeof(GetterDelegate<,>).GetTypeInfo();
        internal static readonly TypeInfo StaticGetterDelegate = typeof(StaticGetterDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo FormatterDelegate = typeof(FormatterDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo ShouldSerializeDelegate = typeof(ShouldSerializeDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo StaticShouldSerializeDelegate = typeof(StaticShouldSerializeDelegate).GetTypeInfo();
        internal static readonly TypeInfo DynamicRowConverterDelegate = typeof(DynamicRowConverterDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo ParseAndSetOnDelegate = typeof(ParseAndSetOnDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo MoveFromHoldToRowDelegate = typeof(MoveFromHoldToRowDelegate<,>).GetTypeInfo();
        internal static readonly TypeInfo GetInstanceGivenHoldDelegate = typeof(GetInstanceGivenHoldDelegate<,>).GetTypeInfo();
        internal static readonly TypeInfo ClearHoldDelegate = typeof(ClearHoldDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo InstanceProviderDelegate = typeof(InstanceProviderDelegate<>).GetTypeInfo();
        internal static readonly TypeInfo NeedsHoldRowConstructor = typeof(NeedsHoldRowConstructor<,>).GetTypeInfo();

        // Cesil enums
        internal static readonly TypeInfo RowEnding = typeof(RowEnding).GetTypeInfo();
        internal static readonly TypeInfo ReadHeader = typeof(ReadHeader).GetTypeInfo();
        internal static readonly TypeInfo WriteHeader = typeof(WriteHeader).GetTypeInfo();
        internal static readonly TypeInfo WriteTrailingRowEnding = typeof(WriteTrailingRowEnding).GetTypeInfo();
        internal static readonly TypeInfo DynamicRowDisposal = typeof(DynamicRowDisposal).GetTypeInfo();
        internal static readonly TypeInfo ManualTypeDescriberFallbackBehavior = typeof(ManualTypeDescriberFallbackBehavior).GetTypeInfo();
        internal static readonly TypeInfo ExtraColumnTreatment = typeof(ExtraColumnTreatment).GetTypeInfo();
        internal static readonly TypeInfo SurrogateTypeDescriberFallbackBehavior = typeof(SurrogateTypeDescriberFallbackBehavior).GetTypeInfo();

        // Cesil structs
        internal static readonly TypeInfo ColumnIdentifier = typeof(ColumnIdentifier).GetTypeInfo();
        internal static readonly TypeInfo NonNull = typeof(NonNull<>).GetTypeInfo();
        internal static readonly TypeInfo ReadContext = typeof(ReadContext).GetTypeInfo();
        internal static readonly TypeInfo WriteContext = typeof(WriteContext).GetTypeInfo();

        // Cesil static classes
        internal static readonly TypeInfo DefaultTypeInstanceProviders = typeof(DefaultTypeInstanceProviders).GetTypeInfo();
        internal static readonly TypeInfo DefaultTypeParsers = typeof(DefaultTypeParsers).GetTypeInfo();
        internal static readonly TypeInfo DefaultEnumTypeParser = typeof(DefaultTypeParsers.DefaultEnumTypeParser<>).GetTypeInfo();
        internal static readonly TypeInfo DefaultTypeFormatters = typeof(DefaultTypeFormatters).GetTypeInfo();
        internal static readonly TypeInfo DefaultEnumTypeFormatter = typeof(DefaultTypeFormatters.DefaultEnumTypeFormatter<>).GetTypeInfo();
        internal static readonly TypeInfo DisposableHelper = typeof(DisposableHelper).GetTypeInfo();
        internal static readonly TypeInfo Throw = typeof(Throw).GetTypeInfo();
        internal static readonly TypeInfo TupleDynamicParsers = typeof(TupleDynamicParsers<>).GetTypeInfo();
        internal static readonly TypeInfo Utils = typeof(Utils).GetTypeInfo();
        internal static readonly TypeInfo WellKnownRowTypes = typeof(WellKnownRowTypes).GetTypeInfo();
        internal static readonly TypeInfo WellKnownEnumRowType = typeof(WellKnownRowTypes.WellKnownEnumRowType<>).GetTypeInfo();

        // Cesil interfaces
        internal static readonly TypeInfo IDynamicRowOwner = typeof(IDynamicRowOwner).GetTypeInfo();
        internal static readonly TypeInfo ITypeDescriber = typeof(ITypeDescriber).GetTypeInfo();
        internal static readonly TypeInfo ITestableDisposable = typeof(ITestableDisposable).GetTypeInfo();


        // Cesil classes
        internal static readonly TypeInfo DynamicCell = typeof(DynamicCell).GetTypeInfo();
        internal static readonly TypeInfo DynamicRow = typeof(DynamicRow).GetTypeInfo();
        internal static readonly TypeInfo DynamicRowEnumerable = typeof(DynamicRowEnumerable<>).GetTypeInfo();
        internal static readonly TypeInfo DynamicRowEnumerableNonGeneric = typeof(DynamicRowEnumerableNonGeneric).GetTypeInfo();
        internal static readonly TypeInfo DynamicRowRange = typeof(DynamicRowRange).GetTypeInfo();
        internal static readonly TypeInfo PassthroughRowEnumerable = typeof(PassthroughRowEnumerable).GetTypeInfo();
        internal static readonly TypeInfo DefaultTypeDescriber = typeof(DefaultTypeDescriber).GetTypeInfo();
        
        // constructor params
        internal static readonly TypeInfo[] ParserConstructorOneParameter_Array = new[] { typeof(ReadOnlySpan<char>).GetTypeInfo() };
        internal static readonly TypeInfo[] ParserConstructorTwoParameter_Array = new[] { typeof(ReadOnlySpan<char>).GetTypeInfo(), typeof(ReadContext).MakeByRefType().GetTypeInfo() };

        internal static class ReaderStateMachine
        {
            internal static readonly TypeInfo State = typeof(Cesil.ReaderStateMachine.State).GetTypeInfo();
            internal static readonly TypeInfo CharacterType = typeof(Cesil.ReaderStateMachine.CharacterType).GetTypeInfo();
        }
    }
}
