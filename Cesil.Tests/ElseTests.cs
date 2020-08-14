using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;

namespace Cesil.Tests
{
    public class ElseTests
    {
        private sealed class _DynamicRowConverters
        {
            public string Foo { get; set; }

            public _DynamicRowConverters(dynamic obj) { }

            public _DynamicRowConverters(string foo) { }

            public _DynamicRowConverters() { }
        }

        private static bool _DynamicRowConverters_Mtd(dynamic row, in ReadContext ctx, out _DynamicRowConverters val)
        {
            val = null;
            return true;
        }

        private delegate bool _DynamicRowConverters_Delegate(object row, in ReadContext ctx, out _DynamicRowConverters val);

        [Fact]
        public void DynamicRowConverters()
        {
            var t = typeof(_DynamicRowConverters).GetTypeInfo();

            var cons1 = t.GetConstructor(new[] { typeof(object) });
            var cons2 = t.GetConstructor(new[] { typeof(string) });
            var cons3 = t.GetConstructor(Type.EmptyTypes);
            var s1 = Setter.ForMethod(typeof(_DynamicRowConverters).GetProperty(nameof(_DynamicRowConverters.Foo)).SetMethod);
            var m1 = typeof(ElseTests).GetMethod(nameof(_DynamicRowConverters_Mtd), BindingFlags.NonPublic | BindingFlags.Static);

            var d1 = DynamicRowConverter.ForConstructorTakingDynamic(cons1);
            var d2 = DynamicRowConverter.ForConstructorTakingTypedParameters(cons2, new[] { ColumnIdentifier.Create(0) });
            var d3 = DynamicRowConverter.ForEmptyConstructorAndSetters(cons3, new[] { s1 }, new[] { ColumnIdentifier.Create(0) });
            var d4 = DynamicRowConverter.ForDelegate((dynamic row, in ReadContext ctx, out _DynamicRowConverters val) => { val = null; return true; });
            var d5 = DynamicRowConverter.ForMethod(m1);

            var chainable = new[] { d1, d2, d3, d4, d5 };

            for (var i = 0; i < chainable.Length; i++)
            {
                var root = chainable[i];

                var chained = root;

                for (var j = 0; j < chainable.Length; j++)
                {
                    chained = chained.Else(chainable[j]);
                }

                var p = Expression.Parameter(Types.Object);
                var ctx = Expression.Parameter(Types.ReadContext.MakeByRefType());
                var outVar = Expression.Parameter(t.MakeByRefType());

                var asRow = Expression.Convert(p, Types.DynamicRow);
                var rowVar = Expressions.Variable_DynamicRow;
                var assignRowVar = Expression.Assign(rowVar, asRow);

                var body = Expression.Block(new[] { rowVar }, assignRowVar, chained.MakeExpression(t, rowVar, ctx, outVar));

                var lambda = Expression.Lambda<_DynamicRowConverters_Delegate>(body, new[] { p, ctx, outVar });
                var del = lambda.Compile();
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => d1.Else(null));

            // doesn't produce same type
            var diffType = DynamicRowConverter.ForDelegate((dynamic row, in ReadContext ctx, out string val) => { val = null; return true; });
            Assert.Throws<ArgumentException>(() => d1.Else(diffType));
        }

        private sealed class _Formatters1 { }

        private static bool _Formatters_Mtd(_Formatters1 data, in WriteContext ctx, IBufferWriter<char> writer) => true;

        private sealed class _Formatters2 { }

        private delegate bool _Formatters_Delegate(_Formatters1 data, in WriteContext ctx, IBufferWriter<char> writer);

