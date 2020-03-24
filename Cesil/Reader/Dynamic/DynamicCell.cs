using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Cesil
{
    // todo: can we move the AssertNotDisposedXXXs here to the generated expressions?
    //       will make it harder to double-dip on them

    internal sealed class DynamicCell : IDynamicMetaObjectProvider, IConvertible
    {
        internal readonly uint Generation;
        internal readonly DynamicRow Row;
        internal readonly int ColumnNumber;

        internal ITypeDescriber Converter => Row.Converter;

        public DynamicCell(DynamicRow row, int num)
        {
            Row = row;
            Generation = row.Generation;
            ColumnNumber = num;
        }

        internal ReadOnlySpan<char> GetDataSpan()
        => SafeRowGet().GetDataSpan(ColumnNumber);

        internal ReadContext GetReadContext()
        {
            var r = SafeRowGet();

            var name = r.Columns[ColumnNumber];

            var owner = r.Owner;

            return ReadContext.ReadingColumn(owner.Options, r.RowNumber, name, owner.Context);
        }

        internal Parser? GetParser(TypeInfo forType, out ReadContext ctx)
        {
            var row = Row;
            var owner = row.Owner;
            var index = ColumnNumber;
            var converterInterface = Converter;

            var col = row.Columns[index];

            ctx = ReadContext.ConvertingColumn(owner.Options, row.RowNumber, col, owner.Context);

            var parser = converterInterface.GetDynamicCellParserFor(in ctx, forType);
            return parser;
        }

        public DynamicMetaObject GetMetaObject(Expression exp)
        => new DynamicCellMetaObject(this, exp);

        private DynamicRow SafeRowGet()
        {
            var ret = Row;
            ret.AssertGenerationMatch(Generation);

            return ret;
        }

        public override string ToString()
        => $"{nameof(DynamicCell)} {nameof(Generation)}={Generation}, {nameof(Row)}={Row}, {nameof(ColumnNumber)}={ColumnNumber}";

        // IConvertible implementation

        TypeCode IConvertible.GetTypeCode()
        {
            GetConversionDetails(out var describer, out var data, out var ctx);

            var boolConf = describer.GetDynamicCellParserFor(in ctx, Types.BoolType);
            var byteConf = describer.GetDynamicCellParserFor(in ctx, Types.ByteType);
            var charConf = describer.GetDynamicCellParserFor(in ctx, Types.CharType);
            var dtConf = describer.GetDynamicCellParserFor(in ctx, Types.DateTimeType);
            //var decConf = describer.GetDynamicCellParserFor(in ctx, Types.DecimalType);
            var doubleConf = describer.GetDynamicCellParserFor(in ctx, Types.DoubleType);
            var shortConf = describer.GetDynamicCellParserFor(in ctx, Types.ShortType);
            var intConf = describer.GetDynamicCellParserFor(in ctx, Types.IntType);
            var longConf = describer.GetDynamicCellParserFor(in ctx, Types.LongType);
            var sbyteConf = describer.GetDynamicCellParserFor(in ctx, Types.SByteType);
            //var floatConf = describer.GetDynamicCellParserFor(in ctx, Types.FloatType);
            var stringConf = describer.GetDynamicCellParserFor(in ctx, Types.StringType);
            var ushortConf = describer.GetDynamicCellParserFor(in ctx, Types.UShortType);
            var uintConf = describer.GetDynamicCellParserFor(in ctx, Types.UIntType);
            var ulongConf = describer.GetDynamicCellParserFor(in ctx, Types.ULongType);

            // this is very tricky, because we don't actually have any type information... so we 
            //   want to be as narrow as we can be

            // first do the ones that require very particular sequences of characters
            if (CanConvert<bool>(boolConf, data, in ctx)) return TypeCode.Boolean;
            if (CanConvert<DateTime>(dtConf, data, in ctx)) return TypeCode.DateTime;

            // smallest ints to largest its, but take the less weird option if both work
            if (CanConvert<byte>(byteConf, data, in ctx)) return TypeCode.Byte;
            if (CanConvert<sbyte>(sbyteConf, data, in ctx)) return TypeCode.SByte;

            if (CanConvert<short>(shortConf, data, in ctx)) return TypeCode.Int16;
            if (CanConvert<ushort>(ushortConf, data, in ctx)) return TypeCode.UInt16;

            if (CanConvert<int>(intConf, data, in ctx)) return TypeCode.Int32;
            if (CanConvert<uint>(uintConf, data, in ctx)) return TypeCode.UInt32;

            if (CanConvert<long>(longConf, data, in ctx)) return TypeCode.Int64;
            if (CanConvert<ulong>(ulongConf, data, in ctx)) return TypeCode.UInt64;

            // double vs float vs decimal is just kind of crazy... so just check double
            if (CanConvert<double>(doubleConf, data, in ctx)) return TypeCode.Double;

            // char before checking string
            if (CanConvert<char>(charConf, data, in ctx)) return TypeCode.Char;

            // string is the last one
            if (CanConvert<string>(stringConf, data, in ctx)) return TypeCode.String;

            return TypeCode.Object;

            // returns true if we can 
            static bool CanConvert<T>(Parser? p, ReadOnlySpan<char> data, in ReadContext ctx)
            {
                if (p == null) return false;

                // we can't cache these because converter may not be the default type converter
                //   thus we can't use any of the ICreatesCacheableDelegate infrastructure
                //   that we use in other dynamic dispatch
                var parserDel = MakeFromParser<T>(p);

                return parserDel(data, in ctx, out _);
            }
        }

        private static ParserDelegate<T> MakeFromParser<T>(Parser p)
        {
            var outType = typeof(T).MakeByRefType().GetTypeInfo();

            var spanVar = Expressions.Parameter_ReadOnlySpanOfChar;
            var ctxVar = Expressions.Parameter_ReadContext_ByRef;
            var outVar = Expression.Parameter(outType);

            var delBody = p.MakeExpression(spanVar, ctxVar, outVar);

            var del = Expression.Lambda<ParserDelegate<T>>(delBody, spanVar, ctxVar, outVar);

            var parserDel = del.Compile();

            return parserDel;
        }

        private void GetConversionDetails(out ITypeDescriber describer, out ReadOnlySpan<char> data, out ReadContext ctx)
        {
            var self = this;

            var row = self.Row;
            row.AssertGenerationMatch(Generation);


            describer = row.Converter;
            var owner = row.Owner;
            var col = row.Columns[ColumnNumber];

            ctx = ReadContext.ConvertingColumn(owner.Options, row.RowNumber, col, owner.Context);
            data = self.GetDataSpan();
        }

        private T ToTypeImpl<T>(IFormatProvider? provider, TypeInfo? toType = null)
        {
            toType = toType ?? typeof(T).GetTypeInfo();

            if (provider != null)
            {
                return Throw.ArgumentException<T>($"Conversions via {nameof(IConvertible)} cannot specify an {nameof(IFormatProvider)}, more fine grained control of conversions should be done via explicit casts and {nameof(ITypeDescriber)}.{nameof(ITypeDescriber.GetDynamicCellParserFor)}.", nameof(provider));
            }

            GetConversionDetails(out var describer, out var data, out var ctx);

            var conf = describer.GetDynamicCellParserFor(in ctx, toType);
            if (conf == null)
            {
                return Throw.InvalidOperationException<T>($"{nameof(Parser)} returned from {nameof(ITypeDescriber.GetDynamicCellParserFor)} for {toType} was null, cannot convert");
            }

            var del = MakeFromParser<T>(conf);
            if (!del(data, in ctx, out var ret))
            {
                return Throw.InvalidOperationException<T>($"{nameof(Parser)} for {toType} returned false, cannot convert");
            }

            return ret;
        }

        bool IConvertible.ToBoolean(IFormatProvider? provider)
        => ToTypeImpl<bool>(provider);

        byte IConvertible.ToByte(IFormatProvider? provider)
        => ToTypeImpl<byte>(provider);

        char IConvertible.ToChar(IFormatProvider? provider)
        => ToTypeImpl<char>(provider);

        DateTime IConvertible.ToDateTime(IFormatProvider? provider)
        => ToTypeImpl<DateTime>(provider);

        decimal IConvertible.ToDecimal(IFormatProvider? provider)
        => ToTypeImpl<decimal>(provider);

        double IConvertible.ToDouble(IFormatProvider? provider)
        => ToTypeImpl<double>(provider);

        short IConvertible.ToInt16(IFormatProvider? provider)
        => ToTypeImpl<short>(provider);

        int IConvertible.ToInt32(IFormatProvider? provider)
        => ToTypeImpl<int>(provider);

        long IConvertible.ToInt64(IFormatProvider? provider)
        => ToTypeImpl<long>(provider);

        sbyte IConvertible.ToSByte(IFormatProvider? provider)
        => ToTypeImpl<sbyte>(provider);

        float IConvertible.ToSingle(IFormatProvider? provider)
        => ToTypeImpl<float>(provider);

        string IConvertible.ToString(IFormatProvider? provider)
        => ToTypeImpl<string>(provider);

        ushort IConvertible.ToUInt16(IFormatProvider? provider)
        => ToTypeImpl<ushort>(provider);

        uint IConvertible.ToUInt32(IFormatProvider? provider)
        => ToTypeImpl<uint>(provider);

        ulong IConvertible.ToUInt64(IFormatProvider? provider)
        => ToTypeImpl<ulong>(provider);

        object IConvertible.ToType(Type conversionType, IFormatProvider? provider)
        {
            Utils.CheckArgumentNull(conversionType, nameof(conversionType));

            return ToTypeImpl<object>(provider, conversionType.GetTypeInfo());
        }
    }
}
