using System;
using System.Buffers;
using System.Reflection;
using Xunit;

namespace Cesil.Tests
{
    public class WrapperTests
    {
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
            aWrapped.Delegate.DynamicInvoke(1, default(WriteContext), null);
            Assert.Equal(1, aCalled);
            ((FormatterDelegate<int>)aWrapped.Delegate)(2, default, null);
            Assert.Equal(2, aCalled);

            var bWrapped = (Formatter)b;
            bWrapped.Delegate.DynamicInvoke(null, default(WriteContext), null);
            Assert.Equal(1, bCalled);
            ((FormatterDelegate<string>)bWrapped.Delegate)(null, default, null);
            Assert.Equal(2, bCalled);

            var cWrapped = (Formatter)c;
            cWrapped.Delegate.DynamicInvoke(2.0, default(WriteContext), null);
            Assert.Equal(1, cCalled);
            Assert.Same(c, cWrapped.Delegate);
            ((FormatterDelegate<double>)cWrapped.Delegate)(3.0, default, null);
            Assert.Equal(2, cCalled);
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
            var aRes1 = (int)aWrapped.Delegate.DynamicInvoke(new _GetterCast { Foo = "yo" });
            Assert.Equal(2, aRes1);
            Assert.Equal(1, aCalled);
            var aRes2 = ((GetterDelegate<_GetterCast, int>)aWrapped.Delegate)(new _GetterCast { Foo = "yolo" });
            Assert.Equal(4, aRes2);
            Assert.Equal(2, aCalled);

            var bWrapped = (Getter)b;
            var bRes1 = (int)bWrapped.Delegate.DynamicInvoke();
            Assert.Equal(3, bRes1);
            Assert.Equal(1, bCalled);
            var bRes2 = ((StaticGetterDelegate<int>)bWrapped.Delegate)();
            Assert.Equal(3, bRes2);
            Assert.Equal(2, bCalled);

            var cWrapped = (Getter)c;
            var cRes1 = (double)cWrapped.Delegate.DynamicInvoke(new _GetterCast { Foo = "yo" });
            Assert.Equal(4.0, cRes1);
            Assert.Equal(1, cCalled);
            var cRes2 = ((GetterDelegate<_GetterCast, double>)cWrapped.Delegate)(new _GetterCast { Foo = "yolo" });
            Assert.Equal(8.0, cRes2);
            Assert.Equal(2, cCalled);

            var dWrapped = (Getter)d;
            var dRes1 = (Guid)dWrapped.Delegate.DynamicInvoke();
            Assert.Equal(dRet, dRes1);
            Assert.Equal(1, dCalled);
            var dRes2 = ((StaticGetterDelegate<Guid>)dWrapped.Delegate)();
            Assert.Equal(dRet, dRes2);
            Assert.Equal(2, dCalled);

            var eWrapped = (Getter)e;
            var eRes1 = (char)eWrapped.Delegate.DynamicInvoke(new _GetterCast { Foo = "yo" });
            Assert.Equal('y', eRes1);
            Assert.Equal(1, eCalled);
            var eRes2 = ((GetterDelegate<_GetterCast, char>)eWrapped.Delegate)(new _GetterCast { Foo = "hello" });
            Assert.Equal('h', eRes2);
            Assert.Equal(2, eCalled);

            var fWrapped = (Getter)f;
            var fRes1 = (sbyte)fWrapped.Delegate.DynamicInvoke();
            Assert.Equal(-127, fRes1);
            Assert.Equal(1, fCalled);
            var fRes2 = ((StaticGetterDelegate<sbyte>)fWrapped.Delegate)();
            Assert.Equal(-127, fRes2);
            Assert.Equal(2, fCalled);

            var gWrapped = (Getter)g;
            var gRes1 = (int)gWrapped.Delegate.DynamicInvoke(new _GetterCast { Foo = "yo" });
            Assert.Equal(3, gRes1);
            Assert.Equal(1, gCalled);
            var gRes2 = ((GetterDelegate<_GetterCast, int>)gWrapped.Delegate)(new _GetterCast { Foo = "yolo" });
            Assert.Equal(5, gRes2);
            Assert.Same(g, gWrapped.Delegate);
            Assert.Equal(2, gCalled);