        [Fact]
        public void Formatters()
        {
            var f1 = Formatter.ForDelegate((_Formatters1 data, in WriteContext ctx, IBufferWriter<char> writer) => true);
            var f2 = Formatter.ForMethod(typeof(ElseTests).GetMethod(nameof(_Formatters_Mtd), BindingFlags.NonPublic | BindingFlags.Static));

            var chainable = new[] { f1, f2 };

            for (var i = 0; i < chainable.Length; i++)
            {
                var root = chainable[i];

                var chained = root;

                for (var j = 0; j < chainable.Length; j++)
                {
                    chained = chained.Else(chainable[j]);
                }

                var data = Expression.Parameter(typeof(_Formatters1).GetTypeInfo());
                var writeContext = Expression.Parameter(Types.WriteContext.MakeByRefType());
                var writer = Expression.Parameter(Types.IBufferWriterOfChar);

                var body = chained.MakeExpression(data, writeContext, writer);

                var lambda = Expression.Lambda<_Formatters_Delegate>(body, new[] { data, writeContext, writer });
                var del = lambda.Compile();
            }

            // nullability
            {
#nullable enable
                var fCanTakeNull = Formatter.ForDelegate((string? foo, in WriteContext ctx, IBufferWriter<char> buffer) => true);
                var fCannotTakeNull = Formatter.ForDelegate((string foo, in WriteContext ctx, IBufferWriter<char> buffer) => true);
#nullable disable

                // something that requires null checks can happen before something that _doesn't_
                var combo = fCannotTakeNull.Else(fCanTakeNull);
                Assert.Equal(NullHandling.ForbidNull, combo.TakesNullability);
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => f1.Else(null));

            // doesn't take same type
            var diffType = Formatter.ForDelegate((_Formatters2 data, in WriteContext ctx, IBufferWriter<char> writer) => true);
            Assert.Throws<ArgumentException>(() => f1.Else(diffType));
        }

        private sealed class _InstanceProviders1 { }

        private sealed class _InstanceProviders2 { }

        private static bool _InstanceProviders_Mtd(in ReadContext ctx, out _InstanceProviders1 val)
        {
            val = null;
            return true;
        }

        private delegate bool _InstanceProviders_Delegate(in ReadContext ctx, out _InstanceProviders1 val);

        [Fact]
        public void InstanceProviders()
        {
            var i1 = InstanceProvider.ForDelegate((in ReadContext ctx, out _InstanceProviders1 val) => { val = null; return true; });
            var i2 = InstanceProvider.ForParameterlessConstructor(typeof(_InstanceProviders1).GetConstructor(Type.EmptyTypes));
            var i3 = InstanceProvider.ForMethod(typeof(ElseTests).GetMethod(nameof(_InstanceProviders_Mtd), BindingFlags.NonPublic | BindingFlags.Static));

            var chainable = new[] { i1, i2, i3 };

            for (var i = 0; i < chainable.Length; i++)
            {
                var root = chainable[i];

                var chained = root;

                for (var j = 0; j < chainable.Length; j++)
                {
                    chained = chained.Else(chainable[j]);
                }

                var t = typeof(_InstanceProviders1).GetTypeInfo();

                var ctx = Expressions.Parameter_ReadContext_ByRef;
                var outVar = Expression.Parameter(t.MakeByRefType());

                var body = chained.MakeExpression(t, ctx, outVar);

                var lambda = Expression.Lambda<_InstanceProviders_Delegate>(body, new[] { ctx, outVar });

                var del = lambda.Compile();
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => i1.Else(null));

            // doesn't produce same type
            var diffType = InstanceProvider.ForDelegate((in ReadContext ctx, out _InstanceProviders2 val) => { val = null; return true; });
            Assert.Throws<ArgumentException>(() => i1.Else(diffType));

            // nullability
            {
#nullable enable
                var ipCanMakeNull = InstanceProvider.ForDelegate((in ReadContext ctx, out string? res) => { res = null; return true; });
                var ipCannotMakeNull = InstanceProvider.ForDelegate((in ReadContext ctx, out string res) => { res = ""; return true; });
#nullable disable
                var ipNullImpossible = InstanceProvider.ForConstructorWithParameters(typeof(string).GetConstructors().First());

                var combo1 = ipCanMakeNull.Else(ipCannotMakeNull);
                var combo2 = ipCanMakeNull.Else(ipCanMakeNull);
                var combo3 = ipCannotMakeNull.Else(ipCannotMakeNull);
                var combo4 = ipNullImpossible.Else(ipCanMakeNull);
                var combo5 = ipNullImpossible.Else(ipCannotMakeNull);
                var combo6 = ipNullImpossible.Else(ipNullImpossible);

                Assert.Equal(NullHandling.AllowNull, combo1.ConstructsNullability);
                Assert.Equal(NullHandling.AllowNull, combo2.ConstructsNullability);
                Assert.Equal(NullHandling.ForbidNull, combo3.ConstructsNullability);
                Assert.Equal(NullHandling.AllowNull, combo4.ConstructsNullability);
                Assert.Equal(NullHandling.ForbidNull, combo5.ConstructsNullability);
                Assert.Equal(NullHandling.CannotBeNull, combo6.ConstructsNullability);
            }
        }

