using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
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
            public static _ColumnSetters_Val StaticField;
            public static _ColumnSetters_Val _Set;

            public bool ResetCalled;

            public _ColumnSetters_Val Prop { get; set; }
#pragma warning disable CS0649
            public _ColumnSetters_Val Field;
#pragma warning restore CS0649

            public static void Set(_ColumnSetters_Val c)
            {
                _Set = c;
            }

            public void ResetXXX()
            {
                ResetCalled = true;
            }

            public static void StaticResetXXX()
            {
                StaticResetCalled = true;
            }
        }

        private static bool _ColumnSetters_Parser(ReadOnlySpan<char> r, in ReadContext ctx, out _ColumnSetters_Val v)
        {
            v = new _ColumnSetters_Val(r);
            return true;
        }

        [Fact]
        public void ColumnSetters()
        {
            // parsers
            var methodParser = Parser.ForMethod(typeof(WrapperTests).GetMethod(nameof(_ColumnSetters_Parser), BindingFlags.Static | BindingFlags.NonPublic));
            var delParser = (Parser)(ParserDelegate<_ColumnSetters_Val>)((ReadOnlySpan<char> data, in ReadContext ctx, out _ColumnSetters_Val result) => { result = new _ColumnSetters_Val(data); return true; });
            var consOneParser = Parser.ForConstructor(typeof(_ColumnSetters_Val).GetConstructor(new[] { typeof(ReadOnlySpan<char>) }));
            var consTwoParser = Parser.ForConstructor(typeof(_ColumnSetters_Val).GetConstructor(new[] { typeof(ReadOnlySpan<char>), typeof(ReadContext).MakeByRefType() }));
            var parsers = new[] { methodParser, delParser, consOneParser, consTwoParser };

            // setters
            var methodSetter = Setter.ForMethod(typeof(_ColumnSetters).GetProperty(nameof(_ColumnSetters.Prop)).SetMethod);
            var staticMethodSetter = Setter.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.Set)));
            var fieldSetter = Setter.ForField(typeof(_ColumnSetters).GetField(nameof(_ColumnSetters.Field)));
            var delSetterCalled = false;
            var delSetter = (Setter)(SetterDelegate<_ColumnSetters, _ColumnSetters_Val>)((_ColumnSetters a, _ColumnSetters_Val v) => { delSetterCalled = true; a.Prop = v; });
            var staticDelSetterCalled = false;
            var staticDelSetter = (Setter)(StaticSetterDelegate<_ColumnSetters_Val>)((_ColumnSetters_Val f) => { staticDelSetterCalled = true; _ColumnSetters.StaticField = f; });
            var setters = new[] { methodSetter, staticMethodSetter, fieldSetter, delSetter, staticDelSetter };

            // resets
            var methodReset = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.ResetXXX)));
            var staticMethodReset = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticResetXXX)));
            var delResetCalled = false;
            var delReset = (Reset)(ResetDelegate<_ColumnSetters>)((_ColumnSetters a) => { delResetCalled = true; });
            var staticDelResetCalled = false;
            var staticDelReset = (Reset)(StaticResetDelegate)(() => { staticDelResetCalled = true; });
            var resets = new[] { methodReset, staticMethodReset, delReset, staticDelReset, null };

            foreach (var p in parsers)
            {
                foreach (var s in setters)
                {
                    foreach (var r in resets)
                    {
                        delSetterCalled = delResetCalled = staticDelResetCalled = false;

                        _ColumnSetters.StaticField = null;
                        _ColumnSetters.StaticResetCalled = false;
                        _ColumnSetters._Set = null;

                        var inst = new _ColumnSetters();

                        var rNonNull = new NonNull<Reset>();
                        rNonNull.SetAllowNull(r);

                        var setter = ColumnSetter.Create(typeof(_ColumnSetters).GetTypeInfo(), p, s, rNonNull);
                        var res = setter("hello", default, inst);

                        Assert.True(res);

                        if (s == methodSetter)
                        {
                            Assert.Equal("hello", inst.Prop.Value);
                        }
                        else if (s == staticMethodSetter)
                        {
                            Assert.Equal("hello", _ColumnSetters._Set.Value);
                        }
                        else if (s == fieldSetter)
                        {
                            Assert.Equal("hello", inst.Field.Value);
                        }
                        else if (s == delSetter)
                        {
                            Assert.True(delSetterCalled);
                            Assert.Equal("hello", inst.Prop.Value);
                        }
                        else if (s == staticDelSetter)
                        {
                            Assert.True(staticDelSetterCalled);
                            Assert.Equal("hello", _ColumnSetters.StaticField.Value);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }

                        if (r == methodReset)
                        {
                            Assert.True(inst.ResetCalled);
                        }
                        else if (r == staticMethodReset)
                        {
                            Assert.True(_ColumnSetters.StaticResetCalled);
                        }
                        else if (r == delReset)
                        {
                            Assert.True(delResetCalled);
                        }
                        else if (r == staticDelReset)
                        {
                            Assert.True(staticDelResetCalled);
                        }
                        else if (r == null)
                        {
                            // intentionally blank
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                }
            }
        }

        private class _ColumnWriters_Val
        {
            public string Value { get; set; }
        }

        private class _ColumnWriters
        {
            public static _ColumnWriters_Val StaticA;
            public _ColumnWriters_Val A;

            public _ColumnWriters_Val Get() => new _ColumnWriters_Val { Value = "A" };

            public static _ColumnWriters_Val GetStatic() => new _ColumnWriters_Val { Value = "static" };

            public bool ShouldSerializeCalled;
            public bool ShouldSerialize()
            {
                ShouldSerializeCalled = true;
                return true;
            }

            public static bool ShouldSerializeStaticCalled;
            public static bool ShouldSerializeStatic()
            {
                ShouldSerializeStaticCalled = true;
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
            var delShouldSerializeCalled = false;
            var delShouldSerialize = (ShouldSerialize)(ShouldSerializeDelegate<_ColumnWriters>)((_ColumnWriters _) => { delShouldSerializeCalled = true; return true; });
            var staticDelShouldSerializeCalled = false;
            var staticDelShouldSerialize = (ShouldSerialize)(StaticShouldSerializeDelegate)(() => { staticDelShouldSerializeCalled = true; return true; });
            var shouldSerializes = new[] { methodShouldSerialize, staticMethodShouldSerialize, delShouldSerialize, staticDelShouldSerialize, null };

            // getter
            var methodGetter = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.Get)));
            var staticMethodGetter = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.GetStatic)));
            var fieldGetter = Getter.ForField(typeof(_ColumnWriters).GetField(nameof(_ColumnWriters.A)));
            var staticFieldGetter = Getter.ForField(typeof(_ColumnWriters).GetField(nameof(_ColumnWriters.StaticA)));
            var delGetterCalled = false;
            var delGetter = (Getter)(GetterDelegate<_ColumnWriters, _ColumnWriters_Val>)((_ColumnWriters row) => { delGetterCalled = true; return row.A; });
            var staticDelGetterCalled = false;
            var staticDelGetter = (Getter)(StaticGetterDelegate<_ColumnWriters_Val>)(() => { staticDelGetterCalled = true; return new _ColumnWriters_Val { Value = "foo" }; });
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
                            _ColumnWriters_Val_Format_Called = false;

                            var inst = new _ColumnWriters { A = new _ColumnWriters_Val { Value = "bar" } };

                            var sNonNull = new NonNull<ShouldSerialize>();
                            sNonNull.SetAllowNull(s);
                            var colWriter = ColumnWriter.Create(typeof(_ColumnWriters).GetTypeInfo(), f, sNonNull, g, e);

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

        [Fact]
        public async Task FormattersAsync()
        {
            // formatters
            var methodFormatter = Formatter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_ColumnWriters_Val_Format), BindingFlags.Static | BindingFlags.NonPublic));
            var delFormatter = (Formatter)(FormatterDelegate<_ColumnWriters_Val>)((_ColumnWriters_Val cell, in WriteContext _, IBufferWriter<char> writeTo) => { writeTo.Write(cell.Value.AsSpan()); return true; });
            var formatters = new[] { methodFormatter, delFormatter };

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

        private delegate int GetterIntEquivDelegate(_GetterCast row);

        private delegate int StaticGetterIntEquivDelegate();

        private delegate V GetterEquivDelegate<T, V>(T row);

        private delegate V StaticGetterEquivDelegate<V>();

        [Fact]
        public void GetterCast()
        {
            var aCalled = 0;
            GetterIntEquivDelegate a =
                (row) =>
                {
                    aCalled++;
                    return row.Foo.Length;
                };
            var bCalled = 0;
            StaticGetterIntEquivDelegate b =
                () =>
                {
                    bCalled++;
                    return 3;
                };
            var cCalled = 0;
            GetterEquivDelegate<_GetterCast, double> c =
                (row) =>
                {
                    cCalled++;

                    return row.Foo.Length * 2.0;
                };
            var dRet = Guid.NewGuid();
            var dCalled = 0;
            StaticGetterEquivDelegate<Guid> d =
                () =>
                {
                    dCalled++;
                    return dRet;
                };
            var eCalled = 0;
            Func<_GetterCast, char> e =
                row =>
                {
                    eCalled++;
                    return row.Foo[0];
                };
            var fCalled = 0;
            Func<sbyte> f =
                () =>
                {
                    fCalled++;
                    return -127;
                };
            var gCalled = 0;
            GetterDelegate<_GetterCast, int> g =
                row =>
                {
                    gCalled++;
                    return row.Foo.Length + 1;
                };
            var hCalled = 0;
            StaticGetterDelegate<int> h =
                () =>
                {
                    hCalled++;
                    return 456;
                };

            var aWrapped = (Getter)a;
            var aRes1 = (int)aWrapped.Delegate.Value.DynamicInvoke(new _GetterCast { Foo = "yo" });
            Assert.Equal(2, aRes1);
            Assert.Equal(1, aCalled);
            var aRes2 = ((GetterDelegate<_GetterCast, int>)aWrapped.Delegate.Value)(new _GetterCast { Foo = "yolo" });
            Assert.Equal(4, aRes2);
            Assert.Equal(2, aCalled);

            var bWrapped = (Getter)b;
            var bRes1 = (int)bWrapped.Delegate.Value.DynamicInvoke();
            Assert.Equal(3, bRes1);
            Assert.Equal(1, bCalled);
            var bRes2 = ((StaticGetterDelegate<int>)bWrapped.Delegate.Value)();
            Assert.Equal(3, bRes2);
            Assert.Equal(2, bCalled);

            var cWrapped = (Getter)c;
            var cRes1 = (double)cWrapped.Delegate.Value.DynamicInvoke(new _GetterCast { Foo = "yo" });
            Assert.Equal(4.0, cRes1);
            Assert.Equal(1, cCalled);
            var cRes2 = ((GetterDelegate<_GetterCast, double>)cWrapped.Delegate.Value)(new _GetterCast { Foo = "yolo" });
            Assert.Equal(8.0, cRes2);
            Assert.Equal(2, cCalled);

            var dWrapped = (Getter)d;
            var dRes1 = (Guid)dWrapped.Delegate.Value.DynamicInvoke();
            Assert.Equal(dRet, dRes1);
            Assert.Equal(1, dCalled);
            var dRes2 = ((StaticGetterDelegate<Guid>)dWrapped.Delegate.Value)();
            Assert.Equal(dRet, dRes2);
            Assert.Equal(2, dCalled);

            var eWrapped = (Getter)e;
            var eRes1 = (char)eWrapped.Delegate.Value.DynamicInvoke(new _GetterCast { Foo = "yo" });
            Assert.Equal('y', eRes1);
            Assert.Equal(1, eCalled);
            var eRes2 = ((GetterDelegate<_GetterCast, char>)eWrapped.Delegate.Value)(new _GetterCast { Foo = "hello" });
            Assert.Equal('h', eRes2);
            Assert.Equal(2, eCalled);

            var fWrapped = (Getter)f;
            var fRes1 = (sbyte)fWrapped.Delegate.Value.DynamicInvoke();
            Assert.Equal(-127, fRes1);
            Assert.Equal(1, fCalled);
            var fRes2 = ((StaticGetterDelegate<sbyte>)fWrapped.Delegate.Value)();
            Assert.Equal(-127, fRes2);
            Assert.Equal(2, fCalled);

            var gWrapped = (Getter)g;
            var gRes1 = (int)gWrapped.Delegate.Value.DynamicInvoke(new _GetterCast { Foo = "yo" });
            Assert.Equal(3, gRes1);
            Assert.Equal(1, gCalled);
            var gRes2 = ((GetterDelegate<_GetterCast, int>)gWrapped.Delegate.Value)(new _GetterCast { Foo = "yolo" });
            Assert.Equal(5, gRes2);
            Assert.Same(g, gWrapped.Delegate.Value);
            Assert.Equal(2, gCalled);

            var hWrapped = (Getter)h;
            var hRes1 = (int)hWrapped.Delegate.Value.DynamicInvoke();
            Assert.Equal(456, hRes1);
            Assert.Equal(1, hCalled);
            var hRes2 = ((StaticGetterDelegate<int>)hWrapped.Delegate.Value)();
            Assert.Equal(456, hRes2);
            Assert.Same(h, hWrapped.Delegate.Value);
            Assert.Equal(2, hCalled);
        }

        private void _BadReturnGetter() { }
        private static string _BadArgs1Getter(int a, int b) { return null; }
        private string _BadArgs2Getter(int a) { return null; }

        [Fact]
        public void Getters()
        {
            // getter
            var methodGetter = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.Get)));
            var staticMethodGetter = Getter.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.GetStatic)));
            var fieldGetter = Getter.ForField(typeof(_ColumnWriters).GetField(nameof(_ColumnWriters.A)));
            var staticFieldGetter = Getter.ForField(typeof(_ColumnWriters).GetField(nameof(_ColumnWriters.StaticA)));
            var delGetter = (Getter)(GetterDelegate<_ColumnWriters, _ColumnWriters_Val>)((_ColumnWriters row) => { return row.A; });
            var staticDelGetter = (Getter)(StaticGetterDelegate<_ColumnWriters_Val>)(() => { return new _ColumnWriters_Val { Value = "foo" }; });
            var getters = new[] { methodGetter, staticMethodGetter, fieldGetter, staticFieldGetter, delGetter, staticDelGetter };

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
                }
                else if (g1 == staticMethodGetter)
                {
                    Assert.Equal(BackingMode.Method, g1.Mode);
                    Assert.True(g1.IsStatic);
                }
                else if (g1 == fieldGetter)
                {
                    Assert.Equal(BackingMode.Field, g1.Mode);
                    Assert.False(g1.IsStatic);
                }
                else if (g1 == staticFieldGetter)
                {
                    Assert.Equal(BackingMode.Field, g1.Mode);
                    Assert.True(g1.IsStatic);
                }
                else if (g1 == delGetter)
                {
                    Assert.Equal(BackingMode.Delegate, g1.Mode);
                    Assert.False(g1.IsStatic);
                }
                else if (g1 == staticDelGetter)
                {
                    Assert.Equal(BackingMode.Delegate, g1.Mode);
                    Assert.True(g1.IsStatic);
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

                var aRes = (_ColumnWriters_Val)a(new _ColumnWriters());
                Assert.Equal("A", aRes.Value);

                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticMethodGetter).Guarantee(cache);
                var c = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticMethodGetter).CachedDelegate.Value;
                Assert.NotNull(c);
                Assert.NotEqual(a, c);
                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticMethodGetter).Guarantee(cache);
                var d = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)staticMethodGetter).CachedDelegate.Value;
                Assert.Equal(c, d);

                var cRes = (_ColumnWriters_Val)c(new _ColumnWriters());
                Assert.Equal("static", cRes.Value);

                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)fieldGetter).Guarantee(cache);
                var e = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)fieldGetter).CachedDelegate.Value;
                Assert.NotNull(e);
                Assert.NotEqual(a, e);
                Assert.NotEqual(c, e);
                ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)fieldGetter).Guarantee(cache);
                var f = ((ICreatesCacheableDelegate<Getter.DynamicGetterDelegate>)fieldGetter).CachedDelegate.Value;
                Assert.Equal(e, f);

                var eRes = (_ColumnWriters_Val)e(new _ColumnWriters { A = new _ColumnWriters_Val { Value = "asdf" } });
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
                var gRes = (_ColumnWriters_Val)g(new _ColumnWriters());
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

                var iRes = (_ColumnWriters_Val)i(new _ColumnWriters { A = new _ColumnWriters_Val { Value = "xxxxx" } });
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

                var lRes = (_ColumnWriters_Val)l(new _ColumnWriters());
                Assert.Equal("foo", lRes.Value);
            }

            // ForMethod errors
            {
                Assert.Throws<ArgumentNullException>(() => Getter.ForMethod(null));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadReturnGetter), BindingFlags.NonPublic | BindingFlags.Instance)));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs1Getter), BindingFlags.NonPublic | BindingFlags.Static)));
                Assert.Throws<ArgumentException>(() => Getter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_BadArgs2Getter), BindingFlags.NonPublic | BindingFlags.Instance)));
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
            }
        }

        private static void _BadResetArgs1(int a, int b) { }

        private class _Resets
        {
            public void Reset(int a) { }
        }

        [Fact]
        public void Resets()
        {
            // resets
            var methodReset = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.ResetXXX)));
            var staticMethodReset = Reset.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.StaticResetXXX)));
            var delReset = (Reset)(ResetDelegate<_ColumnSetters>)((_ColumnSetters a) => { });
            var staticDelReset = (Reset)(StaticResetDelegate)(() => { });
            var resets = new[] { methodReset, staticMethodReset, delReset, staticDelReset };

            var notReset = "";

            for (var i = 0; i < resets.Length; i++)
            {
                var r1 = resets[i];
                Assert.NotNull(r1.ToString());
                Assert.False(r1.Equals(notReset));

                if (r1 == methodReset)
                {
                    Assert.Equal(BackingMode.Method, r1.Mode);
                    Assert.False(r1.IsStatic);
                }
                else if (r1 == staticMethodReset)
                {
                    Assert.Equal(BackingMode.Method, r1.Mode);
                    Assert.True(r1.IsStatic);
                }
                else if (r1 == delReset)
                {
                    Assert.Equal(BackingMode.Delegate, r1.Mode);
                    Assert.False(r1.IsStatic);
                }
                else if (r1 == staticDelReset)
                {
                    Assert.Equal(BackingMode.Delegate, r1.Mode);
                    Assert.True(r1.IsStatic);
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
        private static bool _BadArgs5Parser(ReadOnlySpan<char> data, in WriteContext ctx, int res) { return false; }
        private static string _BadReturnParser(ReadOnlySpan<char> data, in WriteContext ctx, out int res) { res = 0; return ""; }

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
        }

        private class _ResetCast
        {
            public int Foo { get; set; }
        }

        private delegate void ResetConcreteEquivDelegate(_ResetCast row);

        private delegate void ResetEquivDelegate<T>(T row);

        private delegate void StaticResetEquivDelegate();

        [Fact]
        public void ResetCast()
        {
            var aCalled = 0;
            ResetConcreteEquivDelegate a =
                row =>
                {
                    aCalled++;
                };
            var bCalled = 0;
            ResetEquivDelegate<_ResetCast> b =
                row =>
                {
                    bCalled++;
                };
            var cCalled = 0;
            StaticResetEquivDelegate c =
                () =>
                {
                    cCalled++;
                };
            var dCalled = 0;
            Action<_ResetCast> d =
                row =>
                {
                    dCalled++;
                };
            var eCalled = 0;
            Action e =
                () =>
                {
                    eCalled++;
                };
            var fCalled = 0;
            ResetDelegate<_ResetCast> f =
                row =>
                {
                    fCalled++;
                };
            var gCalled = 0;
            StaticResetDelegate g =
                () =>
                {
                    gCalled++;
                };

            var aWrapped = (Reset)a;
            aWrapped.Delegate.Value.DynamicInvoke(new _ResetCast());
            Assert.Equal(1, aCalled);
            ((ResetDelegate<_ResetCast>)aWrapped.Delegate.Value)(new _ResetCast());
            Assert.Equal(2, aCalled);

            var bWrapped = (Reset)b;
            bWrapped.Delegate.Value.DynamicInvoke(new _ResetCast());
            Assert.Equal(1, bCalled);
            ((ResetDelegate<_ResetCast>)bWrapped.Delegate.Value)(new _ResetCast());
            Assert.Equal(2, bCalled);

            var cWrapped = (Reset)c;
            cWrapped.Delegate.Value.DynamicInvoke();
            Assert.Equal(1, cCalled);
            ((StaticResetDelegate)cWrapped.Delegate.Value)();
            Assert.Equal(2, cCalled);

            var dWrapped = (Reset)d;
            dWrapped.Delegate.Value.DynamicInvoke(new _ResetCast());
            Assert.Equal(1, dCalled);
            ((ResetDelegate<_ResetCast>)dWrapped.Delegate.Value)(new _ResetCast());
            Assert.Equal(2, dCalled);

            var eWrapped = (Reset)e;
            eWrapped.Delegate.Value.DynamicInvoke();
            Assert.Equal(1, eCalled);
            ((StaticResetDelegate)eWrapped.Delegate.Value)();
            Assert.Equal(2, eCalled);

            var fWrapped = (Reset)f;
            fWrapped.Delegate.Value.DynamicInvoke(new _ResetCast());
            Assert.Equal(1, fCalled);
            Assert.Same(f, fWrapped.Delegate.Value);
            ((ResetDelegate<_ResetCast>)fWrapped.Delegate.Value)(new _ResetCast());
            Assert.Equal(2, fCalled);

            var gWrapped = (Reset)g;
            gWrapped.Delegate.Value.DynamicInvoke();
            Assert.Equal(1, gCalled);
            Assert.Same(g, gWrapped.Delegate.Value);
            ((StaticResetDelegate)gWrapped.Delegate.Value)();
            Assert.Equal(2, gCalled);
        }

        private class _SetterCast
        {
            public string Foo { get; set; }
        }

        private delegate void SetterConcreteEquivDelegate(_SetterCast row, int val);

        private delegate void StaticSetterConcreteEquivDelegate(int val);

        private delegate void SetterGenEquivDelegate<T, V>(T row, V val);

        private delegate void StaticSetterGenEquivDelegate<V>(V val);

        [Fact]
        public void SetterCast()
        {
            var aCalled = 0;
            SetterConcreteEquivDelegate a =
                (row, val) =>
                {
                    aCalled++;
                    row.Foo = val.ToString();
                };
            var bCalled = 0;
            StaticSetterConcreteEquivDelegate b =
                val =>
                {
                    bCalled++;
                };
            var cCalled = 0;
            SetterGenEquivDelegate<_SetterCast, string> c =
                (row, val) =>
                {
                    cCalled++;
                    row.Foo = val;
                };
            var dCalled = 0;
            StaticSetterGenEquivDelegate<string> d =
                val =>
                {
                    dCalled++;
                };
            var eCalled = 0;
            Action<_SetterCast, Guid> e =
                (row, val) =>
                {
                    eCalled++;
                    row.Foo = val.ToString();
                };
            var fCalled = 0;
            Action<Guid> f =
                (val) =>
                {
                    fCalled++;
                };
            var gCalled = 0;
            SetterDelegate<_SetterCast, TimeSpan> g =
                (row, val) =>
                {
                    gCalled++;
                    row.Foo = val.ToString();
                };
            var hCalled = 0;
            StaticSetterDelegate<TimeSpan> h =
                (val) =>
                {
                    hCalled++;
                };

            var aWrapped = (Setter)a;
            aWrapped.Delegate.Value.DynamicInvoke(new _SetterCast(), 123);
            Assert.Equal(1, aCalled);
            ((SetterDelegate<_SetterCast, int>)aWrapped.Delegate.Value)(new _SetterCast(), 123);
            Assert.Equal(2, aCalled);

            var bWrapped = (Setter)b;
            bWrapped.Delegate.Value.DynamicInvoke(123);
            Assert.Equal(1, bCalled);
            ((StaticSetterDelegate<int>)bWrapped.Delegate.Value)(123);
            Assert.Equal(2, bCalled);

            var cWrapped = (Setter)c;
            cWrapped.Delegate.Value.DynamicInvoke(new _SetterCast(), "123");
            Assert.Equal(1, cCalled);
            ((SetterDelegate<_SetterCast, string>)cWrapped.Delegate.Value)(new _SetterCast(), "123");
            Assert.Equal(2, cCalled);

            var dWrapped = (Setter)d;
            dWrapped.Delegate.Value.DynamicInvoke("123");
            Assert.Equal(1, dCalled);
            ((StaticSetterDelegate<string>)dWrapped.Delegate.Value)("123");
            Assert.Equal(2, dCalled);

            var eWrapped = (Setter)e;
            eWrapped.Delegate.Value.DynamicInvoke(new _SetterCast(), Guid.NewGuid());
            Assert.Equal(1, eCalled);
            ((SetterDelegate<_SetterCast, Guid>)eWrapped.Delegate.Value)(new _SetterCast(), Guid.NewGuid());
            Assert.Equal(2, eCalled);

            var fWrapped = (Setter)f;
            fWrapped.Delegate.Value.DynamicInvoke(Guid.NewGuid());
            Assert.Equal(1, fCalled);
            ((StaticSetterDelegate<Guid>)fWrapped.Delegate.Value)(Guid.NewGuid());
            Assert.Equal(2, fCalled);

            var gWrapped = (Setter)g;
            gWrapped.Delegate.Value.DynamicInvoke(new _SetterCast(), TimeSpan.FromMinutes(1));
            Assert.Equal(1, gCalled);
            ((SetterDelegate<_SetterCast, TimeSpan>)gWrapped.Delegate.Value)(new _SetterCast(), TimeSpan.FromMinutes(1));
            Assert.Same(g, gWrapped.Delegate.Value);
            Assert.Equal(2, gCalled);

            var hWrapped = (Setter)h;
            hWrapped.Delegate.Value.DynamicInvoke(TimeSpan.FromMinutes(1));
            Assert.Equal(1, hCalled);
            ((StaticSetterDelegate<TimeSpan>)hWrapped.Delegate.Value)(TimeSpan.FromMinutes(1));
            Assert.Same(h, hWrapped.Delegate.Value);
            Assert.Equal(2, hCalled);
        }

        private bool _BadReturnSetter() { return false; }
        private void _BadArgs1Setter(int a, int b) { }
        private void _BadArgs2Setter(int a, int b, int c) { }

        [Fact]
        public void Setters()
        {
            // setters
            var methodSetter = Setter.ForMethod(typeof(_ColumnSetters).GetProperty(nameof(_ColumnSetters.Prop)).SetMethod);
            var staticMethodSetter = Setter.ForMethod(typeof(_ColumnSetters).GetMethod(nameof(_ColumnSetters.Set)));
            var fieldSetter = Setter.ForField(typeof(_ColumnSetters).GetField(nameof(_ColumnSetters.Field)));
            var staticFieldSetter = Setter.ForField(typeof(_ColumnSetters).GetField(nameof(_ColumnSetters.StaticField)));
            var delSetter = (Setter)(SetterDelegate<_ColumnSetters, _ColumnSetters_Val>)((_ColumnSetters a, _ColumnSetters_Val v) => { a.Prop = v; });
            var staticDelSetter = (Setter)(StaticSetterDelegate<_ColumnSetters_Val>)((_ColumnSetters_Val f) => { _ColumnSetters.StaticField = f; });
            var setters = new[] { methodSetter, staticMethodSetter, fieldSetter, delSetter, staticDelSetter };

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
                else if (s1 == staticMethodSetter)
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
                else if (s1 == delSetter)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.False(s1.IsStatic);
                }
                else if (s1 == staticDelSetter)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.True(s1.IsStatic);
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
            }

            // ForField errors
            {
                Assert.Throws<ArgumentNullException>(() => Setter.ForField(null));
            }

            // ForDelegate errors
            {
                Assert.Throws<ArgumentNullException>(() => Setter.ForDelegate<string>(null));
                Assert.Throws<ArgumentNullException>(() => Setter.ForDelegate<string, int>(null));
            }

            // delegate cast errors
            {
                Func<string> a = () => "";
                Assert.Throws<InvalidOperationException>(() => (Setter)a);
                Action<int, int, int> b = (_, __, ___) => { };
                Assert.Throws<InvalidOperationException>(() => (Setter)b);
            }
        }

        private class _ShouldSerializeCast
        {
            public int Foo { get; set; }
        }

        private delegate bool ShouldSerializeConcreteEquivDelegate(_ShouldSerializeCast row);

        private delegate bool ShouldSerializeGenEquivDelegate<T>(T row);

        private delegate bool StaticShouldSerializeEquivDelegate();

        [Fact]
        public void ShouldSerializeCast()
        {
            var aCalled = 0;
            ShouldSerializeConcreteEquivDelegate a =
                row =>
                {
                    aCalled++;

                    return true;
                };
            var bCalled = 0;
            ShouldSerializeGenEquivDelegate<_ShouldSerializeCast> b =
                row =>
                {
                    bCalled++;

                    return true;
                };
            var cCalled = 0;
            StaticShouldSerializeDelegate c =
                () =>
                {
                    cCalled++;

                    return true;
                };
            var dCalled = 0;
            Func<_ShouldSerializeCast, bool> d =
                row =>
                {
                    dCalled++;
                    return true;
                };
            var eCalled = 0;
            Func<bool> e =
                () =>
                {
                    eCalled++;
                    return true;
                };
            var fCalled = 0;
            ShouldSerializeDelegate<_ShouldSerializeCast> f =
                row =>
                {
                    fCalled++;

                    return true;
                };
            var gCalled = 0;
            StaticShouldSerializeDelegate g =
                () =>
                {
                    gCalled++;
                    return true;
                };

            var aWrapped = (ShouldSerialize)a;
            var aRes1 = (bool)aWrapped.Delegate.Value.DynamicInvoke(new _ShouldSerializeCast());
            Assert.True(aRes1);
            Assert.Equal(1, aCalled);
            var aRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)aWrapped.Delegate.Value)(new _ShouldSerializeCast());
            Assert.Equal(2, aCalled);
            Assert.True(aRes2);

            var bWrapped = (ShouldSerialize)b;
            var bRes1 = (bool)bWrapped.Delegate.Value.DynamicInvoke(new _ShouldSerializeCast());
            Assert.True(bRes1);
            Assert.Equal(1, bCalled);
            var bRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)bWrapped.Delegate.Value)(new _ShouldSerializeCast());
            Assert.Equal(2, bCalled);
            Assert.True(bRes2);

            var cWrapped = (ShouldSerialize)c;
            var cRes1 = (bool)cWrapped.Delegate.Value.DynamicInvoke();
            Assert.True(cRes1);
            Assert.Equal(1, cCalled);
            var cRes2 = ((StaticShouldSerializeDelegate)cWrapped.Delegate.Value)();
            Assert.Equal(2, cCalled);
            Assert.True(cRes2);

            var dWrapped = (ShouldSerialize)d;
            var dRes1 = (bool)dWrapped.Delegate.Value.DynamicInvoke(new _ShouldSerializeCast());
            Assert.True(dRes1);
            Assert.Equal(1, dCalled);
            var dRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)dWrapped.Delegate.Value)(new _ShouldSerializeCast());
            Assert.Equal(2, dCalled);
            Assert.True(dRes2);

            var eWrapped = (ShouldSerialize)e;
            var eRes1 = (bool)eWrapped.Delegate.Value.DynamicInvoke();
            Assert.True(eRes1);
            Assert.Equal(1, eCalled);
            var eRes2 = ((StaticShouldSerializeDelegate)eWrapped.Delegate.Value)();
            Assert.Equal(2, eCalled);
            Assert.True(eRes2);

            var fWrapped = (ShouldSerialize)f;
            Assert.Same(f, fWrapped.Delegate.Value);
            var fRes1 = (bool)fWrapped.Delegate.Value.DynamicInvoke(new _ShouldSerializeCast());
            Assert.True(fRes1);
            Assert.Equal(1, fCalled);
            var fRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)fWrapped.Delegate.Value)(new _ShouldSerializeCast());
            Assert.Equal(2, fCalled);
            Assert.True(fRes2);

            var gWrapped = (ShouldSerialize)g;
            Assert.Same(g, gWrapped.Delegate.Value);
            var gRes1 = (bool)gWrapped.Delegate.Value.DynamicInvoke();
            Assert.True(gRes1);
            Assert.Equal(1, gCalled);
            var gRes2 = ((StaticShouldSerializeDelegate)gWrapped.Delegate.Value)();
            Assert.Equal(2, gCalled);
            Assert.True(gRes2);
        }

        private bool _BadArgsShouldSerialize(int a) { return true; }
        private void _BadReturnShouldSerialize() { }

        [Fact]
        public void ShouldSerializes()
        {
            // should serialize
            var methodShouldSerialize = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerialize)));
            var staticMethodShouldSerialize = ShouldSerialize.ForMethod(typeof(_ColumnWriters).GetMethod(nameof(_ColumnWriters.ShouldSerializeStatic)));
            var delShouldSerialize = (ShouldSerialize)(ShouldSerializeDelegate<_ColumnWriters>)((_ColumnWriters _) => { return true; });
            var staticDelShouldSerialize = (ShouldSerialize)(StaticShouldSerializeDelegate)(() => { return true; });
            var shouldSerializes = new[] { methodShouldSerialize, staticMethodShouldSerialize, delShouldSerialize, staticDelShouldSerialize };

            var notShouldSerialize = "";

            for (var i = 0; i < shouldSerializes.Length; i++)
            {
                var s1 = shouldSerializes[i];
                Assert.NotNull(s1.ToString());
                Assert.False(s1.Equals(notShouldSerialize));

                if (s1 == methodShouldSerialize)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.False(s1.IsStatic);
                }
                else if (s1 == methodShouldSerialize)
                {
                    Assert.Equal(BackingMode.Method, s1.Mode);
                    Assert.True(s1.IsStatic);
                }
                else if (s1 == delShouldSerialize)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.False(s1.IsStatic);
                }
                else if (s1 == staticDelShouldSerialize)
                {
                    Assert.Equal(BackingMode.Delegate, s1.Mode);
                    Assert.True(s1.IsStatic);
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
            }
        }

        private class _DynamicRowConverters
        {
            public static int FooStatic { get; set; }

            public int Foo { get; set; }

            public _DynamicRowConverters() { }
            public _DynamicRowConverters(string foo) { }

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

            var setter = Setter.ForMethod(typeof(_DynamicRowConverters).GetProperty(nameof(_DynamicRowConverters.Foo)).SetMethod);
            Assert.NotNull(setter);

            var staticSetter = Setter.ForMethod(typeof(_DynamicRowConverters).GetProperty(nameof(_DynamicRowConverters.FooStatic)).SetMethod);
            Assert.NotNull(staticSetter);

            // dynamic row converters
            var cons1Converter = DynamicRowConverter.ForConstructorTakingDynamic(typeof(_DynamicRowConverters).GetConstructor(new[] { typeof(object) }));
            var consParamsConverter1 = DynamicRowConverter.ForConstructorTakingTypedParameters(stringCons, new[] { Cesil.ColumnIdentifier.Create(1) });
            var consParamsConverter2 = DynamicRowConverter.ForConstructorTakingTypedParameters(stringCons, new[] { Cesil.ColumnIdentifier.Create(2) });
            var delConverter = DynamicRowConverter.ForDelegate((object row, in ReadContext ctx, out _DynamicRowConverters res) => { res = null; return true; });
            var cons0Converter1 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { setter }, new[] { Cesil.ColumnIdentifier.Create(1) });
            var cons0Converter2 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { staticSetter }, new[] { Cesil.ColumnIdentifier.Create(1) });
            var cons0Converter3 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { setter, staticSetter }, new[] { Cesil.ColumnIdentifier.Create(1), Cesil.ColumnIdentifier.Create(2) });
            var cons0Converter4 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { setter }, new[] { Cesil.ColumnIdentifier.Create(2) });
            var cons0Converter5 = DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { staticSetter, setter }, new[] { Cesil.ColumnIdentifier.Create(1), Cesil.ColumnIdentifier.Create(2) });
            var mtdConverter = DynamicRowConverter.ForMethod(typeof(WrapperTests).GetMethod(nameof(_DynamicRowConverters_Mtd), BindingFlags.Static | BindingFlags.NonPublic));

            var converters = new[] { cons1Converter, consParamsConverter1, consParamsConverter2, delConverter, cons0Converter1, cons0Converter2, cons0Converter3, cons0Converter4, cons0Converter5, mtdConverter };
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

                var badSetter = Setter.ForDelegate<string, string>(delegate { });
                Assert.Throws<ArgumentException>(() => DynamicRowConverter.ForEmptyConstructorAndSetters(emptyCons, new[] { badSetter }, ixs));

                var badIxs = new[] { Cesil.ColumnIdentifier.CreateInner(-1, "foo") };
                var goodSetter = Setter.ForDelegate<_DynamicRowConverters, int>((r, v) => r.Foo = v);
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
        }

        private class _InstanceBuilderCast
        {
            public int Foo { get; set; }
        }

        private delegate bool InstanceBuilderConcreteEquivalentDelegate(out _InstanceBuilderCast res);

        private delegate bool InstanceBuilderGenericEquivalentDelegate<T>(out T res);

        [Fact]
        public void InstanceBuilderCast()
        {
            var aCalled = 0;
            InstanceBuilderConcreteEquivalentDelegate a =
                (out _InstanceBuilderCast res) =>
                {
                    aCalled++;

                    res = new _InstanceBuilderCast();
                    return true;
                };
            var bCalled = 0;
            InstanceBuilderGenericEquivalentDelegate<_InstanceBuilderCast> b =
                (out _InstanceBuilderCast res) =>
                {
                    bCalled++;

                    res = new _InstanceBuilderCast();
                    return true;
                };
            var cCalled = 0;
            InstanceProviderDelegate<_InstanceBuilderCast> c =
                (out _InstanceBuilderCast res) =>
                {
                    cCalled++;

                    res = new _InstanceBuilderCast();
                    return true;
                };

            var aWrapped = (InstanceProvider)a;
            var aRes = ((InstanceProviderDelegate<_InstanceBuilderCast>)aWrapped.Delegate.Value)(out var aOut);
            Assert.True(aRes);
            Assert.NotNull(aOut);
            Assert.Equal(1, aCalled);

            var bWrapped = (InstanceProvider)b;
            var bRes = ((InstanceProviderDelegate<_InstanceBuilderCast>)bWrapped.Delegate.Value)(out var bOut);
            Assert.True(bRes);
            Assert.NotNull(bOut);
            Assert.Equal(1, bCalled);

            var cWrapped = (InstanceProvider)c;
            var cRes = ((InstanceProviderDelegate<_InstanceBuilderCast>)cWrapped.Delegate.Value)(out var cOut);
            Assert.True(cRes);
            Assert.NotNull(cOut);
            Assert.Same(c, cWrapped.Delegate.Value);
            Assert.Equal(1, cCalled);
        }

        private class _InstanceBuilders
        {
            public _InstanceBuilders() { }

            public _InstanceBuilders(int a) { }
        }

        private static bool _InstanceBuilderStaticMethod(out _InstanceBuilders val) { val = new _InstanceBuilders(); return true; }

        private bool _NonStaticBuilder(out _InstanceBuilders val) { val = new _InstanceBuilders(); return true; }
        private static void _BadReturnBuilder(out _InstanceBuilders val) { val = new _InstanceBuilders(); }
        private static bool _BadArgs1Builder(int a, int b) { return true; }
        private static bool _BadArgs2Builder(_InstanceBuilders val) { return true; }

        private abstract class _InstanceBuilders_Abstract
        {
            public _InstanceBuilders_Abstract() { }
        }

        private class _InstanceBuilders_Generic<T>
        {
            public _InstanceBuilders_Generic() { }
        }

        [Fact]
        public void InstanceBuilders()
        {
            var methodBuilder = InstanceProvider.ForMethod(typeof(WrapperTests).GetMethod(nameof(_InstanceBuilderStaticMethod), BindingFlags.NonPublic | BindingFlags.Static));
            var constructorBuilder = InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders).GetConstructor(Type.EmptyTypes));
            var delBuilder = InstanceProvider.ForDelegate<_InstanceBuilders>((out _InstanceBuilders a) => { a = new _InstanceBuilders(); return true; });
            var builders = new[] { methodBuilder, constructorBuilder, delBuilder };

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
            }

            // ForParameterlessConstructor errors
            {
                Assert.Throws<ArgumentNullException>(() => InstanceProvider.ForParameterlessConstructor(null));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders).GetConstructor(new[] { typeof(int) })));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders_Abstract).GetConstructor(Type.EmptyTypes)));
                Assert.Throws<ArgumentException>(() => InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders_Generic<>).GetConstructor(Type.EmptyTypes)));
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
                Func<_InstanceBuilders, bool> c = (_) => false;
                Assert.Throws<InvalidOperationException>(() => (InstanceProvider)c);
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
        private static bool _Equatable_Getter(out int b)
        {
            b = 123;
            return true;
        }

        private static readonly MethodInfo _Equatable_InstanceBuilder_Mtd = typeof(WrapperTests).GetMethod(nameof(_Equatable_InstanceBuilder), BindingFlags.NonPublic | BindingFlags.Static);
        private static bool _Equatable_InstanceBuilder(out _InstanceBuilderCast a)
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
                    (row) =>
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
                    (out _InstanceBuilderCast res) =>
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
                   row =>
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
                    (row, val) =>
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
                   row =>
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
            var ci1 = Cesil.ColumnIdentifier.Create(0, "A");
            var ci2 = Cesil.ColumnIdentifier.Create(1);
            var ci3 = Cesil.ColumnIdentifier.Create(2, "A");

            var neq = ci1 != ci2;

            var notCi = "hello";

            Assert.True(neq);
            Assert.True(ci1.Equals(ci1));
            Assert.True(ci1.Equals((object)ci1));
            Assert.False(ci1.Equals(ci2));
            Assert.False(ci1.Equals((object)ci2));
            Assert.False(ci1.Equals(ci3));
            Assert.False(ci1.Equals(notCi));

            Assert.True(CompareHash(ci1, ci1));
            Assert.False(CompareHash(ci1, ci2));

            Assert.Equal(0, (int)ci1);

            Assert.Throws<InvalidOperationException>(() => ci2.Name);

            Assert.Throws<ArgumentException>(() => Cesil.ColumnIdentifier.Create(-1));

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
            var names = new[] { "A", "B" };
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