            var hWrapped = (Getter)h;
            var hRes1 = (int)hWrapped.Delegate.DynamicInvoke();
            Assert.Equal(456, hRes1);
            Assert.Equal(1, hCalled);
            var hRes2 = ((StaticGetterDelegate<int>)hWrapped.Delegate)();
            Assert.Equal(456, hRes2);
            Assert.Same(h, hWrapped.Delegate);
            Assert.Equal(2, hCalled);
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
            var aRes = ((ParserDelegate<int>)aWrapped.Delegate)("hello".AsSpan(), default, out int aOut);
            Assert.True(aRes);
            Assert.Equal(1, aOut);
            Assert.Equal(1, aCalled);

            var bWrapped = (Parser)b;
            var bRes = ((ParserDelegate<string>)bWrapped.Delegate)("hello".AsSpan(), default, out string bOut);
            Assert.True(bRes);
            Assert.Equal("hello", bOut);
            Assert.Equal(1, bCalled);

            var cWrapped = (Parser)c;
            var cRes = ((ParserDelegate<int>)cWrapped.Delegate)("hello".AsSpan(), default, out int cOut);
            Assert.True(cRes);
            Assert.Equal(2, cOut);
            Assert.Same(c, cWrapped.Delegate);
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
            aWrapped.Delegate.DynamicInvoke(new _ResetCast());
            Assert.Equal(1, aCalled);
            ((ResetDelegate<_ResetCast>)aWrapped.Delegate)(new _ResetCast());
            Assert.Equal(2, aCalled);

            var bWrapped = (Reset)b;
            bWrapped.Delegate.DynamicInvoke(new _ResetCast());
            Assert.Equal(1, bCalled);
            ((ResetDelegate<_ResetCast>)bWrapped.Delegate)(new _ResetCast());
            Assert.Equal(2, bCalled);

            var cWrapped = (Reset)c;
            cWrapped.Delegate.DynamicInvoke();
            Assert.Equal(1, cCalled);
            ((StaticResetDelegate)cWrapped.Delegate)();
            Assert.Equal(2, cCalled);

            var dWrapped = (Reset)d;
            dWrapped.Delegate.DynamicInvoke(new _ResetCast());
            Assert.Equal(1, dCalled);
            ((ResetDelegate<_ResetCast>)dWrapped.Delegate)(new _ResetCast());
            Assert.Equal(2, dCalled);

            var eWrapped = (Reset)e;
            eWrapped.Delegate.DynamicInvoke();
            Assert.Equal(1, eCalled);
            ((StaticResetDelegate)eWrapped.Delegate)();
            Assert.Equal(2, eCalled);

            var fWrapped = (Reset)f;
            fWrapped.Delegate.DynamicInvoke(new _ResetCast());
            Assert.Equal(1, fCalled);
            Assert.Same(f, fWrapped.Delegate);
            ((ResetDelegate<_ResetCast>)fWrapped.Delegate)(new _ResetCast());
            Assert.Equal(2, fCalled);

            var gWrapped = (Reset)g;
            gWrapped.Delegate.DynamicInvoke();
            Assert.Equal(1, gCalled);
            Assert.Same(g, gWrapped.Delegate);
            ((StaticResetDelegate)gWrapped.Delegate)();
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
            aWrapped.Delegate.DynamicInvoke(new _SetterCast(), 123);
            Assert.Equal(1, aCalled);
            ((SetterDelegate<_SetterCast, int>)aWrapped.Delegate)(new _SetterCast(), 123);
            Assert.Equal(2, aCalled);

            var bWrapped = (Setter)b;
            bWrapped.Delegate.DynamicInvoke(123);
            Assert.Equal(1, bCalled);
            ((StaticSetterDelegate<int>)bWrapped.Delegate)(123);
            Assert.Equal(2, bCalled);

            var cWrapped = (Setter)c;
            cWrapped.Delegate.DynamicInvoke(new _SetterCast(), "123");
            Assert.Equal(1, cCalled);
            ((SetterDelegate<_SetterCast, string>)cWrapped.Delegate)(new _SetterCast(), "123");
            Assert.Equal(2, cCalled);

            var dWrapped = (Setter)d;
            dWrapped.Delegate.DynamicInvoke("123");
            Assert.Equal(1, dCalled);
            ((StaticSetterDelegate<string>)dWrapped.Delegate)("123");
            Assert.Equal(2, dCalled);