        private sealed class _Parsers1
        {
            public _Parsers1(ReadOnlySpan<char> data) { }
            public _Parsers1(ReadOnlySpan<char> data, in ReadContext ctx) { }
        }

        private sealed class _Parsers2 { }

        private static bool _Parsers_Mtd(ReadOnlySpan<char> data, in ReadContext ctx, out _Parsers1 val)
        {
            val = null;
            return true;
        }

        private delegate bool _Parsers_Delegate(ReadOnlySpan<char> data, in ReadContext ctx, out _Parsers1 val);

        [Fact]
        public void Parsers()
        {
            var t1 = typeof(_Parsers1).GetTypeInfo();
            var cons1 = t1.GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
            var cons2 = t1.GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });
            var p1 = Parser.ForConstructor(cons1);
            var p2 = Parser.ForConstructor(cons2);
            var p3 = Parser.ForDelegate((ReadOnlySpan<char> data, in ReadContext ctx, out _Parsers1 val) => { val = null; return true; });
            var m1 = typeof(ElseTests).GetMethod(nameof(_Parsers_Mtd), BindingFlags.NonPublic | BindingFlags.Static);
            var p4 = Parser.ForMethod(m1);

            var chainable = new[] { p1, p2, p3, p4 };

            for (var i = 0; i < chainable.Length; i++)
            {
                var root = chainable[i];

                var chained = root;

                for (var j = 0; j < chainable.Length; j++)
                {
                    chained = chained.Else(chainable[j]);
                }

                var data = Expression.Parameter(Types.ReadOnlySpanOfChar);
                var ctx = Expression.Parameter(Types.ReadContext.MakeByRefType());
                var outVar = Expression.Parameter(t1.MakeByRefType());

                var body = chained.MakeExpression(data, ctx, outVar);

                var lambda = Expression.Lambda<_Parsers_Delegate>(body, new[] { data, ctx, outVar });
                var del = lambda.Compile();
            }

            // errors
            Assert.Throws<ArgumentNullException>(() => p1.Else(null));

            // doesn't produce same type
            var diffType = Parser.ForDelegate((ReadOnlySpan<char> data, in ReadContext ctx, out _Parsers2 val) => { val = null; return true; });
            Assert.Throws<ArgumentException>(() => p1.Else(diffType));

