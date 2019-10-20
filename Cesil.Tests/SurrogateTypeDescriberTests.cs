using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class SurrogateTypeDescriberTests
    {
        [Fact]
        public void ConstructionErrors()
        {
            Assert.Throws<ArgumentException>(() => new SurrogateTypeDescriber((SurrogateTypeDescriberFallbackBehavior)0));
        }

        private class _Resets_Real
        {
            public string Foo { get; set; }
            
            public void ResetFoo() { }
        }

        private class _Resets_Surrogate
        {
            public string Foo { get; set; }

            public void ResetFoo() { }
        }

        private class _Resets_Surrogate_Bad
        {
            public string Foo { get; set; }

            public void NonMatchingReset() { }
        }

        [Fact]
        public void Resets()
        {
            var surrogate = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogate.AddSurrogateType(typeof(_Resets_Real).GetTypeInfo(), typeof(_Resets_Surrogate).GetTypeInfo());

            var res = surrogate.EnumerateMembersToDeserialize(typeof(_Resets_Real).GetTypeInfo()).Single();
            Assert.NotNull(res.Reset);
            Assert.Equal(BackingMode.Method, res.Reset.Mode);
            Assert.Equal(typeof(_Resets_Real).GetMethod(nameof(_Resets_Real.ResetFoo)), res.Reset.Method);

            // resets can't be mapped if they're not backed by a method
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitSetter(
                    typeof(_Resets_Surrogate).GetTypeInfo(),
                    nameof(_Resets_Surrogate.Foo),
                    Setter.ForMethod(typeof(_Resets_Surrogate).GetProperty(nameof(_Resets_Surrogate.Foo)).SetMethod),
                    Parser.GetDefault(typeof(string).GetTypeInfo()),
                    IsMemberRequired.No,
                    Reset.ForDelegate(() => { })
                );
                manual.SetBuilder(InstanceProvider.ForParameterlessConstructor(typeof(_Resets_Surrogate).GetConstructor(Type.EmptyTypes)));

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_Resets_Real).GetTypeInfo(), typeof(_Resets_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToDeserialize(typeof(_Resets_Real).GetTypeInfo()));
            }

            // missing reset
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitSetter(
                    typeof(_Resets_Surrogate_Bad).GetTypeInfo(),
                    nameof(_Resets_Surrogate_Bad.Foo),
                    Setter.ForMethod(typeof(_Resets_Surrogate_Bad).GetProperty(nameof(_Resets_Surrogate_Bad.Foo)).SetMethod),
                    Parser.GetDefault(typeof(string).GetTypeInfo()),
                    IsMemberRequired.No,
                    Reset.ForMethod(typeof(_Resets_Surrogate_Bad).GetMethod(nameof(_Resets_Surrogate_Bad.NonMatchingReset)))
                );
                manual.SetBuilder(InstanceProvider.ForParameterlessConstructor(typeof(_Resets_Surrogate).GetConstructor(Type.EmptyTypes)));

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_Resets_Real).GetTypeInfo(), typeof(_Resets_Surrogate_Bad).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToDeserialize(typeof(_Resets_Real).GetTypeInfo()));
            }
        }

        private class _Setters_Real
        {
            public string Foo { get; set; }
#pragma warning disable CS0649
            public int Bar;
#pragma warning restore CS0649
        }

        private class _Setters_Surrogate
        {
            public string Foo { get; set; }
#pragma warning disable CS0649
            [DataMember]
            public int Bar;
#pragma warning restore CS0649
        }

        [Fact]
        public void Setters()
        {
            var surrogate = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogate.AddSurrogateType(typeof(_Setters_Real).GetTypeInfo(), typeof(_Setters_Surrogate).GetTypeInfo());

            var res = surrogate.EnumerateMembersToDeserialize(typeof(_Setters_Real).GetTypeInfo()).ToList();
            var a = res.Single(r => r.Name == nameof(_Setters_Real.Foo)).Setter;
            Assert.Equal(BackingMode.Method, a.Mode);
            Assert.Equal(typeof(_Setters_Real).GetProperty(nameof(_Setters_Real.Foo)).SetMethod, a.Method);
            var b = res.Single(r => r.Name == nameof(_Setters_Real.Bar)).Setter;
            Assert.Equal(BackingMode.Field, b.Mode);
            Assert.Equal(typeof(_Setters_Real).GetField(nameof(_Setters_Real.Bar)), b.Field);

            // setters can't be mapped if they're not backed by a method
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitSetter(
                    typeof(_Setters_Surrogate).GetTypeInfo(),
                    nameof(_Setters_Surrogate.Foo),
                    Setter.ForDelegate<string>(_ => { }),
                    Parser.GetDefault(typeof(string).GetTypeInfo()),
                    IsMemberRequired.No
                );
                manual.SetBuilder(InstanceProvider.ForParameterlessConstructor(typeof(_Setters_Surrogate).GetConstructor(Type.EmptyTypes)));

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_Setters_Real).GetTypeInfo(), typeof(_Setters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToDeserialize(typeof(_Setters_Real).GetTypeInfo()));
            }
        }

        private sealed class _ShouldSerializes_Real
        {
            public string Foo { get; }

            public bool ShouldSerializeFoo()
            {
                return true;
            }
        }

        private sealed class _ShouldSerializes_Surrogate
        {
            public string Foo { get; }

            public bool ShouldSerializeFoo()
            {
                return true;
            }

            public bool NoMatcher()
            {
                return true;
            }
        }

        [Fact]
        public void ShouldSerializes()
        {
            var surrogate = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogate.AddSurrogateType(typeof(_ShouldSerializes_Real).GetTypeInfo(), typeof(_ShouldSerializes_Surrogate).GetTypeInfo());

            var res = surrogate.EnumerateMembersToSerialize(typeof(_ShouldSerializes_Real).GetTypeInfo()).ToList();
            var a = res.Single(r => r.Name == nameof(_ShouldSerializes_Real.Foo)).ShouldSerialize;
            Assert.Equal(BackingMode.Method, a.Mode);
            Assert.Equal(typeof(_ShouldSerializes_Real).GetMethod(nameof(_ShouldSerializes_Real.ShouldSerializeFoo)), a.Method);

            // no matching method
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitGetter(
                    typeof(_ShouldSerializes_Surrogate).GetTypeInfo(),
                    nameof(_ShouldSerializes_Surrogate.Foo),
                    Getter.ForMethod(typeof(_ShouldSerializes_Surrogate).GetProperty(nameof(_ShouldSerializes_Surrogate.Foo)).GetMethod),
                    Formatter.GetDefault(typeof(string).GetTypeInfo()),
                    ShouldSerialize.ForMethod(typeof(_ShouldSerializes_Surrogate).GetMethod(nameof(_ShouldSerializes_Surrogate.NoMatcher)))
                );

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_ShouldSerializes_Real).GetTypeInfo(), typeof(_ShouldSerializes_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToSerialize(typeof(_ShouldSerializes_Real).GetTypeInfo()));
            }

            // non-method backing isn't allowed
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitGetter(
                    typeof(_ShouldSerializes_Surrogate).GetTypeInfo(),
                    nameof(_ShouldSerializes_Surrogate.Foo),
                    Getter.ForMethod(typeof(_ShouldSerializes_Surrogate).GetProperty(nameof(_ShouldSerializes_Surrogate.Foo)).GetMethod),
                    Formatter.GetDefault(typeof(string).GetTypeInfo()),
                    ShouldSerialize.ForDelegate(() => true)
                );

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_ShouldSerializes_Real).GetTypeInfo(), typeof(_ShouldSerializes_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToSerialize(typeof(_ShouldSerializes_Real).GetTypeInfo()));
            }
        }

        private sealed class _Getters_Real
        {
            public string Foo { get; }
#pragma warning disable CS0649
            public int Bar;
#pragma warning restore CS0649

            public string BadParams1(int a)
            {
                return "";
            }

            public static string BadParams2(_Getters_Real a)
            {
                return "";
            }
        }

        private sealed class _Getters_Surrogate
        {
            public string Foo { get; }
#pragma warning disable CS0649
            [DataMember]
            public int Bar;
#pragma warning restore CS0649

            [IgnoreDataMember]
            public string Missing1 { get; }
#pragma warning disable CS0649
            public int Missing2;
#pragma warning restore CS0649

            public string BadParams1()
            {
                return "";
            }

            public static string BadParams2(_Getters_Surrogate a)
            {
                return "";
            }
        }

        [Fact]
        public void Getters()
        {
            var surrogate = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogate.AddSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

            var res = surrogate.EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()).ToList();
            var a = res.Single(r => r.Name == nameof(_Getters_Real.Foo)).Getter;
            Assert.Equal(BackingMode.Method, a.Mode);
            Assert.Equal(typeof(_Getters_Real).GetProperty(nameof(_Getters_Real.Foo)).GetMethod, a.Method);
            var b = res.Single(r => r.Name == nameof(_Getters_Real.Bar)).Getter;
            Assert.Equal(BackingMode.Field, b.Mode);
            Assert.Equal(typeof(_Getters_Real).GetField(nameof(_Getters_Real.Bar)), b.Field);

            // no equivalent method
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.Missing1),
                    Getter.ForMethod(typeof(_Getters_Surrogate).GetProperty(nameof(_Getters_Surrogate.Missing1)).GetMethod),
                    Formatter.GetDefault(typeof(string).GetTypeInfo())
                );

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
            }

            // no equivalent field
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.Missing2),
                    Getter.ForField(typeof(_Getters_Surrogate).GetField(nameof(_Getters_Surrogate.Missing2))),
                    Formatter.GetDefault(typeof(int).GetTypeInfo())
                );

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
            }

            // can't be backed by a delegate
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.Foo),
                    Getter.ForDelegate(() => ""),
                    Formatter.GetDefault(typeof(string).GetTypeInfo())
                );

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
            }

            // equivalent method must have same parameter count
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.BadParams1),
                    Getter.ForMethod(typeof(_Getters_Surrogate).GetMethod(nameof(_Getters_Surrogate.BadParams1))),
                    Formatter.GetDefault(typeof(string).GetTypeInfo())
                );

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
            }

            // equivalent method must have same parameter types
            {
                var manual = new ManualTypeDescriber();
                manual.AddExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.BadParams2),
                    Getter.ForMethod(typeof(_Getters_Surrogate).GetMethod(nameof(_Getters_Surrogate.BadParams2))),
                    Formatter.GetDefault(typeof(string).GetTypeInfo())
                );

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
            }
        }

        private sealed class _InstanceBuilders_Real
        {
            public string Foo { get; set; }

            public _InstanceBuilders_Real() { }
        }

        private sealed class _InstanceBuilders_Real_NoCons
        {
            public string Foo { get; set; }

            public _InstanceBuilders_Real_NoCons(int a, int b) { }
        }

        private sealed class _InstanceBuilders_Surrogate
        {
            public string Foo { get; set; }

            public _InstanceBuilders_Surrogate() { }
        }

        private static bool _InstanceBuilders_Mtd(out _InstanceBuilders_Surrogate val)
        {
            val = new _InstanceBuilders_Surrogate();
            return true;
        }

        [Fact]
        public void InstanceBuilders()
        {
            var surrogate = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogate.AddSurrogateType(typeof(_InstanceBuilders_Real).GetTypeInfo(), typeof(_InstanceBuilders_Surrogate).GetTypeInfo());

            var res = surrogate.GetInstanceProvider(typeof(_InstanceBuilders_Real).GetTypeInfo());
            Assert.Equal(BackingMode.Constructor, res.Mode);
            Assert.Equal(typeof(_InstanceBuilders_Real).GetConstructor(Type.EmptyTypes), res.Constructor);

            // missing constructor
            {
                var manual = new ManualTypeDescriber();
                manual.SetBuilder(
                    InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders_Surrogate).GetConstructor(Type.EmptyTypes))
                );

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_InstanceBuilders_Real_NoCons).GetTypeInfo(), typeof(_InstanceBuilders_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.GetInstanceProvider(typeof(_InstanceBuilders_Real_NoCons).GetTypeInfo()));
            }

            // cannot be backed by delegate
            {
                var manual = new ManualTypeDescriber();
                manual.SetBuilder(InstanceProvider.ForDelegate((out _InstanceBuilders_Surrogate val) => { val = new _InstanceBuilders_Surrogate(); return true; }));

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_InstanceBuilders_Real).GetTypeInfo(), typeof(_InstanceBuilders_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.GetInstanceProvider(typeof(_InstanceBuilders_Real).GetTypeInfo()));
            }

            // cannot be backed by method
            {
                var manual = new ManualTypeDescriber();
                manual.SetBuilder(InstanceProvider.ForMethod(typeof(SurrogateTypeDescriberTests).GetMethod(nameof(_InstanceBuilders_Mtd), BindingFlags.NonPublic | BindingFlags.Static)));

                var badSurrogate = new SurrogateTypeDescriber(manual);
                badSurrogate.AddSurrogateType(typeof(_InstanceBuilders_Real).GetTypeInfo(), typeof(_InstanceBuilders_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.GetInstanceProvider(typeof(_InstanceBuilders_Real).GetTypeInfo()));
            }
        }

        private class _Simple_Real
        {
            public string Foo { get; set; }
        }

        private class _Simple_Surrogate
        {
            [DataMember(Name = "bar")]
            public string Foo { get; set; }
        }

        [Fact]
        public void Simple_Deserialize()
        {
            var surrogate = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
            surrogate.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo());

            // maps!
            {
                var res = surrogate.EnumerateMembersToDeserialize(typeof(_Simple_Real).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.Null(a.Setter.Field);
                        Assert.Equal(Parser.GetDefault(typeof(string).GetTypeInfo()), a.Parser);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Real).GetProperty(nameof(_Simple_Real.Foo)).SetMethod, a.Setter.Method);
                        Assert.False(a.IsRequired);
                    }
                );

                var builder = surrogate.GetInstanceProvider(typeof(_Simple_Real).GetTypeInfo());
                Assert.Equal(BackingMode.Constructor, builder.Mode);
                Assert.Equal(typeof(_Simple_Real).GetConstructor(Type.EmptyTypes), builder.Constructor);
                Assert.Equal(typeof(_Simple_Real), builder.ConstructsType);
            }

            // doesn't map
            {
                var res = surrogate.EnumerateMembersToDeserialize(typeof(_Simple_Surrogate).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.Null(a.Setter.Field);
                        Assert.Equal(Parser.GetDefault(typeof(string).GetTypeInfo()), a.Parser);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Surrogate).GetProperty(nameof(_Simple_Surrogate.Foo)).SetMethod, a.Setter.Method);
                        Assert.False(a.IsRequired);
                    }
                );

                var builder = surrogate.GetInstanceProvider(typeof(_Simple_Surrogate).GetTypeInfo());
                Assert.Equal(BackingMode.Constructor, builder.Mode);
                Assert.Equal(typeof(_Simple_Surrogate).GetConstructor(Type.EmptyTypes), builder.Constructor);
                Assert.Equal(typeof(_Simple_Surrogate), builder.ConstructsType);
            }
        }

        [Fact]
        public void Simple_Serialize()
        {
            var surrogate = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
            surrogate.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo());

            // maps!
            {
                var res = surrogate.EnumerateMembersToSerialize(typeof(_Simple_Real).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.True(a.EmitDefaultValue);
                        Assert.Null(a.Getter.Field);
                        Assert.Null(a.Getter.Delegate);
                        Assert.Equal(Formatter.GetDefault(typeof(string).GetTypeInfo()), a.Formatter);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Real).GetProperty(nameof(_Simple_Real.Foo)).GetMethod, a.Getter.Method);
                        Assert.Null(a.ShouldSerialize);
                    }
                );
            }

            // doesn't map
            {
                var res = surrogate.EnumerateMembersToSerialize(typeof(_Simple_Surrogate).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.True(a.EmitDefaultValue);
                        Assert.Null(a.Getter.Field);
                        Assert.Null(a.Getter.Delegate);
                        Assert.Equal(Formatter.GetDefault(typeof(string).GetTypeInfo()), a.Formatter);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Surrogate).GetProperty(nameof(_Simple_Surrogate.Foo)).GetMethod, a.Getter.Method);
                        Assert.Null(a.ShouldSerialize);
                    }
                );
            }
        }