            var eWrapped = (Setter)e;
            eWrapped.Delegate.DynamicInvoke(new _SetterCast(), Guid.NewGuid());
            Assert.Equal(1, eCalled);
            ((SetterDelegate<_SetterCast, Guid>)eWrapped.Delegate)(new _SetterCast(), Guid.NewGuid());
            Assert.Equal(2, eCalled);

            var fWrapped = (Setter)f;
            fWrapped.Delegate.DynamicInvoke(Guid.NewGuid());
            Assert.Equal(1, fCalled);
            ((StaticSetterDelegate<Guid>)fWrapped.Delegate)(Guid.NewGuid());
            Assert.Equal(2, fCalled);

            var gWrapped = (Setter)g;
            gWrapped.Delegate.DynamicInvoke(new _SetterCast(), TimeSpan.FromMinutes(1));
            Assert.Equal(1, gCalled);
            ((SetterDelegate<_SetterCast, TimeSpan>)gWrapped.Delegate)(new _SetterCast(), TimeSpan.FromMinutes(1));
            Assert.Same(g, gWrapped.Delegate);
            Assert.Equal(2, gCalled);

            var hWrapped = (Setter)h;
            hWrapped.Delegate.DynamicInvoke(TimeSpan.FromMinutes(1));
            Assert.Equal(1, hCalled);
            ((StaticSetterDelegate<TimeSpan>)hWrapped.Delegate)(TimeSpan.FromMinutes(1));
            Assert.Same(h, hWrapped.Delegate);
            Assert.Equal(2, hCalled);
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
            var aRes1 = (bool)aWrapped.Delegate.DynamicInvoke(new _ShouldSerializeCast());
            Assert.True(aRes1);
            Assert.Equal(1, aCalled);
            var aRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)aWrapped.Delegate)(new _ShouldSerializeCast());
            Assert.Equal(2, aCalled);
            Assert.True(aRes2);

            var bWrapped = (ShouldSerialize)b;
            var bRes1 = (bool)bWrapped.Delegate.DynamicInvoke(new _ShouldSerializeCast());
            Assert.True(bRes1);
            Assert.Equal(1, bCalled);
            var bRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)bWrapped.Delegate)(new _ShouldSerializeCast());
            Assert.Equal(2, bCalled);
            Assert.True(bRes2);

            var cWrapped = (ShouldSerialize)c;
            var cRes1 = (bool)cWrapped.Delegate.DynamicInvoke();
            Assert.True(cRes1);
            Assert.Equal(1, cCalled);
            var cRes2 = ((StaticShouldSerializeDelegate)cWrapped.Delegate)();
            Assert.Equal(2, cCalled);
            Assert.True(cRes2);

            var dWrapped = (ShouldSerialize)d;
            var dRes1 = (bool)dWrapped.Delegate.DynamicInvoke(new _ShouldSerializeCast());
            Assert.True(dRes1);
            Assert.Equal(1, dCalled);
            var dRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)dWrapped.Delegate)(new _ShouldSerializeCast());
            Assert.Equal(2, dCalled);
            Assert.True(dRes2);

            var eWrapped = (ShouldSerialize)e;
            var eRes1 = (bool)eWrapped.Delegate.DynamicInvoke();
            Assert.True(eRes1);
            Assert.Equal(1, eCalled);
            var eRes2 = ((StaticShouldSerializeDelegate)eWrapped.Delegate)();
            Assert.Equal(2, eCalled);
            Assert.True(eRes2);

            var fWrapped = (ShouldSerialize)f;
            Assert.Same(f, fWrapped.Delegate);
            var fRes1 = (bool)fWrapped.Delegate.DynamicInvoke(new _ShouldSerializeCast());
            Assert.True(fRes1);
            Assert.Equal(1, fCalled);
            var fRes2 = ((ShouldSerializeDelegate<_ShouldSerializeCast>)fWrapped.Delegate)(new _ShouldSerializeCast());
            Assert.Equal(2, fCalled);
            Assert.True(fRes2);

            var gWrapped = (ShouldSerialize)g;
            Assert.Same(g, gWrapped.Delegate);
            var gRes1 = (bool)gWrapped.Delegate.DynamicInvoke();
            Assert.True(gRes1);
            Assert.Equal(1, gCalled);
            var gRes2 = ((StaticShouldSerializeDelegate)gWrapped.Delegate)();
            Assert.Equal(2, gCalled);
            Assert.True(gRes2);
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
            var aRes = ((DynamicRowConverterDelegate<_DynamicRowConverterCast>)aWrapped.Delegate)(null, default, out var aOut);
            Assert.True(aRes);
            Assert.NotNull(aOut);
            Assert.Equal(1, aCalled);

            var bWrapped = (DynamicRowConverter)b;
            var bRes = ((DynamicRowConverterDelegate<_DynamicRowConverterCast>)bWrapped.Delegate)(null, default, out var bOut);
            Assert.True(bRes);
            Assert.NotNull(bOut);
            Assert.Equal(1, bCalled);

            var cWrapped = (DynamicRowConverter)c;
            var cRes = ((DynamicRowConverterDelegate<_DynamicRowConverterCast>)cWrapped.Delegate)(null, default, out var cOut);
            Assert.True(cRes);
            Assert.NotNull(cOut);
            Assert.Equal(1, cCalled);
            Assert.Same(c, cWrapped.Delegate);
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
            InstanceBuilderDelegate<_InstanceBuilderCast> c =
                (out _InstanceBuilderCast res) =>
                {
                    cCalled++;

                    res = new _InstanceBuilderCast();
                    return true;
                };

            var aWrapped = (InstanceBuilder)a;
            var aRes = ((InstanceBuilderDelegate<_InstanceBuilderCast>)aWrapped.Delegate)(out var aOut);
            Assert.True(aRes);
            Assert.NotNull(aOut);
            Assert.Equal(1, aCalled);

            var bWrapped = (InstanceBuilder)b;
            var bRes = ((InstanceBuilderDelegate<_InstanceBuilderCast>)bWrapped.Delegate)(out var bOut);
            Assert.True(bRes);
            Assert.NotNull(bOut);
            Assert.Equal(1, bCalled);

            var cWrapped = (InstanceBuilder)c;
            var cRes = ((InstanceBuilderDelegate<_InstanceBuilderCast>)cWrapped.Delegate)(out var cOut);
            Assert.True(cRes);
            Assert.NotNull(cOut);
            Assert.Same(c, cWrapped.Delegate);
            Assert.Equal(1, cCalled);
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
                var a = (InstanceBuilder)aDel;
                var b = (InstanceBuilder)_Equatable_InstanceBuilder_Mtd;
                var c = a;
                var d = (InstanceBuilder)aDel;
                var e = (InstanceBuilder)_Equatable_InstanceBuilder_Mtd;
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

            Assert.True(ci1.Equals(ci1));
            Assert.False(ci1.Equals(ci2));

            Assert.True(CompareHash(ci1, ci1));
            Assert.False(CompareHash(ci1, ci2));

            Assert.Throws<InvalidOperationException>(() => ci2.Name);

            Assert.Throws<ArgumentException>(() => Cesil.ColumnIdentifier.Create(-1));

            static bool CompareHash<T>(T a, T b)
            {
                var code = a.GetHashCode() == b.GetHashCode();

                return code;
            }
        }

        [Fact]
        public void DynamicCellValue()
        {
            var dcv1 = Cesil.DynamicCellValue.Create("Foo", "Bar", Formatter.GetDefault(typeof(string).GetTypeInfo()));
            var dcv2 = dcv1;
            var dcv3 = Cesil.DynamicCellValue.Create("Foo", "Bar", Formatter.GetDefault(typeof(string).GetTypeInfo()));
            var dcv4 = Cesil.DynamicCellValue.Create("Foo", 123, Formatter.GetDefault(typeof(int).GetTypeInfo()));

            Assert.True(dcv1 == dcv2);
            Assert.True(CompareHash(dcv1, dcv2));

            Assert.True(dcv1 == dcv3);
            Assert.True(CompareHash(dcv1, dcv3));

            Assert.True(dcv1 != dcv4);
            Assert.False(CompareHash(dcv1, dcv4));

            Assert.Throws<ArgumentNullException>(() => Cesil.DynamicCellValue.Create("foo", "bar", null));
            Assert.Throws<ArgumentException>(() => Cesil.DynamicCellValue.Create("foo", "bar", Formatter.GetDefault(typeof(int).GetTypeInfo())));

            static bool CompareHash<T>(T a, T b)
            {
                var code = a.GetHashCode() == b.GetHashCode();

                return code;
            }
        }
    }
}
