using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class DynamicReaderTests
    {
        private sealed class _DynamicRowRange : DefaultTypeDescriber
        {
            private static readonly ConcurrentDictionary<string, DynamicRowConverter> Cache = new ConcurrentDictionary<string, DynamicRowConverter>();

            private static readonly DynamicRowConverter TwoParamCons =
                DynamicRowConverter.ForConstructorTakingTypedParameters(
                    typeof(_DynamicRowRange_Cons1).GetConstructor(new[] { typeof(int), typeof(int) }),
                    new[] { ColumnIdentifier.Create(0), ColumnIdentifier.Create(1) }
                );

            private static readonly DynamicRowConverter OneParamCons =
                DynamicRowConverter.ForConstructorTakingDynamic(
                    typeof(_DynamicRowRange_Cons2).GetConstructor(new[] { typeof(object) })
                );

            private static readonly DynamicRowConverter ZeroParamCons_ABC =
                DynamicRowConverter.ForEmptyConstructorAndSetters(
                    typeof(_DynamicRowRange_Cons3).GetConstructor(Array.Empty<Type>()),
                    new[]
                    {
                        Setter.ForDelegate((_DynamicRowRange_Cons3 row, int a, in ReadContext _) => {row.A = a; }),
                        Setter.ForDelegate((_DynamicRowRange_Cons3 row, int b, in ReadContext _) => {row.B = b; }),
                        Setter.ForDelegate((_DynamicRowRange_Cons3 row, int c, in ReadContext _) => {row.C = c; }),
                    },
                    new[]
                    {
                        ColumnIdentifier.Create(0),
                        ColumnIdentifier.Create(1),
                        ColumnIdentifier.Create(2)
                    }
                );

            private static readonly DynamicRowConverter ZeroParamCons_AB =
                DynamicRowConverter.ForEmptyConstructorAndSetters(
                    typeof(_DynamicRowRange_Cons3).GetConstructor(Array.Empty<Type>()),
                    new[]
                    {
                        Setter.ForDelegate((_DynamicRowRange_Cons3 row, int a, in ReadContext _) => {row.A = a; }),
                        Setter.ForDelegate((_DynamicRowRange_Cons3 row, int b, in ReadContext _) => {row.B = b; }),
                    },
                    new[]
                    {
                        ColumnIdentifier.Create(0),
                        ColumnIdentifier.Create(1)
                    }
                );

            private static readonly DynamicRowConverter ZeroParamCons_BC =
               DynamicRowConverter.ForEmptyConstructorAndSetters(
                   typeof(_DynamicRowRange_Cons3).GetConstructor(Array.Empty<Type>()),
                   new[]
                   {
                        Setter.ForDelegate((_DynamicRowRange_Cons3 row, int b, in ReadContext _) => {row.B = b; }),
                        Setter.ForDelegate((_DynamicRowRange_Cons3 row, int c, in ReadContext _) => {row.C = c; }),
                   },
                   new[]
                   {
                        ColumnIdentifier.Create(0),
                        ColumnIdentifier.Create(1)
                   }
               );

            public override DynamicRowConverter GetDynamicRowConverter(in ReadContext context, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            {
                if (targetType == typeof(List<(ColumnIdentifier Id, string Value)>).GetTypeInfo())
                {
                    var key = string.Join(", ", columns.Select(c => $"{c.Index},{c.Name}"));

                    if (Cache.TryGetValue(key, out var ret))
                    {
                        return ret;
                    }

                    ret =
                        DynamicRowConverter.ForDelegate(
                            (dynamic row, in ReadContext context, out List<(ColumnIdentifier Id, string Value)> result) =>
                            {
                                result = new List<(ColumnIdentifier Id, string Value)>();

                                foreach (var col in columns)
                                {
                                    string val = row[col.Index];
                                    result.Add((col, val));
                                }

                                return true;
                            }
                        );

                    Cache.TryAdd(key, ret);

                    return ret;
                }

                if (targetType == typeof(_DynamicRowRange_Cons1).GetTypeInfo())
                {
                    return TwoParamCons;
                }

                if (targetType == typeof(_DynamicRowRange_Cons2).GetTypeInfo())
                {
                    return OneParamCons;
                }

                if (targetType == typeof(_DynamicRowRange_Cons3).GetTypeInfo())
                {
                    switch (columns.Count())
                    {
                        case 3: return ZeroParamCons_ABC;
                        case 2:
                            if (columns.Any(c => c.Name == "A"))
                            {
                                return ZeroParamCons_AB;
                            }

                            return ZeroParamCons_BC;
                        default: throw new Exception("Wat");
                    }
                }

                return base.GetDynamicRowConverter(in context, columns, targetType);
            }
        }

        private sealed class _DynamicRowRange_Cons1
        {
            public int One { get; set; }
            public int Two { get; set; }

            public _DynamicRowRange_Cons1(int one, int two)
            {
                One = one;
                Two = two;
            }
        }

        private sealed class _DynamicRowRange_Cons2
        {
            public IEnumerable<string> Values { get; set; }

            public _DynamicRowRange_Cons2(dynamic val)
            {
                IEnumerable<string> e = val;

                Values = e.ToList();
            }
        }

        private sealed class _DynamicRowRange_Cons3
        {
            public int A { get; set; }
            public int B { get; set; }
            public int C { get; set; }

            public _DynamicRowRange_Cons3() { }
        }

        [Fact]
        public void DynamicRowRange()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(new _DynamicRowRange()).ToOptions();
            var config = Configuration.ForDynamic(opts);

            using var reader = new StringReader("A,B,C\r\n1,2,3");
            using var csv = config.CreateReader(reader);

            var row = csv.ReadAll().Single() as DynamicRow;
            Assert.NotNull(row);

            // BindGetMember & BindGetIndex
            {
                // no change
                {
                    var range = new DynamicRowRange(row, 0, 3);
                    dynamic dyn = range;

                    Assert.Equal(1, (int)dyn[0]);
                    Assert.Equal(1, (int)dyn[(Index)0]);
                    Assert.Equal(1, (int)dyn[^3]);
                    Assert.Equal(1, (int)dyn.A);
                    Assert.Equal(1, (int)dyn["A"]);
                    Assert.Equal(1, (int)dyn[ColumnIdentifier.Create(0)]);
                    Assert.Equal(1, (int)dyn[ColumnIdentifier.Create(0, "A")]);

                    Assert.Equal(2, (int)dyn[1]);
                    Assert.Equal(2, (int)dyn[(Index)1]);
                    Assert.Equal(2, (int)dyn[^2]);
                    Assert.Equal(2, (int)dyn.B);
                    Assert.Equal(2, (int)dyn["B"]);
                    Assert.Equal(2, (int)dyn[ColumnIdentifier.Create(1)]);
                    Assert.Equal(2, (int)dyn[ColumnIdentifier.Create(1, "B")]);

                    Assert.Equal(3, (int)dyn[2]);
                    Assert.Equal(3, (int)dyn[(Index)2]);
                    Assert.Equal(3, (int)dyn[^1]);
                    Assert.Equal(3, (int)dyn.C);
                    Assert.Equal(3, (int)dyn["C"]);
                    Assert.Equal(3, (int)dyn[ColumnIdentifier.Create(2)]);
                    Assert.Equal(3, (int)dyn[ColumnIdentifier.Create(2, "C")]);
                }

                // shift right
                {
                    var range = new DynamicRowRange(row, 1, 2);
                    dynamic dyn = range;

                    Assert.Throws<ArgumentOutOfRangeException>(() => dyn[2]);
                    Assert.Throws<KeyNotFoundException>(() => dyn.A);

                    Assert.Equal(2, (int)dyn[0]);
                    Assert.Equal(2, (int)dyn[(Index)0]);
                    Assert.Equal(2, (int)dyn[^2]);
                    Assert.Equal(2, (int)dyn.B);
                    Assert.Equal(2, (int)dyn["B"]);
                    Assert.Equal(2, (int)dyn[ColumnIdentifier.Create(0)]);
                    Assert.Equal(2, (int)dyn[ColumnIdentifier.Create(0, "B")]);

                    Assert.Equal(3, (int)dyn[1]);
                    Assert.Equal(3, (int)dyn[(Index)1]);
                    Assert.Equal(3, (int)dyn[^1]);
                    Assert.Equal(3, (int)dyn.C);
                    Assert.Equal(3, (int)dyn["C"]);
                    Assert.Equal(3, (int)dyn[ColumnIdentifier.Create(1)]);
                    Assert.Equal(3, (int)dyn[ColumnIdentifier.Create(1, "C")]);
                }

                // shift left
                {
                    var range = new DynamicRowRange(row, 0, 2);
                    dynamic dyn = range;

                    Assert.Throws<ArgumentOutOfRangeException>(() => dyn[2]);
                    Assert.Throws<KeyNotFoundException>(() => dyn.C);

                    Assert.Equal(1, (int)dyn[0]);
                    Assert.Equal(1, (int)dyn[(Index)0]);
                    Assert.Equal(1, (int)dyn[^2]);
                    Assert.Equal(1, (int)dyn.A);
                    Assert.Equal(1, (int)dyn["A"]);
                    Assert.Equal(1, (int)dyn[ColumnIdentifier.Create(0)]);
                    Assert.Equal(1, (int)dyn[ColumnIdentifier.Create(0, "A")]);

                    Assert.Equal(2, (int)dyn[1]);
                    Assert.Equal(2, (int)dyn[(Index)1]);
                    Assert.Equal(2, (int)dyn[^1]);
                    Assert.Equal(2, (int)dyn.B);
                    Assert.Equal(2, (int)dyn["B"]);
                    Assert.Equal(2, (int)dyn[ColumnIdentifier.Create(1)]);
                    Assert.Equal(2, (int)dyn[ColumnIdentifier.Create(1, "B")]);
                }

                // swap dynamic types
                {
                    var rows = new[] { (dynamic)row, (dynamic)new DynamicRowRange(row, 0, 3) };

                    foreach (var r in rows)
                    {
                        Assert.Equal(1, (int)r[0]);
                        Assert.Equal(1, (int)r[(Index)0]);
                        Assert.Equal(1, (int)r[^3]);
                        Assert.Equal(1, (int)r.A);
                        Assert.Equal(1, (int)r["A"]);
                        Assert.Equal(1, (int)r[ColumnIdentifier.Create(0)]);
                        Assert.Equal(1, (int)r[ColumnIdentifier.Create(0, "A")]);

                        Assert.Equal(2, (int)r[1]);
                        Assert.Equal(2, (int)r[(Index)1]);
                        Assert.Equal(2, (int)r[^2]);
                        Assert.Equal(2, (int)r.B);
                        Assert.Equal(2, (int)r["B"]);
                        Assert.Equal(2, (int)r[ColumnIdentifier.Create(1)]);
                        Assert.Equal(2, (int)r[ColumnIdentifier.Create(1, "B")]);

                        Assert.Equal(3, (int)r[2]);
                        Assert.Equal(3, (int)r[(Index)2]);
                        Assert.Equal(3, (int)r[^1]);
                        Assert.Equal(3, (int)r.C);
                        Assert.Equal(3, (int)r["C"]);
                        Assert.Equal(3, (int)r[ColumnIdentifier.Create(2)]);
                        Assert.Equal(3, (int)r[ColumnIdentifier.Create(2, "C")]);
                    }
                }
            }

            // conversions
            {
                // no change
                {
                    var range = new DynamicRowRange(row, 0, 3);
                    dynamic dyn = range;

                    (int First, int Second, int Third) tuple = dyn;
                    Assert.Equal(1, tuple.First);
                    Assert.Equal(2, tuple.Second);
                    Assert.Equal(3, tuple.Third);

                    Tuple<int, int, int> refTuple = dyn;
                    Assert.Equal(1, refTuple.Item1);
                    Assert.Equal(2, refTuple.Item2);
                    Assert.Equal(3, refTuple.Item3);

                    IEnumerable<string> enumerable = dyn;
                    Assert.Equal(new[] { "1", "2", "3" }, enumerable);

                    _DynamicRowRange_Cons1 c1 = dyn;
                    Assert.Equal(1, c1.One);
                    Assert.Equal(2, c1.Two);

                    _DynamicRowRange_Cons2 c2 = dyn;
                    Assert.Equal(new[] { "1", "2", "3" }, c2.Values);

                    _DynamicRowRange_Cons3 c3 = dyn;
                    Assert.Equal(1, c3.A);
                    Assert.Equal(2, c3.B);
                    Assert.Equal(3, c3.C);

                    List<(ColumnIdentifier Id, string Value)> extracted = dyn;
                    Assert.Collection(
                        extracted,
                        a =>
                        {
                            Assert.Equal(0, a.Id.Index);
                            Assert.Equal("A", a.Id.Name);
                            Assert.Equal("1", a.Value);
                        },
                        b =>
                        {
                            Assert.Equal(1, b.Id.Index);
                            Assert.Equal("B", b.Id.Name);
                            Assert.Equal("2", b.Value);
                        },
                        c =>
                        {
                            Assert.Equal(2, c.Id.Index);
                            Assert.Equal("C", c.Id.Name);
                            Assert.Equal("3", c.Value);
                        }
                    );
                }

                // shift left
                {
                    var range = new DynamicRowRange(row, 1, 2);
                    dynamic dyn = range;

                    (int First, int Second) tuple = dyn;
                    Assert.Equal(2, tuple.First);
                    Assert.Equal(3, tuple.Second);

                    Tuple<int, int> refTuple = dyn;
                    Assert.Equal(2, refTuple.Item1);
                    Assert.Equal(3, refTuple.Item2);

                    IEnumerable<string> enumerable = dyn;
                    Assert.Equal(new[] { "2", "3" }, enumerable);

                    _DynamicRowRange_Cons1 c1 = dyn;
                    Assert.Equal(2, c1.One);
                    Assert.Equal(3, c1.Two);

                    _DynamicRowRange_Cons2 c2 = dyn;
                    Assert.Equal(new[] { "2", "3" }, c2.Values);

                    _DynamicRowRange_Cons3 c3 = dyn;
                    Assert.Equal(0, c3.A);
                    Assert.Equal(2, c3.B);
                    Assert.Equal(3, c3.C);

                    List<(ColumnIdentifier Id, string Value)> extracted = dyn;
                    Assert.Collection(
                        extracted,
                        b =>
                        {
                            Assert.Equal(0, b.Id.Index);
                            Assert.Equal("B", b.Id.Name);
                            Assert.Equal("2", b.Value);
                        },
                        c =>
                        {
                            Assert.Equal(1, c.Id.Index);
                            Assert.Equal("C", c.Id.Name);
                            Assert.Equal("3", c.Value);
                        }
                    );
                }

                // shift right
                {
                    var range = new DynamicRowRange(row, 0, 2);
                    dynamic dyn = range;

                    (int First, int Second) tuple = dyn;
                    Assert.Equal(1, tuple.First);
                    Assert.Equal(2, tuple.Second);

                    Tuple<int, int> refTuple = dyn;
                    Assert.Equal(1, refTuple.Item1);
                    Assert.Equal(2, refTuple.Item2);

                    IEnumerable<string> enumerable = dyn;
                    Assert.Equal(new[] { "1", "2" }, enumerable);

                    _DynamicRowRange_Cons1 c1 = dyn;
                    Assert.Equal(1, c1.One);
                    Assert.Equal(2, c1.Two);

                    _DynamicRowRange_Cons2 c2 = dyn;
                    Assert.Equal(new[] { "1", "2" }, c2.Values);

                    _DynamicRowRange_Cons3 c3 = dyn;
                    Assert.Equal(1, c3.A);
                    Assert.Equal(2, c3.B);
                    Assert.Equal(0, c3.C);

                    List<(ColumnIdentifier Id, string Value)> extracted = dyn;
                    Assert.Collection(
                        extracted,
                        b =>
                        {
                            Assert.Equal(0, b.Id.Index);
                            Assert.Equal("A", b.Id.Name);
                            Assert.Equal("1", b.Value);
                        },
                        c =>
                        {
                            Assert.Equal(1, c.Id.Index);
                            Assert.Equal("B", c.Id.Name);
                            Assert.Equal("2", c.Value);
                        }
                    );
                }

                // swap dynamic types
                {
                    var rows = new[] { (dynamic)row, (dynamic)new DynamicRowRange(row, 0, 3) };

                    foreach (var r in rows)
                    {
                        (int First, int Second, int Third) tuple = r;
                        Assert.Equal(1, tuple.First);
                        Assert.Equal(2, tuple.Second);
                        Assert.Equal(3, tuple.Third);

                        Tuple<int, int, int> refTuple = r;
                        Assert.Equal(1, refTuple.Item1);
                        Assert.Equal(2, refTuple.Item2);
                        Assert.Equal(3, refTuple.Item3);

                        IEnumerable<string> enumerable = r;
                        Assert.Equal(new[] { "1", "2", "3" }, enumerable);

                        _DynamicRowRange_Cons1 c1 = r;
                        Assert.Equal(1, c1.One);
                        Assert.Equal(2, c1.Two);

                        _DynamicRowRange_Cons2 c2 = r;
                        Assert.Equal(new[] { "1", "2", "3" }, c2.Values);

                        _DynamicRowRange_Cons3 c3 = r;
                        Assert.Equal(1, c3.A);
                        Assert.Equal(2, c3.B);
                        Assert.Equal(3, c3.C);

                        List<(ColumnIdentifier Id, string Value)> extracted = r;
                        Assert.Collection(
                            extracted,
                            a =>
                            {
                                Assert.Equal(0, a.Id.Index);
                                Assert.Equal("A", a.Id.Name);
                                Assert.Equal("1", a.Value);
                            },
                            b =>
                            {
                                Assert.Equal(1, b.Id.Index);
                                Assert.Equal("B", b.Id.Name);
                                Assert.Equal("2", b.Value);
                            },
                            c =>
                            {
                                Assert.Equal(2, c.Id.Index);
                                Assert.Equal("C", c.Id.Name);
                                Assert.Equal("3", c.Value);
                            }
                        );
                    }
                }
            }

            // (sub)ranges
            {
                // no change
                {
                    var range = new DynamicRowRange(row, 0, 3);
                    dynamic dyn = range;

                    var subDyn1 = dyn[..];
                    var subDyn2 = dyn[0..];
                    var subDyn3 = dyn[0..3];
                    var subDyn4 = dyn[^3..^0];

                    IEnumerable<string> e1 = subDyn1;
                    Assert.Equal(new[] { "1", "2", "3" }, e1);

                    IEnumerable<string> e2 = subDyn2;
                    Assert.Equal(new[] { "1", "2", "3" }, e2);

                    IEnumerable<string> e3 = subDyn3;
                    Assert.Equal(new[] { "1", "2", "3" }, e3);

                    IEnumerable<string> e4 = subDyn4;
                    Assert.Equal(new[] { "1", "2", "3" }, e4);

                    // trim left
                    var subDyn5 = dyn[1..];
                    var subDyn6 = dyn[1..3];
                    var subDyn7 = dyn[^2..3];
                    var subDyn8 = dyn[^2..^0];

                    IEnumerable<string> e5 = subDyn5;
                    Assert.Equal(new[] { "2", "3" }, e5);

                    IEnumerable<string> e6 = subDyn6;
                    Assert.Equal(new[] { "2", "3" }, e6);

                    IEnumerable<string> e7 = subDyn7;
                    Assert.Equal(new[] { "2", "3" }, e7);

                    IEnumerable<string> e8 = subDyn8;
                    Assert.Equal(new[] { "2", "3" }, e8);

                    // trim right
                    var subDyn9 = dyn[..2];
                    var subDyn10 = dyn[0..2];
                    var subDyn11 = dyn[^3..2];
                    var subDyn12 = dyn[^3..^1];

                    IEnumerable<string> e9 = subDyn9;
                    Assert.Equal(new[] { "1", "2" }, e9);

                    IEnumerable<string> e10 = subDyn10;
                    Assert.Equal(new[] { "1", "2" }, e10);

                    IEnumerable<string> e11 = subDyn11;
                    Assert.Equal(new[] { "1", "2" }, e11);

                    IEnumerable<string> e12 = subDyn12;
                    Assert.Equal(new[] { "1", "2" }, e12);
                }

                // trim left
                {
                    var range = new DynamicRowRange(row, 1, 2);
                    dynamic dyn = range;

                    var subDyn1 = dyn[..];
                    var subDyn2 = dyn[0..];
                    var subDyn3 = dyn[0..2];
                    var subDyn4 = dyn[^2..^0];

                    IEnumerable<string> e1 = subDyn1;
                    Assert.Equal(new[] { "2", "3" }, e1);

                    IEnumerable<string> e2 = subDyn2;
                    Assert.Equal(new[] { "2", "3" }, e2);

                    IEnumerable<string> e3 = subDyn3;
                    Assert.Equal(new[] { "2", "3" }, e3);

                    IEnumerable<string> e4 = subDyn4;
                    Assert.Equal(new[] { "2", "3" }, e4);

                    // trim left
                    var subDyn5 = dyn[1..];
                    var subDyn6 = dyn[1..2];
                    var subDyn7 = dyn[^1..2];
                    var subDyn8 = dyn[^1..^0];

                    IEnumerable<string> e5 = subDyn5;
                    Assert.Equal(new[] { "3" }, e5);

                    IEnumerable<string> e6 = subDyn6;
                    Assert.Equal(new[] { "3" }, e6);

                    IEnumerable<string> e7 = subDyn7;
                    Assert.Equal(new[] { "3" }, e7);

                    IEnumerable<string> e8 = subDyn8;
                    Assert.Equal(new[] { "3" }, e8);

                    // trim right
                    var subDyn9 = dyn[..1];
                    var subDyn10 = dyn[0..1];
                    var subDyn11 = dyn[^2..1];
                    var subDyn12 = dyn[^2..^1];

                    IEnumerable<string> e9 = subDyn9;
                    Assert.Equal(new[] { "2" }, e9);

                    IEnumerable<string> e10 = subDyn10;
                    Assert.Equal(new[] { "2" }, e10);

                    IEnumerable<string> e11 = subDyn11;
                    Assert.Equal(new[] { "2" }, e11);

                    IEnumerable<string> e12 = subDyn12;
                    Assert.Equal(new[] { "2" }, e12);
                }

                // trim right
                {
                    var range = new DynamicRowRange(row, 0, 2);
                    dynamic dyn = range;

                    var subDyn1 = dyn[..];
                    var subDyn2 = dyn[0..];
                    var subDyn3 = dyn[0..2];
                    var subDyn4 = dyn[^2..^0];

                    IEnumerable<string> e1 = subDyn1;
                    Assert.Equal(new[] { "1", "2" }, e1);

                    IEnumerable<string> e2 = subDyn2;
                    Assert.Equal(new[] { "1", "2" }, e2);

                    IEnumerable<string> e3 = subDyn3;
                    Assert.Equal(new[] { "1", "2" }, e3);

                    IEnumerable<string> e4 = subDyn4;
                    Assert.Equal(new[] { "1", "2" }, e4);

                    // trim left
                    var subDyn5 = dyn[1..];
                    var subDyn6 = dyn[1..2];
                    var subDyn7 = dyn[^1..2];
                    var subDyn8 = dyn[^1..^0];

                    IEnumerable<string> e5 = subDyn5;
                    Assert.Equal(new[] { "2" }, e5);

                    IEnumerable<string> e6 = subDyn6;
                    Assert.Equal(new[] { "2" }, e6);

                    IEnumerable<string> e7 = subDyn7;
                    Assert.Equal(new[] { "2" }, e7);

                    IEnumerable<string> e8 = subDyn8;
                    Assert.Equal(new[] { "2" }, e8);

                    // trim right
                    var subDyn9 = dyn[..1];
                    var subDyn10 = dyn[0..1];
                    var subDyn11 = dyn[^2..1];
                    var subDyn12 = dyn[^2..^1];

                    IEnumerable<string> e9 = subDyn9;
                    Assert.Equal(new[] { "1" }, e9);

                    IEnumerable<string> e10 = subDyn10;
                    Assert.Equal(new[] { "1" }, e10);

                    IEnumerable<string> e11 = subDyn11;
                    Assert.Equal(new[] { "1" }, e11);

                    IEnumerable<string> e12 = subDyn12;
                    Assert.Equal(new[] { "1" }, e12);
                }
            }
        }

        private sealed class _DynamicRowGetDataIndex : IDynamicRowOwner
        {
            public Options Options => Options.Default;

            public object Context => null;

            public NameLookup AcquireNameLookup()
            => NameLookup.Empty;

            public void ReleaseNameLookup() { }

            public void Remove(DynamicRow row) { }

            void IDelegateCache.AddDelegate<T, V>(T key, V cached) { }

            bool IDelegateCache.TryGetDelegate<T, V>(T key, out V del)
            {
                del = default;
                return false;
            }
        }

        [Fact]
        public void DynamicRowGetDataIndex()
        {
            var owner = new _DynamicRowGetDataIndex();

            var dyn = new DynamicRow();
            dyn.Init(owner, 0, null, TypeDescribers.Default, false, null, 0, MemoryPool<char>.Shared);

            // no data, shouldn't explode
            Assert.Equal(-1, dyn.GetDataIndex(0));

            // reading past the end, also shouldn't explode
            dyn.SetValue(0, "foo");
            Assert.Equal(-1, dyn.GetDataIndex(2));

            // no need for async equivalent
        }

        [Fact]
        public void DynamicRowStringLookup()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    using (var reader = getReader("A\r\n1"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            r =>
                            {
                                var n1 = "A";
                                var nMissing = "D";

                                Assert.Equal("1", (string)r[n1]);

                                Assert.Throws<KeyNotFoundException>(() => r[nMissing]);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void RangesWithoutNames()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("1,2,3,4,5"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            r =>
                            {
                                var range1 = (IEnumerable<int>)(r[2..]);
                                var range2 = (IEnumerable<int>)(r[1..3]);

                                Assert.Equal(new[] { 3, 4, 5 }, range1);
                                Assert.Equal(new[] { 2, 3 }, range2);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void DynamicRowColumnIdentifierLookup()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    using (var reader = getReader("A\r\n1"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            r =>
                            {
                                var c1I = ColumnIdentifier.Create(0);
                                var c1N = ColumnIdentifier.Create(0, "A");

                                var cMissingI = ColumnIdentifier.Create(4);
                                var cMissingN = ColumnIdentifier.Create(4, "D");

                                Assert.Equal("1", (string)r[c1I]);
                                Assert.Equal("1", (string)r[c1N]);

                                Assert.Throws<ArgumentOutOfRangeException>(() => r[cMissingI]);
                                Assert.Throws<KeyNotFoundException>(() => r[cMissingN]);
                            }
                        );
                    }
                }
            );
        }

        private sealed class _DynamicRowDoubleInit : IDynamicRowOwner
        {
            public Options Options => Options.Default;

            public object Context => null;

            public NameLookup AcquireNameLookup()
            => NameLookup.Empty;

            public void ReleaseNameLookup() { }

            public void Remove(DynamicRow row) { }

            void IDelegateCache.AddDelegate<T, V>(T key, V cached) { }

            bool IDelegateCache.TryGetDelegate<T, V>(T key, out V del)
            {
                del = default;
                return false;
            }
        }

        [Fact]
        public void DynamicRowDoubleInit()
        {
            var owner = new _DynamicRowDoubleInit();

            var row = new DynamicRow();
            row.Init(owner, 0, null, TypeDescribers.Default, false, null, 0, MemoryPool<char>.Shared);

            var exc = Assert.Throws<InvalidOperationException>(() => row.Init(owner, 0, null, TypeDescribers.Default, false, null, 0, MemoryPool<char>.Shared));
            Assert.Equal("DynamicRow not in an uninitialized state", exc.Message);

            row.Dispose();
            row.Init(owner, 0, null, TypeDescribers.Default, false, null, 0, MemoryPool<char>.Shared);
        }

        [Fact]
        public void MultiCharacterSeparators()
        {
            // header variants
            {
                // no headers
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a[0]);
                                        Assert.Equal(123, (int)a[1]);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b[0]);
                                        Assert.Equal(456, (int)b[1]);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c[0]);
                                        Assert.Equal(789, (int)c[1]);
                                    }
                                );
                            }
                        }
                    );
                }

                // always headers
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("A#|#B\r\nhello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.A);
                                        Assert.Equal(123, (int)a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b.A);
                                        Assert.Equal(456, (int)b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c.A);
                                        Assert.Equal(789, (int)c.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // detect line endings
            {
                var opts1 = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Never).WithRowEnding(RowEnding.Detect).ToOptions();
                var opts2 = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).WithRowEnding(RowEnding.Detect).ToOptions();

                // \r\n
                {


                    // not present
                    RunSyncDynamicReaderVariants(
                        opts1,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a[0]);
                                        Assert.Equal(123, (int)a[1]);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b[0]);
                                        Assert.Equal(456, (int)b[1]);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c[0]);
                                        Assert.Equal(789, (int)c[1]);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    RunSyncDynamicReaderVariants(
                        opts2,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#A\r\n123#|#hello\r\n456#|#\"world\"\r\n789#|#\"f#|#b\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.A);
                                        Assert.Equal(123, (int)a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b.A);
                                        Assert.Equal(456, (int)b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c.A);
                                        Assert.Equal(789, (int)c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \r
                {
                    // not present
                    RunSyncDynamicReaderVariants(
                        opts1,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("hello#|#123\r\"world\"#|#456\r\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a[0]);
                                        Assert.Equal(123, (int)a[1]);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b[0]);
                                        Assert.Equal(456, (int)b[1]);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c[0]);
                                        Assert.Equal(789, (int)c[1]);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    RunSyncDynamicReaderVariants(
                        opts2,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#A\r123#|#hello\r456#|#\"world\"\r789#|#\"f#|#b\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.A);
                                        Assert.Equal(123, (int)a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b.A);
                                        Assert.Equal(456, (int)b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c.A);
                                        Assert.Equal(789, (int)c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \n
                {
                    // not present
                    RunSyncDynamicReaderVariants(
                        opts1,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("hello#|#123\n\"world\"#|#456\n\"f#|#b\"#|#789"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a[0]);
                                        Assert.Equal(123, (int)a[1]);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b[0]);
                                        Assert.Equal(456, (int)b[1]);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c[0]);
                                        Assert.Equal(789, (int)c[1]);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    RunSyncDynamicReaderVariants(
                        opts2,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#A\n123#|#hello\n456#|#\"world\"\n789#|#\"f#|#b\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.A);
                                        Assert.Equal(123, (int)a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b.A);
                                        Assert.Equal(456, (int)b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c.A);
                                        Assert.Equal(789, (int)c.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public void MultiCharacterSeparatorInHeaders()
        {
            // always
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).ToOptions();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", (string)a["Foo#|#Bar"]);
                                    Assert.Equal(123, (int)a.B);
                                }
                            );
                        }
                    }
                );
            }

            // detect rows endings
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).WithRowEnding(RowEnding.Detect).ToOptions();

                // \r\n
                {
                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a["Foo#|#Bar"]);
                                        Assert.Equal(123, (int)a.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \r
                {
                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#\"Foo#|#Bar\"\r123#|#hello"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a["Foo#|#Bar"]);
                                        Assert.Equal(123, (int)a.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \n
                {
                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, getReader) =>
                        {
                            using (var reader = getReader("B#|#\"Foo#|#Bar\"\n123#|#hello"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a["Foo#|#Bar"]);
                                        Assert.Equal(123, (int)a.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public void NullStringColumn()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    const string DATA = "A\r\nfoo\r\n\r\nbar\r\n\r\n";

                    using (var reader = getReader(DATA))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        var asStrings = rows.Select(d => (string)d.A).ToList();

                        Assert.True(
                            new[]
                            {
                                "foo",
                                null,
                                "bar",
                                null
                            }
                            .SequenceEqual(asStrings)
                        );
                    }
                }
            );
        }

        [Fact]
        public void RangeAccessOutOfBounds()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    const string DATA = "A,B,C,D\r\n1,,,4\r\n,,5,6\r\n7,8,,";

                    using (var reader = getReader(DATA))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                Assert.Equal(1, (int?)r1.A);
                                Assert.Null((int?)r1.B);
                                Assert.Null((int?)r1.C);
                                Assert.Equal(4, (int?)r1.D);

                                var subR1_1 = r1[0..2];
                                Assert.Equal(1, (int?)subR1_1.A);
                                Assert.Null((int?)subR1_1.B);
                                Assert.Throws<KeyNotFoundException>(() => subR1_1.C);
                                Assert.Throws<KeyNotFoundException>(() => subR1_1.D);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR1_1[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR1_1[2]);

                                var subR1_2 = r1[2..];
                                Assert.Null((int?)subR1_2.C);
                                Assert.Equal(4, (int?)subR1_2.D);
                                Assert.Throws<KeyNotFoundException>(() => subR1_2.A);
                                Assert.Throws<KeyNotFoundException>(() => subR1_2.B);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR1_2[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR1_2[2]);
                            },
                            r2 =>
                            {
                                Assert.Null((int?)r2.A);
                                Assert.Null((int?)r2.B);
                                Assert.Equal(5, (int?)r2.C);
                                Assert.Equal(6, (int?)r2.D);

                                var subR2 = r2[1..3];
                                Assert.Null((int?)subR2.B);
                                Assert.Equal(5, (int?)subR2.C);
                                Assert.Throws<KeyNotFoundException>(() => subR2.A);
                                Assert.Throws<KeyNotFoundException>(() => subR2.D);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR2[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR2[2]);
                            },
                            r3 =>
                            {
                                Assert.Equal(7, (int?)r3.A);
                                Assert.Equal(8, (int?)r3.B);
                                Assert.Null((int?)r3.C);
                                Assert.Null((int?)r3.D);

                                var subR3_1 = r3[1..3];
                                Assert.Equal(8, (int?)subR3_1.B);
                                Assert.Null((int?)subR3_1.C);
                                Assert.Throws<KeyNotFoundException>(() => subR3_1.A);
                                Assert.Throws<KeyNotFoundException>(() => subR3_1.D);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR3_1[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR3_1[2]);

                                var subR3_2 = r3[2..];
                                Assert.Null((int?)subR3_2.C);
                                Assert.Null((int?)subR3_2.D);
                                Assert.Throws<KeyNotFoundException>(() => subR3_2.A);
                                Assert.Throws<KeyNotFoundException>(() => subR3_2.B);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR3_2[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR3_2[2]);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void RangeWithSubNulls()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    const string DATA = "A,B,C,D\r\n1,,,4\r\n,,5,6\r\n7,8,,";

                    using (var reader = getReader(DATA))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                Assert.Equal(1, (int?)r1.A);
                                Assert.Null((int?)r1.B);
                                Assert.Null((int?)r1.C);
                                Assert.Equal(4, (int?)r1.D);

                                var subR1_1 = r1[0..2];
                                Assert.Equal(1, (int?)subR1_1.A);
                                Assert.Null((int?)subR1_1.B);

                                var subR1_2 = r1[2..];
                                Assert.Null((int?)subR1_2.C);
                                Assert.Equal(4, (int?)subR1_2.D);
                            },
                            r2 =>
                            {
                                Assert.Null((int?)r2.A);
                                Assert.Null((int?)r2.B);
                                Assert.Equal(5, (int?)r2.C);
                                Assert.Equal(6, (int?)r2.D);

                                var subR2 = r2[1..3];
                                Assert.Null((int?)subR2.B);
                                Assert.Equal(5, (int?)subR2.C);
                            },
                            r3 =>
                            {
                                Assert.Equal(7, (int?)r3.A);
                                Assert.Equal(8, (int?)r3.B);
                                Assert.Null((int?)r3.C);
                                Assert.Null((int?)r3.D);

                                var subR3_1 = r3[1..3];
                                Assert.Equal(8, (int?)subR3_1.B);
                                Assert.Null((int?)subR3_1.C);

                                var subR3_2 = r3[2..];
                                Assert.Null((int?)subR3_2.C);
                                Assert.Null((int?)subR3_2.D);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void NullRoundTrips()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    const string DATA = "A,B,C\r\n1,2,3\r\n4,,5\r\n6,7,\r\n,8,9";

                    using (var reader = getReader(DATA))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                Assert.Equal(1, (int?)r1.A);
                                Assert.Equal(2, (int?)r1.B);
                                Assert.Equal(3, (int?)r1.C);
                            },
                            r2 =>
                            {
                                Assert.Equal(4, (int?)r2.A);
                                Assert.Null((int?)r2.B);
                                Assert.Equal(5, (int?)r2.C);
                            },
                            r3 =>
                            {
                                Assert.Equal(6, (int?)r3.A);
                                Assert.Equal(7, (int?)r3.B);
                                Assert.Null((int?)r3.C);
                            },
                            r4 =>
                            {
                                Assert.Null((int?)r4.A);
                                Assert.Equal(8, (int?)r4.B);
                                Assert.Equal(9, (int?)r4.C);
                            }
                        );

                        string roundTripped;
                        using (var writer = new StringWriter())
                        {
                            using (var writerCsv = config.CreateWriter(writer))
                            {
                                writerCsv.WriteAll(rows);
                            }
                            roundTripped = writer.ToString();
                        }

                        Assert.Equal(DATA, roundTripped);
                    }
                }
            );
        }

        [Fact]
        public void DynamicRowAsObject()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B,C\r\n1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.EnumerateAll().Single();

                        var obj = _DynamicRowAsObject<object>(row);
                        Assert.NotNull(obj);
                    }
                }
            );
        }

        private static T _DynamicRowAsObject<T>(dynamic r)
        {
            return (T)r;
        }

        [Fact]
        public void DynamicCellAsIConvertible()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B,C\r\n1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.EnumerateAll().Single();

                        var cell = row[0];
                        var convertible = (IConvertible)cell;
                        Assert.NotNull(convertible);
                    }
                }
            );
        }

        [Fact]
        public void DynamicCellAsObject()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B,C\r\n1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var row = csv.EnumerateAll().Single();

                        var cell = row[0];
                        var obj = (object)cell;
                        Assert.NotNull(obj);
                    }
                }
            );
        }

        [Fact]
        public void DynamicCellDoesntSupportMethods()
        {
            // row
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    // doesn't support other public methods
                    {
                        using (var reader = getReader("A,B,C\r\n1,2,3"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var row = csv.EnumerateAll().Single();

                            var cell = row[0];
                            Assert.Throws<InvalidOperationException>(() => cell.GetMetaObject(Expressions.Constant_True));
                        }
                    }
                }
            );
        }

        [Fact]
        public void DynamicRowOnlySupportsDispose()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();

            // row
            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    // explicit Dispose
                    {
                        dynamic row;

                        using (var reader = getReader("A,B,C\r\n1,2,3"))
                        using (var csv = config.CreateReader(reader))
                        {
                            row = csv.EnumerateAll().Single();
                        }

                        row.Dispose();
                    }

                    // cast to IDisposable
                    {
                        dynamic row;

                        using (var reader = getReader("A,B,C\r\n1,2,3"))
                        using (var csv = config.CreateReader(reader))
                        {
                            row = csv.EnumerateAll().Single();
                        }

                        var disposable = (IDisposable)row;

                        disposable.Dispose();
                    }

                    // doesn't support other public methods
                    {
                        using (var reader = getReader("A,B,C\r\n1,2,3"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var row = csv.EnumerateAll().Single();

                            Assert.Throws<InvalidOperationException>(() => row.GetMetaObject(Expressions.Constant_True));

                            row.Dispose();
                        }
                    }
                },
                expectedRuns: 3
            );

            // range
            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    // explicit Dispose
                    {
                        dynamic row;
                        dynamic range;

                        using (var reader = getReader("A,B,C\r\n1,2,3"))
                        using (var csv = config.CreateReader(reader))
                        {
                            row = csv.EnumerateAll().Single();
                            range = row[..];
                        }

                        row.Dispose();
                        range.Dispose();
                    }

                    // cast to IDisposable
                    {
                        dynamic row;
                        dynamic range;

                        using (var reader = getReader("A,B,C\r\n1,2,3"))
                        using (var csv = config.CreateReader(reader))
                        {
                            row = csv.EnumerateAll().Single();
                            range = row[..];
                        }

                        var disposableRow = (IDisposable)row;
                        disposableRow.Dispose();

                        var disposableRange = (IDisposable)range;
                        disposableRange.Dispose();
                    }

                    // doesn't support other public methods
                    {
                        using (var reader = getReader("A,B,C\r\n1,2,3"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var row = csv.EnumerateAll().Single();
                            var range = row[..];

                            Assert.Throws<InvalidOperationException>(() => range.GetMetaObject(Expressions.Constant_True));

                            row.Dispose();
                            range.Dispose();
                        }
                    }
                },
                expectedRuns: 3
            );
        }

        [Fact]
        public void MissingTrailingColumns()
        {
            // with headers
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B,C\r\n1,2,3\r\n4,5\r\n6\r\n\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                int a1 = r1.A;
                                int a2 = r1[0];
                                int b1 = r1.B;
                                int b2 = r1[1];
                                int c1 = r1.C;
                                int c2 = r1[2];

                                Assert.Equal(1, a1);
                                Assert.Equal(1, a2);

                                Assert.Equal(2, b1);
                                Assert.Equal(2, b2);

                                Assert.Equal(3, c1);
                                Assert.Equal(3, c2);
                            },
                            r2 =>
                            {
                                int a1 = r2.A;
                                int a2 = r2[0];
                                int b1 = r2.B;
                                int b2 = r2[1];
                                int? c1 = r2.C;
                                int? c2 = r2[2];

                                Assert.Equal(4, a1);
                                Assert.Equal(4, a2);

                                Assert.Equal(5, b1);
                                Assert.Equal(5, b2);

                                Assert.False(c1.HasValue);
                                Assert.False(c2.HasValue);
                            },
                            r3 =>
                            {
                                int a1 = r3.A;
                                int a2 = r3[0];
                                int? b1 = r3.B;
                                int? b2 = r3[1];
                                int? c1 = r3.C;
                                int? c2 = r3[2];

                                Assert.Equal(6, a1);
                                Assert.Equal(6, a2);

                                Assert.False(b1.HasValue);
                                Assert.False(b2.HasValue);

                                Assert.False(c1.HasValue);
                                Assert.False(c2.HasValue);
                            },
                            r4 =>
                            {
                                int? a1 = r4.A;
                                int? a2 = r4[0];
                                int? b1 = r4.B;
                                int? b2 = r4[1];
                                int? c1 = r4.C;
                                int? c2 = r4[2];

                                Assert.False(a1.HasValue);
                                Assert.False(a2.HasValue);

                                Assert.False(b1.HasValue);
                                Assert.False(b2.HasValue);

                                Assert.False(c1.HasValue);
                                Assert.False(c2.HasValue);
                            }
                        );
                    }
                }
            );

            var noHeadersOpts = Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions();

            // without headers
            RunSyncDynamicReaderVariants(
                noHeadersOpts,
                (config, getReader) =>
                {
                    using (var reader = getReader("1,2,3\r\n4,5\r\n6\r\n\r\n"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                int a2 = r1[0];
                                int b2 = r1[1];
                                int c2 = r1[2];

                                Assert.Equal(1, a2);

                                Assert.Equal(2, b2);

                                Assert.Equal(3, c2);
                            },
                            r2 =>
                            {
                                int a2 = r2[0];
                                int b2 = r2[1];
                                int? c2 = r2[2];

                                Assert.Equal(4, a2);

                                Assert.Equal(5, b2);

                                Assert.False(c2.HasValue);
                            },
                            r3 =>
                            {
                                int a2 = r3[0];
                                int? b2 = r3[1];
                                int? c2 = r3[2];

                                Assert.Equal(6, a2);

                                Assert.False(b2.HasValue);

                                Assert.False(c2.HasValue);
                            },
                            r4 =>
                            {
                                int? a2 = r4[0];
                                int? b2 = r4[1];
                                int? c2 = r4[2];

                                Assert.False(a2.HasValue);

                                Assert.False(b2.HasValue);

                                Assert.False(c2.HasValue);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void DynamicCellsIConvertible()
        {
            // normal
            {
                var csvString =
                    "a,b,c,d,e,f,g,h,i,j,k,l,m,n\r\n" +
                    $"{sbyte.MinValue},{byte.MaxValue},{short.MinValue},{ushort.MaxValue},{int.MinValue},{uint.MaxValue},{long.MinValue},{ulong.MaxValue},123.45,-456.789,2020-03-15,f,hello world,True";

                var conf = Configuration.ForDynamic();
                using (var reader = new StringReader(csvString))
                using (var csv = conf.CreateReader(reader))
                {
                    var rows = csv.ReadAll();
                    var row = rows.Single();
                    var cells = (IEnumerable<dynamic>)row;

                    // everything actually goes through what's below
                    //  but prove out this, worse, approach too
                    var firstCell = cells.First() as IConvertible;
                    var mapped = firstCell.ToType(typeof(sbyte), null);
                    Assert.Equal(sbyte.MinValue, (sbyte)mapped);

                    Assert.Collection(
                        cells,
                        // sbyte.MinValue
                        a =>
                        {
                            var c = a as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.SByte, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToSByte(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(sbyte.MinValue, c.ToSByte(null));
                            Assert.Equal(sbyte.MinValue, Convert.ToSByte(a as object));
                            Assert.Equal(sbyte.MinValue, c.ToInt16(null));
                            Assert.Equal(sbyte.MinValue, c.ToInt32(null));
                            Assert.Equal(sbyte.MinValue, c.ToInt64(null));
                            Assert.Equal(sbyte.MinValue, c.ToSingle(null));
                            Assert.Equal(sbyte.MinValue, c.ToDouble(null));
                            Assert.Equal(sbyte.MinValue, c.ToDecimal(null));
                            Assert.Equal(sbyte.MinValue.ToString(), c.ToString(null));

                            // not valid for signed
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // byte.MaxValue
                        b =>
                        {
                            var c = b as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.Byte, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToByte(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(byte.MaxValue, c.ToByte(null));
                            Assert.Equal(byte.MaxValue, Convert.ToByte(b as object));
                            Assert.Equal(byte.MaxValue, c.ToInt16(null));
                            Assert.Equal(byte.MaxValue, c.ToUInt16(null));
                            Assert.Equal(byte.MaxValue, c.ToInt32(null));
                            Assert.Equal(byte.MaxValue, c.ToUInt32(null));
                            Assert.Equal(byte.MaxValue, c.ToInt64(null));
                            Assert.Equal(byte.MaxValue, c.ToUInt64(null));
                            Assert.Equal(byte.MaxValue, c.ToSingle(null));
                            Assert.Equal(byte.MaxValue, c.ToDouble(null));
                            Assert.Equal(byte.MaxValue, c.ToDecimal(null));
                            Assert.Equal(byte.MaxValue.ToString(), c.ToString(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // short.MinValue
                        cc =>
                        {
                            var c = cc as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.Int16, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToInt16(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(short.MinValue, c.ToInt16(null));
                            Assert.Equal(short.MinValue, Convert.ToInt16(cc as object));
                            Assert.Equal(short.MinValue, c.ToInt32(null));
                            Assert.Equal(short.MinValue, c.ToInt64(null));
                            Assert.Equal(short.MinValue, c.ToSingle(null));
                            Assert.Equal(short.MinValue, c.ToDouble(null));
                            Assert.Equal(short.MinValue, c.ToDecimal(null));
                            Assert.Equal(short.MinValue.ToString(), c.ToString(null));

                            // not valid for signed
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // ushort.MaxValue
                        d =>
                        {
                            var c = d as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.UInt16, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToByte(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(ushort.MaxValue, c.ToUInt16(null));
                            Assert.Equal(ushort.MaxValue, Convert.ToUInt16(d as object));
                            Assert.Equal(ushort.MaxValue, c.ToInt32(null));
                            Assert.Equal(ushort.MaxValue, c.ToUInt32(null));
                            Assert.Equal(ushort.MaxValue, c.ToInt64(null));
                            Assert.Equal(ushort.MaxValue, c.ToUInt64(null));
                            Assert.Equal(ushort.MaxValue, c.ToSingle(null));
                            Assert.Equal(ushort.MaxValue, c.ToDouble(null));
                            Assert.Equal(ushort.MaxValue, c.ToDecimal(null));
                            Assert.Equal(ushort.MaxValue.ToString(), c.ToString(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // int.MinValue
                        e =>
                        {
                            var c = e as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.Int32, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToInt32(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(int.MinValue, c.ToInt32(null));
                            Assert.Equal(int.MinValue, Convert.ToInt32(e as object));
                            Assert.Equal(int.MinValue, c.ToInt64(null));
                            Assert.Equal(int.MinValue, c.ToSingle(null));
                            Assert.Equal(int.MinValue, c.ToDouble(null));
                            Assert.Equal(int.MinValue, c.ToDecimal(null));
                            Assert.Equal(int.MinValue.ToString(), c.ToString(null));

                            // not valid for signed
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // uint.MaxValue
                        f =>
                        {
                            var c = f as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.UInt32, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToUInt32(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(uint.MaxValue, c.ToUInt32(null));
                            Assert.Equal(uint.MaxValue, Convert.ToUInt32(f as object));
                            Assert.Equal(uint.MaxValue, c.ToInt64(null));
                            Assert.Equal(uint.MaxValue, c.ToUInt64(null));
                            Assert.Equal(uint.MaxValue, c.ToSingle(null));
                            Assert.Equal(uint.MaxValue, c.ToDouble(null));
                            Assert.Equal(uint.MaxValue, c.ToDecimal(null));
                            Assert.Equal(uint.MaxValue.ToString(), c.ToString(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt32(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // long.MinValue
                        g =>
                        {
                            var c = g as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.Int64, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToInt64(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(long.MinValue, c.ToInt64(null));
                            Assert.Equal(long.MinValue, Convert.ToInt64(g as object));
                            Assert.Equal(long.MinValue, c.ToSingle(null));
                            Assert.Equal(long.MinValue, c.ToDouble(null));
                            Assert.Equal(long.MinValue, c.ToDecimal(null));
                            Assert.Equal(long.MinValue.ToString(), c.ToString(null));

                            // not valid for signed
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // ulong.MaxValue
                        h =>
                        {
                            var c = h as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.UInt64, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToUInt64(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(ulong.MaxValue, c.ToUInt64(null));
                            Assert.Equal(ulong.MaxValue, Convert.ToUInt64(h as object));
                            Assert.Equal(ulong.MaxValue, c.ToSingle(null));
                            Assert.Equal(ulong.MaxValue, c.ToDouble(null));
                            Assert.Equal(ulong.MaxValue, c.ToDecimal(null));
                            Assert.Equal(ulong.MaxValue.ToString(), c.ToString(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt64(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // 123.45
                        i =>
                        {
                            var c = i as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.Double, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToDouble(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(123.45f, c.ToSingle(null));
                            Assert.Equal(123.45, c.ToDouble(null));
                            Assert.Equal(123.45, Convert.ToDouble(i as object));
                            Assert.Equal(123.45, (double)c.ToDecimal(null));
                            Assert.Equal("123.45", c.ToString(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));

                            // not valid because not integer
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // -456.789
                        j =>
                        {
                            var c = j as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.Double, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToDouble(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(-456.789f, c.ToSingle(null));
                            Assert.Equal(-456.789, c.ToDouble(null));
                            Assert.Equal(-456.789, Convert.ToDouble(j as object));
                            Assert.Equal(-456.789, (double)c.ToDecimal(null));
                            Assert.Equal("-456.789", c.ToString(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));

                            // not valid because not integer
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // 2020-03-15
                        k =>
                        {
                            var c = k as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.DateTime, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToDateTime(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal(new DateTime(2020, 03, 15, 0, 0, 0, DateTimeKind.Utc), c.ToDateTime(null));
                            Assert.Equal(new DateTime(2020, 03, 15, 0, 0, 0, DateTimeKind.Utc), Convert.ToDateTime(k as object));
                            Assert.Equal("2020-03-15", c.ToString(null));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));

                            // not valid because not number
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToSingle(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToDouble(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToDecimal(null));
                        },
                        // f
                        l =>
                        {
                            var c = l as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.Char, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToChar(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal('f', c.ToChar(null));
                            Assert.Equal('f', Convert.ToChar(l as object));
                            Assert.Equal("f", c.ToString(null));

                            // not valid because not number
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToSingle(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToDouble(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToDecimal(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // hello world
                        m =>
                        {
                            var c = m as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.String, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToString(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.Equal("hello world", c.ToString(null));
                            Assert.Equal("hello world", Convert.ToString(m as object));

                            // not valid because too small
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));

                            // not valid because not number
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToSingle(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToDouble(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToDecimal(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        },
                        // True
                        n =>
                        {
                            var c = n as IConvertible;
                            Assert.NotNull(c);
                            Assert.Equal(TypeCode.Boolean, c.GetTypeCode());

                            Assert.Throws<ArgumentException>(() => c.ToString(CultureInfo.InvariantCulture));

                            // all valid conversions
                            Assert.True(c.ToBoolean(null));
                            Assert.Equal("True", c.ToString(null));
                            Assert.True(Convert.ToBoolean(n as object));
                            Assert.Equal("True", Convert.ToString(n as object));

                            // not valid because not boolean
                            Assert.Throws<InvalidOperationException>(() => c.ToChar(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToSByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToByte(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt16(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt32(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToUInt64(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToSingle(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToDouble(null));
                            Assert.Throws<InvalidOperationException>(() => c.ToDecimal(null));

                            // not valid because bad form
                            Assert.Throws<InvalidOperationException>(() => c.ToDateTime(null));
                        }
                    );
                }
            }

            // null Parsers
            {
                var csvString =
                    "a,b,c,d,e,f,g,h,i,j,k,l,m,n\r\n" +
                    $"{sbyte.MinValue},{byte.MaxValue},{short.MinValue},{ushort.MaxValue},{int.MinValue},{uint.MaxValue},{long.MinValue},{ulong.MaxValue},123.45,-456.789,2020-03-15,f,hello world,True";

                var td = new _DynamicCellsIConvertible();
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(td).ToOptions();

                var conf = Configuration.ForDynamic(opts);
                using (var reader = new StringReader(csvString))
                using (var csv = conf.CreateReader(reader))
                {
                    var rows = csv.ReadAll();
                    var row = rows.Single();
                    var cells = (IEnumerable<dynamic>)row;

                    // everything actually goes through what's below
                    //  but prove out this, worse, approach too
                    var firstCell = cells.First() as IConvertible;
                    Assert.Equal(TypeCode.Object, firstCell.GetTypeCode());

                    Assert.Throws<InvalidOperationException>(() => firstCell.ToSByte(null));
                    Assert.Throws<InvalidOperationException>(() => Convert.ToSByte(firstCell, null));
                }
            }

            // doesn't need an async equivalent
        }

        private sealed class _DynamicCellsIConvertible : DefaultTypeDescriber
        {
            public override Parser GetDynamicCellParserFor(in ReadContext context, TypeInfo targetType)
            => null;
        }

        [Fact]
        public void IEnumerableOfDynamic()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B,C\r\n1,foo,2020-01-02\r\nfalse,c,100"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            r =>
                            {
                                IEnumerable<dynamic> e = r;
                                Assert.Collection(
                                    e,
                                    a => Assert.Equal(1, (int)a),
                                    a => Assert.Equal("foo", (string)a),
                                    a => Assert.Equal(DateTime.Parse("2020-01-02", CultureInfo.InvariantCulture, DateTimeStyles.None), (DateTime)a)
                                );
                            },
                            r =>
                            {
                                IEnumerable<dynamic> e = r;
                                Assert.Collection(
                                    e,
                                    a => Assert.False((bool)a),
                                    a => Assert.Equal('c', (char)a),
                                    a => Assert.Equal(100, (int)a)
                                );
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void ThrowsOnExcessColumns()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithExtraColumnTreatment(ExtraColumnTreatment.ThrowException).ToOptions();

            // with heaers
            {
                // fine, shouldn't throw
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,B\r\nhello,world\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", (string)a.A); Assert.Equal("world", (string)a.B); }
                            );
                        }
                    }
                );

                // should throw on second read
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz"))
                        using (var csv = config.CreateReader(reader))
                        {
                            Assert.True(csv.TryRead(out var row));
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal("world", (string)row.B);

                            Assert.Throws<InvalidOperationException>(() => csv.TryRead(out var row2));
                        }
                    }
                );
            }

            // without heaers
            {
                var noHeadOpts = Options.CreateBuilder(opts).WithReadHeader(ReadHeader.Never).ToOptions();

                // fine, shouldn't throw
                RunSyncDynamicReaderVariants(
                    noHeadOpts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("hello,world\r\n"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", (string)a[0]); Assert.Equal("world", (string)a[1]); }
                            );
                        }
                    }
                );

                // should throw on second read
                RunSyncDynamicReaderVariants(
                    noHeadOpts,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("hello,world\r\nfizz,buzz,bazz"))
                        using (var csv = config.CreateReader(reader))
                        {
                            Assert.True(csv.TryRead(out var row));
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal("world", (string)row[1]);

                            Assert.Throws<InvalidOperationException>(() => csv.TryRead(out var row2));
                        }
                    }
                );
            }
        }

        [Fact]
        public void IgnoreExcessColumns()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithExtraColumnTreatment(ExtraColumnTreatment.Ignore).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("hello", (string)a.A); Assert.Equal("world", (string)a.B); },
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("fizz", (string)a.A); Assert.Equal("buzz", (string)a.B); Assert.Throws<ArgumentOutOfRangeException>(() => a[2]); },
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("fe", (string)a.A); Assert.Equal("fi", (string)a.B); Assert.Throws<ArgumentOutOfRangeException>(() => a[2]); }
                        );
                    }
                }
            );

            var noHeaderOpts = Options.CreateBuilder(opts).WithReadHeader(ReadHeader.Never).ToOptions();

            RunSyncDynamicReaderVariants(
                noHeaderOpts,
                (config, getReader) =>
                {
                    using (var reader = getReader("hello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("hello", (string)a[0]); Assert.Equal("world", (string)a[1]); },
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("fizz", (string)a[0]); Assert.Equal("buzz", (string)a[1]); Assert.Throws<ArgumentOutOfRangeException>(() => a[2]); },
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("fe", (string)a[0]); Assert.Equal("fi", (string)a[1]); Assert.Throws<ArgumentOutOfRangeException>(() => a[2]); }
                        );
                    }
                }
            );
        }

        [Fact]
        public void AllowExcessColumns()
        {
            // with headers
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello", (string)a.A); Assert.Equal("world", (string)a.B); },
                            a => { Assert.Equal("fizz", (string)a.A); Assert.Equal("buzz", (string)a.B); Assert.Equal("bazz", (string)a[2]); },
                            a => { Assert.Equal("fe", (string)a.A); Assert.Equal("fi", (string)a.B); Assert.Equal("fo", (string)a[2]); Assert.Equal("fum", (string)a[3]); }
                        );
                    }
                }
            );

            var noHeadersOpts = Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions();

            RunSyncDynamicReaderVariants(
                noHeadersOpts,
                (config, getReader) =>
                {
                    using (var reader = getReader("hello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello", (string)a[0]); Assert.Equal("world", (string)a[1]); },
                            a => { Assert.Equal("fizz", (string)a[0]); Assert.Equal("buzz", (string)a[1]); Assert.Equal("bazz", (string)a[2]); },
                            a => { Assert.Equal("fe", (string)a[0]); Assert.Equal("fi", (string)a[1]); Assert.Equal("fo", (string)a[2]); Assert.Equal("fum", (string)a[3]); }
                        );
                    }
                }
            );
        }

        [Fact]
        public void UsingWithDynamicRow()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("Hello,World\r\nNope,Yes"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            row =>
                            {
                                using (row)
                                {
                                    Assert.Equal("Nope", (string)row.Hello);
                                    Assert.Equal("Yes", (string)row.World);
                                }
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void NonGenericRowEnumerable()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getReader) =>
                {
                    using (var reader = getReader("Hello,World\r\nFoo,Bar"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            row =>
                            {
                                System.Collections.IEnumerable e = row;
                                var i = e.GetEnumerator();

                                var ix = 0;
                                while (i.MoveNext())
                                {
                                    switch (ix)
                                    {
                                        case 0: Assert.Equal("Foo", (string)(dynamic)i.Current); break;
                                        case 1: Assert.Equal("Bar", (string)(dynamic)i.Current); break;
                                        default: throw new Exception();
                                    }

                                    ix++;
                                }
                            }
                        );
                    }
                }
            );
        }

        private sealed class _ChainedParsers_Context
        {
            public int Num { get; set; }
        }

        private sealed class _ChainedParsers_TypeDescriber : DefaultTypeDescriber
        {
            private readonly Parser P;

            public _ChainedParsers_TypeDescriber(Parser p)
            {
                P = p;
            }

            public override Parser GetDynamicCellParserFor(in ReadContext context, TypeInfo targetType)
            => P;
        }

        [Fact]
        public void ChainedParsers()
        {
            var p0 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 1)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val *= 2;

                        return true;
                    }
                );

            var p1 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 2)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val--;

                        return true;
                    }
                );

            var p2 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 3)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val = -(val << 3);

                        return true;
                    }
                );

            var p = p0.Else(p1).Else(p2);

            var td = new _ChainedParsers_TypeDescriber(p);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(td).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    var ctx = new _ChainedParsers_Context();

                    using (var reader = getReader("Foo\r\n1\r\n2\r\n3\r\n4"))
                    using (var csv = config.CreateReader(reader, ctx))
                    {
                        ctx.Num = 1;
                        Assert.True(csv.TryRead(out var r1));
                        var c1 = r1.Foo;
                        Assert.Equal(2, (int)c1);

                        ctx.Num = 2;
                        Assert.True(csv.TryRead(out var r2));
                        var c2 = r2.Foo;
                        Assert.Equal(1, (int)c2);

                        ctx.Num = 3;
                        Assert.True(csv.TryRead(out var r3));
                        var c3 = r3.Foo;
                        Assert.Equal(-(3 << 3), (int)c3);

                        ctx.Num = 4;
                        Assert.True(csv.TryRead(out var r4));
                        var c4 = r4.Foo;
                        Assert.Throws<InvalidOperationException>(() => (int)c4);
                    }
                }
            );
        }

        private sealed class _ChainedDynamicRowConverters
        {
            public readonly string Value;
            public readonly int Number;

            public _ChainedDynamicRowConverters(string val, int num)
            {
                Value = val;
                Number = num;
            }
        }

        private sealed class _ChainedDynamicRowConverters_Context
        {
            public int Num { get; set; }
        }

        private sealed class _ChainedDynamicRowConverters_TypeDescriber : DefaultTypeDescriber
        {
            private readonly DynamicRowConverter C;

            public _ChainedDynamicRowConverters_TypeDescriber(DynamicRowConverter c)
            {
                C = c;
            }

            public override DynamicRowConverter GetDynamicRowConverter(in ReadContext context, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => C;
        }

        [Fact]
        public void ChainedDynamicRowConverters()
        {
            var c1 =
                DynamicRowConverter.ForDelegate(
                    (dynamic row, in ReadContext ctx, out _ChainedDynamicRowConverters res) =>
                    {
                        var c = (_ChainedDynamicRowConverters_Context)ctx.Context;
                        if (c.Num != 1)
                        {
                            res = null;
                            return false;
                        }

                        res = new _ChainedDynamicRowConverters((string)row[0], 1);
                        return true;
                    }
                );
            var c2 =
                DynamicRowConverter.ForDelegate(
                    (dynamic row, in ReadContext ctx, out _ChainedDynamicRowConverters res) =>
                    {
                        var c = (_ChainedDynamicRowConverters_Context)ctx.Context;
                        if (c.Num != 2)
                        {
                            res = null;
                            return false;
                        }

                        res = new _ChainedDynamicRowConverters((string)row[0], 2);
                        return true;
                    }
                );
            var c3 =
                DynamicRowConverter.ForDelegate(
                    (dynamic row, in ReadContext ctx, out _ChainedDynamicRowConverters res) =>
                    {
                        var c = (_ChainedDynamicRowConverters_Context)ctx.Context;
                        if (c.Num != 3)
                        {
                            res = null;
                            return false;
                        }

                        res = new _ChainedDynamicRowConverters((string)row[0], 3);
                        return true;
                    }
                );

            var c = c1.Else(c2).Else(c3);

            var td = new _ChainedDynamicRowConverters_TypeDescriber(c);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(td).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    var ctx = new _ChainedDynamicRowConverters_Context();
                    using (var reader = getReader("Foo\r\nabc\r\ndef\r\nghi\r\n123"))
                    using (var csv = config.CreateReader(reader, ctx))
                    {
                        ctx.Num = 1;
                        Assert.True(csv.TryRead(out var r1));
                        _ChainedDynamicRowConverters s1 = r1;
                        Assert.Equal("abc", s1.Value);
                        Assert.Equal(1, s1.Number);

                        ctx.Num = 2;
                        Assert.True(csv.TryRead(out var r2));
                        _ChainedDynamicRowConverters s2 = r2;
                        Assert.Equal("def", s2.Value);
                        Assert.Equal(2, s2.Number);

                        ctx.Num = 3;
                        Assert.True(csv.TryRead(out var r3));
                        _ChainedDynamicRowConverters s3 = r3;
                        Assert.Equal("ghi", s3.Value);
                        Assert.Equal(3, s3.Number);

                        ctx.Num = 4;
                        Assert.True(csv.TryRead(out var r4));
                        Assert.Throws<InvalidOperationException>(() => { _ChainedDynamicRowConverters s4 = r4; });
                    }
                }
            );
        }

        [Fact]
        public void WhitespaceTrimming()
        {
            // in values
            {
                // leading
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimLeadingInValues).ToOptions();

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\",\"\t\t\t789\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // trailing
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimTrailingInValues).ToOptions();

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo   ,Bar   \r\nhello,123\r\nworld   ,456\t\r\n\"fizz\t \n\",\"789\t\t\t\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // both
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimInValues).ToOptions();

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("   Foo   ,   Bar   \r\nhello,123\r\n\tworld   ,   456\t\r\n\"\tfizz\t \n\",\"\t789\t\t\t\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // outside of values
            {
                // leading
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues).ToOptions();

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\", \t \"789\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("\t \nfizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // trailing
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimAfterValues).ToOptions();

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo  ,\"Bar\"  \r\nhello,123\r\nworld   ,456\t\r\n\"fizz\t \n\",\"789\" \t "))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz\t \n", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // leading
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues).ToOptions();

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\", \t \"789\""))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("\t \nfizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // both
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues | WhitespaceTreatments.TrimAfterValues).ToOptions();

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo  ,\t\"Bar\"  \r\nhello,123\r\n\t world   ,456\t\r\n  \"fizz\t \n\",  \"789\" \t "))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz\t \n", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // inside and outside of values
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.Trim).ToOptions();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("  \"  Foo  \"  ,\t\t\"\tBar\t\"  \r\nhello,123\r\n\t world   ,456\t\r\n  \"fizz\t \n\",  \"  789\r\n\" \t "))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", (string)a.Foo);
                                    Assert.Equal(123, (int)a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("world", (string)a.Foo);
                                    Assert.Equal(456, (int)a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("fizz", (string)a.Foo);
                                    Assert.Equal(789, (int)a.Bar);
                                }
                            );
                        }
                    }
                );
            }

            // none
            {
                // no changes in values
                RunSyncDynamicReaderVariants(
                    Options.DynamicDefault,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("Foo,\"Bar\"\r\nhello\t,123\r\n  world,456\r\n\"\r\nfizz\",\"789\""))
                        using (var csv = config.CreateReader(reader))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello\t", (string)a.Foo);
                                    Assert.Equal(123, (int)a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("  world", (string)a.Foo);
                                    Assert.Equal(456, (int)a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("\r\nfizz", (string)a.Foo);
                                    Assert.Equal(789, (int)a.Bar);
                                }
                            );
                        }
                    }
                );

                // bad headers
                {
                    // leading value
                    RunSyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("  Foo,\"Bar\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("foo", (string)a["  Foo"]);
                                        Assert.Equal(123, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // trailing value
                    RunSyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo\t,\"Bar\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("foo", (string)a["Foo\t"]);
                                        Assert.Equal(123, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // leading value, escaped
                    RunSyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo,\"  Bar\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("foo", (string)a.Foo);
                                        Assert.Equal(123, (int)a["  Bar"]);
                                    }
                                );
                            }
                        }
                    );

                    // leading value, escaped, exceptional
                    RunSyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo,\t\"  Bar\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                Assert.Throws<InvalidOperationException>(() => csv.ReadAll());
                            }
                        }
                    );

                    // trailing value, escaped
                    RunSyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo,\"Bar\r\n\"\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var rows = csv.ReadAll();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("foo", (string)a.Foo);
                                        Assert.Equal(123, (int)a["Bar\r\n"]);
                                    }
                                );
                            }
                        }
                    );

                    // trailing value, escaped, exceptional
                    RunSyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader("Foo,\"Bar\r\n\"\t\t\r\nfoo,123"))
                            using (var csv = config.CreateReader(reader))
                            {
                                Assert.Throws<InvalidOperationException>(() => csv.ReadAll());
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public void DynamicRowMemberNameEnumerators()
        {
            // generic
            {

                // with headers
                {
                    var config = Configuration.ForDynamic();
                    using (var txt = new StringReader("foo,bar\r\n1,2"))
                    using (var csv = config.CreateReader(txt))
                    {
                        Assert.True(csv.TryRead(out var row));

                        Assert.Equal(1, (int)row[0]);
                        Assert.Equal(2, (int)row[1]);

                        var dynRow = row as DynamicRow;
                        var e = new DynamicRowMemberNameEnumerable(dynRow);

                        // generic
                        using (var i = e.GetEnumerator())
                        {
                            // iter 1
                            {
                                var ix = 0;
                                while (i.MoveNext())
                                {
                                    switch (ix)
                                    {
                                        case 0: Assert.Equal("foo", i.Current); break;
                                        case 1: Assert.Equal("bar", i.Current); break;
                                        default: throw new Exception();
                                    }

                                    ix++;
                                }

                                Assert.Equal(2, ix);

                                // too far
                                Assert.False(i.MoveNext());
                            }

                            i.Reset();

                            // iter 2
                            {
                                var ix = 0;
                                while (i.MoveNext())
                                {
                                    switch (ix)
                                    {
                                        case 0: Assert.Equal("foo", i.Current); break;
                                        case 1: Assert.Equal("bar", i.Current); break;
                                        default: throw new Exception();
                                    }

                                    ix++;
                                }

                                Assert.Equal(2, ix);

                                // too far
                                Assert.False(i.MoveNext());
                            }
                        }
                    }
                }

                // without headers
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions();
                    var config = Configuration.ForDynamic(opts);
                    using (var txt = new StringReader("1,2"))
                    using (var csv = config.CreateReader(txt))
                    {
                        Assert.True(csv.TryRead(out var row));

                        Assert.Equal(1, (int)row[0]);
                        Assert.Equal(2, (int)row[1]);

                        var dynRow = row as DynamicRow;
                        var e = new DynamicRowMemberNameEnumerable(dynRow);
                        using (var i = e.GetEnumerator())
                        {
                            // iter 1
                            {
                                var ix = 0;
                                while (i.MoveNext())
                                {
                                    ix++;
                                }

                                Assert.Equal(0, ix);

                                // too far
                                Assert.False(i.MoveNext());
                            }

                            i.Reset();

                            // iter 2
                            {
                                var ix = 0;
                                while (i.MoveNext())
                                {
                                    ix++;
                                }

                                Assert.Equal(0, ix);

                                // too far
                                Assert.False(i.MoveNext());
                            }
                        }
                    }
                }
            }

            // non-generic
            {

                // with headers
                {
                    var config = Configuration.ForDynamic();
                    using (var txt = new StringReader("foo,bar\r\n1,2"))
                    using (var csv = config.CreateReader(txt))
                    {
                        Assert.True(csv.TryRead(out var row));

                        Assert.Equal(1, (int)row[0]);
                        Assert.Equal(2, (int)row[1]);

                        var dynRow = row as DynamicRow;
                        System.Collections.IEnumerable e = new DynamicRowMemberNameEnumerable(dynRow);

                        {
                            var i = e.GetEnumerator();

                            // iter 1
                            {
                                var ix = 0;
                                while (i.MoveNext())
                                {
                                    switch (ix)
                                    {
                                        case 0: Assert.Equal("foo", (string)i.Current); break;
                                        case 1: Assert.Equal("bar", (string)i.Current); break;
                                        default: throw new Exception();
                                    }

                                    ix++;
                                }

                                Assert.Equal(2, ix);

                                // too far
                                Assert.False(i.MoveNext());
                            }

                            i.Reset();

                            // iter 2
                            {
                                var ix = 0;
                                while (i.MoveNext())
                                {
                                    switch (ix)
                                    {
                                        case 0: Assert.Equal("foo", (string)i.Current); break;
                                        case 1: Assert.Equal("bar", (string)i.Current); break;
                                        default: throw new Exception();
                                    }

                                    ix++;
                                }

                                Assert.Equal(2, ix);

                                // too far
                                Assert.False(i.MoveNext());
                            }
                        }
                    }
                }

                // without headers
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions();
                    var config = Configuration.ForDynamic(opts);
                    using (var txt = new StringReader("1,2"))
                    using (var csv = config.CreateReader(txt))
                    {
                        Assert.True(csv.TryRead(out var row));

                        Assert.Equal(1, (int)row[0]);
                        Assert.Equal(2, (int)row[1]);

                        var dynRow = row as DynamicRow;
                        System.Collections.IEnumerable e = new DynamicRowMemberNameEnumerable(dynRow);
                        {
                            var i = e.GetEnumerator();

                            // iter 1
                            {
                                var ix = 0;
                                while (i.MoveNext())
                                {
                                    ix++;
                                }

                                Assert.Equal(0, ix);

                                // too far
                                Assert.False(i.MoveNext());
                            }

                            i.Reset();

                            // iter 2
                            {
                                var ix = 0;
                                while (i.MoveNext())
                                {
                                    ix++;
                                }

                                Assert.Equal(0, ix);

                                // too far
                                Assert.False(i.MoveNext());
                            }
                        }
                    }
                }
            }
        }

        private sealed class _CustomRowConverters
        {
#pragma warning disable CS0649
            public int Field;
#pragma warning restore CS0649
            public static int StaticField;

            internal int ByMethod;
            public void Method(int a) { ByMethod = a; }

            internal static int ByStaticMethod;
            public static void StaticMethod(int v) { ByStaticMethod = v; }

            public int Delegate { get; set; }
            public static int StaticDelegate { get; set; }
        }

        private sealed class _CustomRowConverters_TypeDescriber : ITypeDescriber
        {
            private readonly DynamicRowConverter Converter;

            public _CustomRowConverters_TypeDescriber(DynamicRowConverter converter)
            {
                Converter = converter;
            }

            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            {
                throw new NotImplementedException();
            }

            public int GetCellsForDynamicRow(in WriteContext ctx, object row, Span<DynamicCellValue> cells)
            => throw new NotImplementedException();

            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            => TypeDescribers.Default.GetDynamicCellParserFor(in ctx, targetType);

            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            {
                return Converter;
            }

            public InstanceProvider GetInstanceProvider(TypeInfo forType)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void CustomRowConverters()
        {
            var t = typeof(_CustomRowConverters);

            var cons = t.GetConstructor(Type.EmptyTypes);
            Assert.NotNull(cons);

            var field = Setter.ForField(t.GetField(nameof(_CustomRowConverters.Field)));
            var staticField = Setter.ForField(t.GetField(nameof(_CustomRowConverters.StaticField)));
            var method = Setter.ForMethod(t.GetMethod(nameof(_CustomRowConverters.Method)));
            var staticMethod = Setter.ForMethod(t.GetMethod(nameof(_CustomRowConverters.StaticMethod)));
            var del = Setter.ForDelegate((_CustomRowConverters row, int v, in ReadContext _) => row.Delegate = v);
            var staticDel = Setter.ForDelegate((int v, in ReadContext _) => _CustomRowConverters.StaticDelegate = v);

            var setters = new[] { field, staticField, method, staticMethod, del, staticDel };

            var cols =
                new[]
                {
                    ColumnIdentifier.Create(0),
                    ColumnIdentifier.Create(1),
                    ColumnIdentifier.Create(2),
                    ColumnIdentifier.Create(3),
                    ColumnIdentifier.Create(4),
                    ColumnIdentifier.Create(5),

                };

            var converter =
                DynamicRowConverter.ForEmptyConstructorAndSetters(
                    cons,
                    setters,
                    cols
                );

            var describer = new _CustomRowConverters_TypeDescriber(converter);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(describer).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B,C,D,E,F\r\n1,2,3,4,5,6\r\n7,8,9,0,1,2"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();

                        Assert.Collection(
                            rows,
                            a =>
                            {
                                _CustomRowConverters.ByStaticMethod = default;
                                _CustomRowConverters.StaticDelegate = default;
                                _CustomRowConverters.StaticField = default;

                                var val = (_CustomRowConverters)a;
                                Assert.Equal(1, val.Field);
                                Assert.Equal(2, _CustomRowConverters.StaticField);
                                Assert.Equal(3, val.ByMethod);
                                Assert.Equal(4, _CustomRowConverters.ByStaticMethod);
                                Assert.Equal(5, val.Delegate);
                                Assert.Equal(6, _CustomRowConverters.StaticDelegate);
                            },
                            b =>
                            {
                                _CustomRowConverters.ByStaticMethod = default;
                                _CustomRowConverters.StaticDelegate = default;
                                _CustomRowConverters.StaticField = default;

                                var val = (_CustomRowConverters)b;
                                Assert.Equal(7, val.Field);
                                Assert.Equal(8, _CustomRowConverters.StaticField);
                                Assert.Equal(9, val.ByMethod);
                                Assert.Equal(0, _CustomRowConverters.ByStaticMethod);
                                Assert.Equal(1, val.Delegate);
                                Assert.Equal(2, _CustomRowConverters.StaticDelegate);
                            }
                        );

                    }
                }
            );
        }

        [Fact]
        public void GetDynamicMemberNames()
        {
            RunSyncDynamicReaderVariants(
                Options.DynamicDefault,
                (config, getTextReader) =>
                {
                    using (var reader = getTextReader("Hello,World,Foo,Bar\r\n1,2,3,4"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            r =>
                            {
                                var provider = r as IDynamicMetaObjectProvider;
                                var metaObj = provider.GetMetaObject(System.Linq.Expressions.Expression.Variable(typeof(object)));
                                var names = metaObj.GetDynamicMemberNames();

                                var ix = 0;
                                foreach (var n in names)
                                {
                                    var v = r[n];
                                    switch (n)
                                    {
                                        case "Hello": Assert.Equal(1, (int)v); Assert.Equal(0, ix); break;
                                        case "World": Assert.Equal(2, (int)v); Assert.Equal(1, ix); break;
                                        case "Foo": Assert.Equal(3, (int)v); Assert.Equal(2, ix); break;
                                        case "Bar": Assert.Equal(4, (int)v); Assert.Equal(3, ix); break;
                                        default:
                                            Assert.Null("Shouldn't be possible");
                                            break;
                                    }

                                    ix++;
                                }
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void WithComments()
        {
            // \r\n
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).WithReadHeader(ReadHeader.Always).ToOptions();
                var opts2 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).WithReadHeader(ReadHeader.Never).ToOptions();

                // with headers
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\r\n#comment\rwhatever\r\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\nA,Nope\r\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\r\n#again!###foo###"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \r
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).WithReadHeader(ReadHeader.Always).ToOptions();
                var opts2 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).WithReadHeader(ReadHeader.Never).ToOptions();

                // with headers
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\r#comment\nwhatever\rhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\rA,Nope\rhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\rhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\nwhatever\r#again!###foo###"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \n
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).WithReadHeader(ReadHeader.Always).ToOptions();
                var opts2 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).WithReadHeader(ReadHeader.Never).ToOptions();

                // with headers
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("A,Nope\n#comment\rwhatever\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\nA,Nope\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\nhello,123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                RunSyncDynamicReaderVariants(
                    opts2,
                    (config, getReader) =>
                    {
                        using (var reader = getReader("#comment\rwhatever\n#again!###foo###"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var res1 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = csv.TryReadWithComment();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }
        }

        [Fact]
        public void WeirdComments()
        {
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).WithReadHeader(ReadHeader.Always).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\r\nhello,world\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\n#this is a test comment!\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).WithReadHeader(ReadHeader.Always).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\rhello,world\rfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r#this is a test comment!\n\rfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).WithReadHeader(ReadHeader.Always).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\r\nhello,world\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                RunSyncDynamicReaderVariants(
                   opts,
                   (config, getReader) =>
                   {
                       var CSV = "#this is a test comment!\r\r\nhello,world\r\nfoo,bar";
                       using (var str = getReader(CSV))
                       using (var csv = config.CreateReader(str))
                       {
                           var rows = csv.ReadAll();
                           Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                       }
                   }
               );

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\n\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\r\r\nfoo,bar";
                        using (var str = getReader(CSV))
                        using (var csv = config.CreateReader(str))
                        {
                            var rows = csv.ReadAll();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }
        }

        [Fact]
        public void Comments()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithReadHeader(ReadHeader.Always).ToOptions();

            // comment first line
            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    var CSV = "#this is a test comment!\r\nhello,world\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );

            // comment after header
            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\n#this is a test comment\r\nfoo,bar";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );

            // comment between rows
            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!\r\nfizz,buzz";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); },
                            b => { Assert.Equal("fizz", (string)b.hello); Assert.Equal("buzz", (string)b.world); }
                        );
                    }
                }
            );

            // comment at end
            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!";
                    using (var str = getReader(CSV))
                    using (var csv = config.CreateReader(str))
                    {
                        var rows = csv.ReadAll();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );
        }

        [Fact]
        public void RangeUseableAfterDisposeOrReuse()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var row));
                        Assert.False(csv.TryRead(out _));

                        Assert.Equal("1", (string)row[0]);
                        Assert.Equal("2", (string)row[1]);
                        Assert.Equal("3", (string)row[2]);

                        var subset = row[1..3];
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        row.Dispose();

                        Assert.Throws<ObjectDisposedException>(() => row[0]);

                        // subset should still be good here
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        subset.Dispose();

                        Assert.Throws<ObjectDisposedException>(() => subset[0]);
                    }
                }
            );

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3\r\n4,5,6"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var row));

                        Assert.Equal("1", (string)row[0]);
                        Assert.Equal("2", (string)row[1]);
                        Assert.Equal("3", (string)row[2]);

                        var subset = row[1..3];
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        Assert.True(csv.TryReadWithReuse(ref row));
                        Assert.False(csv.TryRead(out _));

                        // row has now changed
                        Assert.Equal("4", (string)row[0]);
                        Assert.Equal("5", (string)row[1]);
                        Assert.Equal("6", (string)row[2]);

                        // subset should be unmodified
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        row.Dispose();

                        Assert.Throws<ObjectDisposedException>(() => row[0]);

                        // subset should still be good
                        Assert.Equal("2", (string)subset[0]);
                        Assert.Equal("3", (string)subset[1]);

                        subset.Dispose();

                        Assert.Throws<ObjectDisposedException>(() => subset[0]);
                    }
                }
            );
        }

        [Fact]
        public void Range()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            var equivalent = new[] { "1", "2", "3" };

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var row));
                        Assert.False(csv.TryRead(out _));

                        // range checks
                        var startLessThan0 = new Range(^4, 3);
                        var endLessthan0 = new Range(0, ^4);
                        var startAfterWidth = new Range(4, 3);
                        var endAfterWidth = new Range(0, 4);
                        var startAfterEnd = new Range(2, 1);

                        Assert.Throws<ArgumentOutOfRangeException>(() => row[startLessThan0]);
                        Assert.Throws<ArgumentOutOfRangeException>(() => row[endLessthan0]);
                        Assert.Throws<ArgumentOutOfRangeException>(() => row[startAfterWidth]);
                        Assert.Throws<ArgumentOutOfRangeException>(() => row[endAfterWidth]);
                        Assert.Throws<ArgumentException>(() => row[startAfterEnd]);
                        // end range

                        var all = 0..3;
                        var allEnd = ^3..^0;
                        var allImp = ..;

                        var skip1Front = 1..3;
                        var skip1FrontImp = 1..;
                        var skip1Back = 0..2;
                        var skip1BackImp = ..2;
                        var skip1FrontEnd = ^2..^0;
                        var skip1FrontEndImp = ^2..;
                        var skip1BackEnd = ^3..^1;
                        var skip1BackEndImp = ..^1;

                        var skip2Front = 2..3;
                        var skip2FrontImp = 2..;
                        var skip2Back = 0..1;
                        var skip2BackImp = ..1;
                        var skip2FrontEnd = ^1..^0;
                        var skip2FrontEndImp = ^1..;
                        var skip2BackEnd = ^3..^2;
                        var skip2BackEndImp = ..^2;

                        var emptyZero = 0..0;
                        var emptyZeroEnd = ^0..^0;
                        var emptyOne = 1..1;
                        var emptyOneEnd = ^1..^1;
                        var emptyTwo = 2..2;
                        var emptyTwoEnd = ^2..^2;

                        Check(all);
                        Check(allEnd);
                        Check(allImp);

                        Check(skip1Front);
                        Check(skip1FrontImp);
                        Check(skip1Back);
                        Check(skip1BackImp);
                        Check(skip1FrontEnd);
                        Check(skip1FrontEndImp);
                        Check(skip1BackEnd);
                        Check(skip1BackEndImp);

                        Check(skip2Front);
                        Check(skip2FrontImp);
                        Check(skip2Back);
                        Check(skip2BackImp);
                        Check(skip2FrontEnd);
                        Check(skip2FrontEndImp);
                        Check(skip2BackEnd);
                        Check(skip2BackEndImp);

                        Check(emptyZero);
                        Check(emptyZeroEnd);
                        Check(emptyOne);
                        Check(emptyOneEnd);
                        Check(emptyTwo);
                        Check(emptyTwoEnd);

                        void Check(Range range)
                        {
                            var dynRes = row[range];
                            var shouldMatchRes = equivalent[range];

                            for (var i = 0; i < shouldMatchRes.Length; i++)
                            {
                                Assert.Equal(shouldMatchRes[i], (string)dynRes[i]);
                            }

                            var ix = 0;
                            foreach (string val in dynRes)
                            {
                                Assert.Equal(shouldMatchRes[ix], val);
                                ix++;
                            }
                        }
                    }
                }
            );
        }

        [Fact]
        public void Index()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("a,b,c\r\n1,2,3"))
                    using (var csv = config.CreateReader(reader))
                    {
                        Assert.True(csv.TryRead(out var row));
                        Assert.False(csv.TryRead(out _));

                        var zeroFromStart = new Index(0, false);
                        var oneFromStart = new Index(1, false);
                        var twoFromStart = new Index(2, false);

                        var oneFromEnd = new Index(1, true);
                        var twoFromEnd = new Index(2, true);
                        var threeFromEnd = new Index(3, true);

                        int a1 = row[zeroFromStart];
                        int b1 = row[oneFromStart];
                        int c1 = row[twoFromStart];

                        int a2 = row[threeFromEnd];
                        int b2 = row[twoFromEnd];
                        int c2 = row[oneFromEnd];

                        int a3 = row[(Index)0];
                        int b3 = row[(Index)1];
                        int c3 = row[(Index)2];

                        int a4 = row[^3];
                        int b4 = row[^2];
                        int c4 = row[^1];

                        Assert.Equal(1, a1);
                        Assert.Equal(1, a2);
                        Assert.Equal(1, a3);
                        Assert.Equal(1, a4);

                        Assert.Equal(2, b1);
                        Assert.Equal(2, b2);
                        Assert.Equal(2, b3);
                        Assert.Equal(2, b4);

                        Assert.Equal(3, c1);
                        Assert.Equal(3, c2);
                        Assert.Equal(3, c3);
                        Assert.Equal(3, c4);
                    }
                }
            );
        }

        [Fact]
        public void DynamicCellErrors()
        {
            // missing conversion
            {
                var row = MakeRow();
                var cell = row[0];

                dynamic o = null;

                o = cell;
                Assert.Throws<InvalidOperationException>(Cast);

                o = this;
                Cast();

                row.Dispose();

                void Cast()
                {
                    GC.KeepAlive((DynamicReaderTests)o);
                }
            }

            // bad conversion
            {
                ParserDelegate<Guid> parser =
                    (ReadOnlySpan<char> data, in ReadContext ctx, out Guid val) =>
                    {
                        val = Guid.NewGuid();
                        return true;
                    };
                var conv = new _DynamicRowOrCellErrors((Parser)parser);

                var row = MakeRow(conv);
                var cell = row[0];

                Assert.Throws<InvalidOperationException>(() => _Cast<(int, int, int)>(cell));

                _Cast<Guid>(cell);

                row.Dispose();
            }

            // create a test row
            static dynamic MakeRow(ITypeDescriber c = null)
            {
                var opts =
                    Options.CreateBuilder(Options.Default)
                        .WithReadHeader(ReadHeader.Never)
                        .WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose)
                        .WithTypeDescriber(c ?? TypeDescribers.Default)
                        .ToOptions();
                var config = Configuration.ForDynamic(opts);

                using (var str = new System.IO.StringReader("1,2,3"))
                using (var csv = config.CreateReader(str))
                {
                    var rows = csv.ReadAll();

                    return rows.Single();
                }
            }
        }

        [Fact]
        public void ChangingRowIndexTypes()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
            var config = Configuration.ForDynamic(opts);

            using (var str = new System.IO.StringReader("a,b,c\r\n1,2,3"))
            using (var csv = config.CreateReader(str))
            {
                var row = csv.ReadAll().Single();

                var ix = 1;
                var key = "c";

                dynamic lookup = null;

                lookup = ix;
                int b = Get();

                lookup = key;
                int c = Get();

                Assert.Equal(2, b);
                Assert.Equal(3, c);

                dynamic Get()
                {
                    return row[lookup];
                }
            }
        }

        private class _DynamicRowOrCellErrors : ITypeDescriber
        {
            private readonly Parser P;
            private readonly DynamicRowConverter D;

            public _DynamicRowOrCellErrors(Parser p)
            {
                P = p;
                D = null;
            }

            public _DynamicRowOrCellErrors(DynamicRowConverter d)
            {
                P = null;
                D = d;
            }

            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            => P ?? TypeDescribers.Default.GetDynamicCellParserFor(in ctx, targetType);

            public int GetCellsForDynamicRow(in WriteContext ctx, object row, Span<DynamicCellValue> cells)
            => TypeDescribers.Default.GetCellsForDynamicRow(in ctx, row, cells);

            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => D ?? TypeDescribers.Default.GetDynamicRowConverter(in ctx, columns, targetType);

            public InstanceProvider GetInstanceProvider(TypeInfo forType)
            => TypeDescribers.Default.GetInstanceProvider(forType);

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToSerialize(forType);

            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToDeserialize(forType);
        }

        [Fact]
        public void DynamicRowErrors()
        {
            // bad indexing
            {
                var row = MakeRow();
                var correct = new int[2, 2];

                dynamic o = null;

                o = row;
                Assert.Throws<InvalidOperationException>(TwoIndexes);
                o = correct;
                TwoIndexes();

                row.Dispose();

                void TwoIndexes()
                {
                    GC.KeepAlive(o[0, 1]);
                }
            }

            // bad index type
            {
                var row = MakeRow();
                var correct = new Dictionary<Guid, Guid>();
                var key = Guid.NewGuid();
                correct[key] = Guid.NewGuid();

                dynamic o = null;

                o = row;
                Assert.Throws<InvalidOperationException>(GuidIndex);
                o = correct;
                GuidIndex();

                row.Dispose();

                void GuidIndex()
                {
                    GC.KeepAlive(o[key]);
                }
            }

            // out of range, index
            {
                var row = MakeRow();

                Assert.Throws<ArgumentOutOfRangeException>(() => row[-1]);
                Assert.Throws<ArgumentOutOfRangeException>(() => row[100]);

                row.Dispose();
            }

            // out of range, System.Index
            {
                var row = MakeRow();

                Assert.Throws<ArgumentOutOfRangeException>(() => row[(System.Index)100]);

                row.Dispose();
            }

            // out of range, range
            {
                var row = MakeRow();

                Assert.Throws<ArgumentOutOfRangeException>(() => row[-1..2]);
                Assert.Throws<ArgumentOutOfRangeException>(() => row[1..16]);

                row.Dispose();
            }

            // missing row conversion
            {
                var row = MakeRow();
                var ok = Guid.NewGuid();
                dynamic o = null;

                o = row;
                Assert.Throws<InvalidOperationException>(Cast);
                o = ok;
                Cast();

                row.Dispose();

                void Cast()
                {
                    GC.KeepAlive((Guid)o);
                }
            }

            // bad row conversion
            {
                DynamicRowConverterDelegate<ValueTuple<int, int, int>> del =
                    (dynamic row, in ReadContext ctx, out ValueTuple<int, int, int> res) =>
                    {
                        int a = row[0];
                        int b = row[1];
                        int c = row[2];

                        res = (a, b, c);

                        return true;
                    };
                var conv = new _DynamicRowOrCellErrors((DynamicRowConverter)del);

                var row = MakeRow(conv);

                Assert.Throws<InvalidOperationException>(() => _Cast<Guid>(row));

                _Cast<(int A, int B, int C)>(row);

                row.Dispose();
            }

            // re-use row while enumerating
            {
                var row = MakeRow();

                using (var e = ((IEnumerable<int>)row).GetEnumerator())
                {
                    Assert.True(e.MoveNext());
                    Assert.Equal(1, e.Current);

                    var opts =
                    Options.CreateBuilder(Options.Default)
                        .WithReadHeader(ReadHeader.Never)
                        .WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose)
                        .WithTypeDescriber(TypeDescribers.Default)
                        .ToOptions();
                    var config = Configuration.ForDynamic(opts);

                    using (var str = new System.IO.StringReader("4,5,6"))
                    using (var csv = config.CreateReader(str))
                    {
                        var res = csv.TryReadWithReuse(ref row);
                        Assert.True(res);
                    }

                    Assert.Throws<InvalidOperationException>(() => e.MoveNext());
                }
            }

            // re-use row while enumerating (non-generic)
            {
                var row = MakeRow();

                var e = ((System.Collections.IEnumerable)row).GetEnumerator();
                Assert.True(e.MoveNext());
                dynamic obj = e.Current;
                Assert.Equal(1, (int)obj);

                var opts =
                Options.CreateBuilder(Options.Default)
                    .WithReadHeader(ReadHeader.Never)
                    .WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose)
                    .WithTypeDescriber(TypeDescribers.Default)
                    .ToOptions();
                var config = Configuration.ForDynamic(opts);

                using (var str = new System.IO.StringReader("4,5,6"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.TryReadWithReuse(ref row);
                    Assert.True(res);
                }

                Assert.Throws<InvalidOperationException>(() => e.MoveNext());
            }

            // missing key
            {
                var row = MakeRow();

                Assert.Throws<KeyNotFoundException>(() => row["foo"]);
            }

            // create a test row
            static dynamic MakeRow(ITypeDescriber c = null)
            {
                var opts =
                    Options.CreateBuilder(Options.Default)
                        .WithReadHeader(ReadHeader.Never)
                        .WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose)
                        .WithTypeDescriber(c ?? TypeDescribers.Default)
                        .ToOptions();
                var config = Configuration.ForDynamic(opts);

                using (var str = new System.IO.StringReader("1,2,3"))
                using (var csv = config.CreateReader(str))
                {
                    var rows = csv.ReadAll();

                    return rows.Single();
                }
            }
        }

        private static T _Cast<T>(dynamic row)
        => (T)row;

        private class _CustomDynamicCellConverter : ITypeDescriber
        {
            private readonly Dictionary<TypeInfo, Parser> Lookup;

            public _CustomDynamicCellConverter()
            {
                Lookup = new Dictionary<TypeInfo, Parser>();
            }

            public void Add(TypeInfo targetType, Parser converter)
            => Lookup.Add(targetType, converter);

            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            => Lookup[targetType];

            public int GetCellsForDynamicRow(in WriteContext ctx, object row, Span<DynamicCellValue> cells)
            => 0;

            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => TypeDescribers.Default.GetDynamicRowConverter(in ctx, columns, targetType);

            public InstanceProvider GetInstanceProvider(TypeInfo forType)
            => TypeDescribers.Default.GetInstanceProvider(forType);

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToSerialize(forType);

            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToDeserialize(forType);
        }

        [Fact]
        public void CustomDynamicCellConverter()
        {
            // method
            {
                var converter = new _CustomDynamicCellConverter();
                var mtd = typeof(DynamicReaderTests).GetMethod(nameof(_CustomDynamicCellConverter_Int), BindingFlags.Public | BindingFlags.Static);
                var cellConverter = Parser.ForMethod(mtd);

                converter.Add(typeof(int).GetTypeInfo(), cellConverter);

                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(converter).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Int_Calls = 0;

                        using (var str = getReader("a,bb,ccc"))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.True(csv.TryRead(out var t1));

                            var res1 = (int)(t1[0]);
                            Assert.Equal(3, res1);
                            Assert.Equal(1, _CustomDynamicCellConverter_Int_Calls);

                            var res2 = (int)(t1[1]);
                            Assert.Equal(4, res2);
                            Assert.Equal(2, _CustomDynamicCellConverter_Int_Calls);

                            var res3 = (int)(t1[2]);
                            Assert.Equal(5, res3);
                            Assert.Equal(3, _CustomDynamicCellConverter_Int_Calls);

                            Assert.False(csv.TryRead(out _));
                        }
                    }
                );
            }

            // delegate
            {
                var converter = new _CustomDynamicCellConverter();
                var called = 0;
                // whole point is to use a delegate here
#pragma warning disable IDE0039 
                ParserDelegate<int> del =
                    (ReadOnlySpan<char> _, in ReadContext ctx, out int val) =>
                    {
                        called++;

                        val = ctx.Column.Index + 4;

                        return true;
                    };
#pragma warning restore IDE0039
                var cellConverter = Parser.ForDelegate(del);

                converter.Add(typeof(int).GetTypeInfo(), cellConverter);

                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(converter).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        called = 0;

                        using (var str = getReader("a,bb,ccc"))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.True(csv.TryRead(out var t1));

                            var res1 = (int)(t1[0]);
                            Assert.Equal(4, res1);
                            Assert.Equal(1, called);

                            var res2 = (int)(t1[1]);
                            Assert.Equal(5, res2);
                            Assert.Equal(2, called);

                            var res3 = (int)(t1[2]);
                            Assert.Equal(6, res3);
                            Assert.Equal(3, called);

                            Assert.False(csv.TryRead(out _));
                        }
                    }
                );
            }

            // 1 param constructor
            {
                var converter = new _CustomDynamicCellConverter();
                var cons = typeof(_CustomDynamicCellConverter_Cons).GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
                var cellConverter = Parser.ForConstructor(cons);

                converter.Add(typeof(_CustomDynamicCellConverter_Cons).GetTypeInfo(), cellConverter);

                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(converter).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Cons1_Called = 0;

                        using (var str = getReader("a,bb,ccc"))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.True(csv.TryRead(out var t1));

                            var res1 = (_CustomDynamicCellConverter_Cons)(t1[0]);
                            Assert.Equal("a", res1.Val);
                            Assert.Equal(1, _CustomDynamicCellConverter_Cons1_Called);

                            var res2 = (_CustomDynamicCellConverter_Cons)(t1[1]);
                            Assert.Equal("bb", res2.Val);
                            Assert.Equal(2, _CustomDynamicCellConverter_Cons1_Called);

                            var res3 = (_CustomDynamicCellConverter_Cons)(t1[2]);
                            Assert.Equal("ccc", res3.Val);
                            Assert.Equal(3, _CustomDynamicCellConverter_Cons1_Called);

                            Assert.False(csv.TryRead(out _));
                        }
                    }
                );
            }

            // 2 params constructor
            {
                var converter = new _CustomDynamicCellConverter();
                var cons = typeof(_CustomDynamicCellConverter_Cons).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });
                var cellConverter = Parser.ForConstructor(cons);

                converter.Add(typeof(_CustomDynamicCellConverter_Cons).GetTypeInfo(), cellConverter);

                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(converter).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Cons2_Called = 0;

                        using (var str = getReader("a,bb,ccc"))
                        using (var csv = config.CreateReader(str))
                        {
                            Assert.True(csv.TryRead(out var t1));

                            var res1 = (_CustomDynamicCellConverter_Cons)(t1[0]);
                            Assert.Equal("a0", res1.Val);
                            Assert.Equal(1, _CustomDynamicCellConverter_Cons2_Called);

                            var res2 = (_CustomDynamicCellConverter_Cons)(t1[1]);
                            Assert.Equal("bb1", res2.Val);
                            Assert.Equal(2, _CustomDynamicCellConverter_Cons2_Called);

                            var res3 = (_CustomDynamicCellConverter_Cons)(t1[2]);
                            Assert.Equal("ccc2", res3.Val);
                            Assert.Equal(3, _CustomDynamicCellConverter_Cons2_Called);

                            Assert.False(csv.TryRead(out _));
                        }
                    }
                );
            }
        }

        private static int _CustomDynamicCellConverter_Cons1_Called = 0;
        private static int _CustomDynamicCellConverter_Cons2_Called = 0;

        private class _CustomDynamicCellConverter_Cons
        {
            public readonly string Val;

            public _CustomDynamicCellConverter_Cons(ReadOnlySpan<char> c)
            {
                _CustomDynamicCellConverter_Cons1_Called++;

                Val = new string(c);
            }

            public _CustomDynamicCellConverter_Cons(ReadOnlySpan<char> c, in ReadContext ctx)
            {
                _CustomDynamicCellConverter_Cons2_Called++;

                Val = new string(c) + ctx.Column.Index;
            }
        }

        private static int _CustomDynamicCellConverter_Int_Calls = 0;
        public static bool _CustomDynamicCellConverter_Int(ReadOnlySpan<char> _, in ReadContext ctx, out int val)
        {
            _CustomDynamicCellConverter_Int_Calls++;

            val = ctx.Column.Index + 3;
            return true;
        }

        [Fact]
        public void DetectLineEndings()
        {
            var opts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.Detect).WithReadHeader(ReadHeader.Never).ToOptions();

            // normal
            {
                // \r\n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\r\ndddd,eeeee,ffffff\r\n1,2,3\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\rdddd,eeeee,ffffff\r1,2,3\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("a,bb,ccc\ndddd,eeeee,ffffff\n1,2,3\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // quoted
            {
                // \r\n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\r\ndddd,\"ee\neee\",ffffff\r\n1,2,\"3\r\n\"\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\rdddd,\"ee\neee\",ffffff\r1,2,\"3\r\n\"\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",bb,ccc\ndddd,\"ee\neee\",ffffff\n1,2,\"3\r\n\"\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }

            // escaped
            {
                // \r\n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\n\"\"\"dddd\",\"ee\neee\",ffffff\r\n1,\"\"\"2\"\"\",\"3\r\n\"\r\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \r
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\r\"\"\"dddd\",\"ee\neee\",ffffff\r1,\"\"\"2\"\"\",\"3\r\n\"\r"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );

                // \n
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, getReader) =>
                    {
                        using (var str = getReader("\"a\r\",\"b\"\"b\",ccc\n\"\"\"dddd\",\"ee\neee\",ffffff\n1,\"\"\"2\"\"\",\"3\r\n\"\n"))
                        using (var reader = config.CreateReader(str))
                        {
                            Assert.True(reader.TryRead(out var t1));
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            Assert.True(reader.TryRead(out var t2));
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            Assert.True(reader.TryRead(out var t3));
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            Assert.False(reader.TryRead(out _));
                        }
                    }
                );
            }
        }

        [Fact]
        public void Multi()
        {
            var optsHeader = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            // with headers
            RunSyncDynamicReaderVariants(
                optsHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1.A;
                                string a2 = row1["A"];
                                string a3 = row1[0];

                                Assert.Equal("foo", a1);
                                Assert.Equal("foo", a2);
                                Assert.Equal("foo", a3);

                                string b1 = row1.B;
                                string b2 = row1["B"];
                                string b3 = row1[1];

                                Assert.Equal("bar", b1);
                                Assert.Equal("bar", b2);
                                Assert.Equal("bar", b3);
                            },
                            row2 =>
                            {
                                int a1 = row2.A;
                                int a2 = row2["A"];
                                int a3 = row2[0];

                                Assert.Equal(1, a1);
                                Assert.Equal(1, a2);
                                Assert.Equal(1, a3);

                                double b1 = row2.B;
                                double b2 = row2["B"];
                                double b3 = row2[1];

                                Assert.Equal(3.3, b1);
                                Assert.Equal(3.3, b2);
                                Assert.Equal(3.3, b3);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3.A;
                                DateTime a2 = row3["A"];
                                DateTime a3 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a2);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a3);

                                char b1 = row3.B;
                                char b2 = row3["B"];
                                char b3 = row3[1];

                                Assert.Equal('d', b1);
                                Assert.Equal('d', b2);
                                Assert.Equal('d', b3);
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no headers
            RunSyncDynamicReaderVariants(
                optsNoHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1[0];

                                Assert.Equal("foo", a1);

                                string b1 = row1[1];

                                Assert.Equal("bar", b1);
                            },
                            row2 =>
                            {
                                int a1 = row2[0];

                                Assert.Equal(1, a1);

                                double b1 = row2[1];

                                Assert.Equal(3.3, b1);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);

                                char b1 = row3[1];

                                Assert.Equal('d', b1);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void Simple()
        {
            var optsHeader = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            // with headers
            RunSyncDynamicReaderVariants(
                optsHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\nfoo,bar"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];
                                string aName = row["A"];
                                string aMem = row.A;
                                string aCol = row[ColumnIdentifier.Create(0, "A")];

                                Assert.Equal("foo", aIx);
                                Assert.Equal("foo", aName);
                                Assert.Equal("foo", aMem);
                                Assert.Equal("foo", aCol);

                                string bIx = row[1];
                                string bName = row["B"];
                                string bMem = row.B;
                                string bCol = row[(ColumnIdentifier)1];

                                Assert.Equal("bar", bIx);
                                Assert.Equal("bar", bName);
                                Assert.Equal("bar", bMem);
                                Assert.Equal("bar", bCol);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // untyped enumerator
                                {
                                    System.Collections.IEnumerable e = row;
                                    var i = e.GetEnumerator();

                                    var reset = true;
loop:
                                    var ix = 0;
                                    while (i.MoveNext())
                                    {
                                        string val = (dynamic)i.Current;
                                        switch (ix)
                                        {
                                            case 0: Assert.Equal("foo", val); break;
                                            case 1: Assert.Equal("bar", val); break;
                                            default:
                                                Assert.Null("Shouldn't be possible");
                                                break;
                                        }
                                        ix++;
                                    }

                                    Assert.Equal(2, ix);

                                    if (reset)
                                    {
                                        reset = false;
                                        i.Reset();
                                        goto loop;
                                    }
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerator
                                {
                                    IEnumerable<string> e = row;
                                    using (var i = e.GetEnumerator())
                                    {
                                        var reset = true;
loop:
                                        var ix = 0;
                                        while (i.MoveNext())
                                        {
                                            string val = i.Current;
                                            switch (ix)
                                            {
                                                case 0: Assert.Equal("foo", val); break;
                                                case 1: Assert.Equal("bar", val); break;
                                                default:
                                                    Assert.Null("Shouldn't be possible");
                                                    break;
                                            }
                                            ix++;
                                        }

                                        Assert.Equal(2, ix);

                                        if (reset)
                                        {
                                            reset = false;
                                            i.Reset();
                                            goto loop;
                                        }
                                    }
                                }
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no headers
            RunSyncDynamicReaderVariants(
                optsNoHeader,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("foo,bar"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];

                                Assert.Equal("foo", aIx);

                                string bIx = row[1];

                                Assert.Equal("bar", bIx);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // untyped enumerator
                                {
                                    System.Collections.IEnumerable e = row;
                                    var i = e.GetEnumerator();

                                    var reset = true;
loop:
                                    var ix = 0;
                                    while (i.MoveNext())
                                    {
                                        string val = (dynamic)i.Current;
                                        switch (ix)
                                        {
                                            case 0: Assert.Equal("foo", val); break;
                                            case 1: Assert.Equal("bar", val); break;
                                            default:
                                                Assert.Null("Shouldn't be possible");
                                                break;
                                        }
                                        ix++;
                                    }

                                    Assert.Equal(2, ix);

                                    if (reset)
                                    {
                                        reset = false;
                                        i.Reset();
                                        goto loop;
                                    }
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerator
                                {
                                    IEnumerable<string> e = row;
                                    using (var i = e.GetEnumerator())
                                    {
                                        var reset = true;
loop:
                                        var ix = 0;
                                        while (i.MoveNext())
                                        {
                                            string val = i.Current;
                                            switch (ix)
                                            {
                                                case 0: Assert.Equal("foo", val); break;
                                                case 1: Assert.Equal("bar", val); break;
                                                default:
                                                    Assert.Null("Shouldn't be possible");
                                                    break;
                                            }
                                            ix++;
                                        }

                                        Assert.Equal(2, ix);

                                        if (reset)
                                        {
                                            reset = false;
                                            i.Reset();
                                            goto loop;
                                        }
                                    }
                                }
                            }
                        );
                    }
                }
            );
        }

        private class _Conversions
        {
            public int I { get; set; }

            public _Conversions(ReadOnlySpan<char> foo)
            {
                I = int.Parse(foo);
            }
        }

        [Fact]
        public void Conversions()
        {
            var optsHeaders = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            // with headers
            RunSyncDynamicReaderVariants(
                optsHeaders,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row.B;
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );

            var optsNoHeaders = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // with no headers
            RunSyncDynamicReaderVariants(
                optsNoHeaders,
                (config, makeReader) =>
                {
                    using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row[1];
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );
        }

        private enum _Tuple
        {
            Red,
            Green
        }

        [Fact]
        public void Tuple()
        {
            var optWithHeaders = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            // headers
            {
                // one
                RunSyncDynamicReaderVariants(
                    optWithHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    optWithHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // skipped
                RunSyncDynamicReaderVariants(
                    optWithHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B,C\r\n1,,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, string, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal("", typed.Item2);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item3);
                                },
                                row2 =>
                                {
                                    Tuple<string, int?, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(default, typed.Item2);
                                    Assert.Equal(-123, typed.Item3);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        optWithHeaders,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            var optNoHeaders = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no headers
            {
                // one
                RunSyncDynamicReaderVariants(
                    optNoHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    optNoHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // skipped
                RunSyncDynamicReaderVariants(
                    optNoHeaders,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, string, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal("", typed.Item2);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item3);
                                },
                                row2 =>
                                {
                                    Tuple<string, int?, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(default, typed.Item2);
                                    Assert.Equal(-123, typed.Item3);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        optNoHeaders,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public void ValueTuple()
        {
            // headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

                // one
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A\r\n1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // skipped
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("A,B,C\r\n1,,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, string, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal("", typed.Item2);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item3);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, int?, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(default, typed.Item2);
                                    Assert.Equal(-123, typed.Item3);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // no headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

                // one
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1\r\nfoo"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // skipped
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var reader = makeReader("1,,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,,-123"))
                        using (var csv = config.CreateReader(reader))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, string, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal("", typed.Item2);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item3);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, int?, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(default, typed.Item2);
                                    Assert.Equal(-123, typed.Item3);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    RunSyncDynamicReaderVariants(
                        opts,
                        (config, makeReader) =>
                        {
                            using (var reader = makeReader(row1))
                            using (var csv = config.CreateReader(reader))
                            {
                                var read = csv.ReadAll();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        private class _POCO_Constructor
        {
            public int Prop1 { get; }
            public string Prop2 { get; }
            public DateTime Prop3 { get; }

            internal _POCO_Constructor(int p1, string p2, DateTime p3)
            {
                Prop1 = p1;
                Prop2 = p2;
                Prop3 = p3;
            }
        }

        [Fact]
        public void POCO_Constructor()
        {
            // headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }
        }

        private class _POCO_Properties
        {
            public int A { get; set; }
            public string B { get; set; }
            internal DateTime C { get; set; }

            public _POCO_Properties() { }
        }

        [Fact]
        public void POCO_Properties()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, makeReader) =>
                {
                    using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                    using (var csv = config.CreateReader(str))
                    {
                        var read = csv.ReadAll();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var lo = (_POCO_Properties)row;

                                Assert.Equal(1, lo.A);
                                Assert.Equal("foo", lo.B);
                                Assert.Equal(new DateTime(2019, 01, 03), lo.C);
                            }
                        );
                    }
                }
            );
        }

        private class _POCO_Fields : ITypeDescriber
        {
            private readonly DynamicRowConverter Converter;

            public _POCO_Fields(DynamicRowConverter conv)
            {
                Converter = conv;
            }

            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToDeserialize(forType);

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToSerialize(forType);

            public int GetCellsForDynamicRow(in WriteContext ctx, object row, Span<DynamicCellValue> cells)
            => TypeDescribers.Default.GetCellsForDynamicRow(in ctx, row, cells);

            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            => TypeDescribers.Default.GetDynamicCellParserFor(in ctx, targetType);


            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => Converter;

            public InstanceProvider GetInstanceProvider(TypeInfo forType)
            => TypeDescribers.Default.GetInstanceProvider(forType);
        }

        private class _POCO_Fields_Obj
        {
#pragma warning disable CS0649
            public int A;
            public string B;
#pragma warning restore CS0649
        }

        [Fact]
        public void POCO_Fields()
        {
            var t = typeof(_POCO_Fields_Obj).GetTypeInfo();
            var conv =
                DynamicRowConverter.ForEmptyConstructorAndSetters(
                    t.GetConstructor(Type.EmptyTypes),
                    new[]
                    {
                        Setter.ForField(t.GetField(nameof(_POCO_Fields_Obj.A))),
                        Setter.ForField(t.GetField(nameof(_POCO_Fields_Obj.B)))
                    },
                    new[]
                    {
                        ColumnIdentifier.Create(0),
                        ColumnIdentifier.Create(1)
                    }
                );

            var describer = new _POCO_Fields(conv);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(describer).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B\r\n1234,foo\r\n567,\"yup, man\""))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        var typed = rows.Select(r => (_POCO_Fields_Obj)r).ToArray();
                        Assert.Collection(
                            typed,
                            a => { Assert.Equal(1234, a.A); Assert.Equal("foo", a.B); },
                            a => { Assert.Equal(567, a.A); Assert.Equal("yup, man", a.B); }
                        );
                    }
                }
            );
        }

        private class _POCO_Delegates_Obj
        {
            public int A;
            public string B { get; set; }
            public Guid C { get; set; }
            public double D { get; set; }
        }

        [Fact]
        public void POCO_Delegates()
        {
            var t = typeof(_POCO_Delegates_Obj).GetTypeInfo();
            var conv =
                DynamicRowConverter.ForEmptyConstructorAndSetters(
                    t.GetConstructor(Type.EmptyTypes),
                    new[]
                    {
                        Setter.ForDelegate<_POCO_Delegates_Obj, int>((_POCO_Delegates_Obj row, int val, in ReadContext _) => {row.A = val; row.B = val+" "+val; }),
                        Setter.ForDelegate<_POCO_Delegates_Obj, string>((_POCO_Delegates_Obj row, string val, in ReadContext _) => {row.C = Guid.Parse("5CEAD5D9-142B-4971-8211-3E2D497BE8BB"); row.D = 3.14159; })
                    },
                    new[]
                    {
                        ColumnIdentifier.Create(0),
                        ColumnIdentifier.Create(1)
                    }
                ); ;

            var describer = new _POCO_Fields(conv);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(describer).ToOptions();

            RunSyncDynamicReaderVariants(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("A,B\r\n9876,foo\r\n0,bar"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var rows = csv.ReadAll();
                        var typed = rows.Select(r => (_POCO_Delegates_Obj)r).ToArray();
                        Assert.Collection(
                            typed,
                            a =>
                            {
                                Assert.Equal(9876, a.A);
                                Assert.Equal("9876 9876", a.B);
                                Assert.Equal(Guid.Parse("5CEAD5D9-142B-4971-8211-3E2D497BE8BB"), a.C);
                                Assert.Equal(3.14159, a.D);
                            },
                            a =>
                            {
                                Assert.Equal(0, a.A);
                                Assert.Equal("0 0", a.B);
                                Assert.Equal(Guid.Parse("5CEAD5D9-142B-4971-8211-3E2D497BE8BB"), a.C);
                                Assert.Equal(3.14159, a.D);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public void DynamicRowDisposalOptions()
        {
            // dispose with reader
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        using (var csv = config.CreateReader(str))
                        {
                            read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // explodes now that reader is disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }

            // explicit disposal
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        List<dynamic> read;

                        using (var str = makeReader("1,2,3"))
                        using (var csv = config.CreateReader(str))
                        {
                            read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // still good after reader
                        Assert.Collection(
                            read,
                            row =>
                            {
                                int a = row[0];
                                int b = row[1];
                                int c = row[2];

                                Assert.Equal(1, a);
                                Assert.Equal(2, b);
                                Assert.Equal(3, c);
                            }
                        );

                        foreach (var r in read)
                        {
                            r.Dispose();
                        }

                        // explodes now that row are disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    }
                );
            }
        }

        [Fact]
        public void ReusingRows()
        {
            // both auto
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts,
                            (config2, makeReader2) =>
                            {
                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    dynamic row = null;
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);

                                    Assert.False(csv1.TryReadWithReuse(ref row));
                                    Assert.False(csv2.TryReadWithReuse(ref row));
                                }
                            }
                        );
                    },
                    expectedRuns: 15
                );
            }

            // auto then explicitly
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                var opts2 = Options.CreateBuilder(opts1).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts2,
                            (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 15
                );
            }

            // explicitly then auto
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
                var opts2 = Options.CreateBuilder(opts1).WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts1,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts2,
                            (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);


                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);
                                }

                                Assert.Equal(4, (int)row[0]);
                                Assert.Equal(5, (int)row[1]);
                                Assert.Equal(6, (int)row[2]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 15
                );
            }

            // both explicitly
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
                RunSyncDynamicReaderVariants(
                    opts,
                    (config1, makeReader1) =>
                    {
                        RunSyncDynamicReaderVariants(
                            opts,
                            (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                using (var r1 = makeReader1("1,2,3\r\n4,5,6"))
                                using (var r2 = makeReader2("7,8\r\n9,10"))
                                using (var csv1 = config1.CreateReader(r1))
                                using (var csv2 = config2.CreateReader(r2))
                                {
                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    Assert.True(csv1.TryReadWithReuse(ref row));
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    Assert.True(csv2.TryReadWithReuse(ref row));
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    expectedRuns: 15
                );
            }
        }

        private class _DelegateRowConversions<T> : ITypeDescriber
        {
            private readonly DynamicRowConverterDelegate<T> D;

            public _DelegateRowConversions(DynamicRowConverterDelegate<T> d)
            {
                D = d;
            }

            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            => TypeDescribers.Default.GetDynamicCellParserFor(in ctx, targetType);

            public int GetCellsForDynamicRow(in WriteContext ctx, object row, Span<DynamicCellValue> cells)
            => TypeDescribers.Default.GetCellsForDynamicRow(in ctx, row, cells);

            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => (DynamicRowConverter)D;

            public InstanceProvider GetInstanceProvider(TypeInfo forType)
            => TypeDescribers.Default.GetInstanceProvider(forType);

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToSerialize(forType);

            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToDeserialize(forType);
        }

        private class __DelegateRowConversions_Row
        {
            public string Yup;
        }

        [Fact]
        public void DelegateRowConversions()
        {
            // whole point is the delegate
#pragma warning disable IDE0039
            DynamicRowConverterDelegate<__DelegateRowConversions_Row> x =
                (dynamic row, in ReadContext ctx, out __DelegateRowConversions_Row res) =>
                {
                    var a = (string)row[0];
                    var b = (string)row[1];
                    var c = (string)row[2];

                    var x = a + b + b + c + c + c;

                    res = new __DelegateRowConversions_Row { Yup = x };

                    return true;
                };
#pragma warning restore IDE0039

            var convert = new _DelegateRowConversions<__DelegateRowConversions_Row>(x);

            // headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithTypeDescriber(convert).ToOptions();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (__DelegateRowConversions_Row)row;

                                    Assert.Equal("1foofoo2019-01-032019-01-032019-01-03", lo.Yup);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(convert).ToOptions();

                RunSyncDynamicReaderVariants(
                    opts,
                    (config, makeReader) =>
                    {
                        using (var str = makeReader("1,foo,2019-01-03"))
                        using (var csv = config.CreateReader(str))
                        {
                            var read = csv.ReadAll();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (__DelegateRowConversions_Row)row;

                                    Assert.Equal("1foofoo2019-01-032019-01-032019-01-03", lo.Yup);
                                }
                            );
                        }
                    }
                );
            }
        }

        // async tests

        [Fact]
        public async Task DynamicRowStringLookupAsync()
        {
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A\r\n1"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            r =>
                            {
                                var n1 = "A";
                                var nMissing = "D";

                                Assert.Equal("1", (string)r[n1]);

                                Assert.Throws<KeyNotFoundException>(() => r[nMissing]);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task RangesWithoutNamesAsync()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions();

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("1,2,3,4,5"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            r =>
                            {
                                var range1 = (IEnumerable<int>)(r[2..]);
                                var range2 = (IEnumerable<int>)(r[1..3]);

                                Assert.Equal(new[] { 3, 4, 5 }, range1);
                                Assert.Equal(new[] { 2, 3 }, range2);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task DynamicRowColumnIdentifierLookupAsync()
        {
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A\r\n1"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            r =>
                            {
                                var c1I = ColumnIdentifier.Create(0);
                                var c1N = ColumnIdentifier.Create(0, "A");

                                var cMissingI = ColumnIdentifier.Create(4);
                                var cMissingN = ColumnIdentifier.Create(4, "D");

                                Assert.Equal("1", (string)r[c1I]);
                                Assert.Equal("1", (string)r[c1N]);

                                Assert.Throws<ArgumentOutOfRangeException>(() => r[cMissingI]);
                                Assert.Throws<KeyNotFoundException>(() => r[cMissingN]);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task MultiCharacterSeparatorsAsync()
        {
            // header variants
            {
                // no headers
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a[0]);
                                        Assert.Equal(123, (int)a[1]);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b[0]);
                                        Assert.Equal(456, (int)b[1]);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c[0]);
                                        Assert.Equal(789, (int)c[1]);
                                    }
                                );
                            }
                        }
                    );
                }

                // always headers
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("A#|#B\r\nhello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.A);
                                        Assert.Equal(123, (int)a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b.A);
                                        Assert.Equal(456, (int)b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c.A);
                                        Assert.Equal(789, (int)c.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // detect line endings
            {
                var opts1 = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Never).WithRowEnding(RowEnding.Detect).ToOptions();
                var opts2 = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).WithRowEnding(RowEnding.Detect).ToOptions();

                // \r\n
                {
                    // not present
                    await RunAsyncDynamicReaderVariants(
                        opts1,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("hello#|#123\r\n\"world\"#|#456\r\n\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a[0]);
                                        Assert.Equal(123, (int)a[1]);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b[0]);
                                        Assert.Equal(456, (int)b[1]);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c[0]);
                                        Assert.Equal(789, (int)c[1]);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    await RunAsyncDynamicReaderVariants(
                        opts2,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#A\r\n123#|#hello\r\n456#|#\"world\"\r\n789#|#\"f#|#b\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.A);
                                        Assert.Equal(123, (int)a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b.A);
                                        Assert.Equal(456, (int)b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c.A);
                                        Assert.Equal(789, (int)c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \r
                {
                    // not present
                    await RunAsyncDynamicReaderVariants(
                        opts1,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("hello#|#123\r\"world\"#|#456\r\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a[0]);
                                        Assert.Equal(123, (int)a[1]);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b[0]);
                                        Assert.Equal(456, (int)b[1]);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c[0]);
                                        Assert.Equal(789, (int)c[1]);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    await RunAsyncDynamicReaderVariants(
                        opts2,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#A\r123#|#hello\r456#|#\"world\"\r789#|#\"f#|#b\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.A);
                                        Assert.Equal(123, (int)a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b.A);
                                        Assert.Equal(456, (int)b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c.A);
                                        Assert.Equal(789, (int)c.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \n
                {
                    // not present
                    await RunAsyncDynamicReaderVariants(
                        opts1,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("hello#|#123\n\"world\"#|#456\n\"f#|#b\"#|#789"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a[0]);
                                        Assert.Equal(123, (int)a[1]);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b[0]);
                                        Assert.Equal(456, (int)b[1]);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c[0]);
                                        Assert.Equal(789, (int)c[1]);
                                    }
                                );
                            }
                        }
                    );

                    // present
                    await RunAsyncDynamicReaderVariants(
                        opts2,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#A\n123#|#hello\n456#|#\"world\"\n789#|#\"f#|#b\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.A);
                                        Assert.Equal(123, (int)a.B);
                                    },
                                    b =>
                                    {
                                        Assert.Equal("world", (string)b.A);
                                        Assert.Equal(456, (int)b.B);
                                    },
                                    c =>
                                    {
                                        Assert.Equal("f#|#b", (string)c.A);
                                        Assert.Equal(789, (int)c.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task MultiCharacterSeparatorInHeadersAsync()
        {
            // always
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).ToOptions();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", (string)a["Foo#|#Bar"]);
                                    Assert.Equal(123, (int)a.B);
                                }
                            );
                        }
                    }
                );
            }

            // detect rows endings
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#|#").WithReadHeader(ReadHeader.Always).WithRowEnding(RowEnding.Detect).ToOptions();

                // \r\n
                {
                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#\"Foo#|#Bar\"\r\n123#|#hello"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a["Foo#|#Bar"]);
                                        Assert.Equal(123, (int)a.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \r
                {
                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#\"Foo#|#Bar\"\r123#|#hello"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a["Foo#|#Bar"]);
                                        Assert.Equal(123, (int)a.B);
                                    }
                                );
                            }
                        }
                    );
                }

                // \n
                {
                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, getReader) =>
                        {
                            await using (var reader = await getReader("B#|#\"Foo#|#Bar\"\n123#|#hello"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a["Foo#|#Bar"]);
                                        Assert.Equal(123, (int)a.B);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task MissingTrailingColumnsAsync()
        {
            // with headers
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A,B,C\r\n1,2,3\r\n4,5\r\n6\r\n\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                int a1 = r1.A;
                                int a2 = r1[0];
                                int b1 = r1.B;
                                int b2 = r1[1];
                                int c1 = r1.C;
                                int c2 = r1[2];

                                Assert.Equal(1, a1);
                                Assert.Equal(1, a2);

                                Assert.Equal(2, b1);
                                Assert.Equal(2, b2);

                                Assert.Equal(3, c1);
                                Assert.Equal(3, c2);
                            },
                            r2 =>
                            {
                                int a1 = r2.A;
                                int a2 = r2[0];
                                int b1 = r2.B;
                                int b2 = r2[1];
                                int? c1 = r2.C;
                                int? c2 = r2[2];

                                Assert.Equal(4, a1);
                                Assert.Equal(4, a2);

                                Assert.Equal(5, b1);
                                Assert.Equal(5, b2);

                                Assert.False(c1.HasValue);
                                Assert.False(c2.HasValue);
                            },
                            r3 =>
                            {
                                int a1 = r3.A;
                                int a2 = r3[0];
                                int? b1 = r3.B;
                                int? b2 = r3[1];
                                int? c1 = r3.C;
                                int? c2 = r3[2];

                                Assert.Equal(6, a1);
                                Assert.Equal(6, a2);

                                Assert.False(b1.HasValue);
                                Assert.False(b2.HasValue);

                                Assert.False(c1.HasValue);
                                Assert.False(c2.HasValue);
                            },
                            r4 =>
                            {
                                int? a1 = r4.A;
                                int? a2 = r4[0];
                                int? b1 = r4.B;
                                int? b2 = r4[1];
                                int? c1 = r4.C;
                                int? c2 = r4[2];

                                Assert.False(a1.HasValue);
                                Assert.False(a2.HasValue);

                                Assert.False(b1.HasValue);
                                Assert.False(b2.HasValue);

                                Assert.False(c1.HasValue);
                                Assert.False(c2.HasValue);
                            }
                        );
                    }
                }
            );

            var noHeadersOpts = Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions();

            // without headers
            await RunAsyncDynamicReaderVariants(
                noHeadersOpts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("1,2,3\r\n4,5\r\n6\r\n\r\n"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                int a2 = r1[0];
                                int b2 = r1[1];
                                int c2 = r1[2];

                                Assert.Equal(1, a2);

                                Assert.Equal(2, b2);

                                Assert.Equal(3, c2);
                            },
                            r2 =>
                            {
                                int a2 = r2[0];
                                int b2 = r2[1];
                                int? c2 = r2[2];

                                Assert.Equal(4, a2);

                                Assert.Equal(5, b2);

                                Assert.False(c2.HasValue);
                            },
                            r3 =>
                            {
                                int a2 = r3[0];
                                int? b2 = r3[1];
                                int? c2 = r3[2];

                                Assert.Equal(6, a2);

                                Assert.False(b2.HasValue);

                                Assert.False(c2.HasValue);
                            },
                            r4 =>
                            {
                                int? a2 = r4[0];
                                int? b2 = r4[1];
                                int? c2 = r4[2];

                                Assert.False(a2.HasValue);

                                Assert.False(b2.HasValue);

                                Assert.False(c2.HasValue);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task NullStringColumnAsync()
        {
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    const string DATA = "A\r\nfoo\r\n\r\nbar\r\n\r\n";

                    await using (var reader = await getReader(DATA))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        var asStrings = rows.Select(d => (string)d.A).ToList();

                        Assert.True(
                            new[]
                            {
                                "foo",
                                null,
                                "bar",
                                null
                            }
                            .SequenceEqual(asStrings)
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task RangeAccessOutOfBoundsAsync()
        {
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    const string DATA = "A,B,C,D\r\n1,,,4\r\n,,5,6\r\n7,8,,";

                    await using (var reader = await getReader(DATA))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                Assert.Equal(1, (int?)r1.A);
                                Assert.Null((int?)r1.B);
                                Assert.Null((int?)r1.C);
                                Assert.Equal(4, (int?)r1.D);

                                var subR1_1 = r1[0..2];
                                Assert.Equal(1, (int?)subR1_1.A);
                                Assert.Null((int?)subR1_1.B);
                                Assert.Throws<KeyNotFoundException>(() => subR1_1.C);
                                Assert.Throws<KeyNotFoundException>(() => subR1_1.D);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR1_1[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR1_1[2]);

                                var subR1_2 = r1[2..];
                                Assert.Null((int?)subR1_2.C);
                                Assert.Equal(4, (int?)subR1_2.D);
                                Assert.Throws<KeyNotFoundException>(() => subR1_2.A);
                                Assert.Throws<KeyNotFoundException>(() => subR1_2.B);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR1_2[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR1_2[2]);
                            },
                            r2 =>
                            {
                                Assert.Null((int?)r2.A);
                                Assert.Null((int?)r2.B);
                                Assert.Equal(5, (int?)r2.C);
                                Assert.Equal(6, (int?)r2.D);

                                var subR2 = r2[1..3];
                                Assert.Null((int?)subR2.B);
                                Assert.Equal(5, (int?)subR2.C);
                                Assert.Throws<KeyNotFoundException>(() => subR2.A);
                                Assert.Throws<KeyNotFoundException>(() => subR2.D);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR2[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR2[2]);
                            },
                            r3 =>
                            {
                                Assert.Equal(7, (int?)r3.A);
                                Assert.Equal(8, (int?)r3.B);
                                Assert.Null((int?)r3.C);
                                Assert.Null((int?)r3.D);

                                var subR3_1 = r3[1..3];
                                Assert.Equal(8, (int?)subR3_1.B);
                                Assert.Null((int?)subR3_1.C);
                                Assert.Throws<KeyNotFoundException>(() => subR3_1.A);
                                Assert.Throws<KeyNotFoundException>(() => subR3_1.D);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR3_1[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR3_1[2]);

                                var subR3_2 = r3[2..];
                                Assert.Null((int?)subR3_2.C);
                                Assert.Null((int?)subR3_2.D);
                                Assert.Throws<KeyNotFoundException>(() => subR3_2.A);
                                Assert.Throws<KeyNotFoundException>(() => subR3_2.B);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR3_2[-1]);
                                Assert.Throws<ArgumentOutOfRangeException>(() => subR3_2[2]);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task RangeWithSubNullsAsync()
        {
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    const string DATA = "A,B,C,D\r\n1,,,4\r\n,,5,6\r\n7,8,,";

                    await using (var reader = await getReader(DATA))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                Assert.Equal(1, (int?)r1.A);
                                Assert.Null((int?)r1.B);
                                Assert.Null((int?)r1.C);
                                Assert.Equal(4, (int?)r1.D);

                                var subR1_1 = r1[0..2];
                                Assert.Equal(1, (int?)subR1_1.A);
                                Assert.Null((int?)subR1_1.B);

                                var subR1_2 = r1[2..];
                                Assert.Null((int?)subR1_2.C);
                                Assert.Equal(4, (int?)subR1_2.D);
                            },
                            r2 =>
                            {
                                Assert.Null((int?)r2.A);
                                Assert.Null((int?)r2.B);
                                Assert.Equal(5, (int?)r2.C);
                                Assert.Equal(6, (int?)r2.D);

                                var subR2 = r2[1..3];
                                Assert.Null((int?)subR2.B);
                                Assert.Equal(5, (int?)subR2.C);
                            },
                            r3 =>
                            {
                                Assert.Equal(7, (int?)r3.A);
                                Assert.Equal(8, (int?)r3.B);
                                Assert.Null((int?)r3.C);
                                Assert.Null((int?)r3.D);

                                var subR3_1 = r3[1..3];
                                Assert.Equal(8, (int?)subR3_1.B);
                                Assert.Null((int?)subR3_1.C);

                                var subR3_2 = r3[2..];
                                Assert.Null((int?)subR3_2.C);
                                Assert.Null((int?)subR3_2.D);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task NullRoundTripsAsync()
        {
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    const string DATA = "A,B,C\r\n1,2,3\r\n4,,5\r\n6,7,\r\n,8,9";

                    await using (var reader = await getReader(DATA))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            r1 =>
                            {
                                Assert.Equal(1, (int?)r1.A);
                                Assert.Equal(2, (int?)r1.B);
                                Assert.Equal(3, (int?)r1.C);
                            },
                            r2 =>
                            {
                                Assert.Equal(4, (int?)r2.A);
                                Assert.Null((int?)r2.B);
                                Assert.Equal(5, (int?)r2.C);
                            },
                            r3 =>
                            {
                                Assert.Equal(6, (int?)r3.A);
                                Assert.Equal(7, (int?)r3.B);
                                Assert.Null((int?)r3.C);
                            },
                            r4 =>
                            {
                                Assert.Null((int?)r4.A);
                                Assert.Equal(8, (int?)r4.B);
                                Assert.Equal(9, (int?)r4.C);
                            }
                        );

                        string roundTripped;
                        using (var writer = new StringWriter())
                        {
                            await using (var writerCsv = config.CreateAsyncWriter(writer))
                            {
                                await writerCsv.WriteAllAsync(rows);
                            }
                            roundTripped = writer.ToString();
                        }

                        Assert.Equal(DATA, roundTripped);
                    }
                }
            );
        }

        [Fact]
        public async Task IEnumerableOfDynamicAsync()
        {
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A,B,C\r\n1,foo,2020-01-02\r\nfalse,c,100"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            r =>
                            {
                                IEnumerable<dynamic> e = r;
                                Assert.Collection(
                                    e,
                                    a => Assert.Equal(1, (int)a),
                                    a => Assert.Equal("foo", (string)a),
                                    a => Assert.Equal(DateTime.Parse("2020-01-02", CultureInfo.InvariantCulture, DateTimeStyles.None), (DateTime)a)
                                );
                            },
                            r =>
                            {
                                IEnumerable<dynamic> e = r;
                                Assert.Collection(
                                    e,
                                    a => Assert.False((bool)a),
                                    a => Assert.Equal('c', (char)a),
                                    a => Assert.Equal(100, (int)a)
                                );
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task IgnoreExcessColumnsAsync()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithExtraColumnTreatment(ExtraColumnTreatment.Ignore).ToOptions();

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("hello", (string)a.A); Assert.Equal("world", (string)a.B); },
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("fizz", (string)a.A); Assert.Equal("buzz", (string)a.B); Assert.Throws<ArgumentOutOfRangeException>(() => a[2]); },
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("fe", (string)a.A); Assert.Equal("fi", (string)a.B); Assert.Throws<ArgumentOutOfRangeException>(() => a[2]); }
                        );
                    }
                }
            );

            var noHeaderOpts = Options.CreateBuilder(opts).WithReadHeader(ReadHeader.Never).ToOptions();

            await RunAsyncDynamicReaderVariants(
                noHeaderOpts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("hello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("hello", (string)a[0]); Assert.Equal("world", (string)a[1]); },
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("fizz", (string)a[0]); Assert.Equal("buzz", (string)a[1]); Assert.Throws<ArgumentOutOfRangeException>(() => a[2]); },
                            a => { Assert.Equal(2, ((IEnumerable<string>)a).Count()); Assert.Equal("fe", (string)a[0]); Assert.Equal("fi", (string)a[1]); Assert.Throws<ArgumentOutOfRangeException>(() => a[2]); }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task ThrowsOnExcessColumnsAsync()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithExtraColumnTreatment(ExtraColumnTreatment.ThrowException).ToOptions();

            // with heaers
            {
                // fine, shouldn't throw
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,B\r\nhello,world\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", (string)a.A); Assert.Equal("world", (string)a.B); }
                            );
                        }
                    }
                );

                // should throw on second read
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadAsync();
                            Assert.True(res.HasValue);
                            var row = res.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal("world", (string)row.B);

                            await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                        }
                    }
                );
            }

            // without heaers
            {
                var noHeadOpts = Options.CreateBuilder(opts).WithReadHeader(ReadHeader.Never).ToOptions();

                // fine, shouldn't throw
                await RunAsyncDynamicReaderVariants(
                    noHeadOpts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("hello,world\r\n"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();

                            Assert.Collection(
                                rows,
                                a => { Assert.Equal("hello", (string)a[0]); Assert.Equal("world", (string)a[1]); }
                            );
                        }
                    }
                );

                // should throw on second read
                await RunAsyncDynamicReaderVariants(
                    noHeadOpts,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("hello,world\r\nfizz,buzz,bazz"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res = await csv.TryReadAsync();
                            Assert.True(res.HasValue);
                            var row = res.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal("world", (string)row[1]);

                            await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.TryReadAsync());
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task AllowExcessColumnsAsync()
        {
            // with headers
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A,B\r\nhello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello", (string)a.A); Assert.Equal("world", (string)a.B); },
                            a => { Assert.Equal("fizz", (string)a.A); Assert.Equal("buzz", (string)a.B); Assert.Equal("bazz", (string)a[2]); },
                            a => { Assert.Equal("fe", (string)a.A); Assert.Equal("fi", (string)a.B); Assert.Equal("fo", (string)a[2]); Assert.Equal("fum", (string)a[3]); }
                        );
                    }
                }
            );

            var noHeadersOpts = Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions();

            await RunAsyncDynamicReaderVariants(
                noHeadersOpts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("hello,world\r\nfizz,buzz,bazz\r\nfe,fi,fo,fum"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();

                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("hello", (string)a[0]); Assert.Equal("world", (string)a[1]); },
                            a => { Assert.Equal("fizz", (string)a[0]); Assert.Equal("buzz", (string)a[1]); Assert.Equal("bazz", (string)a[2]); },
                            a => { Assert.Equal("fe", (string)a[0]); Assert.Equal("fi", (string)a[1]); Assert.Equal("fo", (string)a[2]); Assert.Equal("fum", (string)a[3]); }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task ChainedParsersAsync()
        {
            var p0 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 1)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val *= 2;

                        return true;
                    }
                );

            var p1 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 2)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val--;

                        return true;
                    }
                );

            var p2 =
                Parser.ForDelegate(
                    (ReadOnlySpan<char> data, in ReadContext ctx, out int val) =>
                    {
                        var a = (_ChainedParsers_Context)ctx.Context;

                        if (a.Num != 3)
                        {
                            val = default;
                            return false;
                        }

                        val = int.Parse(data);
                        val = -(val << 3);

                        return true;
                    }
                );

            var p = p0.Else(p1).Else(p2);

            var td = new _ChainedParsers_TypeDescriber(p);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(td).ToOptions();

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    var ctx = new _ChainedParsers_Context();

                    await using (var reader = await getReader("Foo\r\n1\r\n2\r\n3\r\n4"))
                    await using (var csv = config.CreateAsyncReader(reader, ctx))
                    {
                        ctx.Num = 1;
                        var res1 = await csv.TryReadAsync();
                        Assert.True(res1.HasValue);
                        var r1 = res1.Value;
                        var c1 = r1.Foo;
                        Assert.Equal(2, (int)c1);

                        ctx.Num = 2;
                        var res2 = await csv.TryReadAsync();
                        Assert.True(res2.HasValue);
                        var r2 = res2.Value;
                        var c2 = r2.Foo;
                        Assert.Equal(1, (int)c2);

                        ctx.Num = 3;
                        var res3 = await csv.TryReadAsync();
                        Assert.True(res3.HasValue);
                        var r3 = res3.Value;
                        var c3 = r3.Foo;
                        Assert.Equal(-(3 << 3), (int)c3);

                        ctx.Num = 4;
                        var res4 = await csv.TryReadAsync();
                        Assert.True(res4.HasValue);
                        var r4 = res4.Value;
                        var c4 = r4.Foo;
                        Assert.Throws<InvalidOperationException>(() => (int)c4);
                    }
                }
            );
        }

        [Fact]
        public async Task ChainedDynamicRowConvertersAsync()
        {
            var c1 =
                DynamicRowConverter.ForDelegate(
                    (dynamic row, in ReadContext ctx, out _ChainedDynamicRowConverters res) =>
                    {
                        var c = (_ChainedDynamicRowConverters_Context)ctx.Context;
                        if (c.Num != 1)
                        {
                            res = null;
                            return false;
                        }

                        res = new _ChainedDynamicRowConverters((string)row[0], 1);
                        return true;
                    }
                );
            var c2 =
                DynamicRowConverter.ForDelegate(
                    (dynamic row, in ReadContext ctx, out _ChainedDynamicRowConverters res) =>
                    {
                        var c = (_ChainedDynamicRowConverters_Context)ctx.Context;
                        if (c.Num != 2)
                        {
                            res = null;
                            return false;
                        }

                        res = new _ChainedDynamicRowConverters((string)row[0], 2);
                        return true;
                    }
                );
            var c3 =
                DynamicRowConverter.ForDelegate(
                    (dynamic row, in ReadContext ctx, out _ChainedDynamicRowConverters res) =>
                    {
                        var c = (_ChainedDynamicRowConverters_Context)ctx.Context;
                        if (c.Num != 3)
                        {
                            res = null;
                            return false;
                        }

                        res = new _ChainedDynamicRowConverters((string)row[0], 3);
                        return true;
                    }
                );

            var c = c1.Else(c2).Else(c3);

            var td = new _ChainedDynamicRowConverters_TypeDescriber(c);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(td).ToOptions();

            await RunAsyncDynamicReaderVariants(
                 opts,
                 async (config, getReader) =>
                 {
                     var ctx = new _ChainedDynamicRowConverters_Context();
                     await using (var reader = await getReader("Foo\r\nabc\r\ndef\r\nghi\r\n123"))
                     await using (var csv = config.CreateAsyncReader(reader, ctx))
                     {
                         ctx.Num = 1;
                         var res1 = await csv.TryReadAsync();
                         Assert.True(res1.HasValue);
                         var r1 = res1.Value;
                         _ChainedDynamicRowConverters s1 = r1;
                         Assert.Equal("abc", s1.Value);
                         Assert.Equal(1, s1.Number);

                         ctx.Num = 2;
                         var res2 = await csv.TryReadAsync();
                         Assert.True(res2.HasValue);
                         var r2 = res2.Value;
                         _ChainedDynamicRowConverters s2 = r2;
                         Assert.Equal("def", s2.Value);
                         Assert.Equal(2, s2.Number);

                         ctx.Num = 3;
                         var res3 = await csv.TryReadAsync();
                         Assert.True(res3.HasValue);
                         var r3 = res3.Value;
                         _ChainedDynamicRowConverters s3 = r3;
                         Assert.Equal("ghi", s3.Value);
                         Assert.Equal(3, s3.Number);

                         ctx.Num = 4;
                         var res4 = await csv.TryReadAsync();
                         Assert.True(res4.HasValue);
                         var r4 = res4.Value;
                         Assert.Throws<InvalidOperationException>(() => { _ChainedDynamicRowConverters s4 = r4; });
                     }
                 }
             );
        }

        [Fact]
        public async Task WhitespaceTrimmingAsync()
        {
            // in values
            {
                // leading
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimLeadingInValues).ToOptions();

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\",\"\t\t\t789\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // trailing
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimTrailingInValues).ToOptions();

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo   ,Bar   \r\nhello,123\r\nworld   ,456\t\r\n\"fizz\t \n\",\"789\t\t\t\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // both
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimInValues).ToOptions();

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("   Foo   ,   Bar   \r\nhello,123\r\n\tworld   ,   456\t\r\n\"\tfizz\t \n\",\"\t789\t\t\t\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // outside of values
            {
                // leading
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues).ToOptions();

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\", \t \"789\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("\t \nfizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // trailing
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimAfterValues).ToOptions();

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo  ,\"Bar\"  \r\nhello,123\r\nworld   ,456\t\r\n\"fizz\t \n\",\"789\" \t "))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz\t \n", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // leading
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues).ToOptions();

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo,  Bar\r\nhello,123\r\n   world,\t456\r\n\"\t \nfizz\", \t \"789\""))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("\t \nfizz", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }

                // both
                {
                    var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.TrimBeforeValues | WhitespaceTreatments.TrimAfterValues).ToOptions();

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo  ,\t\"Bar\"  \r\nhello,123\r\n\t world   ,456\t\r\n  \"fizz\t \n\",  \"789\" \t "))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();
                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("hello", (string)a.Foo);
                                        Assert.Equal(123, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("world", (string)a.Foo);
                                        Assert.Equal(456, (int)a.Bar);
                                    },
                                    a =>
                                    {
                                        Assert.Equal("fizz\t \n", (string)a.Foo);
                                        Assert.Equal(789, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // inside and outside of values
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWhitespaceTreatment(WhitespaceTreatments.Trim).ToOptions();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("  \"  Foo  \"  ,\t\t\"\tBar\t\"  \r\nhello,123\r\n\t world   ,456\t\r\n  \"fizz\t \n\",  \"  789\r\n\" \t "))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello", (string)a.Foo);
                                    Assert.Equal(123, (int)a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("world", (string)a.Foo);
                                    Assert.Equal(456, (int)a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("fizz", (string)a.Foo);
                                    Assert.Equal(789, (int)a.Bar);
                                }
                            );
                        }
                    }
                );
            }

            // none
            {
                // no changes in values
                await RunAsyncDynamicReaderVariants(
                    Options.DynamicDefault,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("Foo,\"Bar\"\r\nhello\t,123\r\n  world,456\r\n\"\r\nfizz\",\"789\""))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(
                                rows,
                                a =>
                                {
                                    Assert.Equal("hello\t", (string)a.Foo);
                                    Assert.Equal(123, (int)a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("  world", (string)a.Foo);
                                    Assert.Equal(456, (int)a.Bar);
                                },
                                a =>
                                {
                                    Assert.Equal("\r\nfizz", (string)a.Foo);
                                    Assert.Equal(789, (int)a.Bar);
                                }
                            );
                        }
                    }
                );

                // bad headers
                {
                    // leading value
                    await RunAsyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("  Foo,\"Bar\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("foo", (string)a["  Foo"]);
                                        Assert.Equal(123, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // trailing value
                    await RunAsyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo\t,\"Bar\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("foo", (string)a["Foo\t"]);
                                        Assert.Equal(123, (int)a.Bar);
                                    }
                                );
                            }
                        }
                    );

                    // leading value, escaped
                    await RunAsyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo,\"  Bar\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("foo", (string)a.Foo);
                                        Assert.Equal(123, (int)a["  Bar"]);
                                    }
                                );
                            }
                        }
                    );

                    // leading value, escaped, exceptional
                    await RunAsyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo,\t\"  Bar\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                await AssertThrowsInnerAsync<InvalidOperationException>(async () => await csv.ReadAllAsync());
                            }
                        }
                    );

                    // trailing value, escaped
                    await RunAsyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo,\"Bar\r\n\"\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var rows = await csv.ReadAllAsync();

                                Assert.Collection(
                                    rows,
                                    a =>
                                    {
                                        Assert.Equal("foo", (string)a.Foo);
                                        Assert.Equal(123, (int)a["Bar\r\n"]);
                                    }
                                );
                            }
                        }
                    );

                    // trailing value, escaped, exceptional
                    await RunAsyncDynamicReaderVariants(
                        Options.DynamicDefault,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader("Foo,\"Bar\r\n\"\t\t\r\nfoo,123"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                await AssertThrowsInnerAsync<InvalidOperationException>(async () => await csv.ReadAllAsync());
                            }
                        }
                    );
                }
            }

            static async ValueTask AssertThrowsInnerAsync<TException>(Func<ValueTask> func)
                where TException : Exception
            {
                try
                {
                    await func();
                }
                catch (Exception e)
                {
                    if (e is AggregateException)
                    {
                        Assert.IsType<TException>(e.InnerException);
                    }
                    else
                    {
                        Assert.IsType<TException>(e);
                    }
                }
            }
        }

        [Fact]
        public async Task GetDynamicMemberNamesAsync()
        {
            await RunAsyncDynamicReaderVariants(
                Options.DynamicDefault,
                async (config, getTextReader) =>
                {
                    await using (var reader = await getTextReader("Hello,World,Foo,Bar\r\n1,2,3,4"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            r =>
                            {
                                var provider = r as IDynamicMetaObjectProvider;
                                var metaObj = provider.GetMetaObject(System.Linq.Expressions.Expression.Variable(typeof(object)));
                                var names = metaObj.GetDynamicMemberNames();

                                var ix = 0;
                                foreach (var n in names)
                                {
                                    var v = r[n];
                                    switch (n)
                                    {
                                        case "Hello": Assert.Equal(1, (int)v); Assert.Equal(0, ix); break;
                                        case "World": Assert.Equal(2, (int)v); Assert.Equal(1, ix); break;
                                        case "Foo": Assert.Equal(3, (int)v); Assert.Equal(2, ix); break;
                                        case "Bar": Assert.Equal(4, (int)v); Assert.Equal(3, ix); break;
                                        default:
                                            Assert.Null("Shouldn't be possible");
                                            break;
                                    }

                                    ix++;
                                }
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task POCO_DelegatesAsync()
        {
            var t = typeof(_POCO_Delegates_Obj).GetTypeInfo();
            var conv =
                DynamicRowConverter.ForEmptyConstructorAndSetters(
                    t.GetConstructor(Type.EmptyTypes),
                    new[]
                    {
                        Setter.ForDelegate<_POCO_Delegates_Obj, int>((_POCO_Delegates_Obj row, int val, in ReadContext _) => {row.A = val; row.B = val+" "+val; }),
                        Setter.ForDelegate<_POCO_Delegates_Obj, string>((_POCO_Delegates_Obj row, string val, in ReadContext _) => {row.C = Guid.Parse("5CEAD5D9-142B-4971-8211-3E2D497BE8BB"); row.D = 3.14159; })
                    },
                    new[]
                    {
                        ColumnIdentifier.Create(0),
                        ColumnIdentifier.Create(1)
                    }
                ); ;

            var describer = new _POCO_Fields(conv);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(describer).ToOptions();

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A,B\r\n9876,foo\r\n0,bar"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        var typed = rows.Select(r => (_POCO_Delegates_Obj)r).ToArray();
                        Assert.Collection(
                            typed,
                            a =>
                            {
                                Assert.Equal(9876, a.A);
                                Assert.Equal("9876 9876", a.B);
                                Assert.Equal(Guid.Parse("5CEAD5D9-142B-4971-8211-3E2D497BE8BB"), a.C);
                                Assert.Equal(3.14159, a.D);
                            },
                            a =>
                            {
                                Assert.Equal(0, a.A);
                                Assert.Equal("0 0", a.B);
                                Assert.Equal(Guid.Parse("5CEAD5D9-142B-4971-8211-3E2D497BE8BB"), a.C);
                                Assert.Equal(3.14159, a.D);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task POCO_FieldsAsync()
        {
            var t = typeof(_POCO_Fields_Obj).GetTypeInfo();
            var conv =
                DynamicRowConverter.ForEmptyConstructorAndSetters(
                    t.GetConstructor(Type.EmptyTypes),
                    new[]
                    {
                        Setter.ForField(t.GetField(nameof(_POCO_Fields_Obj.A))),
                        Setter.ForField(t.GetField(nameof(_POCO_Fields_Obj.B)))
                    },
                    new[]
                    {
                        ColumnIdentifier.Create(0),
                        ColumnIdentifier.Create(1)
                    }
                );

            var describer = new _POCO_Fields(conv);

            var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(describer).ToOptions();

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    await using (var reader = await getReader("A,B\r\n1234,foo\r\n567,\"yup, man\""))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var rows = await csv.ReadAllAsync();
                        var typed = rows.Select(r => (_POCO_Fields_Obj)r).ToArray();
                        Assert.Collection(
                            typed,
                            a => { Assert.Equal(1234, a.A); Assert.Equal("foo", a.B); },
                            a => { Assert.Equal(567, a.A); Assert.Equal("yup, man", a.B); }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task WithCommentsAsync()
        {
            // \r\n
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).WithReadHeader(ReadHeader.Always).ToOptions();
                var opts2 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).WithReadHeader(ReadHeader.Never).ToOptions();

                // with headers
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope\r\n#comment\rwhatever\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\r\nA,Nope\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\r\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\r\n#again!###foo###"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \r
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).WithReadHeader(ReadHeader.Always).ToOptions();
                var opts2 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).WithReadHeader(ReadHeader.Never).ToOptions();

                // with headers
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope\r#comment\nwhatever\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\nwhatever\rA,Nope\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\nwhatever\rhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\nwhatever\r#again!###foo###"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\nwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }

            // \n
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).WithReadHeader(ReadHeader.Always).ToOptions();
                var opts2 = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).WithReadHeader(ReadHeader.Never).ToOptions();

                // with headers
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("A,Nope\n#comment\rwhatever\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // with headers, comment before header
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\nA,Nope\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row.A);
                            Assert.Equal(123, (int)row.Nope);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // no headers
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\nhello,123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasValue, res2.ResultType);
                            var row = res2.Value;
                            Assert.Equal("hello", (string)row[0]);
                            Assert.Equal(123, (int)row[1]);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );

                // two comments
                await RunAsyncDynamicReaderVariants(
                    opts2,
                    async (config, getReader) =>
                    {
                        await using (var reader = await getReader("#comment\rwhatever\n#again!###foo###"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var res1 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res1.ResultType);
                            Assert.Equal("comment\rwhatever", res1.Comment);

                            var res2 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.HasComment, res2.ResultType);
                            Assert.Equal("again!###foo###", res2.Comment);

                            var res3 = await csv.TryReadWithCommentAsync();
                            Assert.Equal(ReadWithCommentResultType.NoValue, res3.ResultType);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task WeirdCommentsAsync()
        {
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.LineFeed).WithReadHeader(ReadHeader.Always).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\r\nhello,world\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\n#this is a test comment!\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturn).WithReadHeader(ReadHeader.Always).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\rhello,world\rfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r#this is a test comment!\n\rfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }

            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithRowEnding(RowEnding.CarriageReturnLineFeed).WithReadHeader(ReadHeader.Always).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "#this is a test comment!\n\r\nhello,world\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                await RunAsyncDynamicReaderVariants(
                   opts,
                   async (config, getReader) =>
                   {
                       var CSV = "#this is a test comment!\r\r\nhello,world\r\nfoo,bar";
                       await using (var str = await getReader(CSV))
                       await using (var csv = config.CreateAsyncReader(str))
                       {
                           var rows = await csv.ReadAllAsync();
                           Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                       }
                   }
               );

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\n\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        var CSV = "hello,world\r\n#this is a test comment!\r\r\nfoo,bar";
                        await using (var str = await getReader(CSV))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var rows = await csv.ReadAllAsync();
                            Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task CommentsAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithCommentCharacter('#').WithReadHeader(ReadHeader.Always).ToOptions();

            // comment first line
            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    var CSV = "#this is a test comment!\r\nhello,world\r\nfoo,bar";
                    await using (var str = await getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );

            // comment after header
            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    var CSV = "hello,world\r\n#this is a test comment\r\nfoo,bar";
                    await using (var str = await getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );

            // comment between rows
            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!\r\nfizz,buzz";
                    await using (var str = await getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(
                            rows,
                            a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); },
                            b => { Assert.Equal("fizz", (string)b.hello); Assert.Equal("buzz", (string)b.world); }
                        );
                    }
                }
            );

            // comment at end
            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, getReader) =>
                {
                    var CSV = "hello,world\r\nfoo,bar\r\n#comment!";
                    await using (var str = await getReader(CSV))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var rows = await csv.ReadAllAsync();
                        Assert.Collection(rows, a => { Assert.Equal("foo", (string)a.hello); Assert.Equal("bar", (string)a.world); });
                    }
                }
            );
        }

        [Fact]
        public async Task RangeAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            var equivalent = new[] { "1", "2", "3" };

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader("a,b,c\r\n1,2,3"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var r1 = await csv.TryReadAsync();
                        Assert.True(r1.HasValue);
                        var row = r1.Value;
                        var r2 = await csv.TryReadAsync();
                        Assert.False(r2.HasValue);

                        // range checks
                        var startLessThan0 = new Range(^4, 3);
                        var endLessthan0 = new Range(0, ^4);
                        var startAfterWidth = new Range(4, 3);
                        var endAfterWidth = new Range(0, 4);
                        var startAfterEnd = new Range(2, 1);

                        Assert.Throws<ArgumentOutOfRangeException>(() => row[startLessThan0]);
                        Assert.Throws<ArgumentOutOfRangeException>(() => row[endLessthan0]);
                        Assert.Throws<ArgumentOutOfRangeException>(() => row[startAfterWidth]);
                        Assert.Throws<ArgumentOutOfRangeException>(() => row[endAfterWidth]);
                        Assert.Throws<ArgumentException>(() => row[startAfterEnd]);
                        // end range

                        var all = 0..3;
                        var allEnd = ^3..^0;
                        var allImp = ..;

                        var skip1Front = 1..3;
                        var skip1FrontImp = 1..;
                        var skip1Back = 0..2;
                        var skip1BackImp = ..2;
                        var skip1FrontEnd = ^2..^0;
                        var skip1FrontEndImp = ^2..;
                        var skip1BackEnd = ^3..^1;
                        var skip1BackEndImp = ..^1;

                        var skip2Front = 2..3;
                        var skip2FrontImp = 2..;
                        var skip2Back = 0..1;
                        var skip2BackImp = ..1;
                        var skip2FrontEnd = ^1..^0;
                        var skip2FrontEndImp = ^1..;
                        var skip2BackEnd = ^3..^2;
                        var skip2BackEndImp = ..^2;

                        var emptyZero = 0..0;
                        var emptyZeroEnd = ^0..^0;
                        var emptyOne = 1..1;
                        var emptyOneEnd = ^1..^1;
                        var emptyTwo = 2..2;
                        var emptyTwoEnd = ^2..^2;

                        Check(all);
                        Check(allEnd);
                        Check(allImp);

                        Check(skip1Front);
                        Check(skip1FrontImp);
                        Check(skip1Back);
                        Check(skip1BackImp);
                        Check(skip1FrontEnd);
                        Check(skip1FrontEndImp);
                        Check(skip1BackEnd);
                        Check(skip1BackEndImp);

                        Check(skip2Front);
                        Check(skip2FrontImp);
                        Check(skip2Back);
                        Check(skip2BackImp);
                        Check(skip2FrontEnd);
                        Check(skip2FrontEndImp);
                        Check(skip2BackEnd);
                        Check(skip2BackEndImp);

                        Check(emptyZero);
                        Check(emptyZeroEnd);
                        Check(emptyOne);
                        Check(emptyOneEnd);
                        Check(emptyTwo);
                        Check(emptyTwoEnd);

                        void Check(Range range)
                        {
                            var dynRes = row[range];
                            var shouldMatchRes = equivalent[range];

                            for (var i = 0; i < shouldMatchRes.Length; i++)
                            {
                                Assert.Equal(shouldMatchRes[i], (string)dynRes[i]);
                            }

                            var ix = 0;
                            foreach (string val in dynRes)
                            {
                                Assert.Equal(shouldMatchRes[ix], val);
                                ix++;
                            }
                        }
                    }
                }
            );
        }

        [Fact]
        public async Task IndexAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader("a,b,c\r\n1,2,3"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var res1 = await csv.TryReadAsync();
                        Assert.True(res1.HasValue);
                        var row = res1.Value;

                        var res2 = await csv.TryReadAsync();
                        Assert.False(res2.HasValue);

                        var zeroFromStart = new Index(0, false);
                        var oneFromStart = new Index(1, false);
                        var twoFromStart = new Index(2, false);

                        var oneFromEnd = new Index(1, true);
                        var twoFromEnd = new Index(2, true);
                        var threeFromEnd = new Index(3, true);

                        int a1 = row[zeroFromStart];
                        int b1 = row[oneFromStart];
                        int c1 = row[twoFromStart];

                        int a2 = row[threeFromEnd];
                        int b2 = row[twoFromEnd];
                        int c2 = row[oneFromEnd];

                        int a3 = row[(Index)0];
                        int b3 = row[(Index)1];
                        int c3 = row[(Index)2];

                        int a4 = row[^3];
                        int b4 = row[^2];
                        int c4 = row[^1];

                        Assert.Equal(1, a1);
                        Assert.Equal(1, a2);
                        Assert.Equal(1, a3);
                        Assert.Equal(1, a4);

                        Assert.Equal(2, b1);
                        Assert.Equal(2, b2);
                        Assert.Equal(2, b3);
                        Assert.Equal(2, b4);

                        Assert.Equal(3, c1);
                        Assert.Equal(3, c2);
                        Assert.Equal(3, c3);
                        Assert.Equal(3, c4);
                    }
                }
            );
        }

        [Fact]
        public async Task CustomDynamicCellConverterAsync()
        {
            // method
            {
                var converter = new _CustomDynamicCellConverter();
                var mtd = typeof(DynamicReaderTests).GetMethod(nameof(_CustomDynamicCellConverter_Int), BindingFlags.Public | BindingFlags.Static);
                var cellConverter = Parser.ForMethod(mtd);

                converter.Add(typeof(int).GetTypeInfo(), cellConverter);

                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(converter).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Int_Calls = 0;

                        await using (var str = await getReader("a,bb,ccc"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var x = await csv.TryReadAsync();
                            Assert.True(x.HasValue);
                            var t1 = x.Value;

                            var res1 = (int)(t1[0]);
                            Assert.Equal(3, res1);
                            Assert.Equal(1, _CustomDynamicCellConverter_Int_Calls);

                            var res2 = (int)(t1[1]);
                            Assert.Equal(4, res2);
                            Assert.Equal(2, _CustomDynamicCellConverter_Int_Calls);

                            var res3 = (int)(t1[2]);
                            Assert.Equal(5, res3);
                            Assert.Equal(3, _CustomDynamicCellConverter_Int_Calls);

                            x = await csv.TryReadAsync();
                            Assert.False(x.HasValue);
                        }
                    }
                );
            }

            // delegate
            {
                var converter = new _CustomDynamicCellConverter();
                var called = 0;
                // whole point is to use a delegate
#pragma warning disable IDE0039
                ParserDelegate<int> del =
                    (ReadOnlySpan<char> _, in ReadContext ctx, out int val) =>
                    {
                        called++;

                        val = ctx.Column.Index + 4;

                        return true;
                    };
#pragma warning restore IDE0039
                var cellConverter = Parser.ForDelegate(del);

                converter.Add(typeof(int).GetTypeInfo(), cellConverter);

                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(converter).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        called = 0;

                        await using (var str = await getReader("a,bb,ccc"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var x = await csv.TryReadAsync();
                            Assert.True(x.HasValue);
                            var t1 = x.Value;

                            var res1 = (int)(t1[0]);
                            Assert.Equal(4, res1);
                            Assert.Equal(1, called);

                            var res2 = (int)(t1[1]);
                            Assert.Equal(5, res2);
                            Assert.Equal(2, called);

                            var res3 = (int)(t1[2]);
                            Assert.Equal(6, res3);
                            Assert.Equal(3, called);

                            x = await csv.TryReadAsync();
                            Assert.False(x.HasValue);
                        }
                    }
                );
            }

            // 1 param constructor
            {
                var converter = new _CustomDynamicCellConverter();
                var cons = typeof(_CustomDynamicCellConverter_Cons).GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
                var cellConverter = Parser.ForConstructor(cons);

                converter.Add(typeof(_CustomDynamicCellConverter_Cons).GetTypeInfo(), cellConverter);

                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(converter).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Cons1_Called = 0;

                        await using (var str = await getReader("a,bb,ccc"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var x = await csv.TryReadAsync();
                            Assert.True(x.HasValue);
                            var t1 = x.Value;

                            var res1 = (_CustomDynamicCellConverter_Cons)(t1[0]);
                            Assert.Equal("a", res1.Val);
                            Assert.Equal(1, _CustomDynamicCellConverter_Cons1_Called);

                            var res2 = (_CustomDynamicCellConverter_Cons)(t1[1]);
                            Assert.Equal("bb", res2.Val);
                            Assert.Equal(2, _CustomDynamicCellConverter_Cons1_Called);

                            var res3 = (_CustomDynamicCellConverter_Cons)(t1[2]);
                            Assert.Equal("ccc", res3.Val);
                            Assert.Equal(3, _CustomDynamicCellConverter_Cons1_Called);

                            x = await csv.TryReadAsync();
                            Assert.False(x.HasValue);
                        }
                    }
                );
            }

            // 2 params constructor
            {
                var converter = new _CustomDynamicCellConverter();
                var cons = typeof(_CustomDynamicCellConverter_Cons).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });
                var cellConverter = Parser.ForConstructor(cons);

                converter.Add(typeof(_CustomDynamicCellConverter_Cons).GetTypeInfo(), cellConverter);

                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(converter).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        _CustomDynamicCellConverter_Cons2_Called = 0;

                        await using (var str = await getReader("a,bb,ccc"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var r = await csv.TryReadAsync();
                            Assert.True(r.HasValue);
                            var t1 = r.Value;

                            var res1 = (_CustomDynamicCellConverter_Cons)(t1[0]);
                            Assert.Equal("a0", res1.Val);
                            Assert.Equal(1, _CustomDynamicCellConverter_Cons2_Called);

                            var res2 = (_CustomDynamicCellConverter_Cons)(t1[1]);
                            Assert.Equal("bb1", res2.Val);
                            Assert.Equal(2, _CustomDynamicCellConverter_Cons2_Called);

                            var res3 = (_CustomDynamicCellConverter_Cons)(t1[2]);
                            Assert.Equal("ccc2", res3.Val);
                            Assert.Equal(3, _CustomDynamicCellConverter_Cons2_Called);

                            r = await csv.TryReadAsync();
                            Assert.False(r.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task DetectLineEndingsAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithRowEnding(RowEnding.Detect).WithReadHeader(ReadHeader.Never).ToOptions();

            // normal
            {
                // \r\n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("a,bb,ccc\r\ndddd,eeeee,ffffff\r\n1,2,3\r\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("a,bb,ccc\rdddd,eeeee,ffffff\r1,2,3\r"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("a,bb,ccc\ndddd,eeeee,ffffff\n1,2,3\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("eeeee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal(3, (int)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );
            }

            // quoted
            {
                // \r\n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"a\r\",bb,ccc\r\ndddd,\"ee\neee\",ffffff\r\n1,2,\"3\r\n\"\r\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"a\r\",bb,ccc\rdddd,\"ee\neee\",ffffff\r1,2,\"3\r\n\"\r"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"a\r\",bb,ccc\ndddd,\"ee\neee\",ffffff\n1,2,\"3\r\n\"\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("bb", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal(2, (int)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );
            }

            // escaped
            {
                // \r\n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"a\r\",\"b\"\"b\",ccc\r\n\"\"\"dddd\",\"ee\neee\",ffffff\r\n1,\"\"\"2\"\"\",\"3\r\n\"\r\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \r
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"a\r\",\"b\"\"b\",ccc\r\"\"\"dddd\",\"ee\neee\",ffffff\r1,\"\"\"2\"\"\",\"3\r\n\"\r"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );

                // \n
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, getReader) =>
                    {
                        await using (var str = await getReader("\"a\r\",\"b\"\"b\",ccc\n\"\"\"dddd\",\"ee\neee\",ffffff\n1,\"\"\"2\"\"\",\"3\r\n\"\n"))
                        await using (var reader = config.CreateAsyncReader(str))
                        {
                            var res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t1 = res.Value;
                            Assert.Equal("a\r", (string)t1[0]);
                            Assert.Equal("b\"b", (string)t1[1]);
                            Assert.Equal("ccc", (string)t1[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t2 = res.Value;
                            Assert.Equal("\"dddd", (string)t2[0]);
                            Assert.Equal("ee\neee", (string)t2[1]);
                            Assert.Equal("ffffff", (string)t2[2]);

                            res = await reader.TryReadAsync();
                            Assert.True(res.HasValue);
                            var t3 = res.Value;
                            Assert.Equal(1, (int)t3[0]);
                            Assert.Equal("\"2\"", (string)t3[1]);
                            Assert.Equal("3\r\n", (string)t3[2]);

                            res = await reader.TryReadAsync();
                            Assert.False(res.HasValue);
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task MultiAsync()
        {
            var optsHeader = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            // with headers
            await RunAsyncDynamicReaderVariants(
                optsHeader,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader("A,B\r\nfoo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1.A;
                                string a2 = row1["A"];
                                string a3 = row1[0];

                                Assert.Equal("foo", a1);
                                Assert.Equal("foo", a2);
                                Assert.Equal("foo", a3);

                                string b1 = row1.B;
                                string b2 = row1["B"];
                                string b3 = row1[1];

                                Assert.Equal("bar", b1);
                                Assert.Equal("bar", b2);
                                Assert.Equal("bar", b3);
                            },
                            row2 =>
                            {
                                int a1 = row2.A;
                                int a2 = row2["A"];
                                int a3 = row2[0];

                                Assert.Equal(1, a1);
                                Assert.Equal(1, a2);
                                Assert.Equal(1, a3);

                                double b1 = row2.B;
                                double b2 = row2["B"];
                                double b3 = row2[1];

                                Assert.Equal(3.3, b1);
                                Assert.Equal(3.3, b2);
                                Assert.Equal(3.3, b3);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3.A;
                                DateTime a2 = row3["A"];
                                DateTime a3 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a2);
                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a3);

                                char b1 = row3.B;
                                char b2 = row3["B"];
                                char b3 = row3[1];

                                Assert.Equal('d', b1);
                                Assert.Equal('d', b2);
                                Assert.Equal('d', b3);
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no headers
            await RunAsyncDynamicReaderVariants(
                optsNoHeader,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader("foo,bar\r\n1,3.3\r\n2019-01-01,d"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row1 =>
                            {
                                string a1 = row1[0];

                                Assert.Equal("foo", a1);

                                string b1 = row1[1];

                                Assert.Equal("bar", b1);
                            },
                            row2 =>
                            {
                                int a1 = row2[0];

                                Assert.Equal(1, a1);

                                double b1 = row2[1];

                                Assert.Equal(3.3, b1);
                            },
                            row3 =>
                            {
                                DateTime a1 = row3[0];

                                Assert.Equal(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), a1);

                                char b1 = row3[1];

                                Assert.Equal('d', b1);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task SimpleAsync()
        {
            var optsHeader = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            // with headers
            await RunAsyncDynamicReaderVariants(
                optsHeader,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader("A,B\r\nfoo,bar"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];
                                string aName = row["A"];
                                string aMem = row.A;
                                string aCol = row[ColumnIdentifier.Create(0, "A")];

                                Assert.Equal("foo", aIx);
                                Assert.Equal("foo", aName);
                                Assert.Equal("foo", aMem);
                                Assert.Equal("foo", aCol);

                                string bIx = row[1];
                                string bName = row["B"];
                                string bMem = row.B;
                                string bCol = row[ColumnIdentifier.Create(1)];

                                Assert.Equal("bar", bIx);
                                Assert.Equal("bar", bName);
                                Assert.Equal("bar", bMem);
                                Assert.Equal("bar", bCol);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // untyped enumerator
                                {
                                    System.Collections.IEnumerable e = row;
                                    var i = e.GetEnumerator();

                                    var reset = true;
loop:
                                    var ix = 0;
                                    while (i.MoveNext())
                                    {
                                        string val = (dynamic)i.Current;
                                        switch (ix)
                                        {
                                            case 0: Assert.Equal("foo", val); break;
                                            case 1: Assert.Equal("bar", val); break;
                                            default:
                                                Assert.Null("Shouldn't be possible");
                                                break;
                                        }
                                        ix++;
                                    }

                                    Assert.Equal(2, ix);

                                    if (reset)
                                    {
                                        reset = false;
                                        i.Reset();
                                        goto loop;
                                    }
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerator
                                {
                                    IEnumerable<string> e = row;
                                    using (var i = e.GetEnumerator())
                                    {
                                        var reset = true;
loop:
                                        var ix = 0;
                                        while (i.MoveNext())
                                        {
                                            string val = i.Current;
                                            switch (ix)
                                            {
                                                case 0: Assert.Equal("foo", val); break;
                                                case 1: Assert.Equal("bar", val); break;
                                                default:
                                                    Assert.Null("Shouldn't be possible");
                                                    break;
                                            }
                                            ix++;
                                        }

                                        Assert.Equal(2, ix);

                                        if (reset)
                                        {
                                            reset = false;
                                            i.Reset();
                                            goto loop;
                                        }
                                    }
                                }
                            }
                        );
                    }
                }
            );

            var optsNoHeader = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no headers
            await RunAsyncDynamicReaderVariants(
                optsNoHeader,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader("foo,bar"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                string aIx = row[0];

                                Assert.Equal("foo", aIx);

                                string bIx = row[1];

                                Assert.Equal("bar", bIx);

                                // untyped enumerable
                                {
                                    System.Collections.IEnumerable e = row;
                                    Assert.Collection(
                                        e.Cast<dynamic>().Select(o => (string)o),
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // untyped enumerator
                                {
                                    System.Collections.IEnumerable e = row;
                                    var i = e.GetEnumerator();

                                    var reset = true;
loop:
                                    var ix = 0;
                                    while (i.MoveNext())
                                    {
                                        string val = (dynamic)i.Current;
                                        switch (ix)
                                        {
                                            case 0: Assert.Equal("foo", val); break;
                                            case 1: Assert.Equal("bar", val); break;
                                            default:
                                                Assert.Null("Shouldn't be possible");
                                                break;
                                        }
                                        ix++;
                                    }

                                    Assert.Equal(2, ix);

                                    if (reset)
                                    {
                                        reset = false;
                                        i.Reset();
                                        goto loop;
                                    }
                                }

                                // typed enumerable
                                {
                                    IEnumerable<string> e = row;
                                    Assert.Collection(
                                        e,
                                        a => Assert.Equal("foo", a),
                                        b => Assert.Equal("bar", b)
                                    );
                                }

                                // typed enumerator
                                {
                                    IEnumerable<string> e = row;
                                    using (var i = e.GetEnumerator())
                                    {
                                        var reset = true;
loop:
                                        var ix = 0;
                                        while (i.MoveNext())
                                        {
                                            string val = i.Current;
                                            switch (ix)
                                            {
                                                case 0: Assert.Equal("foo", val); break;
                                                case 1: Assert.Equal("bar", val); break;
                                                default:
                                                    Assert.Null("Shouldn't be possible");
                                                    break;
                                            }
                                            ix++;
                                        }

                                        Assert.Equal(2, ix);

                                        if (reset)
                                        {
                                            reset = false;
                                            i.Reset();
                                            goto loop;
                                        }
                                    }
                                }
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task ConversionsAsync()
        {
            var optsHeaders = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            // with headers
            await RunAsyncDynamicReaderVariants(
                optsHeaders,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row.B;
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );

            var optsNoHeaders = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // with no headers
            await RunAsyncDynamicReaderVariants(
                optsNoHeaders,
                async (config, makeReader) =>
                {
                    await using (var reader = await makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500"))
                    await using (var csv = config.CreateAsyncReader(reader))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var a = row[0];
                                string aStr = a;
                                int aInt = a;
                                float aFloat = a;
                                _Conversions aC = a;

                                Assert.Equal("1", aStr);
                                Assert.Equal(1, aInt);
                                Assert.Equal(1f, aFloat);
                                Assert.Equal(1, aC.I);

                                var b = row[1];
                                string bStr = b;
                                Guid bGuid = b;

                                Assert.Equal("57DEC02E-BDD6-4AF1-90F5-037596E08500", bStr);
                                Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), bGuid);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task TupleAsync()
        {
            var optWithHeaders = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            // headers
            {
                // one
                await RunAsyncDynamicReaderVariants(
                    optWithHeaders,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("A\r\n1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    optWithHeaders,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // skipped
                await RunAsyncDynamicReaderVariants(
                    optWithHeaders,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("A,B,C\r\n1,,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, string, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal("", typed.Item2);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item3);
                                },
                                row2 =>
                                {
                                    Tuple<string, int?, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(default, typed.Item2);
                                    Assert.Equal(-123, typed.Item3);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        optWithHeaders,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            var optNoHeaders = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no headers
            {
                // one
                await RunAsyncDynamicReaderVariants(
                    optNoHeaders,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    Tuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    optNoHeaders,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    Tuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // skipped
                await RunAsyncDynamicReaderVariants(
                    optNoHeaders,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("1,,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    Tuple<int, string, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal("", typed.Item2);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item3);
                                },
                                row2 =>
                                {
                                    Tuple<string, int?, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(default, typed.Item2);
                                    Assert.Equal(-123, typed.Item3);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                        1,
                        Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                        true,
                        false,
                        long.MaxValue,
                        0.123f,
                        (sbyte)-123,
                        (byte)128,
                        TimeSpan.FromMilliseconds(1234567890),

                        2,
                        Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                        "blue",
                        _Tuple.Green,
                        ulong.MaxValue,
                        -999999.99m,
                        (short)-12300,
                        (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        optNoHeaders,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader(row1))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        Tuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            Tuple<
                                                byte,
                                                TimeSpan,

                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                Tuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task ValueTupleAsync()
        {
            // headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

                // one
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("A\r\n1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("A,B\r\n1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // skipped
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("A,B,C\r\n1,,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, string, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal("", typed.Item2);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item3);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, int?, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(default, typed.Item2);
                                    Assert.Equal(-123, typed.Item3);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader($"A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q\r\n{row1}"))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }

            // no headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

                // one
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("1\r\nfoo"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                },
                                row2 =>
                                {
                                    ValueTuple<string> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                }
                            );
                        }
                    }
                );

                // two
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("1,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item2);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(-123, typed.Item2);
                                }
                            );
                        }
                    }
                );

                // skipped
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var reader = await makeReader("1,,57DEC02E-BDD6-4AF1-90F5-037596E08500\r\nfoo,,-123"))
                        await using (var csv = config.CreateAsyncReader(reader))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row1 =>
                                {
                                    ValueTuple<int, string, Guid> typed = row1;
                                    Assert.Equal(1, typed.Item1);
                                    Assert.Equal("", typed.Item2);
                                    Assert.Equal(Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"), typed.Item3);
                                },
                                row2 =>
                                {
                                    ValueTuple<string, int?, short> typed = row2;
                                    Assert.Equal("foo", typed.Item1);
                                    Assert.Equal(default, typed.Item2);
                                    Assert.Equal(-123, typed.Item3);
                                }
                            );
                        }
                    }
                );

                // 17
                {
                    var row1Val =
                        new object[]
                        {
                            1,
                            Guid.Parse("57DEC02E-BDD6-4AF1-90F5-037596E08500"),
                            true,
                            false,
                            long.MaxValue,
                            0.123f,
                            (sbyte)-123,
                            (byte)128,
                            TimeSpan.FromMilliseconds(1234567890),

                            2,
                            Guid.Parse("77DEF02E-BDD6-4AF1-90F5-037596E08599"),
                            "blue",
                            _Tuple.Green,
                            ulong.MaxValue,
                            -999999.99m,
                            (short)-12300,
                            (sbyte)-2
                        };
                    var row1 = string.Join(",", row1Val);

                    await RunAsyncDynamicReaderVariants(
                        opts,
                        async (config, makeReader) =>
                        {
                            await using (var reader = await makeReader(row1))
                            await using (var csv = config.CreateAsyncReader(reader))
                            {
                                var read = await csv.ReadAllAsync();

                                Assert.Collection(
                                    read,
                                    row1 =>
                                    {
                                        ValueTuple<
                                            int,
                                            Guid,
                                            bool,
                                            bool,
                                            long,
                                            float,
                                            sbyte,
                                            ValueTuple<
                                                byte,
                                                TimeSpan,
                                                int,
                                                Guid,
                                                string,
                                                _Tuple,
                                                ulong,
                                                ValueTuple<
                                                    decimal,
                                                    short,
                                                    sbyte
                                                >
                                            >
                                        > typed = row1;
                                        Assert.Equal(row1Val[0], typed.Item1);
                                        Assert.Equal(row1Val[1], typed.Item2);
                                        Assert.Equal(row1Val[2], typed.Item3);
                                        Assert.Equal(row1Val[3], typed.Item4);
                                        Assert.Equal(row1Val[4], typed.Item5);
                                        Assert.Equal(row1Val[5], typed.Item6);
                                        Assert.Equal(row1Val[6], typed.Item7);
                                        Assert.Equal(row1Val[7], typed.Rest.Item1);
                                        Assert.Equal(row1Val[8], typed.Rest.Item2);
                                        Assert.Equal(row1Val[9], typed.Rest.Item3);
                                        Assert.Equal(row1Val[10], typed.Rest.Item4);
                                        Assert.Equal(row1Val[11], typed.Rest.Item5);
                                        Assert.Equal(row1Val[12], typed.Rest.Item6);
                                        Assert.Equal(row1Val[13], typed.Rest.Item7);
                                        Assert.Equal(row1Val[14], typed.Rest.Rest.Item1);
                                        Assert.Equal(row1Val[15], typed.Rest.Rest.Item2);
                                        Assert.Equal(row1Val[16], typed.Rest.Rest.Item3);
                                    }
                                );
                            }
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task POCO_ConstructorAsync()
        {
            // headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var str = await makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var str = await makeReader("1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (_POCO_Constructor)row;

                                    Assert.Equal(1, lo.Prop1);
                                    Assert.Equal("foo", lo.Prop2);
                                    Assert.Equal(new DateTime(2019, 01, 03), lo.Prop3);
                                }
                            );
                        }
                    }
                );
            }
        }

        [Fact]
        public async Task POCO_PropertiesAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).ToOptions();

            await RunAsyncDynamicReaderVariants(
                opts,
                async (config, makeReader) =>
                {
                    await using (var str = await makeReader("A,B,C\r\n1,foo,2019-01-03"))
                    await using (var csv = config.CreateAsyncReader(str))
                    {
                        var read = await csv.ReadAllAsync();

                        Assert.Collection(
                            read,
                            row =>
                            {
                                var lo = (_POCO_Properties)row;

                                Assert.Equal(1, lo.A);
                                Assert.Equal("foo", lo.B);
                                Assert.Equal(new DateTime(2019, 01, 03), lo.C);
                            }
                        );
                    }
                }
            );
        }

        [Fact]
        public async Task DynamicRowDisposalOptionsAsync()
        {
            // dispose with reader
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        List<dynamic> read;

                        await using (var str = await makeReader("1,2,3"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // explodes now that reader is disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    cancellable: false
                );
            }

            // explicit disposal
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        List<dynamic> read;

                        await using (var str = await makeReader("1,2,3"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    int a = row[0];
                                    int b = row[1];
                                    int c = row[2];

                                    Assert.Equal(1, a);
                                    Assert.Equal(2, b);
                                    Assert.Equal(3, c);
                                }
                            );
                        }

                        // still good after reader
                        Assert.Collection(
                            read,
                            row =>
                            {
                                int a = row[0];
                                int b = row[1];
                                int c = row[2];

                                Assert.Equal(1, a);
                                Assert.Equal(2, b);
                                Assert.Equal(3, c);
                            }
                        );

                        foreach (var r in read)
                        {
                            r.Dispose();
                        }

                        // explodes now that row are disposed
                        Assert.Collection(
                            read,
                            row =>
                            {
                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            }
                        );
                    },
                    cancellable: false
                );
            }
        }

        [Fact]
        public async Task ReusingRowsAsync()
        {
            // both auto
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts,
                            async (config2, makeReader2) =>
                            {
                                await using (var r1 = await makeReader1("1,2,3\r\n4,5,6"))
                                await using (var r2 = await makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    dynamic row = null;
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.False(res.HasValue);
                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.False(res.HasValue);
                                }
                            },
                            cancellable: false
                        );
                    },
                    checkRunCounts: false,
                    cancellable: false
                );
            }

            // auto then explicitly
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();
                var opts2 = Options.CreateBuilder(opts1).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts2,
                            async (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                await using (var r1 = await makeReader1("1,2,3\r\n4,5,6"))
                                await using (var r2 = await makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            },
                            cancellable: false
                        );
                    },
                    checkRunCounts: false,
                    cancellable: false
                );
            }

            // explicitly then auto
            {
                var opts1 = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
                var opts2 = Options.CreateBuilder(opts1).WithDynamicRowDisposal(DynamicRowDisposal.OnReaderDispose).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts1,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts2,
                            async (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                await using (var r1 = await makeReader1("1,2,3\r\n4,5,6"))
                                await using (var r2 = await makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);


                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);
                                }

                                Assert.Equal(4, (int)row[0]);
                                Assert.Equal(5, (int)row[1]);
                                Assert.Equal(6, (int)row[2]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            },
                            cancellable: false
                        );
                    },
                    checkRunCounts: false,
                    cancellable: false
                );
            }

            // both explicitly
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose).ToOptions();
                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config1, makeReader1) =>
                    {
                        await RunAsyncDynamicReaderVariants(
                            opts,
                            async (config2, makeReader2) =>
                            {
                                dynamic row = null;

                                await using (var r1 = await makeReader1("1,2,3\r\n4,5,6"))
                                await using (var r2 = await makeReader2("7,8\r\n9,10"))
                                await using (var csv1 = config1.CreateAsyncReader(r1))
                                await using (var csv2 = config2.CreateAsyncReader(r2))
                                {
                                    var res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(1, (int)row[0]);
                                    Assert.Equal(2, (int)row[1]);
                                    Assert.Equal(3, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(7, (int)row[0]);
                                    Assert.Equal(8, (int)row[1]);

                                    res = await csv1.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(4, (int)row[0]);
                                    Assert.Equal(5, (int)row[1]);
                                    Assert.Equal(6, (int)row[2]);

                                    res = await csv2.TryReadWithReuseAsync(ref row);
                                    Assert.True(res.HasValue);
                                    row = res.Value;
                                    Assert.Equal(9, (int)row[0]);
                                    Assert.Equal(10, (int)row[1]);
                                }

                                Assert.Equal(9, (int)row[0]);
                                Assert.Equal(10, (int)row[1]);

                                row.Dispose();

                                Assert.Throws<ObjectDisposedException>(() => row[0]);
                            },
                            cancellable: false
                        );
                    },
                    checkRunCounts: false,
                    cancellable: false
                );
            }
        }

        [Fact]
        public async Task DelegateRowConversionsAsync()
        {
            // whole point is to use a delegate
#pragma warning disable IDE0039
            DynamicRowConverterDelegate<__DelegateRowConversions_Row> x =
                (dynamic row, in ReadContext ctx, out __DelegateRowConversions_Row res) =>
                {
                    var a = (string)row[0];
                    var b = (string)row[1];
                    var c = (string)row[2];

                    var x = a + b + b + c + c + c;

                    res = new __DelegateRowConversions_Row { Yup = x };

                    return true;
                };
#pragma warning restore IDE0039

            var convert = new _DelegateRowConversions<__DelegateRowConversions_Row>(x);

            // headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Always).WithTypeDescriber(convert).ToOptions();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var str = await makeReader("A,B,C\r\n1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (__DelegateRowConversions_Row)row;

                                    Assert.Equal("1foofoo2019-01-032019-01-032019-01-03", lo.Yup);
                                }
                            );
                        }
                    }
                );
            }

            // no headers
            {
                var opts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).WithTypeDescriber(convert).ToOptions();

                await RunAsyncDynamicReaderVariants(
                    opts,
                    async (config, makeReader) =>
                    {
                        await using (var str = await makeReader("1,foo,2019-01-03"))
                        await using (var csv = config.CreateAsyncReader(str))
                        {
                            var read = await csv.ReadAllAsync();

                            Assert.Collection(
                                read,
                                row =>
                                {
                                    var lo = (__DelegateRowConversions_Row)row;

                                    Assert.Equal("1foofoo2019-01-032019-01-032019-01-03", lo.Yup);
                                }
                            );
                        }
                    }
                );
            }
        }
    }
}