#pragma warning disable 0649
        private class _Errors_Field
        {
            public string Foo;
        }

        private class _Errors_Field_Missing
        {
            [DataMember]
            public string Bar;
        }

        private class _Errors_Field_Mismatch
        {
            [DataMember]
            public int Foo;
        }
#pragma warning restore 0649

        private class _Errors_Property
        {
            public string Fizz { get; set; }
        }

        private class _Errors_Property_Missing
        {
            public string Buzz { get; set; }
        }

        private class _Errors_Property_Mismatch
        {
            public int Fizz { get; set; }
        }

        private class _Errors_ExplicitSetter
        {
            public void SetVal(string val) { }
        }

        private class _Errors_ExplicitSetter_Mismatch
        {
            public void SetVal(int val) { }
        }

        private class _Errors_ExplicitStaticSetter
        {
            public static void SetVal(string val) { }
        }

        private class _Errors_ExplicitStaticSetter_Mismatch
        {
            public static void SetVal(int val) { }
        }

        private class _Errors_ExplicitStaticSetter_ArityMismatch
        {
            public static void SetVal(_Errors_ExplicitStaticSetter foo, int val) { }
        }

        private class _Errors_ExplicitGetter
        {
            public string GetVal() => "";
        }

        private class _Errors_ExplicitGetter_Mismatch
        {
            public int GetVal() => 0;
        }

        private class _Errors_ExplicitStaticGetter
        {
            public static string GetVal() => "";
        }

        private class _Errors_ExplicitStaticGetter_Mismatch
        {
            public static int GetVal() => 0;
        }

        private class _Errors_ExplicitStaticGetter_ArityMismatch : _Errors_ExplicitStaticGetter
        {
            public static string GetVal(_Errors_ExplicitStaticGetter row) => "";
        }

        [Fact]
        public void Errors()
        {
            // null inner describer
            {
                Assert.Throws<ArgumentNullException>(() => new SurrogateTypeDescriber(null, SurrogateTypeDescriberFallbackBehavior.Throw));
            }

            // null forType
            {
                var s = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
                Assert.Throws<ArgumentNullException>(() => s.AddSurrogateType(null, typeof(_Simple_Real).GetTypeInfo()));
            }

            // null surrogateType
            {
                var s = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
                Assert.Throws<ArgumentNullException>(() => s.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), null));
            }

            // same registration
            {
                var s = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
                Assert.Throws<InvalidOperationException>(() => s.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Real).GetTypeInfo()));
            }

            // double registration
            {
                var s = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => s.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo()));
            }

            // no registration
            {
                var s = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.Throw);
                Assert.Throws<InvalidOperationException>(() => s.GetInstanceProvider(typeof(object).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToSerialize(typeof(object).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(object).GetTypeInfo()));
            }

            // field missing
            {
                var s = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Errors_Field).GetTypeInfo(), typeof(_Errors_Field_Missing).GetTypeInfo());
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Field).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Field).GetTypeInfo()));
            }

            // field type mismatch
            {
                var s = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Errors_Field).GetTypeInfo(), typeof(_Errors_Field_Mismatch).GetTypeInfo());
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Field).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Field).GetTypeInfo()));
            }

            // prop missing
            {
                var s = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Errors_Property).GetTypeInfo(), typeof(_Errors_Property_Missing).GetTypeInfo());
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Property).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Property).GetTypeInfo()));
            }

            // prop type mismatch
            {
                var s = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Errors_Property).GetTypeInfo(), typeof(_Errors_Property_Mismatch).GetTypeInfo());
                Assert.Throws<ArgumentException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Property).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Property).GetTypeInfo()));
            }

            // explicit setter mismatch
            {
                var i = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
                i.AddExplicitSetter(typeof(_Errors_ExplicitSetter_Mismatch).GetTypeInfo(), "Val", (Setter)typeof(_Errors_ExplicitSetter_Mismatch).GetMethod("SetVal"));

                var s = new SurrogateTypeDescriber(i, SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Errors_ExplicitSetter).GetTypeInfo(), typeof(_Errors_ExplicitSetter_Mismatch).GetTypeInfo());
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_ExplicitSetter).GetTypeInfo()));
            }

            // explicit static setter mismatch
            {
                var i = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
                i.AddExplicitSetter(typeof(_Errors_ExplicitStaticSetter_Mismatch).GetTypeInfo(), "Val", (Setter)typeof(_Errors_ExplicitStaticSetter_Mismatch).GetMethod("SetVal"));

                var s = new SurrogateTypeDescriber(i, SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticSetter_Mismatch).GetTypeInfo());
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo()));
            }

            // explicit static setter arity mismatch
            {
                var i = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
                i.AddExplicitSetter(typeof(_Errors_ExplicitStaticSetter_ArityMismatch).GetTypeInfo(), "Val", (Setter)typeof(_Errors_ExplicitStaticSetter_ArityMismatch).GetMethod("SetVal"));

                var s = new SurrogateTypeDescriber(i, SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticSetter_ArityMismatch).GetTypeInfo());
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo()));
            }

            // explicit getter mismatch
            {
                var i = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
                i.AddExplicitGetter(typeof(_Errors_ExplicitGetter_Mismatch).GetTypeInfo(), "Val", (Getter)typeof(_Errors_ExplicitGetter_Mismatch).GetMethod("GetVal"));

                var s = new SurrogateTypeDescriber(i, SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Errors_ExplicitGetter).GetTypeInfo(), typeof(_Errors_ExplicitGetter_Mismatch).GetTypeInfo());
                Assert.Throws<ArgumentException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_ExplicitGetter).GetTypeInfo()));
            }

            // explicit static getter mismatch
            {
                var i = new ManualTypeDescriber(ManualTypeDescriberFallbackBehavior.UseDefault);
                i.AddExplicitGetter(typeof(_Errors_ExplicitStaticGetter_Mismatch).GetTypeInfo(), "Val", (Getter)typeof(_Errors_ExplicitStaticGetter_Mismatch).GetMethod("GetVal"));

                var s = new SurrogateTypeDescriber(i, SurrogateTypeDescriberFallbackBehavior.UseDefault);
                s.AddSurrogateType(typeof(_Errors_ExplicitStaticGetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticGetter_Mismatch).GetTypeInfo());
                Assert.Throws<ArgumentException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_ExplicitStaticGetter).GetTypeInfo()));
            }
        }

        private sealed class _ToStrings1 { }
        private sealed class _ToStrings1_Surrogate { }

        private sealed class _ToStrings2 { }
        private sealed class _ToStrings2_Surrogate { }

        [Fact]
        public void ToStrings()
        {
            var s1 = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.UseDefault);
            var s2 = new SurrogateTypeDescriber(SurrogateTypeDescriberFallbackBehavior.Throw);

            s1.AddSurrogateType(typeof(_ToStrings1).GetTypeInfo(), typeof(_ToStrings1_Surrogate).GetTypeInfo());
            s1.AddSurrogateType(typeof(_ToStrings2).GetTypeInfo(), typeof(_ToStrings2_Surrogate).GetTypeInfo());

            s2.AddSurrogateType(typeof(_ToStrings1).GetTypeInfo(), typeof(_ToStrings1_Surrogate).GetTypeInfo());
            s2.AddSurrogateType(typeof(_ToStrings2).GetTypeInfo(), typeof(_ToStrings2_Surrogate).GetTypeInfo());

            var str1 = s1.ToString();
            var str2 = s2.ToString();

            Assert.Equal("SurrogateTypeDescriber using type describer DefaultTypeDescriber Shared Instance which delegates when no surrogate registered and uses Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1, Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2", str1);
            Assert.Equal("SurrogateTypeDescriber using type describer DefaultTypeDescriber Shared Instance which throws when no surrogate registered and uses Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1, Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2", str2);
        }

        private sealed class _DynamicPassThrough : ITypeDescriber
        {
            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToDeserialize(forType);

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            => TypeDescribers.Default.EnumerateMembersToSerialize(forType);

            public int GetCellsForDynamicRowCalls { get; private set; }
            public IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext ctx, object row)
            {
                GetCellsForDynamicRowCalls++;
                return default;
            }

            public int GetDynamicCellParserForCalls { get; private set; }
            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            {
                GetDynamicCellParserForCalls++;
                return default;
            }

            public int GetDynamicRowConverterCalls { get; private set; }
            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            {
                GetDynamicRowConverterCalls++;
                return default;
            }

            public InstanceProvider GetInstanceProvider(TypeInfo forType)
            => TypeDescribers.Default.GetInstanceProvider(forType);
        }

        [Fact]
        public void DynamicPassThrough()
        {
            var lower = new _DynamicPassThrough();
            var surrogate = new SurrogateTypeDescriber(lower);

            surrogate.GetCellsForDynamicRow(default, default);
            Assert.Equal(1, lower.GetCellsForDynamicRowCalls);

            surrogate.GetDynamicCellParserFor(default, default);
            Assert.Equal(1, lower.GetDynamicCellParserForCalls);

            surrogate.GetDynamicRowConverter(default, default, default);
            Assert.Equal(1, lower.GetDynamicRowConverterCalls);
        }

        [Fact]
        public void InternalEnumHelpers()
        {
            Assert.Equal(WillEmitDefaultValue.Yes, SurrogateTypeDescriber.GetEquivalentEmitFor(true));
            Assert.Equal(WillEmitDefaultValue.No, SurrogateTypeDescriber.GetEquivalentEmitFor(false));

            Assert.Equal(BindingFlags.Public | BindingFlags.Static, SurrogateTypeDescriber.GetEquivalentFlagsFor(true, true));
            Assert.Equal(BindingFlags.Public | BindingFlags.Instance, SurrogateTypeDescriber.GetEquivalentFlagsFor(true, false));
            Assert.Equal(BindingFlags.NonPublic | BindingFlags.Static, SurrogateTypeDescriber.GetEquivalentFlagsFor(false, true));
            Assert.Equal(BindingFlags.NonPublic | BindingFlags.Instance, SurrogateTypeDescriber.GetEquivalentFlagsFor(false, false));

            Assert.Equal(IsMemberRequired.Yes, SurrogateTypeDescriber.GetEquivalentRequiredFor(true));
            Assert.Equal(IsMemberRequired.No, SurrogateTypeDescriber.GetEquivalentRequiredFor(false));
        }
    }
#pragma warning restore IDE1006
}
