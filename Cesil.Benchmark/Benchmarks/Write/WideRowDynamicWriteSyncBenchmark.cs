using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    public class WideRowDynamicWriteSyncBenchmark
    {
        [ParamsSource(nameof(KnownRowSets))]
        public string RowSet { get; set; }

        public IEnumerable<string> KnownRowSets => new[] { nameof(WideRow.ShallowRows), nameof(WideRow.DeepRows) };

        private IBoundConfiguration<WideRow> StaticConfig;
        private IBoundConfiguration<dynamic> DynamicConfig;

        private IEnumerable<WideRow> StaticRows;
        private IEnumerable<dynamic> DynamicRows_Static;
        private IEnumerable<dynamic> DynamicRows_Cesil;
        private IEnumerable<dynamic> DynamicRows_ExpandoObject;
        private IEnumerable<dynamic> DynamicRows_Custom;


        [GlobalSetup]
        public void Initialize()
        {
            WideRow.Initialize();

            StaticConfig = Configuration.For<WideRow>();
            DynamicConfig = Configuration.ForDynamic();

            StaticRows = GetStaticRows(RowSet);
            DynamicRows_Static = GetRows(RowSet, "Static");
            DynamicRows_Cesil = GetRows(RowSet, "Cesil");
            DynamicRows_ExpandoObject = GetRows(RowSet, nameof(ExpandoObject));
            DynamicRows_Custom = GetRows(RowSet, "Custom");
        }

        public void InitializeAndTest()
        {
            foreach (var rows in KnownRowSets)
            {
                RowSet = rows;

                Initialize();

                string shouldMatchCsv;
                using (var txt = new StringWriter())
                {
                    WriteStatic(txt, StaticRows);
                    shouldMatchCsv = txt.ToString();
                }


                foreach (var rowSet in new[] { DynamicRows_Static, DynamicRows_Cesil, DynamicRows_ExpandoObject, DynamicRows_Custom })
                {
                    string csv;
                    using (var txt = new StringWriter())
                    {
                        WriteDynamic(txt, rowSet);

                        csv = txt.ToString();
                    }

                    if (csv != shouldMatchCsv) throw new Exception();
                }
            }
        }

        [Benchmark(Baseline = true)]
        public void Static()
        {
            WriteStatic(TextWriter.Null, StaticRows);
        }

        [Benchmark]
        public void Dynamic_Static()
        {
            WriteDynamic(TextWriter.Null, DynamicRows_Static);
        }

        [Benchmark]
        public void Dynamic_Cesil()
        {
            WriteDynamic(TextWriter.Null, DynamicRows_Cesil);
        }

        [Benchmark]
        public void Dynamic_ExpandoObject()
        {
            WriteDynamic(TextWriter.Null, DynamicRows_ExpandoObject);
        }

        [Benchmark]
        public void Dynamic_Custom()
        {
            WriteDynamic(TextWriter.Null, DynamicRows_Custom);
        }

        private void WriteDynamic(TextWriter stream, IEnumerable<dynamic> rows)
        {
            using (var csv = DynamicConfig.CreateWriter(stream))
            {
                csv.WriteAll(rows);
            }
        }

        private void WriteStatic(TextWriter stream, IEnumerable<WideRow> rows)
        {
            using (var csv = StaticConfig.CreateWriter(stream))
            {
                csv.WriteAll(rows);
            }
        }

        private IEnumerable<WideRow> GetStaticRows(string rows)
        {
            IEnumerable<WideRow> toMakeDynamic;
            switch (rows)
            {
                case nameof(WideRow.ShallowRows): toMakeDynamic = WideRow.ShallowRows; break;
                case nameof(WideRow.DeepRows): toMakeDynamic = WideRow.DeepRows; break;
                default: throw new Exception();
            }

            return toMakeDynamic;
        }

        private IEnumerable<dynamic> GetRows(string rows, string type)
        {
            var toMakeDynamic = GetStaticRows(rows);

            switch (type)
            {
                case "Static": return toMakeDynamic;
                case "Cesil":
                    {
                        string csvText;
                        using (var writer = new StringWriter())
                        {
                            using (var csv = StaticConfig.CreateWriter(writer))
                            {
                                csv.WriteAll(toMakeDynamic);
                            }
                            csvText = writer.ToString();
                        }

                        var opts = Options.CreateBuilder(Options.DynamicDefault).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();

                        var ret = CesilUtils.EnumerateDynamicFromString(csvText, opts).ToList();

                        return ret;
                    }
                case nameof(ExpandoObject):
                    {
                        var ret = new List<dynamic>();
                        foreach (var row in toMakeDynamic)
                        {
                            var expandoObj = new ExpandoObject();
                            var expando = (IDictionary<string, dynamic>)expandoObj;
                            expando["Byte"] = row.Byte;
                            expando["SByte"] = row.SByte;
                            expando["Short"] = row.Short;
                            expando["UShort"] = row.UShort;
                            expando["Int"] = row.Int;
                            expando["UInt"] = row.UInt;
                            expando["Long"] = row.Long;
                            expando["ULong"] = row.ULong;
                            expando["Float"] = row.Float;
                            expando["Double"] = row.Double;
                            expando["Decimal"] = row.Decimal;
                            expando["NullableByte"] = row.NullableByte;
                            expando["NullableSByte"] = row.NullableSByte;
                            expando["NullableShort"] = row.NullableShort;
                            expando["NullableUShort"] = row.NullableUShort;
                            expando["NullableInt"] = row.NullableInt;
                            expando["NullableUInt"] = row.NullableUInt;
                            expando["NullableLong"] = row.NullableLong;
                            expando["NullableULong"] = row.NullableULong;
                            expando["NullableFloat"] = row.NullableFloat;
                            expando["NullableDouble"] = row.NullableDouble;
                            expando["NullableDecimal"] = row.NullableDecimal;
                            expando["String"] = row.String;
                            expando["Char"] = row.Char;
                            expando["NullableChar"] = row.NullableChar;
                            expando["Guid"] = row.Guid;
                            expando["NullableGuid"] = row.NullableGuid;
                            expando["DateTime"] = row.DateTime;
                            expando["DateTimeOffset"] = row.DateTimeOffset;
                            expando["NullableDateTime"] = row.NullableDateTime;
                            expando["NullableDateTimeOffset"] = row.NullableDateTimeOffset;
                            expando["Uri"] = row.Uri;
                            expando["Enum"] = row.Enum;
                            expando["FlagsEnum"] = row.FlagsEnum;
                            expando["NullableEnum"] = row.NullableEnum;
                            expando["NullableFlagsEnum"] = row.NullableFlagsEnum;

                            ret.Add(expandoObj);
                        }

                        return ret;
                    }
                case "Custom":
                    {
                        var ret = new List<dynamic>();
                        foreach (var row in toMakeDynamic)
                        {
                            var expandoObj = new FakeExpandoObject();
                            var expando = (IDictionary<string, dynamic>)expandoObj;
                            expando["Byte"] = row.Byte;
                            expando["SByte"] = row.SByte;
                            expando["Short"] = row.Short;
                            expando["UShort"] = row.UShort;
                            expando["Int"] = row.Int;
                            expando["UInt"] = row.UInt;
                            expando["Long"] = row.Long;
                            expando["ULong"] = row.ULong;
                            expando["Float"] = row.Float;
                            expando["Double"] = row.Double;
                            expando["Decimal"] = row.Decimal;
                            expando["NullableByte"] = row.NullableByte;
                            expando["NullableSByte"] = row.NullableSByte;
                            expando["NullableShort"] = row.NullableShort;
                            expando["NullableUShort"] = row.NullableUShort;
                            expando["NullableInt"] = row.NullableInt;
                            expando["NullableUInt"] = row.NullableUInt;
                            expando["NullableLong"] = row.NullableLong;
                            expando["NullableULong"] = row.NullableULong;
                            expando["NullableFloat"] = row.NullableFloat;
                            expando["NullableDouble"] = row.NullableDouble;
                            expando["NullableDecimal"] = row.NullableDecimal;
                            expando["String"] = row.String;
                            expando["Char"] = row.Char;
                            expando["NullableChar"] = row.NullableChar;
                            expando["Guid"] = row.Guid;
                            expando["NullableGuid"] = row.NullableGuid;
                            expando["DateTime"] = row.DateTime;
                            expando["DateTimeOffset"] = row.DateTimeOffset;
                            expando["NullableDateTime"] = row.NullableDateTime;
                            expando["NullableDateTimeOffset"] = row.NullableDateTimeOffset;
                            expando["Uri"] = row.Uri;
                            expando["Enum"] = row.Enum;
                            expando["FlagsEnum"] = row.FlagsEnum;
                            expando["NullableEnum"] = row.NullableEnum;
                            expando["NullableFlagsEnum"] = row.NullableFlagsEnum;

                            ret.Add(expandoObj);
                        }

                        return ret;
                    }
                default:
                    throw new Exception();
            }
        }
    }
}
