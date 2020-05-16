using System;
using System.Reflection;

namespace Cesil
{
    internal static class TupleDynamicParsers<T>
    {
        private static readonly TypeInfo[] ArgTypes = GetTupleArgTypes(typeof(T).GetTypeInfo());

        internal static bool TryConvertTuple(object row, in ReadContext _, out T res)
        {
            var rowDyn = (DynamicRow)row;

            var untyped = GetTuple(rowDyn, ArgTypes);
            res = (T)untyped;

            return true;
        }

        internal static bool TryConvertValueTuple(object row, in ReadContext _, out T res)
        {
            var rowDyn = (DynamicRow)row;

            var untyped = GetValueTuple(rowDyn, ArgTypes);
            res = (T)untyped;

            return true;
        }

        private static object GetTuple(DynamicRow row, TypeInfo[] colTypes)
        {
            var data = MakeArrayOfObjects(row, colTypes);

            return ConvertToTuple(colTypes, data, 0, Types.Tuple_Array);
        }

        private static object GetValueTuple(DynamicRow row, TypeInfo[] colTypes)
        {
            var data = MakeArrayOfObjects(row, colTypes);

            return ConvertToTuple(colTypes, data, 0, Types.ValueTuple_Array);
        }

        private static object?[] MakeArrayOfObjects(DynamicRow row, TypeInfo[] colTypes)
        {
            var ret = new object?[colTypes.Length];

            var i = 0;
            foreach (var col in row.Columns)
            {
                if (!row.IsSet(i))
                {
                    goto end;
                }

                var cell = row.GetCellAt(i);
                if (cell == null)
                {
                    return Throw.InvalidOperationException<object[]>("Unexpected null value in dynamic row cell");
                }

                var colType = colTypes[i];

                var parser = cell.GetParser(colType, out var ctx);
                if (parser == null)
                {
                    return Throw.InvalidOperationException<object[]>($"No parser found to convert cell at index={i} to {colType}");
                }

                var delProvider = (ICreatesCacheableDelegate<Parser.DynamicParserDelegate>)parser;

                var del = delProvider.Guarantee(DefaultTypeDescriberCache.Instance);

                var data = cell.GetDataSpan();
                if (!del(data, ctx, out var res))
                {
                    return Throw.InvalidOperationException<object[]>($"{nameof(Parser)} {parser} returned false");
                }

                ret[i] = res;

end:
                i++;
            }

            return ret;
        }

        private static TypeInfo[] GetTupleArgTypes(TypeInfo tupleType)
        {
            const int REST_INDEX = 7;

            var cur = tupleType;
            Type[]? initalArgs = cur.GetGenericArguments();

            var ret = new TypeInfo[initalArgs.Length];
            var nextIx = 0;

mapTypes:
            var args = initalArgs ?? cur.GetGenericArguments();
            initalArgs = null;
            for (var i = 0; i < args.Length && i < REST_INDEX; i++)
            {
                var arg = args[i];

                if (nextIx == ret.Length)
                {
                    Array.Resize(ref ret, ret.Length * 2);
                }

                ret[nextIx] = arg.GetTypeInfo();
                nextIx++;
            }

            if (args.Length == REST_INDEX + 1)
            {
                cur = args[REST_INDEX].GetTypeInfo();
                goto mapTypes;
            }

            if (ret.Length != nextIx)
            {
                Array.Resize(ref ret, nextIx);
            }

            return ret;
        }

        private static object ConvertToTuple(TypeInfo[] types, object?[] data, int from, TypeInfo[] tupleTypes)
        {
            var toTake = data.Length - from;
            var allocSize = toTake;
            if (toTake > 7)
            {
                toTake = 7;
                allocSize = 8;
            }

            var ps = new object[allocSize];
            Array.Copy(data, from, ps, 0, toTake);

            var ts = new TypeInfo[allocSize];
            for (var i = 0; i < toTake; i++)
            {
                ts[i] = types[from + i];
            }

            if (toTake != allocSize)
            {
                var rest = ConvertToTuple(types, data, from + 7, tupleTypes);
                ps[^1] = rest;
                ts[^1] = rest.GetType().GetTypeInfo();
            }

            var tupleType = tupleTypes[allocSize - 1].MakeGenericType(ts).GetTypeInfo();
            var cons = tupleType.GetConstructorNonNull(ts);
            var ret = cons.Invoke(ps);

            return ret;
        }
    }
}
