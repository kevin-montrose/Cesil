using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Cesil
{
    internal static class RecordDynamicRowConverter<TRecord>
    {
        private static readonly ImmutableArray<(string Name, TypeInfo Type)> RequiredColumns = GetRequiredColumns();
        private static readonly Delegate ConstructorDelegate = MakeConstructorDelegate();

        private static readonly ImmutableArray<(string Name, TypeInfo Type, Delegate Setter)> OptionalColumns = GetOptionalColumns();

        private static ImmutableArray<(string Name, TypeInfo Type, Delegate Setter)> GetOptionalColumns()
        {
            var recordType = typeof(TRecord).GetTypeInfo();

            var (_, propsSetByCons, _) = recordType.ReadRecordType();

            var propsWithSetters = recordType.GetProperties().Where(p => p.SetMethod != null).Except(propsSetByCons);

            var ret = ImmutableArray.CreateBuilder<(string Name, TypeInfo Type, Delegate Setter)>();

            foreach (var prop in propsWithSetters)
            {
                var name = prop.Name;
                var type = prop.PropertyType.GetTypeInfo();
                var setMtd = Utils.NonNull(prop.SetMethod);

                var delType = Types.Action2.MakeGenericType(typeof(TRecord), type);
                var del = Delegate.CreateDelegate(delType, setMtd);

                ret.Add((name, type, del));
            }

            return ret.ToImmutable();
        }

        private static ImmutableArray<(string Name, TypeInfo Type)> GetRequiredColumns()
        {
            var recordType = typeof(TRecord).GetTypeInfo();
            var (cons, _, _) = recordType.ReadRecordType();

            return ImmutableArray.CreateRange(cons.GetParameters().Select(p => (Utils.NonNull(p.Name), p.ParameterType.GetTypeInfo())));
        }

        private static Delegate MakeConstructorDelegate()
        {
            var recordType = typeof(TRecord).GetTypeInfo();
            var (primaryCons, _, _) = recordType.ReadRecordType();

            var statements = new List<Expression>();

            var ps = primaryCons.GetParameters();

            var paramList = ps.Select(p => Expression.Parameter(p.ParameterType, $"__param_{p.Name}")).ToArray();
            var callCons = Expression.New(primaryCons, paramList);
            var lambda = Expression.Lambda(callCons, paramList);
            var ret = lambda.Compile();

            return ret;
        }

        internal static bool TryConvert(object row, in ReadContext _, out TRecord res)
        {
            if (row is DynamicRow rowDyn)
            {
                res = GetRecordForRow(rowDyn);

                return true;
            }
            else
            {
                var rowRangeDyn = Utils.NonNull(row as DynamicRowRange);

                res = GetRecordForRange(rowRangeDyn);

                return true;
            }
        }

        private static TRecord GetRecordForRow(DynamicRow row)
        {
            var consVals = new object?[RequiredColumns.Length];

            for (var i = 0; i < RequiredColumns.Length; i++)
            {
                var (colName, colType) = RequiredColumns[i];

                object? cellRaw;

                if (row.HasNames)
                {
                    cellRaw = row.GetByName(colName, row, null, null);
                }
                else
                {
                    cellRaw = row.GetByIndex(i, row, null, null);
                }

                object? val;

                if (cellRaw == null)
                {
                    val = null;
                }
                else
                {
                    var cell = (DynamicCell)cellRaw;

                    var parser = cell.GetParser(colType, out var ctx);
                    if (parser == null)
                    {
                        Throw.InvalidOperationException($"No parser found to convert cell at index={i} to {colType}");
                    }

                    var delProvider = (ICreatesCacheableDelegate<Parser.DynamicParserDelegate>)parser;

                    var del = delProvider.Guarantee(row.Owner);

                    var data = cell.GetDataSpan();
                    if (!del(data, ctx, out var res))
                    {
                        Throw.InvalidOperationException($"{nameof(Parser)} {parser} returned false");
                    }

                    val = res;
                }

                consVals[i] = val;
            }

            var ret = ConstructForObjectArray(consVals);
            return SetOptionalPropertyForRow(row, ret);
        }

        private static TRecord GetRecordForRange(DynamicRowRange rowRange)
        {
            var consVals = new object?[RequiredColumns.Length];

            for (var i = 0; i < RequiredColumns.Length; i++)
            {
                var (colName, colType) = RequiredColumns[i];

                object? cellRaw;

                if (rowRange.Parent.HasNames)
                {
                    cellRaw = rowRange.Parent.GetByName(colName, rowRange.Parent, rowRange.Offset, rowRange.Length);
                }
                else
                {
                    cellRaw = rowRange.Parent.GetByIndex(i, rowRange.Parent, rowRange.Offset, rowRange.Length);
                }

                object? val;

                if (cellRaw == null)
                {
                    val = null;
                }
                else
                {
                    var cell = (DynamicCell)cellRaw;

                    var parser = cell.GetParser(colType, out var ctx);
                    if (parser == null)
                    {
                        Throw.InvalidOperationException($"No parser found to convert cell at index={i} to {colType}");
                    }

                    var delProvider = (ICreatesCacheableDelegate<Parser.DynamicParserDelegate>)parser;

                    var del = delProvider.Guarantee(rowRange.Parent.Owner);

                    var data = cell.GetDataSpan();
                    if (!del(data, ctx, out var res))
                    {
                        Throw.InvalidOperationException($"{nameof(Parser)} {parser} returned false");
                    }

                    val = res;
                }

                consVals[i] = val;
            }

            var ret = ConstructForObjectArray(consVals);
            return SetOptionalPropertyForRange(rowRange, ret);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TRecord ConstructForObjectArray(object?[] args)
        {
            var constructed = ConstructorDelegate.DynamicInvoke(args);

            return (TRecord)Utils.NonNull(constructed);
        }

        private static TRecord SetOptionalPropertyForRow(DynamicRow row, TRecord record)
        {
            if (!row.HasNames)
            {
                // can't find anything else by ordinal, so no names means no additional work
                return record;
            }

            var args = new object?[2];
            args[0] = record;

            foreach (var (name, type, setter) in OptionalColumns)
            {
                if (row.TryGetValue(name, out var cellRaw, row, null, null))
                {
                    object? val;

                    if (cellRaw == null)
                    {
                        val = null;
                    }
                    else
                    {
                        var cell = (DynamicCell)cellRaw;

                        var parser = cell.GetParser(type, out var ctx);
                        if (parser == null)
                        {
                            Throw.InvalidOperationException($"No parser found to convert cell with name={name} to {type}");
                        }

                        var delProvider = (ICreatesCacheableDelegate<Parser.DynamicParserDelegate>)parser;

                        var del = delProvider.Guarantee(row.Owner);

                        var data = cell.GetDataSpan();
                        if (!del(data, ctx, out var res))
                        {
                            Throw.InvalidOperationException($"{nameof(Parser)} {parser} returned false");
                        }

                        val = res;
                    }

                    args[1] = val;

                    setter.DynamicInvoke(args);
                }
            }

            return record;
        }

        private static TRecord SetOptionalPropertyForRange(DynamicRowRange rowRange, TRecord record)
        {
            if (!rowRange.Parent.HasNames)
            {
                // can't find anything else by ordinal, so no names means no additional work
                return record;
            }

            var args = new object?[2];
            args[0] = record;

            foreach (var (name, type, setter) in OptionalColumns)
            {
                if (rowRange.Parent.TryGetValue(name, out var cellRaw, rowRange.Parent, rowRange.Offset, rowRange.Length))
                {
                    object? val;

                    if (cellRaw == null)
                    {
                        val = null;
                    }
                    else
                    {
                        var cell = (DynamicCell)cellRaw;

                        var parser = cell.GetParser(type, out var ctx);
                        if (parser == null)
                        {
                            Throw.InvalidOperationException($"No parser found to convert cell with name={name} to {type}");
                        }

                        var delProvider = (ICreatesCacheableDelegate<Parser.DynamicParserDelegate>)parser;

                        var del = delProvider.Guarantee(rowRange.Parent.Owner);

                        var data = cell.GetDataSpan();
                        if (!del(data, ctx, out var res))
                        {
                            Throw.InvalidOperationException($"{nameof(Parser)} {parser} returned false");
                        }

                        val = res;
                    }

                    args[1] = val;

                    setter.DynamicInvoke(args);
                }
            }

            return record;
        }
    }
}
