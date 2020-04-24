using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace Cesil.Tests
{
    public class WrapperTests
    {
        private class _IDelegateCaches
        {
#pragma warning disable CS0649
            public int A;
#pragma warning restore CS0649
        }

        private class _IDelegateCaches_Cache : IDelegateCache
        {
            private readonly Dictionary<object, Delegate> Cache = new Dictionary<object, Delegate>();

            void IDelegateCache.Add<T, V>(T key, V cached)
            => Cache.Add(key, cached);

            CachedDelegate<V> IDelegateCache.TryGet<T, V>(T key)
            {
                if (!Cache.TryGetValue(key, out var obj))
                {
                    return CachedDelegate<V>.Empty;
                }

                return new CachedDelegate<V>(obj as V);
            }
        }

        [Fact]
        public void IDelegateCaches()
        {
            var cache = new _IDelegateCaches_Cache();

            var getter = Getter.ForField(typeof(_IDelegateCaches).GetField(nameof(_IDelegateCaches.A)));
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());

            {
                var getterI = (ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)getter;
                getterI.CachedDelegate.Clear();
                getterI.Guarantee(cache);
                var a = getterI.CachedDelegate.Value;
                Assert.NotNull(a);
                getterI.CachedDelegate.Clear();
                getterI.Guarantee(cache);
                var b = getterI.CachedDelegate.Value;
                Assert.NotNull(b);
                Assert.True(ReferenceEquals(a, b));
            }

            {
                var formatterI = (ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)formatter;
                formatterI.CachedDelegate.Clear();
                formatterI.Guarantee(cache);
                var a = formatterI.CachedDelegate.Value;
                Assert.NotNull(a);
                formatterI.CachedDelegate.Clear();
                formatterI.Guarantee(cache);
                var b = formatterI.CachedDelegate.Value;
                Assert.NotNull(b);
                Assert.True(ReferenceEquals(a, b));
            }
        }

        private class _ColumnSetters_Val
        {
            public readonly string Value;

            public _ColumnSetters_Val(ReadOnlySpan<char> r)
            {
                Value = new string(r);
            }

            public _ColumnSetters_Val(ReadOnlySpan<char> r, in ReadContext ctx) : this(r) { }
        }

        private class _ColumnSetters
        {
            public static bool StaticResetCalled;
            public static bool StaticResetCtxCalled;
            public static bool StaticResetXXXParamCalled;
            public static bool StaticResetXXXParamCtxCalled;
            public static _ColumnSetters_Val StaticField;
            public static _ColumnSetters_Val _Set;

            public bool ResetCalled;
            public bool ResetCtxCalled;

            public _ColumnSetters_Val Prop { get; set; }
#pragma warning disable CS0649
            public _ColumnSetters_Val Field;
#pragma warning restore CS0649

            public _ColumnSetters(_ColumnSetters_Val p) { }

            public void Set(_ColumnSetters_Val c)
            {
                Prop = c;
            }

            public void SetCtx(_ColumnSetters_Val c, in ReadContext ctx)
            {
                Prop = c;
            }

            public static void StaticSet(_ColumnSetters_Val c)
            {
                _Set = c;
            }

            public static void StaticSetParam(_ColumnSetters row, _ColumnSetters_Val c)
            {
                _Set = c;
            }

            public static void StaticSetByRefParam(ref _ColumnSetters row, _ColumnSetters_Val c)
            {
                _Set = c;
            }

            public static void StaticSetCtx(_ColumnSetters_Val c, in ReadContext ctx)
            {
                _Set = c;
            }

            public static void StaticSetParamCtx(_ColumnSetters row, _ColumnSetters_Val c, in ReadContext ctx)
            {
                _Set = c;
            }

            public void ResetXXX()
            {
                ResetCalled = true;
            }

            public void ResetXXXCtx(in ReadContext _)
            {
                ResetCtxCalled = true;
            }

            public static void StaticResetXXX()
            {
                StaticResetCalled = true;
            }

            public static void StaticResetXXXCtx(in ReadContext _)
            {
                StaticResetCtxCalled = true;
            }

            public static void StaticResetXXXParam(_ColumnSetters s)
            {
                StaticResetXXXParamCalled = true;
            }

            public static void StaticResetXXXParamCtx(_ColumnSetters s, in ReadContext _)
            {
                StaticResetXXXParamCtxCalled = true;
            }
        }

        private static bool _ColumnSetters_Parser(ReadOnlySpan<char> r, in ReadContext ctx, out _ColumnSetters_Val v)
        {
            v = new _ColumnSetters_Val(r);
            return true;
        }

        private class _ColumnWriters_Val
        {
            public string Value { get; set; }
        }

        private class _ColumnWriters2
        {
            public _ColumnWriters_Val Get() => new _ColumnWriters_Val { Value = "A" };
            public bool ShouldSerialize() => true;
        }

        private class _ColumnWriters
        {
            public _ColumnWriters_Val GetProp => new _ColumnWriters_Val { Value = "prop" };

            public static _ColumnWriters_Val GetPropStatic => new _ColumnWriters_Val { Value = "static prop" };

            public static _ColumnWriters_Val StaticA;
            public _ColumnWriters_Val A;

            public _ColumnWriters_Val Get() => new _ColumnWriters_Val { Value = "A" };
            public string GetString() => "foo";

            public _ColumnWriters_Val GetCtx(in WriteContext ctx) => new _ColumnWriters_Val { Value = "A" };

            public static _ColumnWriters_Val GetStatic() => new _ColumnWriters_Val { Value = "static" };
            public static _ColumnWriters_Val GetStaticCtx(in WriteContext ctx) => new _ColumnWriters_Val { Value = "static" };
            public static _ColumnWriters_Val GetStaticRow(_ColumnWriters row) => new _ColumnWriters_Val { Value = "static" };
            public static _ColumnWriters_Val GetStaticRowCtx(_ColumnWriters row, in WriteContext ctx) => new _ColumnWriters_Val { Value = "static" };

            public bool ShouldSerializeCalled;
            public bool ShouldSerialize()
            {
                ShouldSerializeCalled = true;
                return true;
            }

            public bool ShouldSerializeCtxCalled;
            public bool ShouldSerializeCtx(in WriteContext _)
            {
                ShouldSerializeCtxCalled = true;
                return true;
            }

            public static bool ShouldSerializeStaticCalled;
            public static bool ShouldSerializeStatic()
            {
                ShouldSerializeStaticCalled = true;
                return true;
            }

            public static bool ShouldSerializeStaticCtxCalled;
            public static bool ShouldSerializeStaticCtx(in WriteContext _)
            {
                ShouldSerializeStaticCtxCalled = true;
                return true;
            }

            public static bool ShouldSerializeStaticWithParamCalled;
            public static bool ShouldSerializeStaticWithParam(_ColumnWriters p)
            {
                ShouldSerializeStaticWithParamCalled = true;
                return true;
            }

            public static bool ShouldSerializeStaticWithParamCtxCalled;
            public static bool ShouldSerializeStaticWithParamCtx(_ColumnWriters p, in WriteContext _)
            {
                ShouldSerializeStaticWithParamCtxCalled = true;
                return true;
            }
        }

        private static bool _ColumnWriters_Val_Format_Called;
        private static bool _ColumnWriters_Val_Format(_ColumnWriters_Val cell, in WriteContext _, IBufferWriter<char> writeTo)
        {
            _ColumnWriters_Val_Format_Called = true;
            writeTo.Write(cell.Value.AsSpan());
            return true;
        }

        [Fact]
        public async Task ColumnWritersAsync()
        {
            string BufferToString(ReadOnlySequence<byte> buff)
            {
                var bytes = new List<byte>();
                foreach (var b in buff)
                {
                    bytes.AddRange(b.ToArray());
                }

                var byteArray = bytes.ToArray();
                var byteSpan = new Span<byte>(byteArray);
                var charSpan = MemoryMarshal.Cast<byte, char>(byteSpan);

                return new string(charSpan);
            }

            // formatters
            var methodFormatter = Formatter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_ColumnWriters_Val_Format), BindingFlags.Static | BindingFlags.NonPublic));
            var delFormatterCalled = false;
            var delFormatter = (Formatter)(FormatterDelegate<_ColumnWriters_Val>)((_ColumnWriters_Val cell, in WriteContext _, IBufferWriter<char> writeTo) => { delFormatterCalled = true; writeTo.Write(cell.Value.AsSpan()); return true; });
            var formatters = new[] { methodFormatter, delFormatter };

            // should serialize
            var methodShouldSerialize = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerialize)));
            var staticMethodShouldSerialize = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerializeStatic)));
            var staticMethodShouldSerializeParam = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerializeStaticWithParam)));
            var delShouldSerializeCalled = false;
            var delShouldSerialize = (ShouldSerialize)(ShouldSerializeDelegate<_ColumnWriters>)((_ColumnWriters _, in WriteContext __) => { delShouldSerializeCalled = true; return true; });
            var staticDelShouldSerializeCalled = false;
            var staticDelShouldSerialize = (ShouldSerialize)(StaticShouldSerializeDelegate)((in WriteContext _) => { staticDelShouldSerializeCalled = true; return true; });
            var shouldSerializes = new[] { methodShouldSerialize, staticMethodShouldSerialize, staticMethodShouldSerializeParam, delShouldSerialize, staticDelShouldSerialize, null };

            // getter
            var methodGetter = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.Get)));
            var staticMethodGetter = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.GetStatic)));
            var fieldGetter = Getter.ForField(typeof(_ColumnWriters).GetField(nameof(_ColumnWriters.A)));
            var staticFieldGetter = Getter.ForField(typeof(_ColumnWriters).GetField(nameof(_ColumnWriters.StaticA)));
            var delGetterCalled = false;
            var delGetter = (Getter)(GetterDelegate<_ColumnWriters, _ColumnWriters_Val>)((_ColumnWriters row, in WriteContext _) => { delGetterCalled = true; return row.A; });
            var staticDelGetterCalled = false;
            var staticDelGetter = (Getter)(StaticGetterDelegate<_ColumnWriters_Val>)((in WriteContext _) => { staticDelGetterCalled = true; return new _ColumnWriters_Val { Value = "foo" }; });
            var getters = new[] { methodGetter, staticMethodGetter, fieldGetter, staticFieldGetter, delGetter, staticDelGetter };

            foreach (var f in formatters)
            {
                foreach (var s in shouldSerializes)
                {
                    foreach (var g in getters)
                    {
                        foreach (var e in new[] { true, false })
                        {
                            delFormatterCalled = delShouldSerializeCalled = staticDelShouldSerializeCalled = delGetterCalled = staticDelGetterCalled = false;

                            _ColumnWriters.StaticA = new _ColumnWriters_Val { Value = "static field" };
                            _ColumnWriters.ShouldSerializeStaticCalled = false;
                            _ColumnWriters.ShouldSerializeStaticWithParamCalled = false;
                            _ColumnWriters_Val_Format_Called = false;

                            var inst = new _ColumnWriters { A = new _ColumnWriters_Val { Value = "bar" } };

                            var sNonNull = new NonNull<ShouldSerialize>();
                            sNonNull.SetAllowNull(s);
                            var colWriter = ColumnWriter.Create(typeof(_ColumnWriters).GetTypeInfo(), Options.Default, f, sNonNull, g, e);

                            var pipe = new Pipe();
                            var writer = new CharWriter(pipe.Writer);
                            var reader = pipe.Reader;

                            var res = colWriter(inst, default, writer);
                            Assert.True(res);

                            await writer.FlushAsync();

                            Assert.True(reader.TryRead(out var data));

                            var str = BufferToString(data.Buffer);

                            if (f == methodFormatter)
                            {
                                Assert.True(_ColumnWriters_Val_Format_Called);
                            }
                            else if (f == delFormatter)
                            {
                                Assert.True(delFormatterCalled);
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }

                            if (s == methodShouldSerialize)
                            {
                                Assert.True(inst.ShouldSerializeCalled);
                            }
                            else if (s == staticMethodShouldSerialize)
                            {
                                Assert.True(_ColumnWriters.ShouldSerializeStaticCalled);
                            }
                            else if (s == staticMethodShouldSerializeParam)
                            {
                                Assert.True(_ColumnWriters.ShouldSerializeStaticWithParamCalled);
                            }
                            else if (s == delShouldSerialize)
                            {
                                Assert.True(delShouldSerializeCalled);
                            }
                            else if (s == staticDelShouldSerialize)
                            {
                                Assert.True(staticDelShouldSerializeCalled);
                            }
                            else if (s == null)
                            {
                                // intentionally empty
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }

                            if (g == methodGetter)
                            {
                                Assert.Equal("A", str);
                            }
                            else if (g == staticMethodGetter)
                            {
                                Assert.Equal("static", str);
                            }
                            else if (g == fieldGetter)
                            {
                                Assert.Equal("bar", str);
                            }
                            else if (g == staticFieldGetter)
                            {
                                Assert.Equal("static field", str);
                            }
                            else if (g == delGetter)
                            {
                                Assert.True(delGetterCalled);
                                Assert.Equal("bar", str);
                            }
                            else if (g == staticDelGetter)
                            {
                                Assert.True(staticDelGetterCalled);
                                Assert.Equal("foo", str);
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        }
                    }
                }
            }
        }

        private delegate bool FormatterIntEquivDelegate(int value, in WriteContext context, IBufferWriter<char> buffer);

        private delegate bool FormatterEquivDelegate<T>(T value, in WriteContext context, IBufferWriter<char> buffer);

        [Fact]
        public void FormatterCast()
        {
            var aCalled = 0;
            FormatterIntEquivDelegate a =
                (int _, in WriteContext __, IBufferWriter<char> ___) =>
                {
                    aCalled++;

                    return false;
                };

            int bCalled = 0;
            FormatterEquivDelegate<string> b =
                (string _, in WriteContext __, IBufferWriter<char> ___) =>
                {
                    bCalled++;

                    return false;
                };

            int cCalled = 0;
            FormatterDelegate<double> c =
                (double _, in WriteContext __, IBufferWriter<char> ___) =>
                {
                    cCalled++;

                    return false;
                };

            var aWrapped = (Formatter)a;
            aWrapped.Delegate.Value.DynamicInvoke(1, default(WriteContext), null);
            Assert.Equal(1, aCalled);
            ((FormatterDelegate<int>)aWrapped.Delegate.Value)(2, default, null);
            Assert.Equal(2, aCalled);

            var bWrapped = (Formatter)b;
            bWrapped.Delegate.Value.DynamicInvoke(null, default(WriteContext), null);
            Assert.Equal(1, bCalled);
            ((FormatterDelegate<string>)bWrapped.Delegate.Value)(null, default, null);
            Assert.Equal(2, bCalled);

            var cWrapped = (Formatter)c;
            cWrapped.Delegate.Value.DynamicInvoke(2.0, default(WriteContext), null);
            Assert.Equal(1, cCalled);
            Assert.Same(c, cWrapped.Delegate.Value);
            ((FormatterDelegate<double>)cWrapped.Delegate.Value)(3.0, default, null);
            Assert.Equal(2, cCalled);

            Assert.Null((Formatter)default(MethodInfo));
            Assert.Null((Formatter)default(Delegate));
        }

        private class _IDelegateCache : IDelegateCache
        {
            private readonly Dictionary<object, object> Cache = new Dictionary<object, object>();

            void IDelegateCache.Add<T, V>(T key, V cached)
            => Cache.Add(key, cached);

            CachedDelegate<V> IDelegateCache.TryGet<T, V>(T key)
            {
                if (!Cache.TryGetValue(key, out var obj))
                {
                    return CachedDelegate<V>.Empty;
                }

                return new CachedDelegate<V>(obj as V);
            }
        }

        private bool _NonStaticFormatter(string a, in WriteContext wc, IBufferWriter<char> bw) { return true; }
        private static void _BadReturnFormatter(string a, in WriteContext wc, IBufferWriter<char> bw) { }
        private static bool _BadArgs1Formatter() { return true; }
        private static bool _BadArgs2Formatter(string a, WriteContext wc, IBufferWriter<char> bw) { return true; }
        private static bool _BadArgs3Formatter(string a, in ReadContext wc, IBufferWriter<char> bw) { return true; }
        private static bool _BadArgs4Formatter(string a, in WriteContext wc, List<char> bw) { return true; }

        private delegate bool _BadFormatterDelegate(string a, in WriteContext wc, List<char> bw);
        private delegate bool _BadFormatterDelegate2(string a, in ReadContext wc, IBufferWriter<char> bw);

        [Fact]
        public async Task FormattersAsync()
        {
            // formatters
            var methodFormatter = Formatter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_ColumnWriters_Val_Format), BindingFlags.Static | BindingFlags.NonPublic));
            var delFormatter = (Formatter)(FormatterDelegate<_ColumnWriters_Val>)((_ColumnWriters_Val cell, in WriteContext _, IBufferWriter<char> writeTo) => { writeTo.Write(cell.Value.AsSpan()); return true; });
            var chainedFormatter1 = methodFormatter.Else(delFormatter);
            var chainedFormatter2 = delFormatter.Else(methodFormatter);
            var chainedFormatter3 = delFormatter.Else(methodFormatter).Else(delFormatter);
            var formatters = new[] { methodFormatter, delFormatter, chainedFormatter1, chainedFormatter2, chainedFormatter3 };

            var notFormatter = "";

            for (var i = 0; i < formatters.Length; i++)
            {
                var f1 = formatters[i];
                Assert.NotNull(f1.ToString());
                Assert.False(f1.Equals(notFormatter));

                if (f1 == methodFormatter)
                {
                    Assert.Equal(BackingMode.Method, f1.Mode);
                }
                else if (f1 == delFormatter)
                {
                    Assert.Equal(BackingMode.Delegate, f1.Mode);
                }

                for (var j = i; j < formatters.Length; j++)
                {
                    var f2 = formatters[j];

                    var eq = f1 == f2;
                    var neq = f1 != f2;
                    var hashEq = f1.GetHashCode() == f2.GetHashCode();

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                    }
                }
            }

            // PrimeDynamicDelegate
            {
                var cache = new _IDelegateCache();
                ((ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)methodFormatter).Guarantee(cache);
                var a = ((ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)methodFormatter).CachedDelegate.Value;
                Assert.NotNull(a);
                ((ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)methodFormatter).Guarantee(cache);
                var b = ((ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)methodFormatter).CachedDelegate.Value;
                Assert.Equal(a, b);

                {
                    var pipe = new Pipe();
                    var writer = new CharWriter(pipe.Writer);
                    var reader = pipe.Reader;

                    Assert.True(a(new _ColumnWriters_Val { Value = "foo" }, default, writer));

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("foo", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }

                ((ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)delFormatter).Guarantee(cache);
                var c = ((ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)delFormatter).CachedDelegate.Value;
                Assert.NotNull(c);
                Assert.NotEqual(a, c);
                ((ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)delFormatter).Guarantee(cache);
                var d = ((ICreatesCacheableDelegate<Formatter.DynamicFormatterDelegate>)delFormatter).CachedDelegate.Value;
                Assert.Equal(c, d);
                Assert.NotEqual(a, d);

                {
                    var pipe = new Pipe();
                    var writer = new CharWriter(pipe.Writer);
                    var reader = pipe.Reader;

                    Assert.True(d(new _ColumnWriters_Val { Value = "bar" }, default, writer));

                    await writer.FlushAsync();

                    Assert.True(reader.TryRead(out var buff));
                    Assert.Equal("bar", BufferToString(buff.Buffer));
                    reader.AdvanceTo(buff.Buffer.End);
                }
            }

            // ForMethod errors
            {
                Assert.Throws<ArgumentNullException>(() => Formatter.ForMethod(null));
                Assert.Throws<ArgumentException>(() => Formatter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_NonStaticFormatter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Formatter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadReturnFormatter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Formatter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs1Formatter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Formatter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs2Formatter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Formatter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs3Formatter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Formatter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs4Formatter), BindingFlags.NonPublic | BindingFlags.Static)));
            }

            // ForDelegate errors
            {
                Assert.Throws<ArgumentNullException>(() => Formatter.ForDelegate<string>(null));
            }

            // Delegate cast errors
            {
                Action a = () => { };
                Assert.Throws<InvalidOperationException>(() => (Formatter)a);
                Func<bool> b = () => true;
                Assert.Throws<InvalidOperationException>(() => (Formatter)b);
                Func<string, WriteContext, IBufferWriter<char>, bool> c = (_, __, ___) => true;
                Assert.Throws<InvalidOperationException>(() => (Formatter)c);
                _BadFormatterDelegate d = delegate { return true; };
                Assert.Throws<InvalidOperationException>(() => (Formatter)d);
                _BadFormatterDelegate2 e = (string _, in ReadContext __, IBufferWriter<char> ___) => { return true; };
                Assert.Throws<InvalidOperationException>(() => (Formatter)e);
            }

            string BufferToString(ReadOnlySequence<byte> buff)
            {
                var bytes = new List<byte>();
                foreach (var b in buff)
                {
                    bytes.AddRange(b.ToArray());
                }

                var byteArray = bytes.ToArray();
                var byteSpan = new Span<byte>(byteArray);
                var charSpan = MemoryMarshal.Cast<byte, char>(byteSpan);

                return new string(charSpan);
            }
        }

        private class _GetterCast
        {
            public string Foo { get; set; }
        }

        private delegate int GetterIntEquivDelegate(_GetterCast row, in WriteContext _);

        private delegate int StaticGetterIntEquivDelegate(in WriteContext _);

        private delegate V GetterEquivDelegate<T, V>(T row, in WriteContext _);

        private delegate V StaticGetterEquivDelegate<V>(in WriteContext _);

        [Fact]
        public void GetterCast()
        {
            var aCalled = 0;
            GetterIntEquivDelegate a =
                (_GetterCast row, in WriteContext _) =>
                {
                    aCalled++;
                    return row.Foo.Length;
                };
            var bCalled = 0;
            StaticGetterIntEquivDelegate b =
                (in WriteContext _) =>
                {
                    bCalled++;
                    return 3;
                };
            var cCalled = 0;
            GetterEquivDelegate<_GetterCast, double> c =
                (_GetterCast row, in WriteContext _) =>
                {
                    cCalled++;

                    return row.Foo.Length * 2.0;
                };
            var dRet = Guid.NewGuid();
            var dCalled = 0;
            StaticGetterEquivDelegate<Guid> d =
                (in WriteContext _) =>
                {
                    dCalled++;
                    return dRet;
                };
            var gCalled = 0;
            GetterDelegate<_GetterCast, int> g =
                (_GetterCast row, in WriteContext ctx) =>
                {
                    gCalled++;
                    return row.Foo.Length + 1;
                };
            var hCalled = 0;
            StaticGetterDelegate<int> h =
                (in WriteContext ctx) =>
                {
                    hCalled++;
                    return 456;
                };

            var aWrapped = (Getter)a;
            var aRes1 = (int)aWrapped.Delegate.Value.DynamicInvoke(new _GetterCast { Foo = "yo" }, default(WriteContext));
            Assert.Equal(2, aRes1);
            Assert.Equal(1, aCalled);
            var aRes2 = ((GetterDelegate<_GetterCast, int>)aWrapped.Delegate.Value)(new _GetterCast { Foo = "yolo" }, default);
            Assert.Equal(4, aRes2);
            Assert.Equal(2, aCalled);

            var bWrapped = (Getter)b;
            var bRes1 = (int)bWrapped.Delegate.Value.DynamicInvoke(default(WriteContext));
            Assert.Equal(3, bRes1);
            Assert.Equal(1, bCalled);
            var bRes2 = ((StaticGetterDelegate<int>)bWrapped.Delegate.Value)(default);
            Assert.Equal(3, bRes2);
            Assert.Equal(2, bCalled);

            var cWrapped = (Getter)c;
            var cRes1 = (double)cWrapped.Delegate.Value.DynamicInvoke(new _GetterCast { Foo = "yo" }, default(WriteContext));
            Assert.Equal(4.0, cRes1);
            Assert.Equal(1, cCalled);
            var cRes2 = ((GetterDelegate<_GetterCast, double>)cWrapped.Delegate.Value)(new _GetterCast { Foo = "yolo" }, default);
            Assert.Equal(8.0, cRes2);
            Assert.Equal(2, cCalled);

            var dWrapped = (Getter)d;
            var dRes1 = (Guid)dWrapped.Delegate.Value.DynamicInvoke(default(WriteContext));
            Assert.Equal(dRet, dRes1);
            Assert.Equal(1, dCalled);
            var dRes2 = ((StaticGetterDelegate<Guid>)dWrapped.Delegate.Value)(default);
            Assert.Equal(dRet, dRes2);
            Assert.Equal(2, dCalled);

            var gWrapped = (Getter)g;
            var gRes1 = (int)gWrapped.Delegate.Value.DynamicInvoke(new _GetterCast { Foo = "yo" }, default(WriteContext));
            Assert.Equal(3, gRes1);
            Assert.Equal(1, gCalled);
            var gRes2 = ((GetterDelegate<_GetterCast, int>)gWrapped.Delegate.Value)(new _GetterCast { Foo = "yolo" }, default);
            Assert.Equal(5, gRes2);
            Assert.Same(g, gWrapped.Delegate.Value);
            Assert.Equal(2, gCalled);

            var hWrapped = (Getter)h;
            var hRes1 = (int)hWrapped.Delegate.Value.DynamicInvoke(default(WriteContext));
            Assert.Equal(456, hRes1);
            Assert.Equal(1, hCalled);
            var hRes2 = ((StaticGetterDelegate<int>)hWrapped.Delegate.Value)(default);
            Assert.Equal(456, hRes2);
            Assert.Same(h, hWrapped.Delegate.Value);
            Assert.Equal(2, hCalled);

            Assert.Null((Getter)default(Delegate));
        }

        private void _BadReturnGetter() { }
        private static string _BadArgs1Getter(int a, int b) { return null; }
        private string _BadArgs2Getter(int a) { return null; }
        private string _BadArgs3Getter(ref object _) { return null; }
        private static string _BadArgs4Getter(ref object _, ref WriteContext __) { return null; }
        private static string _BadArgs5Getter(object a, object b, object c) { return null; }
        private string _BadArgs6Getter(object a, object b) { return null; }

        private static string _BadArgs7Getter(ref string a) { return null; }

        private sealed class _Getters
        {
            public string NoGetProperty
            {
                set
                {
                    // intentionally empty
                }
            }
        }

        [Fact]
        public void Getters()
        {
            // getter
            var methodGetter = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.Get)));
            var methodGetter2 = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.GetString)));
            var methodGetter3 = Getter.ForMethod(typeof(_ColumnWriters2).GetMethod(nameof(_ColumnWriters.Get)));
            var methodGetterCtx = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.GetCtx)));
            var staticMethodGetter = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.GetStatic)));
            var staticMethodGetterCtx = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.GetStaticCtx)));
            var staticMethodGetterRow = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.GetStaticRow)));
            var staticMethodGetterRowCtx = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.GetStaticRowCtx)));
            var fieldGetter = Getter.ForField(typeof(_ColumnWriters).GetField(nameof(_ColumnWriters.A)));
            var staticFieldGetter = Getter.ForField(typeof(_ColumnWriters).GetField(nameof(_ColumnWriters.StaticA)));
            var delGetter = (Getter)(GetterDelegate<_ColumnWriters, _ColumnWriters_Val>)((_ColumnWriters row, in WriteContext _) => { return row.A; });
            var staticDelGetter = (Getter)(StaticGetterDelegate<_ColumnWriters_Val>)((in WriteContext _) => { return new _ColumnWriters_Val { Value = "foo" }; });
            var propGetter = Getter.ForProperty(typeof(_ColumnWriters).GetProperty(nameof(_ColumnWriters.GetProp)));
            var staticPropGetter = Getter.ForProperty(typeof(_ColumnWriters).GetProperty(nameof(_ColumnWriters.GetPropStatic)));
            var getters = new[] { methodGetter, methodGetter2, methodGetter3, methodGetterCtx, staticMethodGetter, staticMethodGetterCtx, staticMethodGetterRow, staticMethodGetterRowCtx, fieldGetter, staticFieldGetter, delGetter, staticDelGetter, propGetter, staticPropGetter };

            var notGetter = "";

            for (var i = 0; i < getters.Length; i++)
            {
                var g1 = getters[i];
                Assert.NotNull(g1.ToString());
                Assert.False(g1.Equals(notGetter));

                if (g1 == methodGetter)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.False(g1.IsStatic);
                    Assert.False(g1.TakesContext);
                }
                else if (g1 == methodGetter2)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.False(g1.IsStatic);
                    Assert.False(g1.TakesContext);
                }
                else if (g1 == methodGetter3)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.False(g1.IsStatic);
                    Assert.False(g1.TakesContext);
                }
                else if (g1 == methodGetterCtx)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.True(g1.TakesContext);
                    Assert.False(g1.IsStatic);
                }
                else if (g1 == staticMethodGetter)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.False(g1.TakesContext);
                    Assert.True(g1.IsStatic);
                }
                else if (g1 == staticMethodGetterCtx)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.True(g1.TakesContext);
                    Assert.True(g1.IsStatic);
                }
                else if (g1 == staticMethodGetterRow)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.False(g1.TakesContext);
                    Assert.True(g1.IsStatic);
                }
                else if (g1 == staticMethodGetterRowCtx)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.True(g1.TakesContext);
                    Assert.True(g1.IsStatic);
                }
                else if (g1 == fieldGetter)
                {
                    Assert.Equal(BackingMode.Field, g1.Mode);
                    Assert.False(g1.TakesContext);
                    Assert.False(g1.IsStatic);
                }
                else if (g1 == staticFieldGetter)
                {
                    Assert.Equal(BackingMode.Field, g1.Mode);
                    Assert.False(g1.TakesContext);
                    Assert.True(g1.IsStatic);
                }
                else if (g1 == delGetter)
                {
                    Assert.Equal(BackingMode.Delegate, g1.Mode);
                    Assert.True(g1.TakesContext);
                    Assert.False(g1.IsStatic);
                }
                else if (g1 == staticDelGetter)
                {
                    Assert.Equal(BackingMode.Delegate, g1.Mode);
                    Assert.True(g1.TakesContext);
                    Assert.True(g1.IsStatic);
                }
                else if (g1 == propGetter)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.False(g1.TakesContext);
                    Assert.False(g1.IsStatic);
                }
                else if (g1 == staticPropGetter)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.False(g1.TakesContext);
                    Assert.True(g1.IsStatic);
                }
                else
                {
                    throw new Exception();
                }

                for (var j = i; j < getters.Length; j++)
                {
                    var g2 = getters[j];

                    var eq = g1 == g2;
                    var neq = g1 != g2;
                    var hashEq = g1.GetHashCode() == g2.GetHashCode();
                    var objEq = g1.Equals((object)g2);

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.True(objEq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.False(objEq);
                        Assert.True(neq);
                    }
                }
            }

            // PrimeDynamicDelegate
            {
                var cache = new _IDelegateCache();
                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)methodGetter).Guarantee(cache);
                var a = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)methodGetter).CachedDelegate.Value;
                Assert.NotNull(a);
                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)methodGetter).Guarantee(cache);
                var b = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)methodGetter).CachedDelegate.Value;
                Assert.Equal(a, b);

                var aRes = (_ColumnWriters_Val)a(new _ColumnWriters(), default);
                Assert.Equal("A", aRes.Value);

                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticMethodGetter).Guarantee(cache);
                var c = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticMethodGetter).CachedDelegate.Value;
                Assert.NotNull(c);
                Assert.NotEqual(a, c);
                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticMethodGetter).Guarantee(cache);
                var d = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticMethodGetter).CachedDelegate.Value;
                Assert.Equal(c, d);

                var cRes = (_ColumnWriters_Val)c(new _ColumnWriters(), default);
                Assert.Equal("static", cRes.Value);

                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)fieldGetter).Guarantee(cache);
                var e = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)fieldGetter).CachedDelegate.Value;
                Assert.NotNull(e);
                Assert.NotEqual(a, e);
                Assert.NotEqual(c, e);
                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)fieldGetter).Guarantee(cache);
                var f = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)fieldGetter).CachedDelegate.Value;
                Assert.Equal(e, f);

                var eRes = (_ColumnWriters_Val)e(new _ColumnWriters { A = new _ColumnWriters_Val { Value = "asdf" } }, default);
                Assert.Equal("asdf", eRes.Value);

                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticFieldGetter).Guarantee(cache);
                var g = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticFieldGetter).CachedDelegate.Value;
                Assert.NotNull(g);
                Assert.NotEqual(a, g);
                Assert.NotEqual(c, g);
                Assert.NotEqual(e, g);
                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticFieldGetter).Guarantee(cache);
                var h = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticFieldGetter).CachedDelegate.Value;
                Assert.Equal(g, h);

                _ColumnWriters.StaticA = new _ColumnWriters_Val { Value = "qwerty" };
                var gRes = (_ColumnWriters_Val)g(new _ColumnWriters(), default);
                Assert.Equal("qwerty", gRes.Value);

                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)delGetter).Guarantee(cache);
                var i = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)delGetter).CachedDelegate.Value;
                Assert.NotNull(i);
                Assert.NotEqual(a, i);
                Assert.NotEqual(c, i);
                Assert.NotEqual(e, i);
                Assert.NotEqual(g, i);
                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)delGetter).Guarantee(cache);
                var j = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)delGetter).CachedDelegate.Value;
                Assert.Equal(i, j);

                var iRes = (_ColumnWriters_Val)i(new _ColumnWriters { A = new _ColumnWriters_Val { Value = "xxxxx" } }, default);
                Assert.Equal("xxxxx", iRes.Value);

                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticDelGetter).Guarantee(cache);
                var k = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticDelGetter).CachedDelegate.Value;
                Assert.NotNull(k);
                Assert.NotEqual(a, k);
                Assert.NotEqual(c, k);
                Assert.NotEqual(e, k);
                Assert.NotEqual(g, k);
                Assert.NotEqual(i, k);
                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticDelGetter).Guarantee(cache);
                var l = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticDelGetter).CachedDelegate.Value;
                Assert.Equal(k, l);

                var lRes = (_ColumnWriters_Val)l(new _ColumnWriters(), default);
                Assert.Equal("foo", lRes.Value);
            }

            // ForMethod errors
            {
                Assert.Throws<ArgumentNullException>(() => Getter.ForMethod(null));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadReturnGetter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs1Getter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs2Getter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs3Getter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs4Getter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs5Getter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs6Getter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs7Getter), BindingFlags.NonPublic | BindingFlags.Static)));
            }

            // ForField errors
            {
                Assert.Throws<ArgumentNullException>(() => Getter.ForField(null));
            }

            // ForDelegate errors
            {
                Assert.Throws<ArgumentNullException>(() => Getter.ForDelegate(default(GetterDelegate<string, int>)));
                Assert.Throws<ArgumentNullException>(() => Getter.ForDelegate(default(StaticGetterDelegate<string>)));
            }

            // Delegate casts
            {
                Action a = () => { };
                Assert.Throws<InvalidOperationException>(() => (Getter)a);
                Func<int, string, bool> b = (_, __) => false;
                Assert.Throws<InvalidOperationException>(() => (Getter)b);
                Func<string, string> c = _ => "";
                Assert.Throws<InvalidOperationException>(() => (Getter)c);
                Func<int, int, int, string> d = (_, __, ___) => "";
                Assert.Throws<InvalidOperationException>(() => (Getter)d);
            }

            // ForProperty errors
            {
                Assert.Throws<ArgumentNullException>(() => Getter.ForProperty(null));

                var noGetProp = typeof(_Getters).GetProperty(nameof(_Getters.NoGetProperty));
                Assert.Throws<ArgumentException>(() => Getter.ForProperty(noGetProp));
            }
        }

        private static void _BadResetArgs1(int a, int b) { }

        private class _Resets
        {
            public void Reset(int a) { }

            public int ResetWithReturn() => 1;

            public static void ResetWrongByRef(ref object _) { }

            public static void StaticResetTooManyArgs(_Resets row, in ReadContext ctx, object _) { }
            public void InstanceResetTooManyArgs(in ReadContext ctx, object _) { }
        }

        [Fact]
        public void Resets()
        {
            // resets
            var methodReset = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.ResetXXX)));
            var methodResetCtx = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.ResetXXXCtx)));
            var staticMethodReset = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticResetXXX)));
            var staticMethodResetParam = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticResetXXXParam)));
            var staticMethodResetCtx = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticResetXXXCtx)));
            var staticMethodResetParamCtx = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticResetXXXParamCtx)));
            var delReset = (Reset)(ResetDelegate<_ColumnSetters>)((_ColumnSetters a, in ReadContext _) => { });
            var staticDelReset = (Reset)(StaticResetDelegate)((in ReadContext _) => { });
            var resets = new[] { methodReset, methodResetCtx, staticMethodReset, staticMethodResetParam, staticMethodResetCtx, staticMethodResetParamCtx, delReset, staticDelReset };

            var notReset = "";

            for (var i = 0; i < resets.Length; i++)
            {
                var r1 = resets[i];
                Assert.NotNull(r1.ToString());
                Assert.False(r1.Equals(notReset));

                if (r1 == methodReset)
                {
                    Assert.Equal(BackingMode.Method, r1.Mode);
                    Assert.Equal(typeof(_ColumnSetters), r1.RowType.Value);
                    Assert.False(r1.IsStatic);
                    Assert.False(r1.TakesContext);
                }
                else if (r1 == methodResetCtx)
                {
                    Assert.Equal(BackingMode.Method, r1.Mode);
                    Assert.Equal(typeof(_ColumnSetters), r1.RowType.Value);
                    Assert.False(r1.IsStatic);
                    Assert.True(r1.TakesContext);
                }
                else if (r1 == staticMethodReset)
                {
                    Assert.Equal(BackingMode.Method, r1.Mode);
                    Assert.False(r1.RowType.HasValue);
                    Assert.True(r1.IsStatic);
                    Assert.False(r1.TakesContext);
                }
                else if (r1 == staticMethodResetParam)
                {
                    Assert.Equal(BackingMode.Method, r1.Mode);
                    Assert.Equal(typeof(_ColumnSetters), r1.RowType.Value);
                    Assert.True(r1.IsStatic);
                    Assert.False(r1.TakesContext);
                }
                else if (r1 == staticMethodResetCtx)
                {
                    Assert.Equal(BackingMode.Method, r1.Mode);
                    Assert.False(r1.RowType.HasValue);
                    Assert.True(r1.IsStatic);
                    Assert.True(r1.TakesContext);
                }
                else if (r1 == staticMethodResetParamCtx)
                {
                    Assert.Equal(BackingMode.Method, r1.Mode);
                    Assert.Equal(typeof(_ColumnSetters), r1.RowType.Value);
                    Assert.True(r1.IsStatic);
                    Assert.True(r1.TakesContext);
                }
                else if (r1 == delReset)
                {
                    Assert.Equal(BackingMode.Delegate, r1.Mode);
                    Assert.Equal(typeof(_ColumnSetters), r1.RowType.Value);
                    Assert.False(r1.IsStatic);
                }
                else if (r1 == staticDelReset)
                {
                    Assert.Equal(BackingMode.Delegate, r1.Mode);
                    Assert.False(r1.RowType.HasValue);
                    Assert.True(r1.IsStatic);
                }
                else
                {
                    throw new Exception();
                }

                for (var j = i; j < resets.Length; j++)
                {
                    var r2 = resets[j];

                    var eq = r1 == r2;
                    var neq = r1 != r2;
                    var hashEq = r1.GetHashCode() == r2.GetHashCode();
                    var objEq = r1.Equals((object)r2);

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.True(objEq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.False(objEq);
                        Assert.True(neq);
                    }
                }
            }

            // ForMethod errors
            {
                Assert.Throws<ArgumentNullException>(() => Reset.ForMethod(null));
                Assert.Throws<ArgumentException>(() => Reset.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadResetArgs1), BindingFlags.Static | BindingFlags.NonPublic)));
                Assert.Throws<ArgumentException>(() => Reset.ForMethod(typeof(_Resets).GetMethod(nameof(_Resets.Reset), BindingFlags.Instance | BindingFlags.Public)));
                Assert.Throws<ArgumentException>(() => Reset.ForMethod(typeof(_Resets).GetMethod(nameof(_Resets.ResetWithReturn), BindingFlags.Instance | BindingFlags.Public)));
                Assert.Throws<ArgumentException>(() => Reset.ForMethod(typeof(_Resets).GetMethod(nameof(_Resets.ResetWrongByRef), BindingFlags.Static | BindingFlags.Public)));
                Assert.Throws<ArgumentException>(() => Reset.ForMethod(typeof(_Resets).GetMethod(nameof(_Resets.StaticResetTooManyArgs), BindingFlags.Static | BindingFlags.Public)));
                Assert.Throws<ArgumentException>(() => Reset.ForMethod(typeof(_Resets).GetMethod(nameof(_Resets.InstanceResetTooManyArgs), BindingFlags.Instance | BindingFlags.Public)));
            }

            // ForDelegate errors
            {
                Assert.Throws<ArgumentNullException>(() => Reset.ForDelegate(default(ResetDelegate<string>)));
                Assert.Throws<ArgumentNullException>(() => Reset.ForDelegate(default(StaticResetDelegate)));
            }

            // Delegate casts
            {
                Func<string> a = () => "";
                Assert.Throws<InvalidOperationException>(() => (Reset)a);
                Action<int, int> b = (_, __) => { };
                Assert.Throws<InvalidOperationException>(() => (Reset)b);
                Action<int> c = _ => { };
                Assert.Throws<InvalidOperationException>(() => (Reset)c);
                Action<int, int, int> d = (_, __, ___) => { };
                Assert.Throws<InvalidOperationException>(() => (Reset)d);
            }
        }

        private bool _NonStaticParser(ReadOnlySpan<char> data, in ReadContext ctx, out int res)
        {
            res = default;
            return false;
        }

        private static bool _BadArgs1Parser() { return false; }
        private static bool _BadArgs2Parser(ReadOnlyMemory<char> data, in ReadContext ctx, out int res) { res = 0; return false; }
        private static bool _BadArgs3Parser(ReadOnlySpan<char> data, ReadContext ctx, out int res) { res = 0; return false; }
        private static bool _BadArgs4Parser(ReadOnlySpan<char> data, in WriteContext ctx, out int res) { res = 0; return false; }
        private static bool _BadArgs5Parser(ReadOnlySpan<char> data, in ReadContext ctx, int res) { return false; }
        private static string _BadReturnParser(ReadOnlySpan<char> data, in ReadContext ctx, out int res) { res = 0; return ""; }

        private class _Parsers
        {
            public _Parsers(int a) { }
            public _Parsers(int a, int b) { }
            public _Parsers(ReadOnlySpan<char> a, int b) { }
            public _Parsers(ReadOnlySpan<char> a, ReadContext b) { }
            public _Parsers(int a, int b, int c) { }
        }

        private delegate bool BadParser1(ReadOnlySpan<char> _, ReadContext __, int ___);
        private delegate bool BadParser2(ReadOnlySpan<char> _, in int __, int ___);
        private delegate bool BadParser3(ReadOnlySpan<char> _, in ReadContext __, int ___);

        [Fact]
        public void Parsers()
        {
            // parsers
            var methodParser = (Parser)typeof(WrapperTests).GetMethod(nameof(_ColumnSetters_Parser), BindingFlags.Static | BindingFlags.NonPublic);
            var delParser = (Parser)(ParserDelegate<_ColumnSetters_Val>)((ReadOnlySpan<char> data, in ReadContext ctx, out _ColumnSetters_Val result) => { result = new _ColumnSetters_Val(data); return true; });
            var consOneParser = (Parser)typeof(_ColumnSetters_Val).GetConstructor(new[] { typeof(ReadOnlySpan<char>) });
            var consTwoParser = (Parser)typeof(_ColumnSetters_Val).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() });
            var parsers = new[] { methodParser, delParser, consOneParser, consTwoParser };

            for (var i = 0; i < parsers.Length; i++)
            {
                var p1 = parsers[i];
                Assert.NotNull(p1.ToString());

                if (p1 == methodParser)
                {
                    Assert.Equal(BackingMode.Method, p1.Mode);
                }
                else if (p1 == delParser)
                {
                    Assert.Equal(BackingMode.Delegate, p1.Mode);
                }
                else if (p1 == consOneParser)
                {
                    Assert.Equal(BackingMode.Constructor, p1.Mode);
                }
                else if (p1 == consTwoParser)
                {
                    Assert.Equal(BackingMode.Constructor, p1.Mode);
                }

                for (var j = i; j < parsers.Length; j++)
                {
                    var p2 = parsers[j];

                    var eq = p1 == p2;
                    var neq = p1 != p2;
                    var hashEq = p1.GetHashCode() == p2.GetHashCode();

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                    }
                }
            }

            // ForMethod errors
            {
                Assert.Throws<ArgumentNullException>(() => Parser.ForMethod(null));
                Assert.Throws<ArgumentException>(() => Parser.ForMethod(typeof(WrapperTests).GetMethod(nameof(_NonStaticParser), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Parser.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs1Parser), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Parser.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs2Parser), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Parser.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs3Parser), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Parser.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs4Parser), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Parser.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs5Parser), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Parser.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadReturnParser), BindingFlags.NonPublic | BindingFlags.Static)));
            }

            // ForConstructor errors
            {
                Assert.Throws<ArgumentNullException>(() => Parser.ForConstructor(null));
                Assert.Throws<ArgumentException>(() => Parser.ForConstructor(typeof(_Parsers).GetConstructor(new[] { typeof(int), typeof(int), typeof(int) })));
                Assert.Throws<ArgumentException>(() => Parser.ForConstructor(typeof(_Parsers).GetConstructor(new[] { typeof(int) })));
                Assert.Throws<ArgumentException>(() => Parser.ForConstructor(typeof(_Parsers).GetConstructor(new[] { typeof(int), typeof(int) })));
                Assert.Throws<ArgumentException>(() => Parser.ForConstructor(typeof(_Parsers).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(int) })));
                Assert.Throws<ArgumentException>(() => Parser.ForConstructor(typeof(_Parsers).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext) })));
            }

            // ForDelegate errors
            {
                Assert.Throws<ArgumentNullException>(() => Parser.ForDelegate<string>(null));
            }

            // delegate cast errors
            {
                Func<string> a = () => "";
                Assert.Throws<InvalidOperationException>(() => (Parser)a);
                Func<bool> b = () => false;
                Assert.Throws<InvalidOperationException>(() => (Parser)b);
                Func<int, int, int, bool> c = (_, __, ___) => false;
                Assert.Throws<InvalidOperationException>(() => (Parser)c);
                BadParser1 d = (_, __, ___) => false;
                Assert.Throws<InvalidOperationException>(() => (Parser)d);
                BadParser2 e = delegate { return false; };
                Assert.Throws<InvalidOperationException>(() => (Parser)e);
                BadParser3 f = delegate { return false; };
                Assert.Throws<InvalidOperationException>(() => (Parser)f);
            }
        }

        private delegate bool ParserIntEquivDelegate(ReadOnlySpan<char> value, in ReadContext context, out int res);

        private delegate bool ParserEquivDelegate<T>(ReadOnlySpan<char> value, in ReadContext context, out T res);

        [Fact]
        public void ParserCast()
        {
            var aCalled = 0;
            ParserIntEquivDelegate a =
                (ReadOnlySpan<char> _, in ReadContext __, out int res) =>
                {
                    aCalled++;
                    res = 1;
                    return true;
                };
            var bCalled = 0;
            ParserEquivDelegate<string> b =
                (ReadOnlySpan<char> _, in ReadContext __, out string res) =>
                {
                    bCalled++;
                    res = "hello";
                    return true;
                };
            var cCalled = 0;
            ParserDelegate<int> c =
                (ReadOnlySpan<char> _, in ReadContext __, out int res) =>
                {
                    cCalled++;
                    res = 2;
                    return true;
                };

            var aWrapped = (Parser)a;
            var aRes = ((ParserDelegate<int>)aWrapped.Delegate.Value)("hello".AsSpan(), default, out int aOut);
            Assert.True(aRes);
            Assert.Equal(1, aOut);
            Assert.Equal(1, aCalled);

            var bWrapped = (Parser)b;
            var bRes = ((ParserDelegate<string>)bWrapped.Delegate.Value)("hello".AsSpan(), default, out string bOut);
            Assert.True(bRes);
            Assert.Equal("hello", bOut);
            Assert.Equal(1, bCalled);

            var cWrapped = (Parser)c;
            var cRes = ((ParserDelegate<int>)cWrapped.Delegate.Value)("hello".AsSpan(), default, out int cOut);
            Assert.True(cRes);
            Assert.Equal(2, cOut);
            Assert.Same(c, cWrapped.Delegate.Value);
            Assert.Equal(1, cCalled);

            Assert.Null((Parser)default(MethodInfo));
            Assert.Null((Parser)default(Delegate));
            Assert.Null((Parser)default(ConstructorInfo));
        }

        private class _ResetCast
        {
            public int Foo { get; set; }
        }

        private delegate void ResetConcreteEquivDelegate(_ResetCast row, in ReadContext _);

        private delegate void ResetEquivDelegate<T>(T row, in ReadContext _);

        private delegate void StaticResetEquivDelegate(in ReadContext _);

        [Fact]
        public void ResetCast()
        {
            var aCalled = 0;
            ResetConcreteEquivDelegate a =
                (_ResetCast row, in ReadContext _) =>
                {
                    aCalled++;
                };
            var bCalled = 0;
            ResetEquivDelegate<_ResetCast> b =
                (_ResetCast row, in ReadContext _) =>
                {
                    bCalled++;
                };
            var cCalled = 0;
            StaticResetEquivDelegate c =
                (in ReadContext _) =>
                {
                    cCalled++;
                };
            var fCalled = 0;
            ResetDelegate<_ResetCast> f =
                (_ResetCast row, in ReadContext _) =>
                {
                    fCalled++;
                };
            var gCalled = 0;
            StaticResetDelegate g =
                (in ReadContext _) =>
                {
                    gCalled++;
                };

            var aWrapped = (Reset)a;
            aWrapped.Delegate.Value.DynamicInvoke(new _ResetCast(), default(ReadContext));
            Assert.Equal(1, aCalled);
            ((ResetDelegate<_ResetCast>)aWrapped.Delegate.Value)(new _ResetCast(), default);
            Assert.Equal(2, aCalled);

            var bWrapped = (Reset)b;
            bWrapped.Delegate.Value.DynamicInvoke(new _ResetCast(), default(ReadContext));
            Assert.Equal(1, bCalled);
            ((ResetDelegate<_ResetCast>)bWrapped.Delegate.Value)(new _ResetCast(), default);
            Assert.Equal(2, bCalled);

            var cWrapped = (Reset)c;
            cWrapped.Delegate.Value.DynamicInvoke(default(ReadContext));
            Assert.Equal(1, cCalled);
            ((StaticResetDelegate)cWrapped.Delegate.Value)(default);
            Assert.Equal(2, cCalled);

            var fWrapped = (Reset)f;
            fWrapped.Delegate.Value.DynamicInvoke(new _ResetCast(), default(ReadContext));
            Assert.Equal(1, fCalled);
            Assert.Same(f, fWrapped.Delegate.Value);
            ((ResetDelegate<_ResetCast>)fWrapped.Delegate.Value)(new _ResetCast(), default);
            Assert.Equal(2, fCalled);

            var gWrapped = (Reset)g;
            gWrapped.Delegate.Value.DynamicInvoke(default(ReadContext));
            Assert.Equal(1, gCalled);
            Assert.Same(g, gWrapped.Delegate.Value);
            ((StaticResetDelegate)gWrapped.Delegate.Value)(default);
            Assert.Equal(2, gCalled);

            Assert.Null((Reset)default(Delegate));
        }

        private class _SetterCast
        {
            public string Foo { get; set; }

            public _SetterCast() { }

            public _SetterCast(int p) { }

            public string NoSetter => "nope";
        }

        private delegate void SetterConcreteEquivDelegate(_SetterCast row, int val, in ReadContext ctx);

        private delegate void StaticSetterConcreteEquivDelegate(int val, in ReadContext ctx);

        private delegate void SetterGenEquivDelegate<T, V>(T row, V val, in ReadContext ctx);

        private delegate void StaticSetterGenEquivDelegate<V>(V val, in ReadContext ctx);

        [Fact]
        public void SetterCast()
        {
            var aCalled = 0;
            SetterConcreteEquivDelegate a =
                (_SetterCast row, int val, in ReadContext _) =>
                {
                    aCalled++;
                    row.Foo = val.ToString();
                };
            var bCalled = 0;
            StaticSetterConcreteEquivDelegate b =
                (int val, in ReadContext _) =>
                {
                    bCalled++;
                };
            var cCalled = 0;
            SetterGenEquivDelegate<_SetterCast, string> c =
                (_SetterCast row, string val, in ReadContext _) =>
                {
                    cCalled++;
                    row.Foo = val;
                };
            var dCalled = 0;
            StaticSetterGenEquivDelegate<string> d =
                (string val, in ReadContext _) =>
                {
                    dCalled++;
                };
            var gCalled = 0;
            SetterDelegate<_SetterCast, TimeSpan> g =
                (_SetterCast row, TimeSpan val, in ReadContext _) =>
                {
                    gCalled++;
                    row.Foo = val.ToString();
                };
            var hCalled = 0;
            StaticSetterDelegate<TimeSpan> h =
                (TimeSpan val, in ReadContext _) =>
                {
                    hCalled++;
                };

            var aWrapped = (Setter)a;
            aWrapped.Delegate.Value.DynamicInvoke(new _SetterCast(), 123, default(ReadContext));
            Assert.Equal(1, aCalled);
            ((SetterDelegate<_SetterCast, int>)aWrapped.Delegate.Value)(new _SetterCast(), 123, default);
            Assert.Equal(2, aCalled);

            var bWrapped = (Setter)b;
            bWrapped.Delegate.Value.DynamicInvoke(123, default(ReadContext));
            Assert.Equal(1, bCalled);
            ((StaticSetterDelegate<int>)bWrapped.Delegate.Value)(123, default);
            Assert.Equal(2, bCalled);

            var cWrapped = (Setter)c;
            cWrapped.Delegate.Value.DynamicInvoke(new _SetterCast(), "123", default(ReadContext));
            Assert.Equal(1, cCalled);
            ((SetterDelegate<_SetterCast, string>)cWrapped.Delegate.Value)(new _SetterCast(), "123", default);
            Assert.Equal(2, cCalled);

            var dWrapped = (Setter)d;
            dWrapped.Delegate.Value.DynamicInvoke("123", default(ReadContext));
            Assert.Equal(1, dCalled);
            ((StaticSetterDelegate<string>)dWrapped.Delegate.Value)("123", default);
            Assert.Equal(2, dCalled);

            var gWrapped = (Setter)g;
            gWrapped.Delegate.Value.DynamicInvoke(new _SetterCast(), TimeSpan.FromMinutes(1), default(ReadContext));
            Assert.Equal(1, gCalled);
            ((SetterDelegate<_SetterCast, TimeSpan>)gWrapped.Delegate.Value)(new _SetterCast(), TimeSpan.FromMinutes(1), default);
            Assert.Same(g, gWrapped.Delegate.Value);
            Assert.Equal(2, gCalled);

            var hWrapped = (Setter)h;
            hWrapped.Delegate.Value.DynamicInvoke(TimeSpan.FromMinutes(1), default(ReadContext));
            Assert.Equal(1, hCalled);
            ((StaticSetterDelegate<TimeSpan>)hWrapped.Delegate.Value)(TimeSpan.FromMinutes(1), default);
            Assert.Same(h, hWrapped.Delegate.Value);
            Assert.Equal(2, hCalled);

            Assert.Null((Setter)default(ParameterInfo));

            var t = typeof(_SetterCast).GetTypeInfo();
            var cons = t.GetConstructor(new[] { typeof(int) });
            var p = cons.GetParameters()[0];
            var pCast = (Setter)p;
            Assert.NotNull(pCast);
            var pMtd = Setter.ForConstructorParameter(p);
            Assert.Equal(pMtd, pCast);
        }

        private bool _BadReturnSetter() { return false; }
        private void _BadArgs1Setter(int a, int b) { }
        private void _BadArgs2Setter(int a, int b, int c) { }
        private static void _BadArgs3Setter(int a, ref object notContext) { }
        private static void _BadArgs4Setter(int a, int b, ReadContext notContext) { }
        private static void _BadArgs5Setter(int a, int b, int c, int d) { }

        private delegate void _BadSetterDelegate(int a, ref int b, in ReadContext _);

        [Fact]
        public void Setters()
        {
            // setters
            var methodSetter = Setter.ForProperty(typeof(_ColumnSetters).GetProperty(nameof(_ColumnSetters.Prop)));
            var methodSetterCtx = Setter.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.SetCtx)));
            var staticMethodSet = Setter.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticSet)));
            var staticMethodSetCtx = Setter.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticSetCtx)));
            var staticMethodSetParam = Setter.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticSetParam)));
            var staticMethodSetByRefParam = Setter.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticSetByRefParam)));
            var staticMethodSetParamCtx = Setter.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticSetParamCtx)));
            var fieldSetter = Setter.ForField(typeof(_ColumnSetters).GetField(nameof(_ColumnSetters.Field)));
            var staticFieldSetter = Setter.ForField(typeof(_ColumnSetters).GetField(nameof(_ColumnSetters.StaticField)));

            var delSetter1 = (Setter)(SetterDelegate<_ColumnSetters, _ColumnSetters_Val>)((_ColumnSetters a, _ColumnSetters_Val v, in ReadContext _) => { a.Prop = v; });
            var delSetter2 = (Setter)(SetterDelegate<_ColumnSetters, _ColumnWriters_Val>)((_ColumnSetters a, _ColumnWriters_Val v, in ReadContext _) => { /* intentionally empty */ });
            var delSetter3 = (Setter)(SetterDelegate<_ColumnWriters, _ColumnSetters_Val>)((_ColumnWriters a, _ColumnSetters_Val v, in ReadContext _) => { /* intentionally empty */ });
            var delSetter4 = (Setter)(SetterDelegate<_ColumnWriters, _ColumnWriters_Val>)((_ColumnWriters a, _ColumnWriters_Val v, in ReadContext _) => { a.A = v; });

            var consSetter = Setter.ForConstructorParameter(typeof(_ColumnSetters).GetConstructors().Single().GetParameters().Single());

            var staticDelSetter = (Setter)(StaticSetterDelegate<_ColumnSetters_Val>)((_ColumnSetters_Val f, in ReadContext _) => { _ColumnSetters.StaticField = f; });

            var byRefDelSetter = (Setter)(SetterByRefDelegate<_ColumnWriters, _ColumnWriters_Val>)((ref _ColumnWriters a, _ColumnWriters_Val v, in ReadContext _) => { a.A = v; });

            var setters = new[] { methodSetter, methodSetterCtx, staticMethodSet, staticMethodSetCtx, staticMethodSetParam, staticMethodSetByRefParam, staticMethodSetParamCtx, fieldSetter, delSetter1, delSetter2, delSetter3, delSetter4, staticDelSetter, consSetter, byRefDelSetter };

            var notSetter = "";

            for (var i = 0; i < setters.Length; i++)
            {
                var s1 = setters[i];
                Assert.NotNull(s1.ToString());
                Assert.False(s1.Equals(notSetter));

                if (s1 == methodSetter)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.False(s1.IsStatic);
                }
                else if (s1 == methodSetterCtx)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.False(s1.IsStatic);
                }
                else if (s1 == staticMethodSet)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.True(s1.IsStatic);
                }
                else if (s1 == staticMethodSetCtx)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.True(s1.IsStatic);
                }
                else if (s1 == staticMethodSetParam)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.True(s1.IsStatic);
                }
                else if (s1 == staticMethodSetByRefParam)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.True(s1.IsStatic);
                }
                else if (s1 == staticMethodSetParamCtx)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.True(s1.IsStatic);
                }
                else if (s1 == fieldSetter)
                {
                    Assert.Equal(BackingMode.Field, s1.Mode);
                    Assert.False(s1.IsStatic);
                }
                else if (s1 == staticFieldSetter)
                {
                    Assert.Equal(BackingMode.Field, s1.Mode);
                    Assert.True(s1.IsStatic);
                }
                else if (s1 == delSetter1 || s1 == delSetter2 || s1 == delSetter3 || s1 == delSetter4)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.False(s1.IsStatic);
                }
                else if (s1 == staticDelSetter)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.True(s1.IsStatic);
                }
                else if (s1 == byRefDelSetter)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.False(s1.IsStatic);
                }
                else if (s1 == consSetter)
                {
                    Assert.Equal(BackingMode.ConstructorParameter, s1.Mode);
                    Assert.False(s1.IsStatic);
                }
                else
                {
                    throw new Exception();
                }

                for (var j = i; j < setters.Length; j++)
                {
                    var s2 = setters[j];

                    var eq = s1 == s2;
                    var neq = s1 != s2;
                    var hashEq = s1.GetHashCode() == s2.GetHashCode();

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                    }
                }
            }

            // ForMethod errors
            {
                Assert.Throws<ArgumentNullException>(() => Setter.ForMethod(null));
                Assert.Throws<ArgumentException>(() => Setter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadReturnSetter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Setter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs1Setter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Setter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs2Setter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Setter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs3Setter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Setter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs4Setter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Setter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs5Setter), BindingFlags.NonPublic | BindingFlags.Static)));
            }

            // ForField errors
            {
                Assert.Throws<ArgumentNullException>(() => Setter.ForField(null));
            }

            // ForDelegate errors
            {
                Assert.Throws<ArgumentNullException>(() => Setter.ForDelegate<string>(null));
                Assert.Throws<ArgumentNullException>(() => Setter.ForDelegate(default(SetterDelegate<string, int>)));
                Assert.Throws<ArgumentNullException>(() => Setter.ForDelegate(default(SetterByRefDelegate<string, int>)));
            }

            // ForParser errors
            {
                Assert.Throws<ArgumentNullException>(() => Setter.ForProperty(null));
                Assert.Throws<ArgumentException>(() => Setter.ForProperty(typeof(_SetterCast).GetProperty(nameof(_SetterCast.NoSetter))));
            }

            // ForConstructorParameter errors
            {
                Assert.Throws<ArgumentNullException>(() => Setter.ForConstructorParameter(null));

                var mtd = typeof(WrapperTests).GetMethod(nameof(_BadArgs5Setter), BindingFlags.NonPublic | BindingFlags.Static);
                var ps = mtd.GetParameters().First();

                Assert.Throws<ArgumentException>(() => Setter.ForConstructorParameter(ps));
            }

            // delegate cast errors
            {
                Func<string> a = () => "";
                Assert.Throws<InvalidOperationException>(() => (Setter)a);
                Action<int, int, int> b = (_, __, ___) => { };
                Assert.Throws<InvalidOperationException>(() => (Setter)b);
                Action<int, int> c = (_, __) => { };
                Assert.Throws<InvalidOperationException>(() => (Setter)c);
                Action<int> d = _ => { };
                Assert.Throws<InvalidOperationException>(() => (Setter)d);
                _BadSetterDelegate e = (int _, ref int __, in ReadContext ___) => { };
                Assert.Throws<InvalidOperationException>(() => (Setter)e);
            }
        }

        private class _ShouldSerializeCast
        {
            public int Foo { get; set; }
        }

        private delegate bool ShouldSerializeConcreteEquivDelegate(_ShouldSerializeCast row, in WriteContext _);

        private delegate bool ShouldSerializeGenEquivDelegate<T>(T row, in WriteContext _);

        private delegate bool StaticShouldSerializeEquivDelegate(in WriteContext _);

        [Fact]
        public void ShouldSerializeCast()
        {
            var aCalled = 0;
            ShouldSerializeConcreteEquivDelegate a =
                (_ShouldSerializeCast row, in WriteContext _) =>
                {
                    aCalled++;

                    return true;
                };
            var bCalled = 0;
            ShouldSerializeGenEquivDelegate<_ShouldSerializeCast> b =
                (_ShouldSerializeCast row, in WriteContext _) =>
                {
                    bCalled++;

                    return true;
                };
            var cCalled = 0;
            StaticShouldSerializeDelegate c =
                (in WriteContext _) =>
                {
                    cCalled++;

                    return true;
                };
            var fCalled = 0;
            ShouldSerializeDelegate<_ShouldSerializeCast> f =
                (_ShouldSerializeCast row, in WriteContext _) =>
                {
                    fCalled++;

                    return true;
                };
            var gCalled = 0;
            StaticShouldSerializeDelegate g =
                (in WriteContext _) =>
                {
                    gCalled++;
                    return true;
                };

            var aWrapped = (ShouldSerialize)a;
            var aRes1 = (bool)aWrapped.Delegate.Value.DynamicInvoke(new _ShouldSerializeCast(), default(WriteContext));
            Assert.True(aRes1);
            Assert.Equal(1, aCalled);
            var aRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)aWrapped.Delegate.Value)(new _ShouldSerializeCast(), default);
            Assert.Equal(2, aCalled);
            Assert.True(aRes2);

            var bWrapped = (ShouldSerialize)b;
            var bRes1 = (bool)bWrapped.Delegate.Value.DynamicInvoke(new _ShouldSerializeCast(), default(WriteContext));
            Assert.True(bRes1);
            Assert.Equal(1, bCalled);
            var bRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)bWrapped.Delegate.Value)(new _ShouldSerializeCast(), default);
            Assert.Equal(2, bCalled);
            Assert.True(bRes2);

            var cWrapped = (ShouldSerialize)c;
            var cRes1 = (bool)cWrapped.Delegate.Value.DynamicInvoke(default(WriteContext));
            Assert.True(cRes1);
            Assert.Equal(1, cCalled);
            var cRes2 = ((StaticShouldSerializeDelegate)cWrapped.Delegate.Value)(default);
            Assert.Equal(2, cCalled);
            Assert.True(cRes2);

            var fWrapped = (ShouldSerialize)f;
            Assert.Same(f, fWrapped.Delegate.Value);
            var fRes1 = (bool)fWrapped.Delegate.Value.DynamicInvoke(new _ShouldSerializeCast(), default(WriteContext));
            Assert.True(fRes1);
            Assert.Equal(1, fCalled);
            var fRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)fWrapped.Delegate.Value)(new _ShouldSerializeCast(), default);
            Assert.Equal(2, fCalled);
            Assert.True(fRes2);

            var gWrapped = (ShouldSerialize)g;
            Assert.Same(g, gWrapped.Delegate.Value);
            var gRes1 = (bool)gWrapped.Delegate.Value.DynamicInvoke(default(WriteContext));
            Assert.True(gRes1);
            Assert.Equal(1, gCalled);
            var gRes2 = ((StaticShouldSerializeDelegate)gWrapped.Delegate.Value)(default);
            Assert.Equal(2, gCalled);
            Assert.True(gRes2);

            Assert.Null((ShouldSerialize)default(MethodInfo));
            Assert.Null((ShouldSerialize)default(Delegate));
        }

        private bool _BadArgsShouldSerialize(int a) { return true; }
        private bool _BadArgs2ShouldSerialize(_ShouldSerializeCast a, int b) { return true; }
        private static bool _BadArgs3ShouldSerialize(ref object _) { return true; }
        private static bool _BadArgs4ShouldSerialize(_ShouldSerializeCast a, object _) { return true; }
        private static bool _BadArgs5ShouldSerialize(_ShouldSerializeCast a, object _, object __) { return true; }
        private void _BadReturnShouldSerialize() { }

        [Fact]
        public void ShouldSerializes()
        {
            // should serialize
            var methodShouldSerialize = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerialize)));
            var methodShouldSerialize2 = ShouldSerialize.ForMethod(typeof(_ColumnWriters2).GetMethod(nameof(_ColumnWriters2.ShouldSerialize)));
            var methodShouldSerializeCtx = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerializeCtx)));
            var staticMethodShouldSerialize = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerializeStatic)));
            var staticMethodShouldSerializeCtx = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerializeStaticCtx)));
            var staticMethodShouldSerializeParam = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerializeStaticWithParam)));
            var staticMethodShouldSerializeParamCtx = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerializeStaticWithParamCtx)));
            var delShouldSerialize = (ShouldSerialize)(ShouldSerializeDelegate<_ColumnWriters>)((_ColumnWriters _, in WriteContext __) => { return true; });
            var delOtherShouldSerialize = (ShouldSerialize)(ShouldSerializeGenEquivDelegate<_ColumnWriters>)((_ColumnWriters _, in WriteContext __) => { return true; });
            var staticDelShouldSerialize = (ShouldSerialize)(StaticShouldSerializeDelegate)((in WriteContext __) => { return true; });
            var staticDelOtherShouldSerialize = (ShouldSerialize)(StaticShouldSerializeEquivDelegate)((in WriteContext __) => { return true; });
            var shouldSerializes = new[] { methodShouldSerialize, methodShouldSerialize2, methodShouldSerializeCtx, staticMethodShouldSerialize, staticMethodShouldSerializeCtx, staticMethodShouldSerializeParam, staticMethodShouldSerializeParamCtx, delShouldSerialize, delOtherShouldSerialize, staticDelShouldSerialize, staticDelOtherShouldSerialize };

            var notShouldSerialize = "";

            for (var i = 0; i < shouldSerializes.Length; i++)
            {
                var s1 = shouldSerializes[i];
                Assert.NotNull(s1.ToString());
                Assert.False(s1.Equals(notShouldSerialize));

                if (s1 == methodShouldSerialize)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.Equal(typeof(_ColumnWriters).GetTypeInfo(), s1.Takes.Value);
                    Assert.False(s1.IsStatic);
                    Assert.False(s1.TakesContext);
                }
                else if(s1 == methodShouldSerialize2)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.Equal(typeof(_ColumnWriters2).GetTypeInfo(), s1.Takes.Value);
                    Assert.False(s1.IsStatic);
                    Assert.False(s1.TakesContext);
                }
                else if (s1 == methodShouldSerializeCtx)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.Equal(typeof(_ColumnWriters).GetTypeInfo(), s1.Takes.Value);
                    Assert.False(s1.IsStatic);
                    Assert.True(s1.TakesContext);
                }
                else if (s1 == staticMethodShouldSerialize)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.False(s1.Takes.HasValue);
                    Assert.True(s1.IsStatic);
                    Assert.False(s1.TakesContext);
                }
                else if (s1 == staticMethodShouldSerializeParam)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.Equal(typeof(_ColumnWriters).GetTypeInfo(), s1.Takes.Value);
                    Assert.True(s1.IsStatic);
                    Assert.False(s1.TakesContext);
                }
                else if (s1 == staticMethodShouldSerializeCtx)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.False(s1.Takes.HasValue);
                    Assert.True(s1.IsStatic);
                    Assert.True(s1.TakesContext);
                }
                else if (s1 == staticMethodShouldSerializeParamCtx)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.Equal(typeof(_ColumnWriters).GetTypeInfo(), s1.Takes.Value);
                    Assert.True(s1.IsStatic);
                    Assert.True(s1.TakesContext);
                }
                else if (s1 == delShouldSerialize)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.Equal(typeof(_ColumnWriters).GetTypeInfo(), s1.Takes.Value);
                    Assert.False(s1.IsStatic);
                    Assert.True(s1.TakesContext);
                }
                else if (s1 == delOtherShouldSerialize)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.Equal(typeof(_ColumnWriters).GetTypeInfo(), s1.Takes.Value);
                    Assert.False(s1.IsStatic);
                    Assert.True(s1.TakesContext);
                }
                else if (s1 == staticDelShouldSerialize)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.False(s1.Takes.HasValue);
                    Assert.True(s1.IsStatic);
                    Assert.True(s1.TakesContext);
                }
                else if (s1 == staticDelOtherShouldSerialize)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.False(s1.Takes.HasValue);
                    Assert.True(s1.IsStatic);
                    Assert.True(s1.TakesContext);
                }
                else
                {
                    throw new Exception();
                }

                for (var j = i; j < shouldSerializes.Length; j++)
                {
                    var s2 = shouldSerializes[j];

                    var eq = s1 == s2;
                    var neq = s1 != s2;
                    var hashEq = s1.GetHashCode() == s2.GetHashCode();

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                    }
                }
            }

            // ForMethod errors
            {
                Assert.Throws<ArgumentNullException>(() => ShouldSerialize.ForMethod(null));
                Assert.Throws<ArgumentException>(() => ShouldSerialize.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgsShouldSerialize), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => ShouldSerialize.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs2ShouldSerialize), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => ShouldSerialize.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs3ShouldSerialize), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => ShouldSerialize.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs4ShouldSerialize), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => ShouldSerialize.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs5ShouldSerialize), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => ShouldSerialize.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadReturnShouldSerialize), BindingFlags.NonPublic | BindingFlags.Instance)));
            }

            // ForDelegate errors
            {
                Assert.Throws<ArgumentNullException>(() => ShouldSerialize.ForDelegate(null));
                Assert.Throws<ArgumentNullException>(() => ShouldSerialize.ForDelegate<string>(null));
            }

            // delegate cast errors
            {
                Action a = () => { };
                Assert.Throws<InvalidOperationException>(() => (ShouldSerialize)a);
                Func<int, int, bool> b = (_, __) => true;
                Assert.Throws<InvalidOperationException>(() => (ShouldSerialize)b);
                Func<int, int, int, bool> c = (_, __, ___) => true;
                Assert.Throws<InvalidOperationException>(() => (ShouldSerialize)c);
                Func<int, bool> d = _ => true;
                Assert.Throws<InvalidOperationException>(() => (ShouldSerialize)d);
            }
        }

        private class _DynamicRowConverters
        {
            public static int FooStatic { get; set; }

            public int Foo { get; set; }

            public _DynamicRowConverters() { }
            public _DynamicRowConverters(string foo) { }
            public _DynamicRowConverters(int foo) { }

            public _DynamicRowConverters(object obj) { }

        }

        private bool _InstanceDynamicRowConverter(object row, in ReadContext ctx, out string result) { result = ""; return true; }
        private static void _BadRetDynamicRowConverter(object row, in ReadContext ctx, out string result) { result = ""; }
        private static bool _BadArgs1DynamicRowConverter() { return true; }
        private static bool _BadArgs2DynamicRowConverter(string row, in ReadContext ctx, out string result) { result = ""; return true; }
        private static bool _BadArgs3DynamicRowConverter(object row, ReadContext ctx, out string result) { result = ""; return true; }
        private static bool _BadArgs4DynamicRowConverter(object row, in WriteContext ctx, out string result) { result = ""; return true; }
        private static bool _BadArgs5DynamicRowConverter(object row, in ReadContext ctx, string result) { result = ""; return true; }

        private delegate bool _BadDynamicRowDelegate1(object row, ReadContext ctx, string result);
        private delegate bool _BadDynamicRowDelegate2(object row, in WriteContext ctx, out string result);
        private delegate bool _BadDynamicRowDelegate3(object row, in ReadContext ctx, string result);

        private static bool _DynamicRowConverters_Mtd(object row, in ReadContext ctx, out _DynamicRowConverters res) { res = null; return true; }

        [Fact]
        public void DynamicRowConverters()
        {
            var emptyCons = typeof(_DynamicRowConverters).GetConstructor(Type.EmptyTypes);
            Assert.NotNull(emptyCons);

            var stringCons = typeof(_DynamicRowConverters).GetConstructor(new[] { typeof(string) });
            Assert.NotNull(stringCons);

            var intCons = typeof(_DynamicRowConverters).GetConstructor(new[] { typeof(int) });
            Assert.NotNull(intCons);

            var setter = Setter.ForMethod(typeof(_DynamicRowConverters).GetProperty(nameof(_DynamicRowConverters.Foo)).SetMethod);
            Assert.NotNull(setter);

            var staticSetter = Setter.ForMethod(typeof(_DynamicRowConverters).GetProperty(nameof(_DynamicRowConverters.FooStatic)).SetMethod);
            Assert.NotNull(staticSetter);

            // dynamic row converters
            var cons1Converter = DynamicRowConverter.ForConstructorTakingDynamic(typeof(_DynamicRowConverters).GetConstructor(new[] { typeof(object) }));
            var consParamsConverter1 = DynamicRowConverter.ForConstructorTakingTypedParameters(stringCons, new[] { Cesil.ColumnIdentifier.Create(1) });
            var consParamsConverter2 = DynamicRowConverter.ForConstructorTakingTypedParameters(stringCons, new[] { Cesil.ColumnIdentifier.Create(2) });
            var consParamsConverter3 = DynamicRowConverter.ForConstructorTakingTypedParameters(intCons, new[] { Cesil.ColumnIdentifier.Create(2) });

            var consChained1 = cons1Converter.Else(consParamsConverter1);
            var consChained2 = consParamsConverter1.Else(cons1Converter);

            var delConverter = DynamicRowConverter.ForDelegate((object row, in ReadContext ctx, out _DynamicRowConverters res) => { res = null; return true; });
            var cons0Converter1 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { setter }, new[] { Cesil.ColumnIdentifier.Create(1) });
            var cons0Converter2 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { staticSetter }, new[] { Cesil.ColumnIdentifier.Create(1) });
            var cons0Converter3 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { setter, staticSetter }, new[] { Cesil.ColumnIdentifier.Create(1), Cesil.ColumnIdentifier.Create(2) });
            var cons0Converter4 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { setter }, new[] { Cesil.ColumnIdentifier.Create(2) });
            var cons0Converter5 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { staticSetter, setter }, new[] { Cesil.ColumnIdentifier.Create(1), Cesil.ColumnIdentifier.Create(2) });
            var mtdConverter = DynamicRowConverter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_DynamicRowConverters_Mtd), BindingFlags.Static | BindingFlags.NonPublic));

            var converters = new[] { cons1Converter, consParamsConverter1, consParamsConverter2, consParamsConverter3, consChained1, consChained2, delConverter, cons0Converter1, cons0Converter2, cons0Converter3, cons0Converter4, cons0Converter5, mtdConverter };
            for (var i = 0; i < converters.Length; i++)
            {
                var c1 = converters[i];
                Assert.False(c1.Equals(""));
                Assert.NotNull(c1.ToString());

                for (var j = i; j < converters.Length; j++)
                {
                    var c2 = converters[j];

                    var eq = c1 == c2;
                    var neq = c1 != c2;
                    var objEq = c1.Equals((object)c2);
                    var hashEq = c1.GetHashCode() == c2.GetHashCode();

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(objEq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                        Assert.False(objEq);
                    }
                }
            }

            // ForDelegate errors
            {
                Assert.Throws<ArgumentNullException>(() => DynamicRowConverter.ForDelegate<string>(null));
            }

            // ForConstructorTakingDynamic errors
            {
                Assert.Throws<ArgumentNullException>(() => DynamicRowConverter.ForConstructorTakingDynamic(null));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForConstructorTakingDynamic(emptyCons));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForConstructorTakingDynamic(stringCons));
            }

            // ForConstructorTakingTypedParameters errors
            {

                Assert.Throws<ArgumentNullException>(() => DynamicRowConverter.ForConstructorTakingTypedParameters(null, Array.Empty<ColumnIdentifier>()));
                Assert.Throws<ArgumentNullException>(() => DynamicRowConverter.ForConstructorTakingTypedParameters(stringCons, null));
                Assert.Throws<InvalidOperationException>(() => DynamicRowConverter.ForConstructorTakingTypedParameters(stringCons, Array.Empty<ColumnIdentifier>()));

                var badIxs = new[] { Cesil.ColumnIdentifier.CreateInner(-1, "foo") };
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForConstructorTakingTypedParameters(stringCons, badIxs));
            }

            // ForEmptyConstructorAndSetters errors
            {
                Assert.Throws<ArgumentNullException>(() => DynamicRowConverter.ForEmptyConstructorAndSetters(null, Array.Empty<Setter>(), Array.Empty<ColumnIdentifier>()));
                Assert.Throws<ArgumentNullException>(() => DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, null, Array.Empty<ColumnIdentifier>()));
                Assert.Throws<ArgumentNullException>(() => DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, Array.Empty<Setter>(), null));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForEmptyConstructorAndSetters(stringCons, Array.Empty<Setter>(), Array.Empty<ColumnIdentifier>()));

                var ixs = new[] { Cesil.ColumnIdentifier.Create(2, "foo") };

                Assert.Throws<InvalidOperationException>(() => DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, Array.Empty<Setter>(), ixs));

                var badSetter = Setter.ForDelegate<string, string>((string a, string b, in ReadContext _) => { });
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { badSetter }, ixs));

                var badIxs = new[] { Cesil.ColumnIdentifier.CreateInner(-1, "foo") };
                var goodSetter = Setter.ForDelegate<_DynamicRowConverters, int>((_DynamicRowConverters r, int v, in ReadContext _) => r.Foo = v);
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { goodSetter }, badIxs));
            }

            // ForMethods errors
            {
                Assert.Throws<ArgumentNullException>(() => DynamicRowConverter.ForMethod(null));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_InstanceDynamicRowConverter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadRetDynamicRowConverter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs1DynamicRowConverter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs2DynamicRowConverter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs3DynamicRowConverter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs4DynamicRowConverter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs5DynamicRowConverter), BindingFlags.NonPublic | BindingFlags.Static)));
            }

            // Constructor cast errors
            {
                Assert.Throws<ArgumentException>(() => (DynamicRowConverter)stringCons);
            }

            // Delegate cast errors
            {
                Action a = () => { };
                Assert.Throws<InvalidOperationException>(() => (DynamicRowConverter)a);
                Func<bool> b = () => true;
                Assert.Throws<InvalidOperationException>(() => (DynamicRowConverter)b);
                Func<string, string, string, bool> c = delegate { return true; };
                Assert.Throws<InvalidOperationException>(() => (DynamicRowConverter)c);
                Func<object, string, string, bool> d = delegate { return true; };
                Assert.Throws<InvalidOperationException>(() => (DynamicRowConverter)d);
                _BadDynamicRowDelegate1 e = delegate { return true; };
                Assert.Throws<InvalidOperationException>(() => (DynamicRowConverter)e);
                _BadDynamicRowDelegate2 f = (object _, in WriteContext __, out string ___) => { ___ = ""; return true; };
                Assert.Throws<InvalidOperationException>(() => (DynamicRowConverter)f);
                _BadDynamicRowDelegate3 g = (object _, in ReadContext __, string ___) => { return true; };
                Assert.Throws<InvalidOperationException>(() => (DynamicRowConverter)g);
            }
        }

        private class _DynamicRowConverterCast
        {
            public int Foo { get; set; }
        }

        private delegate bool DynamicRowConverterConcreteEquivalentDelegate(dynamic row, in ReadContext ctx, out _DynamicRowConverterCast res);

        private delegate bool DynamicRowConverterGenericEquivalentDelegate<T>(dynamic row, in ReadContext ctx, out T res);

        [Fact]
        public void DynamicRowConverterCast()
        {
            var aCalled = 0;
            DynamicRowConverterConcreteEquivalentDelegate a =
                (dynamic row, in ReadContext ctx, out _DynamicRowConverterCast res) =>
                {
                    aCalled++;
                    res = new _DynamicRowConverterCast();
                    return true;
                };
            var bCalled = 0;
            DynamicRowConverterGenericEquivalentDelegate<_DynamicRowConverterCast> b =
                (dynamic row, in ReadContext ctx, out _DynamicRowConverterCast res) =>
                {
                    bCalled++;
                    res = new _DynamicRowConverterCast();
                    return true;
                };
            var cCalled = 0;
            DynamicRowConverterDelegate<_DynamicRowConverterCast> c =
                (dynamic row, in ReadContext ctx, out _DynamicRowConverterCast res) =>
                {
                    cCalled++;
                    res = new _DynamicRowConverterCast();
                    return true;
                };

            var aWrapped = (DynamicRowConverter)a;
            var aRes = ((DynamicRowConverterDelegate<_DynamicRowConverterCast>)aWrapped.Delegate.Value)(null, default, out var aOut);
            Assert.True(aRes);
            Assert.NotNull(aOut);
            Assert.Equal(1, aCalled);

            var bWrapped = (DynamicRowConverter)b;
            var bRes = ((DynamicRowConverterDelegate<_DynamicRowConverterCast>)bWrapped.Delegate.Value)(null, default, out var bOut);
            Assert.True(bRes);
            Assert.NotNull(bOut);
            Assert.Equal(1, bCalled);

            var cWrapped = (DynamicRowConverter)c;
            var cRes = ((DynamicRowConverterDelegate<_DynamicRowConverterCast>)cWrapped.Delegate.Value)(null, default, out var cOut);
            Assert.True(cRes);
            Assert.NotNull(cOut);
            Assert.Equal(1, cCalled);
            Assert.Same(c, cWrapped.Delegate.Value);

            Assert.Null((DynamicRowConverter)default(MethodInfo));
            Assert.Null((DynamicRowConverter)default(ConstructorInfo));
            Assert.Null((DynamicRowConverter)default(Delegate));
        }

        private class _InstanceBuilderCast
        {
            public int Foo { get; set; }
        }

        private delegate bool InstanceBuilderConcreteEquivalentDelegate(in ReadContext ctx, out _InstanceBuilderCast res);

        private delegate bool InstanceBuilderGenericEquivalentDelegate<T>(in ReadContext ctx, out T res);

        [Fact]
        public void InstanceProvidersCast()
        {
            var aCalled = 0;
            InstanceBuilderConcreteEquivalentDelegate a =
                (in ReadContext _, out _InstanceBuilderCast res) =>
                {
                    aCalled++;

                    res = new _InstanceBuilderCast();
                    return true;
                };
            var bCalled = 0;
            InstanceBuilderGenericEquivalentDelegate<_InstanceBuilderCast> b =
                (in ReadContext _, out _InstanceBuilderCast res) =>
                {
                    bCalled++;

                    res = new _InstanceBuilderCast();
                    return true;
                };
            var cCalled = 0;
            InstanceProviderDelegate<_InstanceBuilderCast> c =
                (in ReadContext _, out _InstanceBuilderCast res) =>
                {
                    cCalled++;

                    res = new _InstanceBuilderCast();
                    return true;
                };

            var aWrapped = (InstanceProvider)a;
            var aRes = ((InstanceProviderDelegate<_InstanceBuilderCast>)aWrapped.Delegate.Value)(default, out var aOut);
            Assert.True(aRes);
            Assert.NotNull(aOut);
            Assert.Equal(1, aCalled);

            var bWrapped = (InstanceProvider)b;
            var bRes = ((InstanceProviderDelegate<_InstanceBuilderCast>)bWrapped.Delegate.Value)(default, out var bOut);
            Assert.True(bRes);
            Assert.NotNull(bOut);
            Assert.Equal(1, bCalled);

            var cWrapped = (InstanceProvider)c;
            var cRes = ((InstanceProviderDelegate<_InstanceBuilderCast>)cWrapped.Delegate.Value)(default, out var cOut);
            Assert.True(cRes);
            Assert.NotNull(cOut);
            Assert.Same(c, cWrapped.Delegate.Value);
            Assert.Equal(1, cCalled);

            var consPs = typeof(_InstanceBuilders).GetConstructor(new[] { typeof(int) });
            Assert.NotNull(consPs);
            var consPsCast = (InstanceProvider)consPs;
            var consPsMtd = InstanceProvider.ForConstructorWithParameters(consPs);
            Assert.Equal(consPsMtd, consPsCast);

            Assert.Null((InstanceProvider)default(MethodInfo));
            Assert.Null((InstanceProvider)default(ConstructorInfo));
            Assert.Null((InstanceProvider)default(Delegate));
        }

        private class _InstanceBuilders
        {
            public _InstanceBuilders() { }

            public _InstanceBuilders(int a) { }
        }

        private static bool _InstanceBuilderStaticMethod(in ReadContext _, out _InstanceBuilders val) { val = new _InstanceBuilders(); return true; }

        private bool _NonStaticBuilder(in ReadContext _, out _InstanceBuilders val) { val = new _InstanceBuilders(); return true; }
        private static void _BadReturnBuilder(out _InstanceBuilders val) { val = new _InstanceBuilders(); }
        private static bool _BadArgs1Builder(int a, int b) { return true; }
        private static bool _BadArgs2Builder(_InstanceBuilders val) { return true; }
        private static bool _BadArgs3Builder(in WriteContext _, out _InstanceBuilders val) { val = null; return true; }
        private static bool _BadArgs4Builder(in ReadContext _, _InstanceBuilders val) { return true; }

        private abstract class _InstanceBuilders_Abstract
        {
            public _InstanceBuilders_Abstract() { }

            public _InstanceBuilders_Abstract(int i) { }
        }

        private class _InstanceBuilders_Generic<T>
        {
            public _InstanceBuilders_Generic() { }
            public _InstanceBuilders_Generic(int i) { }
        }

        private delegate bool _InstanceProvidersBadArgs1(in WriteContext ctx, out _InstanceBuilders val);
        private delegate bool _InstanceProvidersBadArgs2(in ReadContext ctx, _InstanceBuilders val);

        [Fact]
        public void InstanceProviders()
        {
            var methodBuilder = InstanceProvider.ForMethod(typeof(WrapperTests).GetMethod(nameof(_InstanceBuilderStaticMethod), BindingFlags.NonPublic | BindingFlags.Static));
            var constructorBuilder = InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders).GetConstructor(Type.EmptyTypes));
            var delBuilder = InstanceProvider.ForDelegate<_InstanceBuilders>((in ReadContext _, out _InstanceBuilders a) => { a = new _InstanceBuilders(); return true; });
            var chain1 = methodBuilder.Else(constructorBuilder);
            var chain2 = constructorBuilder.Else(methodBuilder);
            var chain3 = constructorBuilder.Else(methodBuilder).Else(delBuilder);
            var builders = new[] { methodBuilder, constructorBuilder, delBuilder, chain1, chain2, chain3 };

            var notBuilder = "";

            for (var i = 0; i < builders.Length; i++)
            {
                var b1 = builders[i];
                Assert.NotNull(b1.ToString());
                Assert.False(b1.Equals(notBuilder));

                if (b1 == methodBuilder)
                {
                    Assert.Equal(BackingMode.Method, b1.Mode);
                }
                else if (b1 == constructorBuilder)
                {
                    Assert.Equal(BackingMode.Constructor, b1.Mode);
                }
                else if (b1 == delBuilder)
                {
                    Assert.Equal(BackingMode.Delegate, b1.Mode);
                }

                for (var j = i; j < builders.Length; j++)
                {
                    var b2 = builders[j];

                    var eq = b1 == b2;
                    var neq = b1 != b2;
                    var hashEq = b1.GetHashCode() == b2.GetHashCode();
                    var objEq = b1.Equals((object)b2);

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.True(objEq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.False(objEq);
                        Assert.True(neq);
                    }
                }
            }

            // ForMethod errors
            {
                Assert.Throws<ArgumentNullException>(() => InstanceProvider.ForMethod(null));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForMethod(typeof(WrapperTests).GetMethod(nameof(_NonStaticBuilder), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadReturnBuilder), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs1Builder), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs2Builder), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs3Builder), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs4Builder), BindingFlags.NonPublic | BindingFlags.Static)));
            }

            // ForParameterlessConstructor errors
            {
                Assert.Throws<ArgumentNullException>(() => InstanceProvider.ForParameterlessConstructor(null));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders).GetConstructor(new[] { typeof(int) })));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders_Abstract).GetConstructor(Type.EmptyTypes)));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders_Generic<>).GetConstructor(Type.EmptyTypes)));
            }

            // ForConstructorWithParameters errors
            {
                Assert.Throws<ArgumentNullException>(() => InstanceProvider.ForConstructorWithParameters(null));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForConstructorWithParameters(typeof(_InstanceBuilders).GetConstructor(Type.EmptyTypes)));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForConstructorWithParameters(typeof(_InstanceBuilders_Abstract).GetConstructor(new[] { typeof(int) })));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForConstructorWithParameters(typeof(_InstanceBuilders_Generic<>).GetConstructor(new[] { typeof(int) })));
            }

            // ForDelegate errors
            {
                Assert.Throws<ArgumentNullException>(() => InstanceProvider.ForDelegate<_InstanceBuilders>(null));
            }

            // delegate cast errors
            {
                Action a = () => { };
                Assert.Throws<InvalidOperationException>(() => (InstanceProvider)a);
                Func<bool> b = () => false;
                Assert.Throws<InvalidOperationException>(() => (InstanceProvider)b);
                Func<ReadContext, _InstanceBuilders, bool> c = (_, __) => false;
                Assert.Throws<InvalidOperationException>(() => (InstanceProvider)c);
                _InstanceProvidersBadArgs1 d = (in WriteContext _, out _InstanceBuilders val) => { val = null; return true; };
                Assert.Throws<InvalidOperationException>(() => (InstanceProvider)d);
                _InstanceProvidersBadArgs2 e = (in ReadContext _, _InstanceBuilders val) => { return true; };
                Assert.Throws<InvalidOperationException>(() => (InstanceProvider)e);
            }

            // GetDefault errors
            {
                Assert.Throws<ArgumentNullException>(() => InstanceProvider.GetDefault(null));
                var intType = typeof(int).GetTypeInfo();
                var intPtrType = intType.MakePointerType().GetTypeInfo();
                var intRefType = intType.MakeByRefType().GetTypeInfo();
                Assert.Throws<ArgumentException>(() => InstanceProvider.GetDefault(intPtrType));
                Assert.Throws<ArgumentException>(() => InstanceProvider.GetDefault(intRefType));

                var listType = typeof(List<>).GetTypeInfo();
                var genParamType = listType.GetGenericArguments()[0].GetTypeInfo();
                Assert.Throws<ArgumentException>(() => InstanceProvider.GetDefault(listType));
                Assert.Throws<ArgumentException>(() => InstanceProvider.GetDefault(genParamType));
            }
        }

        private static readonly MethodInfo _Equatable_DynamicRowConverter_Mtd = typeof(WrapperTests).GetMethod(nameof(_Equatable_DynamicRowConverter), BindingFlags.NonPublic | BindingFlags.Static);
        private static bool _Equatable_DynamicRowConverter(object o, in ReadContext ctx, out _DynamicRowConverterCast res)
        {
            res = new _DynamicRowConverterCast();
            return true;
        }

        private static readonly MethodInfo _Equatable_Formatter_Mtd = typeof(WrapperTests).GetMethod(nameof(_Equatable_Formatter), BindingFlags.NonPublic | BindingFlags.Static);
        private static bool _Equatable_Formatter(int a, in WriteContext b, IBufferWriter<char> c)
        {
            var mem = c.GetMemory(100);
            if (!a.TryFormat(mem.Span, out var d)) return false;

            c.Advance(d);
            return true;
        }

        private static readonly MethodInfo _Equatable_Getter_Mtd = typeof(WrapperTests).GetMethod(nameof(_Equatable_Getter), BindingFlags.NonPublic | BindingFlags.Static);
        private static int _Equatable_Getter(in WriteContext _)
        {
            return 123;
        }

        private static readonly MethodInfo _Equatable_InstanceBuilder_Mtd = typeof(WrapperTests).GetMethod(nameof(_Equatable_InstanceBuilder), BindingFlags.NonPublic | BindingFlags.Static);
        private static bool _Equatable_InstanceBuilder(in ReadContext ctx, out _InstanceBuilderCast a)
        {
            a = new _InstanceBuilderCast();
            return true;
        }

        private static readonly MethodInfo _Equatable_Parser_Mtd = typeof(WrapperTests).GetMethod(nameof(_Equatable_Parser), BindingFlags.NonPublic | BindingFlags.Static);
        private static bool _Equatable_Parser(ReadOnlySpan<char> _, in ReadContext __, out int res)
        {
            res = 44;
            return true;
        }

        private static readonly MethodInfo _Equatable_Reset_Mtd = typeof(WrapperTests).GetMethod(nameof(_Equatable_Reset), BindingFlags.NonPublic | BindingFlags.Static);
        private static void _Equatable_Reset() { }

        private static readonly MethodInfo _Equatable_Setter_Mtd = typeof(WrapperTests).GetMethod(nameof(_Equatable_Setter), BindingFlags.NonPublic | BindingFlags.Static);
        private static void _Equatable_Setter(_SetterCast row, int val) { }

        private static readonly MethodInfo _Equatable_ShouldSerialize_Mtd = typeof(WrapperTests).GetMethod(nameof(_Equatable_ShouldSerialize), BindingFlags.NonPublic | BindingFlags.Static);
        private static bool _Equatable_ShouldSerialize() => true;

        [Fact]
        public void Equatable()
        {
            // DynamicRowConverter
            {
                DynamicRowConverterConcreteEquivalentDelegate aDel =
                    (dynamic row, in ReadContext ctx, out _DynamicRowConverterCast res) =>
                    {
                        res = new _DynamicRowConverterCast();
                        return true;
                    };
                var a = (DynamicRowConverter)aDel;
                var b = (DynamicRowConverter)_Equatable_DynamicRowConverter_Mtd;
                var c = a;
                var d = (DynamicRowConverter)aDel;
                var e = (DynamicRowConverter)_Equatable_DynamicRowConverter_Mtd;
                Assert.False(a == b);

                Assert.True(CompareHashAndReference(a, c));
                Assert.True(a == c);

                Assert.False(CompareHashAndReference(a, d));
                Assert.True(a == d);

                Assert.False(CompareHashAndReference(b, e));
                Assert.True(b == e);
            }

            // Formatter
            {
                FormatterIntEquivDelegate aDel =
                    (int _, in WriteContext __, IBufferWriter<char> ___) =>
                    {
                        return false;
                    };
                var a = (Formatter)aDel;
                var b = (Formatter)_Equatable_Formatter_Mtd;
                var c = a;
                var d = (Formatter)aDel;
                var e = (Formatter)_Equatable_Formatter_Mtd;
                Assert.False(a == b);

                Assert.True(CompareHashAndReference(a, c));
                Assert.True(a == c);

                Assert.False(CompareHashAndReference(a, d));
                Assert.True(a == d);

                Assert.False(CompareHashAndReference(b, e));
                Assert.True(b == e);
            }

            // Getter
            {
                GetterIntEquivDelegate aDel =
                    (_GetterCast row, in WriteContext ctx) =>
                    {
                        return row.Foo.Length;
                    };
                var a = (Getter)aDel;
                var b = (Getter)_Equatable_Getter_Mtd;
                var c = a;
                var d = (Getter)aDel;
                var e = (Getter)_Equatable_Getter_Mtd;
                Assert.False(a == b);

                Assert.True(CompareHashAndReference(a, c));
                Assert.True(a == c);

                Assert.False(CompareHashAndReference(a, d));
                Assert.True(a == d);

                Assert.False(CompareHashAndReference(b, e));
                Assert.True(b == e);
            }

            // InstanceBuilder
            {
                InstanceBuilderConcreteEquivalentDelegate aDel =
                    (in ReadContext _, out _InstanceBuilderCast res) =>
                    {
                        res = new _InstanceBuilderCast();
                        return true;
                    };
                var a = (InstanceProvider)aDel;
                var b = (InstanceProvider)_Equatable_InstanceBuilder_Mtd;
                var c = a;
                var d = (InstanceProvider)aDel;
                var e = (InstanceProvider)_Equatable_InstanceBuilder_Mtd;
                Assert.False(a == b);

                Assert.True(CompareHashAndReference(a, c));
                Assert.True(a == c);

                Assert.False(CompareHashAndReference(a, d));
                Assert.True(a == d);

                Assert.False(CompareHashAndReference(b, e));
                Assert.True(b == e);
            }

            // Parser
            {
                ParserIntEquivDelegate aDel =
                    (ReadOnlySpan<char> _, in ReadContext __, out int res) =>
                    {
                        res = 1;
                        return true;
                    };
                var a = (Parser)aDel;
                var b = (Parser)_Equatable_Parser_Mtd;
                var c = a;
                var d = (Parser)aDel;
                var e = (Parser)_Equatable_Parser_Mtd;
                Assert.False(a == b);

                Assert.True(CompareHashAndReference(a, c));
                Assert.True(a == c);

                Assert.False(CompareHashAndReference(a, d));
                Assert.True(a == d);

                Assert.False(CompareHashAndReference(b, e));
                Assert.True(b == e);
            }

            // Reset
            {
                ResetConcreteEquivDelegate aDel =
                   (_ResetCast row, in ReadContext _) =>
                   {
                   };
                var a = (Reset)aDel;
                var b = (Reset)_Equatable_Reset_Mtd;
                var c = a;
                var d = (Reset)aDel;
                var e = (Reset)_Equatable_Reset_Mtd;
                Assert.False(a == b);

                Assert.True(CompareHashAndReference(a, c));
                Assert.True(a == c);

                Assert.False(CompareHashAndReference(a, d));
                Assert.True(a == d);

                Assert.False(CompareHashAndReference(b, e));
                Assert.True(b == e);
            }

            // Setter
            {
                SetterConcreteEquivDelegate aDel =
                    (_SetterCast row, int val, in ReadContext _) =>
                    {
                        row.Foo = val.ToString();
                    };
                var a = (Setter)aDel;
                var b = (Setter)_Equatable_Setter_Mtd;
                var c = a;
                var d = (Setter)aDel;
                var e = (Setter)_Equatable_Setter_Mtd;
                Assert.False(a == b);

                Assert.True(CompareHashAndReference(a, c));
                Assert.True(a == c);

                Assert.False(CompareHashAndReference(a, d));
                Assert.True(a == d);

                Assert.False(CompareHashAndReference(b, e));
                Assert.True(b == e);
            }

            // ShouldSerialize
            {
                ShouldSerializeConcreteEquivDelegate aDel =
                   (_ShouldSerializeCast row, in WriteContext _) =>
                   {
                       return true;
                   };
                var a = (ShouldSerialize)aDel;
                var b = (ShouldSerialize)_Equatable_ShouldSerialize_Mtd;
                var c = a;
                var d = (ShouldSerialize)aDel;
                var e = (ShouldSerialize)_Equatable_ShouldSerialize_Mtd;
                Assert.False(a == b);

                Assert.True(CompareHashAndReference(a, c));
                Assert.True(a == c);

                Assert.False(CompareHashAndReference(a, d));
                Assert.True(a == d);

                Assert.False(CompareHashAndReference(b, e));
                Assert.True(b == e);
            }

            static bool CompareHashAndReference(object a, object b)
            {
                var code = a.GetHashCode() == b.GetHashCode();
                var reference = object.ReferenceEquals(a, b);

                return code && reference;
            }
        }

        [Fact]
        public void ColumnIdentifier()
        {
            var ci1 = Cesil.ColumnIdentifier.Create(0);
            var ci2 = Cesil.ColumnIdentifier.Create(0, "A");
            var ci3 = Cesil.ColumnIdentifier.Create(1);
            var ci4 = Cesil.ColumnIdentifier.Create(1, "A");
            var ci5 = Cesil.ColumnIdentifier.Create(1, "B");
            var ci6 = Cesil.ColumnIdentifier.Create(0, "B");
            var ci7 = Cesil.ColumnIdentifier.Create(2, "C");

            var cis = new[] { ci1, ci2, ci3, ci4, ci5, ci6, ci7 };

            var notCi = "";

            for (var i = 0; i < cis.Length; i++)
            {
                var a = cis[i];

                Assert.False(a.Equals(notCi));

                for (var j = i; j < cis.Length; j++)
                {
                    var b = cis[j];

                    var eq = a == b;
                    var neq = a != b;
                    var hashEq = CompareHash(a, b);

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                    }
                }
            }

            Assert.Equal(0, (int)ci1);

            Assert.Throws<InvalidOperationException>(() => ci1.Name);

            Assert.Throws<ArgumentException>(() => Cesil.ColumnIdentifier.Create(-1));

            Assert.Throws<ArgumentException>(() => Cesil.ColumnIdentifier.Create(-1, "foo"));
            Assert.Throws<ArgumentNullException>(() => Cesil.ColumnIdentifier.Create(10, null));

            static bool CompareHash<T>(T a, T b)
            {
                var code = a.GetHashCode() == b.GetHashCode();

                return code;
            }
        }

        [Fact]
        public void DynamicCellValues()
        {
            // exporing equality
            var names = new[] { "A", "B", null };
            var values = new[] { "hello", "world", null };
            var mtdFormatter = Formatter.GetDefault(typeof(string).GetTypeInfo());
            var delFormatter = Formatter.ForDelegate((string val, in WriteContext ctx, IBufferWriter<char> buffer) => true);
            var formatters = new[] { mtdFormatter, delFormatter };

            var vals = new List<DynamicCellValue>();
            foreach (var n in names)
            {
                foreach (var v in values)
                {
                    foreach (var f in formatters)
                    {
                        vals.Add(DynamicCellValue.Create(n, v, f));
                    }
                }
            }

            for (var i = 0; i < vals.Count; i++)
            {
                var v1 = vals[i];

                Assert.False(v1.Equals("hello"));

                for (var j = i; j < vals.Count; j++)
                {
                    var v2 = vals[j];

                    var eq = v1 == v2;
                    var neq = v1 != v2;
                    var objEq = v1.Equals((object)v2);
                    var hashEq = v1.GetHashCode() == v2.GetHashCode();

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(objEq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                        Assert.False(objEq);
                    }
                }
            }

            // more direct tests

            var dcv1 = DynamicCellValue.Create("Foo", "Bar", Formatter.GetDefault(typeof(string).GetTypeInfo()));
            var dcv2 = dcv1;
            var dcv3 = DynamicCellValue.Create("Foo", "Bar", Formatter.GetDefault(typeof(string).GetTypeInfo()));
            var dcv4 = DynamicCellValue.Create("Foo", 123, Formatter.GetDefault(typeof(int).GetTypeInfo()));

            Assert.True(dcv1 == dcv2);
            Assert.True(CompareHash(dcv1, dcv2));
            Assert.True(dcv1.Equals((object)dcv2));

            Assert.True(dcv1 == dcv3);
            Assert.True(CompareHash(dcv1, dcv3));
            Assert.True(dcv1.Equals((object)dcv3));

            Assert.True(dcv1 != dcv4);
            Assert.False(CompareHash(dcv1, dcv4));
            Assert.False(dcv1.Equals((object)dcv4));

            Assert.False(dcv1.Equals(null));
            Assert.False(dcv1.Equals(""));

            var dcvNull1 = DynamicCellValue.Create("Foo", null, Formatter.GetDefault(typeof(string).GetTypeInfo()));
            var dcvNull2 = DynamicCellValue.Create("Foo", null, Formatter.GetDefault(typeof(string).GetTypeInfo()));

            Assert.True(dcvNull1 == dcvNull2);
            Assert.True(dcvNull1.Equals((object)dcvNull2));
            Assert.False(dcvNull1 == dcv1);
            Assert.False(dcvNull1.Equals(""));

            Assert.Throws<ArgumentNullException>(() => DynamicCellValue.Create("foo", "bar", null));
            Assert.Throws<ArgumentException>(() => DynamicCellValue.Create("foo", "bar", Formatter.GetDefault(typeof(int).GetTypeInfo())));

            static bool CompareHash<T>(T a, T b)
            {
                var code = a.GetHashCode() == b.GetHashCode();

                return code;
            }
        }
    }
}
