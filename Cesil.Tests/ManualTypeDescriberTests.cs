using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cesil.Tests
{
    public class ManualTypeDescriberTests
    {
        [Fact]
        public void NotSupported()
        {
            var m = ManualTypeDescriber.CreateBuilder().ToManualTypeDescriber();
            Assert.Throws<NotSupportedException>(() => m.GetCellsForDynamicRow(default, null));
            Assert.Throws<NotSupportedException>(() => m.GetDynamicCellParserFor(default, null));
            Assert.Throws<NotSupportedException>(() => m.GetDynamicRowConverter(default, null, null));
        }

        [Fact]
        public void ConstructionErrors()
        {
            Assert.Throws<ArgumentException>(() => ManualTypeDescriber.CreateBuilder((ManualTypeDescriberFallbackBehavior)0));
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

                    m.SetInstanceProvider(InstanceProvider.ForDelegate((out _Serializing val) => { val = new _Serializing(); return true; }));
                }


                // 2 arg
                {
                    var m = ManualTypeDescriber.CreateBuilder();

                    m.SetInstanceProvider(typeof(_Serializing).GetTypeInfo(), InstanceProvider.ForDelegate((out _Serializing val) => { val = new _Serializing(); return true; }));
                }
            }

            var t = typeof(_Serializing).GetTypeInfo();
            var prop = t.GetProperty(nameof(_Serializing.Prop));
            Assert.NotNull(prop);
            var g = Getter.ForMethod(prop.GetMethod);
            Assert.NotNull(g);
            var f = Formatter.GetDefault(typeof(string).GetTypeInfo());
            Assert.NotNull(f);
            var s = ShouldSerialize.ForDelegate(() => true);
            Assert.NotNull(s);

            // AddExplicitGetter
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 3 arg
                m.AddExplicitGetter(t, "foo", g);

                // 4 arg
                m.AddExplicitGetter(t, "foo", g, f);

                // 5 arg 
                m.AddExplicitGetter(t, "foo", g, f, s);

                // 6 arg
                m.AddExplicitGetter(t, "foo", g, f, s, EmitDefaultValue.Yes);
            }

            var field = t.GetField(nameof(_Serializing.Field));
            Assert.NotNull(field);

            // AddSerializableField
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 1 arg
                m.AddSerializableField(field);

                // 2 arg
                m.AddSerializableField(field, "foo");
                m.AddSerializableField(t, field);

                // 3 arg
                m.AddSerializableField(field, "foo", f);
                m.AddSerializableField(t, field, "foo");

                // 4 arg
                m.AddSerializableField(field, "foo", f, s);
                m.AddSerializableField(t, field, "foo", f, s);

                // 5 arg
                m.AddSerializableField(field, "foo", f, s, EmitDefaultValue.Yes);
                m.AddSerializableField(t, field, "foo", f, s);

                // 6 arg
                m.AddSerializableField(t, field, "foo", f, s, EmitDefaultValue.Yes);
            }

            // AddSerializableProperty
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 1 arg
                m.AddSerializableProperty(prop);

                // 2 arg
                m.AddSerializableProperty(prop, "foo");
                m.AddSerializableProperty(t, prop);

                // 3 arg
                m.AddSerializableProperty(prop, "foo", f);
                m.AddSerializableProperty(t, prop, "foo");

                // 4 arg
                m.AddSerializableProperty(prop, "foo", f, s);
                m.AddSerializableProperty(t, prop, "foo", f, s);

                // 5 arg
                m.AddSerializableProperty(prop, "foo", f, s, EmitDefaultValue.Yes);
                m.AddSerializableProperty(t, prop, "foo", f, s);

                // 6 arg
                m.AddSerializableProperty(t, prop, "foo", f, s, EmitDefaultValue.Yes);
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

            var r = Reset.ForDelegate(() => { });
            Assert.NotNull(r);

            // AddExplicitSetter
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 3 arg
                m.AddExplicitSetter(t, "foo", s);

                // 4 arg
                m.AddExplicitSetter(t, "foo", s, p);

                // 5 arg
                m.AddExplicitSetter(t, "foo", s, p, MemberRequired.Yes);

                // 6 arg
                m.AddExplicitSetter(t, "foo", s, p, MemberRequired.Yes, r);
            }

            // AddDeserializableField
            {
                var m = ManualTypeDescriber.CreateBuilder();

                // 1 arg
                m.AddDeserializableField(field);

                // 2 arg
                m.AddDeserializableField(field, "foo");
                m.AddDeserializableField(t, field);

                // 3 arg
                m.AddDeserializableField(field, "foo", p);
                m.AddDeserializableField(t, field, "foo");

                // 4 arg
                m.AddDeserializableField(field, "foo", p, MemberRequired.Yes);
                m.AddDeserializableField(t, field, "foo", p);

                // 5 arg
                m.AddDeserializableField(field, "foo", p, MemberRequired.Yes, r);
                m.AddDeserializableField(t, field, "foo", p, MemberRequired.Yes);

                // 6 arg
                m.AddDeserializableField(t, field, "foo", p, MemberRequired.Yes, r);
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
                Assert.Throws<ArgumentNullException>(() => m.SetInstanceProvider(null));
                Assert.Throws<ArgumentNullException>(() => m.SetInstanceProvider(null, InstanceProvider.ForDelegate((out string val) => { val = ""; return true; })));
                Assert.Throws<ArgumentNullException>(() => m.SetInstanceProvider(typeof(string).GetTypeInfo(), null));
                Assert.Throws<InvalidOperationException>(() => m.SetInstanceProvider(typeof(int).GetTypeInfo(), InstanceProvider.ForDelegate((out string val) => { val = ""; return true; })));
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
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(null, "foo", Getter.ForDelegate(() => 0)));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), null, Getter.ForDelegate(() => 0)));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), "foo", null));

                // 4 arg version
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(null, "foo", Getter.ForDelegate(() => 0), Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), null, Getter.ForDelegate(() => 0), Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), "foo", null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate(() => 0), null));

                // 5 arg version
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(null, "foo", Getter.ForDelegate(() => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), null, Getter.ForDelegate(() => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), "foo", null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate(() => 0), null, ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate(() => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                // 6 arg version
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(null, "foo", Getter.ForDelegate(() => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), null, Getter.ForDelegate(() => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), "foo", null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate(() => 0), null, ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitGetter(typeof(int).GetTypeInfo(), "foo", Getter.ForDelegate(() => 0), Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null
            }

            // AddSerializableField
            {
                var m = ManualTypeDescriber.CreateBuilder();
                var f = typeof(_Errors).GetField(nameof(_Errors.Field));

                Assert.NotNull(f);

                // 1 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null));

                // 2 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(f, null));

                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null, f));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), null));

                // 3 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(f, null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(f, "foo", null));

                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null, f, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), f, null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(f, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(f, "foo", null, ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null, f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), f, null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(f, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(f, "foo", null, ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null

                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null, f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), f, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null, ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(null, f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), f, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null, ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableField(typeof(_Errors).GetTypeInfo(), f, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null
            }

            // AddSerializableProperty
            {
                var m = ManualTypeDescriber.CreateBuilder();
                var p = typeof(_Errors).GetProperty(nameof(_Errors.Property));

                Assert.NotNull(p);

                // 1 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null));

                // 2 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(p, null));

                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null, p));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), null));

                // 3 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(p, null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(p, "foo", null));

                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null, p, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), p, null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(p, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(p, "foo", null, ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null, p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Formatter.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(p, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(p, "foo", null, ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null

                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null, p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null, ShouldSerialize.ForDelegate(() => true)));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null));

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(null, p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Formatter.GetDefault(typeof(int).GetTypeInfo()), ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null, ShouldSerialize.ForDelegate(() => true), EmitDefaultValue.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddSerializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
                // WillEmitDefaultValue cannot be null
            }

            // AddSerializableMember (private)
            {
                var m = ManualTypeDescriber.CreateBuilder();
                var p = typeof(_Errors).GetProperty(nameof(_Errors.Property));
                var g = Getter.ForMethod(p.GetMethod);

                // on illegal class
                Assert.Throws<InvalidOperationException>(() => m.AddSerializableMember(typeof(int).GetTypeInfo(), g, "Foo", Formatter.GetDefault(typeof(int).GetTypeInfo()), null, EmitDefaultValue.Yes));
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

            m.AddExplicitGetter(typeof(_SerializeBaseClass_Sub).GetTypeInfo(), "Hello", getter);

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
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(null, "foo", Setter.ForDelegate<int>(delegate { })));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), null, Setter.ForDelegate<int>(delegate { })));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(null, "foo", Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), null, Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", Setter.ForDelegate<int>(delegate { }), null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(null, "foo", Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), null, Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", Setter.ForDelegate<int>(delegate { }), null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(null, "foo", Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), null, Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", Setter.ForDelegate<int>(delegate { }), null, MemberRequired.Yes, Reset.ForDelegate(() => { })));
                // IsMemberRequired cannot be null
                // reset can be null
                Assert.Throws<ArgumentNullException>(() => m.AddExplicitSetter(typeof(_Errors).GetTypeInfo(), "foo", Setter.ForDelegate<int>(delegate { }), Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));
            }

            // AddDeserializableField
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder();
                var f = typeof(_Errors).GetField(nameof(_Errors.Field));

                Assert.NotNull(f);

                // 1 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null));

                // 2 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(f, null));

                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null, f));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), null));

                // 3 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(f, null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(f, "foo", null));

                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null, f, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), f, null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(f, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(f, "foo", null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null, f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), f, null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(f, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(f, "foo", null, MemberRequired.Yes, Reset.ForDelegate(() => { })));
                // IsMemberRequired cannot be null
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));

                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null, f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), f, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(null, f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), f, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), f, "foo", null, MemberRequired.Yes, Reset.ForDelegate(() => { })));
                // IsMemberRequired cannot be null
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableField(typeof(_Errors).GetTypeInfo(), f, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));
            }

            // AddDeserializableProperty
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder();
                var p = typeof(_Errors).GetProperty(nameof(_Errors.Property));

                Assert.NotNull(p);

                // 1 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null));

                // 2 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(p, null));

                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null, p));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), null));

                // 3 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(p, null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(p, "foo", null));

                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null, p, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo"));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, null));

                // 4 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(p, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(p, "foo", null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null, p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Parser.GetDefault(typeof(int).GetTypeInfo())));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null));

                // 5 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(p, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(p, "foo", null, MemberRequired.Yes, Reset.ForDelegate(() => { })));
                // IsMemberRequired cannot be null
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));

                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null, p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null, MemberRequired.Yes));
                // IsMemberRequired cannot be null

                // 6 arg
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(null, p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), null, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, null, Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, Reset.ForDelegate(() => { })));
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", null, MemberRequired.Yes, Reset.ForDelegate(() => { })));
                // IsMemberRequired cannot be null
                Assert.Throws<ArgumentNullException>(() => m.AddDeserializableProperty(typeof(_Errors).GetTypeInfo(), p, "foo", Parser.GetDefault(typeof(int).GetTypeInfo()), MemberRequired.Yes, null));
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

                m.SetInstanceProvider(InstanceProvider.ForDelegate((out string foo) => { foo = ""; return true; }));
                m.SetInstanceProvider(InstanceProvider.ForDelegate((out int foo) => { foo = 10; return true; }));

                m.AddDeserializableField(typeof(_Errors).GetField(nameof(_Errors.Field)));
                m.AddDeserializableProperty(typeof(_Errors).GetProperty(nameof(_Errors.Property)));
                m.AddDeserializableProperty(typeof(_ToStringOverride).GetProperty(nameof(_ToStringOverride.Bar)));

                m.AddSerializableField(typeof(_Errors).GetField(nameof(_Errors.Field)));
                m.AddSerializableProperty(typeof(_Errors).GetProperty(nameof(_Errors.Property)));
                m.AddSerializableProperty(typeof(_ToStringOverride).GetProperty(nameof(_ToStringOverride.Bar)));

                Assert.NotNull(m.ToString());
            }

            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);

                m.SetInstanceProvider(InstanceProvider.ForDelegate((out string foo) => { foo = ""; return true; }));
                m.SetInstanceProvider(InstanceProvider.ForDelegate((out int foo) => { foo = 10; return true; }));

                m.AddDeserializableField(typeof(_Errors).GetField(nameof(_Errors.Field)));
                m.AddDeserializableProperty(typeof(_Errors).GetProperty(nameof(_Errors.Property)));
                m.AddDeserializableProperty(typeof(_ToStringOverride).GetProperty(nameof(_ToStringOverride.Bar)));

                m.AddSerializableField(typeof(_Errors).GetField(nameof(_Errors.Field)));
                m.AddSerializableProperty(typeof(_Errors).GetProperty(nameof(_Errors.Property)));
                m.AddSerializableProperty(typeof(_ToStringOverride).GetProperty(nameof(_ToStringOverride.Bar)));

                Assert.NotNull(m.ToString());
            }
        }
    }
}
