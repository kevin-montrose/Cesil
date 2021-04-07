using System;
using System.Reflection;

namespace Cesil
{
    internal static class TupleDynamicRowConverters<T>
    {
        private static readonly TypeInfo[] ArgTypes = GetTupleArgTypes(typeof(T).GetTypeInfo());

        internal static bool TryConvertTuple(object row, in ReadContext _, out T res)
        {
            if (row is DynamicRow rowDyn)
            {
                var untyped = GetTupleForRow(rowDyn, ArgTypes);
                res = (T)untyped;

                return true;
            }
            else if (row is DynamicRowRange rowRangeDyn)
            {
                var untyped = GetTupleForRange(rowRangeDyn, ArgTypes);
                res = (T)untyped;

                return true;
            }
            else
            {
#pragma warning disable CES0005 // a generic type that we aren't actually going to produce
                res = default!;
#pragma warning restore CES0005
                Throw.ImpossibleException($"Tried to convert unexpected dynamic type ({row.GetType()?.GetTypeInfo()})");
                return default;
            }
        }

        internal static bool TryConvertValueTuple(object row, in ReadContext _, out T res)
        {
            if (row is DynamicRow rowDyn)
            {
                var untyped = GetValueTupleForRow(rowDyn, ArgTypes);
                res = (T)untyped;

                return true;
            }
            else if (row is DynamicRowRange rowRangeDyn)
            {
                var untyped = GetValueTupleForRange(rowRangeDyn, ArgTypes);
                res = (T)untyped;

                return true;
            }
            else
            {
#pragma warning disable CES0005 // a generic type that we aren't actually going to produce
                res = default!;
#pragma warning restore CES0005
                Throw.ImpossibleException($"Tried to convert unexpected dynamic type ({row.GetType()?.GetTypeInfo()})");
                return default;
            }
        }

        private static object GetTupleForRow(DynamicRow row, TypeInfo[] colTypes)
        {
            var data = Utils.MakeArrayOfObjects(row, row, null, null, colTypes);

            return ConvertToTuple(colTypes, data, 0, Types.Tuple_Array);
        }

        private static object GetTupleForRange(DynamicRowRange row, TypeInfo[] colTypes)
        {
            var data = Utils.MakeArrayOfObjects(row.Parent, row, row.Offset, row.Length, colTypes);

            return ConvertToTuple(colTypes, data, 0, Types.Tuple_Array);
        }

        private static object GetValueTupleForRow(DynamicRow row, TypeInfo[] colTypes)
        {
            var data = Utils.MakeArrayOfObjects(row, row, null, null, colTypes);

            return ConvertToTuple(colTypes, data, 0, Types.ValueTuple_Array);
        }

        private static object GetValueTupleForRange(DynamicRowRange row, TypeInfo[] colTypes)
        {
            var data = Utils.MakeArrayOfObjects(row.Parent, row, row.Offset, row.Length, colTypes);

            return ConvertToTuple(colTypes, data, 0, Types.ValueTuple_Array);
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
