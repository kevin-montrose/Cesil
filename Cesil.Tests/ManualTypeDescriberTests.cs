using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cesil.Tests
{
    public class ManualTypeDescriberTests
    {
        [Fact]
        public void Equality()
        {
            var behaviors = new[] { ManualTypeDescriberFallbackBehavior.Throw, ManualTypeDescriberFallbackBehavior.UseFallback };
            var fallbacks = new[] { TypeDescribers.Default, ManualTypeDescriberBuilder.CreateBuilder().ToManualTypeDescriber() };
            var providers = new[] { InstanceProvider.ForDelegate((in ReadContext _, out string x) => { x = ""; return true; }), InstanceProvider.ForDelegate((in ReadContext _, out object o) => { o = new object(); return true; }) };
            var getters =
                new[]
                {
                    new[] { Getter.ForDelegate((in WriteContext _) => 0) },
                    new[] { Getter.ForDelegate((in WriteContext _) => "") },
                    new [] { Getter.ForDelegate((in WriteContext _) => 0), Getter.ForDelegate((in WriteContext _) => "") }
                };
            var setters =
                new[] {
                    new [] {Setter.ForDelegate((int i, in ReadContext _) => { }) },
                    new [] { Setter.ForDelegate((string s, in ReadContext _) => { }) },
                    new [] { Setter.ForDelegate((int i, in ReadContext _) => { }) , Setter.ForDelegate((string s, in ReadContext _) => { }) }
                };

            var manuals = new List<ManualTypeDescriber>();

            foreach (var a in behaviors)
            {
                foreach (var b in fallbacks)
                {
                    foreach (var c in providers)
                    {
                        foreach (var d in getters)
                        {
                            foreach (var e in setters)
                            {
                                var x = ManualTypeDescriberBuilder.CreateBuilder(a, b);
                                x.WithInstanceProvider(c);

                                for (var i = 0; i < d.Length; i++)
                                {
                                    x.WithExplicitGetter(typeof(string).GetTypeInfo(), i.ToString(), d[i]);
                                }
                                for (var i = 0; i < e.Length; i++)
                                {
                                    x.WithExplicitSetter(typeof(string).GetTypeInfo(), i.ToString(), e[i]);
                                }

                                manuals.Add(x.ToManualTypeDescriber());
                            }
                        }
                    }
                }
            }

            for (var i = 0; i < manuals.Count; i++)
            {
                var m1 = manuals[i];
                for (var j = i; j < manuals.Count; j++)
                {
                    var m2 = manuals[j];

                    var eq = m1 == m2;
                    var eqObj = m1.Equals((object)m2);
                    var neq = m1 != m2;
                    var hashEqual = m1.GetHashCode() == m2.GetHashCode();

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.True(eqObj);
                        Assert.False(neq);
                        Assert.True(hashEqual);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.False(eqObj);
                        Assert.True(neq);
                    }
                }
            }
        }

        [Fact]
        public void NotSupported()
        {
            var m = ManualTypeDescriber.CreateBuilder().ToManualTypeDescriber();
            Assert.Throws<NotSupportedException>(() => m.GetCellsForDynamicRow(default, null, default));
            Assert.Throws<NotSupportedException>(() => m.GetDynamicCellParserFor(default, null));
            Assert.Throws<NotSupportedException>(() => m.GetDynamicRowConverter(default, null, null));
        }

        [Fact]
        public void ConstructionErrors()
        {
            Assert.Throws<ArgumentException>(() => ManualTypeDescriber.CreateBuilder((ManualTypeDescriberFallbackBehavior)0));
            Assert.Throws<ArgumentException>(() => ManualTypeDescriber.CreateBuilder((ManualTypeDescriberFallbackBehavior)0, TypeDescribers.Default));
            Assert.Throws<ArgumentNullException>(() => ManualTypeDescriber.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw, null));
            Assert.Throws<ArgumentNullException>(() => ManualTypeDescriber.CreateBuilder(null));
        }

        private sealed class _Copy
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void Copy()
        {
            var m1 = ManualTypeDescriberBuilder.CreateBuilder();

            var t = typeof(_Copy).GetTypeInfo();
            var g = Getter.ForMethod(t.GetProperty(nameof(_Copy.Foo)).GetMethod);
            m1.WithExplicitGetter(t, "Nope", g);

            var ip = InstanceProvider.ForParameterlessConstructor(t.GetConstructor(Type.EmptyTypes));
            m1.WithInstanceProvider(ip);

            var s = Setter.ForMethod(t.GetProperty(nameof(_Copy.Foo)).SetMethod);
            m1.WithExplicitSetter(t, "Nope", s);

            var t1 = m1.ToManualTypeDescriber();

            var m2 = ManualTypeDescriberBuilder.CreateBuilder(t1);
            var t2 = m2.ToManualTypeDescriber();

            Assert.Equal(t1, t2);
        }

        [Fact]
        public void Setters()
        {
            // FallbackBehavior
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw);
                Assert.Equal(ManualTypeDescriberFallbackBehavior.Throw, m.FallbackBehavior);
                m.WithFallbackBehavior(ManualTypeDescriberFallbackBehavior.UseFallback);
                Assert.Equal(ManualTypeDescriberFallbackBehavior.UseFallback, m.FallbackBehavior);
            }

            // FallbackBehavior, exception
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw);
                Assert.Throws<ArgumentException>(() => m.WithFallbackBehavior(0));
            }

            // FallbackTypeDescriber
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw, TypeDescribers.Default);
                Assert.Same(TypeDescribers.Default, m.FallbackTypeDescriber);

                var s = SurrogateTypeDescriber.CreateBuilder().ToSurrogateTypeDescriber();
                m.WithFallbackTypeDescriber(s);
                Assert.Same(s, m.FallbackTypeDescriber);
            }

            // FallbackTypeDescriber, exception
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw, TypeDescribers.Default);
                Assert.Throws<ArgumentNullException>(() => m.WithFallbackTypeDescriber(null));
            }

            // SetInstanceProvider, duplicate
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder();
                m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out string str) => { str = ""; return true; }));

                Assert.Throws<InvalidOperationException>(() => m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out string str) => { str = ""; return true; })));
            }
        }

        private class _Serializing
        {
#pragma warning disable CS0649
            public string Field;
            public string Prop { get; set; }
        }

        [Fact]
        public void Serializing()
        {
            // SetBuilder
            {

                // 1 arg
                {
                    var m = ManualTypeDescriber.CreateBuilder();

                    m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out _Serializing val) => { val = new _Serializing(); return true; }));
                }


                // 2 arg
                {
                    var m = ManualTypeDescriber.CreateBuilder();

                    m.WithInstanceProvider(typeof(_Serializing).GetTypeInfo(), InstanceProvider.ForDelegate((in ReadContext _, out _Serializing val) => { val = new _Serializing(); return true; }));
                }
            }

            var t = typeof(_Serializing).GetTypeInfo();
            var prop = t.GetProperty(nameof(_Serializing.Prop));
            Assert.NotNull(prop);
            var g = Getter.ForMethod(prop.GetMethod);
            Assert.NotNull(g);
            var f = Formatter.GetDefault(typeof(string).GetTypeInfo());
            Assert.NotNull(f);
            var s = ShouldSerialize.ForDelegate((in WriteContext _) => true);
            Assert.NotNull(s);

            // AddExplicitGetter
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 3 arg
                m.WithExplicitGetter(t, "foo", g);

                // 4 arg
                m.WithExplicitGetter(t, "foo", g, f);

                // 5 arg 
                m.WithExplicitGetter(t, "foo", g, f, s);

                // 6 arg
                m.WithExplicitGetter(t, "foo", g, f, s, EmitDefaultValue.Yes);
            }

            var field = t.GetField(nameof(_Serializing.Field));
            Assert.NotNull(field);

            // AddSerializableField
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 1 arg
                m.WithSerializableField(field);

                // 2 arg
                m.WithSerializableField(field, "foo");
                m.WithSerializableField(t, field);

                // 3 arg
                m.WithSerializableField(field, "foo", f);
                m.WithSerializableField(t, field, "foo");

                // 4 arg
                m.WithSerializableField(field, "foo", f, s);
                m.WithSerializableField(t, field, "foo", f, s);

                // 5 arg
                m.WithSerializableField(field, "foo", f, s, EmitDefaultValue.Yes);
                m.WithSerializableField(t, field, "foo", f, s);

                // 6 arg
                m.WithSerializableField(t, field, "foo", f, s, EmitDefaultValue.Yes);
            }

            // AddSerializableProperty
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 1 arg
                m.WithSerializableProperty(prop);

                // 2 arg
                m.WithSerializableProperty(prop, "foo");
                m.WithSerializableProperty(t, prop);

                // 3 arg
                m.WithSerializableProperty(prop, "foo", f);
                m.WithSerializableProperty(t, prop, "foo");

                // 4 arg
                m.WithSerializableProperty(prop, "foo", f, s);
                m.WithSerializableProperty(t, prop, "foo", f, s);

                // 5 arg
                m.WithSerializableProperty(prop, "foo", f, s, EmitDefaultValue.Yes);
                m.WithSerializableProperty(t, prop, "foo", f, s);

                // 6 arg
                m.WithSerializableProperty(t, prop, "foo", f, s, EmitDefaultValue.Yes);
            }
        }

        private class _Deserializing
        {
#pragma warning disable CS0649
            public double Field;
#pragma warning restore CS0649
            public double Prop { get; set; }
        }

        [Fact]
        public void Deserializing()
        {
            var t = typeof(_Deserializing).GetTypeInfo();
            var field = t.GetField(nameof(_Deserializing.Field));
            Assert.NotNull(field);
            var prop = t.GetProperty(nameof(_Deserializing.Prop));
            Assert.NotNull(prop);

            var s = Setter.ForField(field);
            Assert.NotNull(s);

            var p = Parser.GetDefault(typeof(double).GetTypeInfo());
            Assert.NotNull(p);

            var r = Reset.ForDelegate((in ReadContext _) => { });
            Assert.NotNull(r);

            // AddExplicitSetter
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 3 arg
                m.WithExplicitSetter(t, "foo", s);

                // 4 arg
                m.WithExplicitSetter(t, "foo", s, p);

                // 5 arg
                m.WithExplicitSetter(t, "foo", s, p, MemberRequired.Yes);

                // 6 arg
                m.WithExplicitSetter(t, "foo", s, p, MemberRequired.Yes, r);
            }

            // AddDeserializableField
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 1 arg
                m.WithDeserializableField(field);

                // 2 arg
                m.WithDeserializableField(field, "foo");
                m.WithDeserializableField(t, field);

                // 3 arg
                m.WithDeserializableField(field, "foo", p);
                m.WithDeserializableField(t, field, "foo");

                // 4 arg
                m.WithDeserializableField(field, "foo", p, MemberRequired.Yes);
                m.WithDeserializableField(t, field, "foo", p);

                // 5 arg
                m.WithDeserializableField(field, "foo", p, MemberRequired.Yes, r);
                m.WithDeserializableField(t, field, "foo", p, MemberRequired.Yes);

                // 6 arg
                m.WithDeserializableField(t, field, "foo", p, MemberRequired.Yes, r);
            }
        }

        private class _Errors
        {
#pragma warning disable CS0649
            public int Field;
#pragma warning restore CS0649
            public int Property { get; set; }
        }

        [Fact]
        public void SerializingErrors()
        {
            // SetBuilder
            {
                var m = ManualTypeDescriber.CreateBuilder();
                Assert.Throws<ArgumentNullException>(() => m.WithInstanceProvider(null));
                Assert.Throws<ArgumentNullException>(() => m.WithInstanceProvider(null, InstanceProvider.ForDelegate((in ReadContext _, out string val) => { val = ""; return true; })));
                Assert.Throws<ArgumentNullException>(() => m.WithInstanceProvider(typeof(string).GetTypeInfo(), null));
                Assert.Throws<InvalidOperationException>(() => m.WithInstanceProvider(typeof(int).GetTypeInfo(), InstanceProvider.ForDelegate((in ReadContext _, out string val) => { val = ""; return true; })));
            }

            // EnumerateMembersToSerialize
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw).ToManualTypeDescriber();

                // null
                Assert.Throws<ArgumentNullException>(() => m.EnumerateMembersToSerialize(null));

                // nothing registered
                Assert.Throws<InvalidOperationException>(() => m.EnumerateMembersToSerialize(typeof(object).GetTypeInfo()));
            }

            // AddExplicitGetter
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 3 arg version
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(null, "foo", Getter.ForDelegate((in WriteContext _) => 0)));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), null, Getter.ForDelegate((in WriteContext _) => 0)));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), "foo", null));

                // 4 arg version
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(null, "foo", Getter.ForDelegate((in WriteContext _) => 0), Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), null, Getter.ForDelegate((in WriteContext _) => 0), Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), "foo", null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate((in WriteContext _) => 0), null));

                // 5 arg version
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(null, "foo", Getter.ForDelegate((in WriteContext _) => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), null, Getter.ForDelegate((in WriteContext _) => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), "foo", null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate((in WriteContext _) => 0), null, ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate((in WriteContext _) => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                // 6 arg version
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(null, "foo", Getter.ForDelegate((in WriteContext _) => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), null, Getter.ForDelegate((in WriteContext _) => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), "foo", null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate((in WriteContext _) => 0), null, ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate((in WriteContext _) => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null
            }

            // AddSerializableField
            {
                var m = ManualTypeDescriber.CreateBuilder();
                var f = typeof(_Errors).GetField(nameof(_Errors.Field));

                Assert.NotNull(f);

                // 1 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null));

                // 2 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(f, null));

                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null, f));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), null));

                // 3 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(f, null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(f, "foo", null));

                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null, f, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), f, null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(f, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(f, "foo", null, ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null, f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), f, null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(f, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(f, "foo", null, ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null

                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null, f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), f, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null, ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(null, f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), f, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null, ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null
            }

            // AddSerializableProperty
            {
                var m = ManualTypeDescriber.CreateBuilder();
                var p = typeof(_Errors).GetProperty(nameof(_Errors.Property));

                Assert.NotNull(p);

                // 1 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null));

                // 2 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(p, null));

                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null, p));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), null));

                // 3 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(p, null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(p, "foo", null));

                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null, p, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), p, null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(p, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(p, "foo", null, ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null, p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(p, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(p, "foo", null, ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null

                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null, p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null, ShouldSerialize.ForDelegate((in WriteContext _) => true)));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(null, p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null, ShouldSerialize.ForDelegate((in WriteContext _) => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null
            }

            // AddSerializableMember (private)
            {
                var m = ManualTypeDescriber.CreateBuilder();
                var p = typeof(_Errors).GetProperty(nameof(_Errors.Property));
                var g = Getter.ForMethod(p.GetMethod);

                // on illegal class
                Assert.Throws<InvalidOperationException>(() => m.WithSerializableMember(typeof(int).GetTypeInfo(), g, "Foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
            }
        }

        private class _SerializeBaseClass
        {
#pragma warning disable CS0649
            public int Foo;
#pragma warning disable CS0649
        }

        private sealed class _SerializeBaseClass_Sub : _SerializeBaseClass
        {

        }


        [Fact]
        public void SerializeBaseClass()
        {
            var m = ManualTypeDescriber.CreateBuilder();
            var field = typeof(_SerializeBaseClass).GetField(nameof(_SerializeBaseClass.Foo));
            var getter = Getter.ForField(field);

            m.WithExplicitGetter(typeof(_SerializeBaseClass_Sub).GetTypeInfo(), "Hello", getter);

            var mx = m.ToManualTypeDescriber();

            var mems = mx.EnumerateMembersToSerialize(typeof(_SerializeBaseClass_Sub).GetTypeInfo()).ToList();
            Assert.Collection(
                mems,
                mem =>
                {
                    Assert.Equal("Hello", mem.Name);
                    Assert.Equal(getter, mem.Getter);
                }
            );
        }

        [Fact]
        public void DeserializeErrors()
        {
            // GetInstanceProvider
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw).ToManualTypeDescriber();

                // null
                Assert.Throws<ArgumentNullException>(() => m.GetInstanceProvider(null));

                // nothing registered
                Assert.Throws<InvalidOperationException>(() => m.GetInstanceProvider(typeof(object).GetTypeInfo()));
            }

            // EnumerateMembersToDeserialize
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw).ToManualTypeDescriber();

                // null
                Assert.Throws<ArgumentNullException>(() => m.EnumerateMembersToDeserialize(null));

                // nothing registered
                Assert.Throws<InvalidOperationException>(() => m.EnumerateMembersToDeserialize(typeof(object).GetTypeInfo()));
            }

            // AddExplicitSetter
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder();

                // 3 arg
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(null, "foo", Setter.ForDelegate<int>(delegate { })));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), null, Setter.ForDelegate<int>(delegate { })));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(null, "foo", Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), null, Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", Setter.ForDelegate<int>(delegate { }), null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(null, "foo", Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), null, Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", Setter.ForDelegate<int>(delegate { }), null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(null, "foo", Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), null, Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", Setter.ForDelegate<int>(delegate { }), null, MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                // IsMemberRequired cannot be null
                // reset can be null
                Assert.Throws<ArgumentNullException>(() => m.WithExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));
            }

            // AddDeserializableField
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder();
                var f = typeof(_Errors).GetField(nameof(_Errors.Field));

                Assert.NotNull(f);

                // 1 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null));

                // 2 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(f, null));

                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null, f));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), null));

                // 3 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(f, null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(f, "foo", null));

                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null, f, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), f, null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(f, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(f, "foo", null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null, f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), f, null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(f, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(f, "foo", null, MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                // IsMemberRequired cannot be null
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));

                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null, f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), f, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(null, f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), f, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null, MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                // IsMemberRequired cannot be null
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableField(typeof(_Errors).GetTypeInfo(), f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));
            }

            // AddDeserializableProperty
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder();
                var p = typeof(_Errors).GetProperty(nameof(_Errors.Property));

                Assert.NotNull(p);

                // 1 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null));

                // 2 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(p, null));

                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null, p));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), null));

                // 3 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(p, null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(p, "foo", null));

                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null, p, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(p, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(p, "foo", null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null, p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(p, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(p, "foo", null, MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                // IsMemberRequired cannot be null
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));

                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null, p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(null, p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null, MemberRequired.Yes, Reset.ForDelegate((in ReadContext _) => { })));
                // IsMemberRequired cannot be null
                Assert.Throws<ArgumentNullException>(() => m.WithDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));
            }
        }

        private class _ToStringOverride
        {
            public int Bar { get; set; }
        }

        [Fact]
        public void ToStringOverride()
        {
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw);

                m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out string foo) => { foo = ""; return true; }));
                m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out int foo) => { foo = 10; return true; }));

                m.WithDeserializableField(typeof(_Errors).GetField(nameof(_Errors.Field)));
                m.WithDeserializableProperty(typeof(_Errors).GetProperty(nameof(_Errors.Property)));
                m.WithDeserializableProperty(typeof(_ToStringOverride).GetProperty(nameof(_ToStringOverride.Bar)));

                m.WithSerializableField(typeof(_Errors).GetField(nameof(_Errors.Field)));
                m.WithSerializableProperty(typeof(_Errors).GetProperty(nameof(_Errors.Property)));
                m.WithSerializableProperty(typeof(_ToStringOverride).GetProperty(nameof(_ToStringOverride.Bar)));

                Assert.NotNull(m.ToString());
            }

            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);

                m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out string foo) => { foo = ""; return true; }));
                m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out int foo) => { foo = 10; return true; }));

                m.WithDeserializableField(typeof(_Errors).GetField(nameof(_Errors.Field)));
                m.WithDeserializableProperty(typeof(_Errors).GetProperty(nameof(_Errors.Property)));
                m.WithDeserializableProperty(typeof(_ToStringOverride).GetProperty(nameof(_ToStringOverride.Bar)));

                m.WithSerializableField(typeof(_Errors).GetField(nameof(_Errors.Field)));
                m.WithSerializableProperty(typeof(_Errors).GetProperty(nameof(_Errors.Property)));
                m.WithSerializableProperty(typeof(_ToStringOverride).GetProperty(nameof(_ToStringOverride.Bar)));

                Assert.NotNull(m.ToString());
            }
        }
    }
}