            // nullability
            {
#nullable enable
                var pCanMakeNull = Parser.ForDelegate((ReadOnlySpan<char> data, in ReadContext ctx, out string? res) => { res = null; return true; });
                var pCannotMakeNull = Parser.ForDelegate((ReadOnlySpan<char> data, in ReadContext ctx, out string res) => { res = ""; return true; });
#nullable disable

                var pNullImpossible = Parser.ForConstructor(typeof(string).GetConstructor(new[] { typeof(ReadOnlySpan<char>) }));

                var combo1 = pCanMakeNull.Else(pCanMakeNull);
                var combo2 = pCanMakeNull.Else(pCannotMakeNull);
                var combo3 = pCanMakeNull.Else(pNullImpossible);
                var combo4 = pCannotMakeNull.Else(pCannotMakeNull);
                var combo5 = pCannotMakeNull.Else(pNullImpossible);
                var combo6 = pNullImpossible.Else(pNullImpossible);

                Assert.Equal(NullHandling.AllowNull, combo1.CreatesNullability);
                Assert.Equal(NullHandling.AllowNull, combo2.CreatesNullability);
                Assert.Equal(NullHandling.AllowNull, combo3.CreatesNullability);
                Assert.Equal(NullHandling.ForbidNull, combo4.CreatesNullability);
                Assert.Equal(NullHandling.ForbidNull, combo5.CreatesNullability);
                Assert.Equal(NullHandling.CannotBeNull, combo6.CreatesNullability);
            }
        }

        private sealed class _Simple : IElseSupporting<_Simple>
        {
            public int Val { get; }
            public ImmutableArray<_Simple> Fallbacks { get; }

            public _Simple(int val) : this(val, ImmutableArray<_Simple>.Empty) { }

            private _Simple(int val, ImmutableArray<_Simple> fallbacks)
            {
                Val = val;
                Fallbacks = fallbacks;
            }

            public _Simple Clone(ImmutableArray<_Simple> newFallbacks, NullHandling? _, NullHandling? __)
            => new _Simple(Val, newFallbacks);
        }

        [Fact]
        public void Simple()
        {
            var a = new _Simple(1);
            var b = new _Simple(2);
            var c = new _Simple(3);
            var d = new _Simple(4);

            var abcd = a.DoElse(b, null, null).DoElse(c, null, null).DoElse(d, null, null);
            Assert.NotSame(a, abcd);
            Assert.NotSame(b, abcd);
            Assert.NotSame(c, abcd);
            Assert.NotSame(d, abcd);
            Assert.Equal(1, abcd.Val);
            Assert.Collection(
                abcd.Fallbacks,
                v => Assert.Equal(2, v.Val),
                v => Assert.Equal(3, v.Val),
                v => Assert.Equal(4, v.Val)
            );

            var dcba = d.DoElse(c, null, null).DoElse(b, null, null).DoElse(a, null, null);
            Assert.NotSame(a, dcba);
            Assert.NotSame(b, dcba);
            Assert.NotSame(c, dcba);
            Assert.NotSame(d, dcba);
            Assert.Equal(4, dcba.Val);
            Assert.Collection(
                dcba.Fallbacks,
                v => Assert.Equal(3, v.Val),
                v => Assert.Equal(2, v.Val),
                v => Assert.Equal(1, v.Val)
            );

            // 4 cases
            //  - left has no fallbacks, right has no fallbacks
            //  - left has no fallbacks, right has fallbacks
            //  - left has fallbacks, right has no fallbacks
            //  - both left and right have fallbacks

            // case 1
            {
                var res = a.DoElse(b, null, null);
                Assert.NotSame(a, res);
                Assert.NotSame(b, res);
                Assert.Equal(1, res.Val);
                Assert.Collection(
                    res.Fallbacks,
                    v => Assert.Equal(2, v.Val)
                );
            }

            // case 2
            {
                var res = a.DoElse(abcd, null, null);
                Assert.NotSame(a, res);
                Assert.NotSame(abcd, res);

                Assert.Equal(1, res.Val);
                Assert.Collection(
                    res.Fallbacks,
                    v => Assert.Equal(1, v.Val),
                    v => Assert.Equal(2, v.Val),
                    v => Assert.Equal(3, v.Val),
                    v => Assert.Equal(4, v.Val)
                );
            }

            // case 3
            {
                var res = dcba.DoElse(a, null, null);
                Assert.NotSame(a, res);
                Assert.NotSame(dcba, res);

                Assert.Equal(4, res.Val);
                Assert.Collection(
                    res.Fallbacks,
                    v => Assert.Equal(3, v.Val),
                    v => Assert.Equal(2, v.Val),
                    v => Assert.Equal(1, v.Val),
                    v => Assert.Equal(1, v.Val)
                );
            }

            // case 4
            {
                var res = abcd.DoElse(dcba, null, null);
                Assert.NotSame(abcd, res);
                Assert.NotSame(dcba, res);

                Assert.Equal(1, res.Val);
                Assert.Collection(
                    res.Fallbacks,
                    v => Assert.Equal(2, v.Val),
                    v => Assert.Equal(3, v.Val),
                    v => Assert.Equal(4, v.Val),
                    v => Assert.Equal(4, v.Val),
                    v => Assert.Equal(3, v.Val),
                    v => Assert.Equal(2, v.Val),
                    v => Assert.Equal(1, v.Val)
                );
            }
        }
    }
}
