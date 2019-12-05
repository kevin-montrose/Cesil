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
        public void Equality()
        {
            var behaviors = new[] { SurrogateTypeDescriberFallbackBehavior.Throw, SurrogateTypeDescriberFallbackBehavior.UseFallback };
            var types = new[] { new[] { typeof(string).GetTypeInfo(), typeof(int).GetTypeInfo() }, new[] { typeof(object).GetTypeInfo(), typeof(long).GetTypeInfo() } };
            var describers = new[] { TypeDescribers.Default, ManualTypeDescriberBuilder.CreateBuilder().ToManualTypeDescriber() };
            var fallbacks = new[] { TypeDescribers.Default, ManualTypeDescriberBuilder.CreateBuilder().ToManualTypeDescriber() };

            var surrogates = new List<SurrogateTypeDescriber>();

            foreach(var a in behaviors)
            {
                foreach(var b in types)
                {
                    foreach (var c in describers)
                    {
                        foreach (var d in fallbacks)
                        {
                            var x = SurrogateTypeDescriberBuilder.CreateBuilder(a);
                            x.WithSurrogateType(b[0], b[1]);
                            x.WithTypeDescriber(c);
                            x.WithFallbackTypeDescriber(d);

                            surrogates.Add(x.ToSurrogateTypeDescriber());
                        }
                    }
                }
            }

            for(var i = 0; i < surrogates.Count; i++)
            {
                var s1 = surrogates[i];
                for(var j = i; j < surrogates.Count; j++)
                {
                    var s2 = surrogates[j];

                    var eq = s1 == s2;
                    var eqObj = s1.Equals((object)s2);
                    var neq = s1 != s2;
                    var hashEqual = s1.GetHashCode() == s2.GetHashCode();

                    if(i == j)
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
        public void ConstructionErrors()
        {
            Assert.Throws<ArgumentException>(() => SurrogateTypeDescriberBuilder.CreateBuilder((SurrogateTypeDescriberFallbackBehavior)0));
            Assert.Throws<ArgumentNullException>(() => SurrogateTypeDescriberBuilder.CreateBuilder(null));
            Assert.Throws<ArgumentNullException>(() => SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, null));
            Assert.Throws<ArgumentNullException>(() => SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, null, TypeDescribers.Default));
            Assert.Throws<ArgumentNullException>(() => SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default, null));
        }

        [Fact]
        public void Copy()
        {
            var s1 = SurrogateTypeDescriberBuilder.CreateBuilder();
            s1.WithSurrogateType(typeof(string).GetTypeInfo(), typeof(object).GetTypeInfo());

            var t1 = s1.ToSurrogateTypeDescriber();

            var s2 = SurrogateTypeDescriberBuilder.CreateBuilder(t1);
            var t2 = s2.ToSurrogateTypeDescriber();

            Assert.Equal(t1, t2);
        }

        [Fact]
        public void SetterMethods()
        {
            // FallbackBehavior
            {
                var s1 = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw);
                Assert.Equal(SurrogateTypeDescriberFallbackBehavior.Throw, s1.FallbackBehavior);
                s1.WithFallbackBehavior(SurrogateTypeDescriberFallbackBehavior.UseFallback);
                Assert.Equal(SurrogateTypeDescriberFallbackBehavior.UseFallback, s1.FallbackBehavior);
            }

            // FallbackBehavior, exception
            {
                var s1 = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw);
                Assert.Throws<ArgumentException>(() => s1.WithFallbackBehavior(0));
            }

            // TypeDescriber
            {
                var m = ManualTypeDescriber.CreateBuilder().ToManualTypeDescriber();
                var s1 = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default);
                Assert.Same(TypeDescribers.Default, s1.TypeDescriber);
                s1.WithTypeDescriber(m);
                Assert.Same(m, s1.TypeDescriber);
            }

            // TypeDescriber, exception
            {
                var s1 = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default);
                Assert.Throws<ArgumentNullException>(() => s1.WithTypeDescriber(null));
            }

            // FallbackTypeDescriber
            {
                var m1 = ManualTypeDescriber.CreateBuilder().ToManualTypeDescriber();
                var m2 = ManualTypeDescriber.CreateBuilder().ToManualTypeDescriber();
                var s1 = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, m1, TypeDescribers.Default);
                Assert.Same(TypeDescribers.Default, s1.FallbackTypeDescriber);
                s1.WithFallbackTypeDescriber(m2);
                Assert.Same(m2, s1.FallbackTypeDescriber);
            }

            // FallbackTypeDescriber, exception
            {
                var s1 = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, TypeDescribers.Default, TypeDescribers.Default);
                Assert.Throws<ArgumentNullException>(() => s1.WithFallbackTypeDescriber(null));
            }
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
            var surrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogate.WithSurrogateType(typeof(_Resets_Real).GetTypeInfo(), typeof(_Resets_Surrogate).GetTypeInfo());

            var res = surrogate.ToSurrogateTypeDescriber().EnumerateMembersToDeserialize(typeof(_Resets_Real).GetTypeInfo()).Single();
            Assert.True(res.Reset.HasValue);
            Assert.Equal(BackingMode.Method, res.Reset.Value.Mode);
            Assert.Equal(typeof(_Resets_Real).GetMethod(nameof(_Resets_Real.ResetFoo)), res.Reset.Value.Method.Value);

            // resets can't be mapped if they're not backed by a method
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitSetter(
                    typeof(_Resets_Surrogate).GetTypeInfo(),
                    nameof(_Resets_Surrogate.Foo),
                    Setter.ForMethod(typeof(_Resets_Surrogate).GetProperty(nameof(_Resets_Surrogate.Foo)).SetMethod),
                    Parser.GetDefault(typeof(string).GetTypeInfo()),
                    MemberRequired.No,
                    Reset.ForDelegate(() => { })
                );
                manual.WithInstanceProvider(InstanceProvider.ForParameterlessConstructor(typeof(_Resets_Surrogate).GetConstructor(Type.EmptyTypes)));

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_Resets_Real).GetTypeInfo(), typeof(_Resets_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToDeserialize(typeof(_Resets_Real).GetTypeInfo()));
            }

            // missing reset
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitSetter(
                    typeof(_Resets_Surrogate_Bad).GetTypeInfo(),
                    nameof(_Resets_Surrogate_Bad.Foo),
                    Setter.ForMethod(typeof(_Resets_Surrogate_Bad).GetProperty(nameof(_Resets_Surrogate_Bad.Foo)).SetMethod),
                    Parser.GetDefault(typeof(string).GetTypeInfo()),
                    MemberRequired.No,
                    Reset.ForMethod(typeof(_Resets_Surrogate_Bad).GetMethod(nameof(_Resets_Surrogate_Bad.NonMatchingReset)))
                );
                manual.WithInstanceProvider(InstanceProvider.ForParameterlessConstructor(typeof(_Resets_Surrogate).GetConstructor(Type.EmptyTypes)));

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_Resets_Real).GetTypeInfo(), typeof(_Resets_Surrogate_Bad).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToDeserialize(typeof(_Resets_Real).GetTypeInfo()));
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
            var surrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogate.WithSurrogateType(typeof(_Setters_Real).GetTypeInfo(), typeof(_Setters_Surrogate).GetTypeInfo());

            var res = surrogate.ToSurrogateTypeDescriber().EnumerateMembersToDeserialize(typeof(_Setters_Real).GetTypeInfo()).ToList();
            var a = res.Single(r => r.Name == nameof(_Setters_Real.Foo)).Setter;
            Assert.Equal(BackingMode.Method, a.Mode);
            Assert.Equal(typeof(_Setters_Real).GetProperty(nameof(_Setters_Real.Foo)).SetMethod, a.Method.Value);
            var b = res.Single(r => r.Name == nameof(_Setters_Real.Bar)).Setter;
            Assert.Equal(BackingMode.Field, b.Mode);
            Assert.Equal(typeof(_Setters_Real).GetField(nameof(_Setters_Real.Bar)), b.Field.Value);

            // setters can't be mapped if they're not backed by a method
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitSetter(
                    typeof(_Setters_Surrogate).GetTypeInfo(),
                    nameof(_Setters_Surrogate.Foo),
                    Setter.ForDelegate<string>(_ => { }),
                    Parser.GetDefault(typeof(string).GetTypeInfo()),
                    MemberRequired.No
                );
                manual.WithInstanceProvider(InstanceProvider.ForParameterlessConstructor(typeof(_Setters_Surrogate).GetConstructor(Type.EmptyTypes)));

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_Setters_Real).GetTypeInfo(), typeof(_Setters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToDeserialize(typeof(_Setters_Real).GetTypeInfo()));
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
            var surrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogate.WithSurrogateType(typeof(_ShouldSerializes_Real).GetTypeInfo(), typeof(_ShouldSerializes_Surrogate).GetTypeInfo());

            var res = surrogate.ToSurrogateTypeDescriber().EnumerateMembersToSerialize(typeof(_ShouldSerializes_Real).GetTypeInfo()).ToList();
            var a = res.Single(r => r.Name == nameof(_ShouldSerializes_Real.Foo)).ShouldSerialize.Value;
            Assert.Equal(BackingMode.Method, a.Mode);
            Assert.Equal(typeof(_ShouldSerializes_Real).GetMethod(nameof(_ShouldSerializes_Real.ShouldSerializeFoo)), a.Method.Value);

            // no matching method
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitGetter(
                    typeof(_ShouldSerializes_Surrogate).GetTypeInfo(),
                    nameof(_ShouldSerializes_Surrogate.Foo),
                    Getter.ForMethod(typeof(_ShouldSerializes_Surrogate).GetProperty(nameof(_ShouldSerializes_Surrogate.Foo)).GetMethod),
                    Formatter.GetDefault(typeof(string).GetTypeInfo()),
                    ShouldSerialize.ForMethod(typeof(_ShouldSerializes_Surrogate).GetMethod(nameof(_ShouldSerializes_Surrogate.NoMatcher)))
                );

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_ShouldSerializes_Real).GetTypeInfo(), typeof(_ShouldSerializes_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToSerialize(typeof(_ShouldSerializes_Real).GetTypeInfo()));
            }

            // non-method backing isn't allowed
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitGetter(
                    typeof(_ShouldSerializes_Surrogate).GetTypeInfo(),
                    nameof(_ShouldSerializes_Surrogate.Foo),
                    Getter.ForMethod(typeof(_ShouldSerializes_Surrogate).GetProperty(nameof(_ShouldSerializes_Surrogate.Foo)).GetMethod),
                    Formatter.GetDefault(typeof(string).GetTypeInfo()),
                    ShouldSerialize.ForDelegate(() => true)
                );

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_ShouldSerializes_Real).GetTypeInfo(), typeof(_ShouldSerializes_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToSerialize(typeof(_ShouldSerializes_Real).GetTypeInfo()));
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
            var surrogateB = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogateB.WithSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

            var surrogate = surrogateB.ToSurrogateTypeDescriber();

            var res = surrogate.EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()).ToList();
            var a = res.Single(r => r.Name == nameof(_Getters_Real.Foo)).Getter;
            Assert.Equal(BackingMode.Method, a.Mode);
            Assert.Equal(typeof(_Getters_Real).GetProperty(nameof(_Getters_Real.Foo)).GetMethod, a.Method.Value);
            var b = res.Single(r => r.Name == nameof(_Getters_Real.Bar)).Getter;
            Assert.Equal(BackingMode.Field, b.Mode);
            Assert.Equal(typeof(_Getters_Real).GetField(nameof(_Getters_Real.Bar)), b.Field.Value);

            // no equivalent method
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.Missing1),
                    Getter.ForMethod(typeof(_Getters_Surrogate).GetProperty(nameof(_Getters_Surrogate.Missing1)).GetMethod),
                    Formatter.GetDefault(typeof(string).GetTypeInfo())
                );

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
            }

            // no equivalent field
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.Missing2),
                    Getter.ForField(typeof(_Getters_Surrogate).GetField(nameof(_Getters_Surrogate.Missing2))),
                    Formatter.GetDefault(typeof(int).GetTypeInfo())
                );

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
            }

            // can't be backed by a delegate
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.Foo),
                    Getter.ForDelegate(() => ""),
                    Formatter.GetDefault(typeof(string).GetTypeInfo())
                );

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
            }

            // equivalent method must have same parameter count
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.BadParams1),
                    Getter.ForMethod(typeof(_Getters_Surrogate).GetMethod(nameof(_Getters_Surrogate.BadParams1))),
                    Formatter.GetDefault(typeof(string).GetTypeInfo())
                );

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
            }

            // equivalent method must have same parameter types
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithExplicitGetter(
                    typeof(_Getters_Surrogate).GetTypeInfo(),
                    nameof(_Getters_Surrogate.BadParams2),
                    Getter.ForMethod(typeof(_Getters_Surrogate).GetMethod(nameof(_Getters_Surrogate.BadParams2))),
                    Formatter.GetDefault(typeof(string).GetTypeInfo())
                );

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_Getters_Real).GetTypeInfo(), typeof(_Getters_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().EnumerateMembersToSerialize(typeof(_Getters_Real).GetTypeInfo()));
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
            var surrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw);
            surrogate.WithSurrogateType(typeof(_InstanceBuilders_Real).GetTypeInfo(), typeof(_InstanceBuilders_Surrogate).GetTypeInfo());

            var res = surrogate.ToSurrogateTypeDescriber().GetInstanceProvider(typeof(_InstanceBuilders_Real).GetTypeInfo());
            Assert.Equal(BackingMode.Constructor, res.Mode);
            Assert.Equal(typeof(_InstanceBuilders_Real).GetConstructor(Type.EmptyTypes), res.Constructor.Value);

            // missing constructor
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithInstanceProvider(
                    InstanceProvider.ForParameterlessConstructor(typeof(_InstanceBuilders_Surrogate).GetConstructor(Type.EmptyTypes))
                );

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_InstanceBuilders_Real_NoCons).GetTypeInfo(), typeof(_InstanceBuilders_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().GetInstanceProvider(typeof(_InstanceBuilders_Real_NoCons).GetTypeInfo()));
            }

            // cannot be backed by delegate
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithInstanceProvider(InstanceProvider.ForDelegate((out _InstanceBuilders_Surrogate val) => { val = new _InstanceBuilders_Surrogate(); return true; }));

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_InstanceBuilders_Real).GetTypeInfo(), typeof(_InstanceBuilders_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().GetInstanceProvider(typeof(_InstanceBuilders_Real).GetTypeInfo()));
            }

            // cannot be backed by method
            {
                var manual = ManualTypeDescriberBuilder.CreateBuilder();
                manual.WithInstanceProvider(InstanceProvider.ForMethod(typeof(SurrogateTypeDescriberTests).GetMethod(nameof(_InstanceBuilders_Mtd), BindingFlags.NonPublic | BindingFlags.Static)));

                var badSurrogate = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, manual.ToManualTypeDescriber());
                badSurrogate.WithSurrogateType(typeof(_InstanceBuilders_Real).GetTypeInfo(), typeof(_InstanceBuilders_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => badSurrogate.ToSurrogateTypeDescriber().GetInstanceProvider(typeof(_InstanceBuilders_Real).GetTypeInfo()));
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
            var surrogateB = SurrogateTypeDescriberBuilder.CreateBuilder();
            surrogateB.WithSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo());

            var surrogate = surrogateB.ToSurrogateTypeDescriber();

            // maps!
            {
                var res = surrogate.EnumerateMembersToDeserialize(typeof(_Simple_Real).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.False(a.Setter.Field.HasValue);
                        Assert.Equal(Parser.GetDefault(typeof(string).GetTypeInfo()), a.Parser);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Real).GetProperty(nameof(_Simple_Real.Foo)).SetMethod, a.Setter.Method.Value);
                        Assert.False(a.IsRequired);
                    }
                );

                var builder = surrogate.GetInstanceProvider(typeof(_Simple_Real).GetTypeInfo());
                Assert.Equal(BackingMode.Constructor, builder.Mode);
                Assert.Equal(typeof(_Simple_Real).GetConstructor(Type.EmptyTypes), builder.Constructor.Value);
                Assert.Equal(typeof(_Simple_Real), builder.ConstructsType);
            }

            // doesn't map
            {
                var res = surrogate.EnumerateMembersToDeserialize(typeof(_Simple_Surrogate).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.False(a.Setter.Field.HasValue);
                        Assert.Equal(Parser.GetDefault(typeof(string).GetTypeInfo()), a.Parser);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Surrogate).GetProperty(nameof(_Simple_Surrogate.Foo)).SetMethod, a.Setter.Method.Value);
                        Assert.False(a.IsRequired);
                    }
                );

                var builder = surrogate.GetInstanceProvider(typeof(_Simple_Surrogate).GetTypeInfo());
                Assert.Equal(BackingMode.Constructor, builder.Mode);
                Assert.Equal(typeof(_Simple_Surrogate).GetConstructor(Type.EmptyTypes), builder.Constructor.Value);
                Assert.Equal(typeof(_Simple_Surrogate), builder.ConstructsType);
            }
        }

        [Fact]
        public void Simple_Serialize()
        {
            var surrogateB = SurrogateTypeDescriberBuilder.CreateBuilder();
            surrogateB.WithSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo());

            var surrogate = surrogateB.ToSurrogateTypeDescriber();

            // maps!
            {
                var res = surrogate.EnumerateMembersToSerialize(typeof(_Simple_Real).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.True(a.EmitDefaultValue);
                        Assert.False(a.Getter.Field.HasValue);
                        Assert.False(a.Getter.Delegate.HasValue);
                        Assert.Equal(Formatter.GetDefault(typeof(string).GetTypeInfo()), a.Formatter);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Real).GetProperty(nameof(_Simple_Real.Foo)).GetMethod, a.Getter.Method.Value);
                        Assert.False(a.ShouldSerialize.HasValue);
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
                        Assert.False(a.Getter.Field.HasValue);
                        Assert.False(a.Getter.Delegate.HasValue);
                        Assert.Equal(Formatter.GetDefault(typeof(string).GetTypeInfo()), a.Formatter);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Surrogate).GetProperty(nameof(_Simple_Surrogate.Foo)).GetMethod, a.Getter.Method.Value);
                        Assert.False(a.ShouldSerialize.HasValue);
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
                Assert.Throws<ArgumentNullException>(() => SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw, null));
            }

            // null forType
            {
                var s = SurrogateTypeDescriberBuilder.CreateBuilder();
                Assert.Throws<ArgumentNullException>(() => s.WithSurrogateType(null, typeof(_Simple_Real).GetTypeInfo()));
            }

            // null surrogateType
            {
                var s = SurrogateTypeDescriberBuilder.CreateBuilder();
                Assert.Throws<ArgumentNullException>(() => s.WithSurrogateType(typeof(_Simple_Real).GetTypeInfo(), null));
            }

            // same registration
            {
                var s = SurrogateTypeDescriberBuilder.CreateBuilder();
                Assert.Throws<InvalidOperationException>(() => s.WithSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Real).GetTypeInfo()));
            }

            // double registration
            {
                var s = SurrogateTypeDescriberBuilder.CreateBuilder();
                s.WithSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo());

                Assert.Throws<InvalidOperationException>(() => s.WithSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo()));
            }

            // no registration
            {
                var s = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw).ToSurrogateTypeDescriber();
                Assert.Throws<InvalidOperationException>(() => s.GetInstanceProvider(typeof(object).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToSerialize(typeof(object).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(object).GetTypeInfo()));
            }

            // field missing
            {
                var sB = SurrogateTypeDescriberBuilder.CreateBuilder();
                sB.WithSurrogateType(typeof(_Errors_Field).GetTypeInfo(), typeof(_Errors_Field_Missing).GetTypeInfo());

                var s = sB.ToSurrogateTypeDescriber();
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Field).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Field).GetTypeInfo()));
            }

            // field type mismatch
            {
                var sB = SurrogateTypeDescriberBuilder.CreateBuilder();
                sB.WithSurrogateType(typeof(_Errors_Field).GetTypeInfo(), typeof(_Errors_Field_Mismatch).GetTypeInfo());

                var s = sB.ToSurrogateTypeDescriber();
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Field).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Field).GetTypeInfo()));
            }

            // prop missing
            {
                var sB = SurrogateTypeDescriberBuilder.CreateBuilder();
                sB.WithSurrogateType(typeof(_Errors_Property).GetTypeInfo(), typeof(_Errors_Property_Missing).GetTypeInfo());

                var s = sB.ToSurrogateTypeDescriber();

                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Property).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Property).GetTypeInfo()));
            }

            // prop type mismatch
            {
                var sB = SurrogateTypeDescriberBuilder.CreateBuilder();
                sB.WithSurrogateType(typeof(_Errors_Property).GetTypeInfo(), typeof(_Errors_Property_Mismatch).GetTypeInfo());

                var s = sB.ToSurrogateTypeDescriber();

                Assert.Throws<ArgumentException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Property).GetTypeInfo()));
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Property).GetTypeInfo()));
            }

            // explicit setter mismatch
            {
                var i = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
                i.WithExplicitSetter(typeof(_Errors_ExplicitSetter_Mismatch).GetTypeInfo(), "Val", (Setter)typeof(_Errors_ExplicitSetter_Mismatch).GetMethod("SetVal"));

                var sB = SurrogateTypeDescriber.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, i.ToManualTypeDescriber(), TypeDescribers.Default);
                sB.WithSurrogateType(typeof(_Errors_ExplicitSetter).GetTypeInfo(), typeof(_Errors_ExplicitSetter_Mismatch).GetTypeInfo());

                var s = sB.ToSurrogateTypeDescriber();
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_ExplicitSetter).GetTypeInfo()));
            }

            // explicit static setter mismatch
            {
                var i = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
                i.WithExplicitSetter(typeof(_Errors_ExplicitStaticSetter_Mismatch).GetTypeInfo(), "Val", (Setter)typeof(_Errors_ExplicitStaticSetter_Mismatch).GetMethod("SetVal"));

                var sB = SurrogateTypeDescriber.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, i.ToManualTypeDescriber(), TypeDescribers.Default);
                sB.WithSurrogateType(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticSetter_Mismatch).GetTypeInfo());

                var s = sB.ToSurrogateTypeDescriber();
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo()));
            }

            // explicit static setter arity mismatch
            {
                var i = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
                i.WithExplicitSetter(typeof(_Errors_ExplicitStaticSetter_ArityMismatch).GetTypeInfo(), "Val", (Setter)typeof(_Errors_ExplicitStaticSetter_ArityMismatch).GetMethod("SetVal"));

                var sB = SurrogateTypeDescriber.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, i.ToManualTypeDescriber(), TypeDescribers.Default);
                sB.WithSurrogateType(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticSetter_ArityMismatch).GetTypeInfo());

                var s = sB.ToSurrogateTypeDescriber();
                Assert.Throws<InvalidOperationException>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo()));
            }

            // explicit getter mismatch
            {
                var i = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
                i.WithExplicitGetter(typeof(_Errors_ExplicitGetter_Mismatch).GetTypeInfo(), "Val", (Getter)typeof(_Errors_ExplicitGetter_Mismatch).GetMethod("GetVal"));

                var sB = SurrogateTypeDescriber.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, i.ToManualTypeDescriber(), TypeDescribers.Default);
                sB.WithSurrogateType(typeof(_Errors_ExplicitGetter).GetTypeInfo(), typeof(_Errors_ExplicitGetter_Mismatch).GetTypeInfo());

                var s = sB.ToSurrogateTypeDescriber();
                Assert.Throws<ArgumentException>(() => s.EnumerateMembersToSerialize(typeof(_Errors_ExplicitGetter).GetTypeInfo()));
            }

            // explicit static getter mismatch
            {
                var i = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
                i.WithExplicitGetter(typeof(_Errors_ExplicitStaticGetter_Mismatch).GetTypeInfo(), "Val", (Getter)typeof(_Errors_ExplicitStaticGetter_Mismatch).GetMethod("GetVal"));

                var sB = SurrogateTypeDescriber.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, i.ToManualTypeDescriber(), TypeDescribers.Default);
                sB.WithSurrogateType(typeof(_Errors_ExplicitStaticGetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticGetter_Mismatch).GetTypeInfo());

                var s = sB.ToSurrogateTypeDescriber();
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
            var s1B = SurrogateTypeDescriber.CreateBuilder();
            var s2B = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.Throw);

            s1B.WithSurrogateType(typeof(_ToStrings1).GetTypeInfo(), typeof(_ToStrings1_Surrogate).GetTypeInfo());
            s1B.WithSurrogateType(typeof(_ToStrings2).GetTypeInfo(), typeof(_ToStrings2_Surrogate).GetTypeInfo());

            s2B.WithSurrogateType(typeof(_ToStrings1).GetTypeInfo(), typeof(_ToStrings1_Surrogate).GetTypeInfo());
            s2B.WithSurrogateType(typeof(_ToStrings2).GetTypeInfo(), typeof(_ToStrings2_Surrogate).GetTypeInfo());

            var s1 = s1B.ToSurrogateTypeDescriber();
            var s2 = s2B.ToSurrogateTypeDescriber();

            var str1 = s1.ToString();
            var str2 = s2.ToString();

            // can be one of two things, because dictionary order isn't stable
            var str1Legal = new[]
                {
                    "SurrogateTypeDescriber using type describer DefaultTypeDescriber Shared Instance which delegates when no surrogate registered and falls back to DefaultTypeDescriber Shared Instance if no surrogate is registered and uses Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2, Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1",
                    "SurrogateTypeDescriber using type describer DefaultTypeDescriber Shared Instance which delegates when no surrogate registered and falls back to DefaultTypeDescriber Shared Instance if no surrogate is registered and uses Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1, Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2",
                };

            Assert.Contains(str1, str1Legal);

            // can be one of two things, because dictionary order isn't stable
            var str2Legal = new[]
                {
                    "SurrogateTypeDescriber using type describer DefaultTypeDescriber Shared Instance which throws when no surrogate registered and falls back to DefaultTypeDescriber Shared Instance if no surrogate is registered and uses Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2, Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1",
                    "SurrogateTypeDescriber using type describer DefaultTypeDescriber Shared Instance which throws when no surrogate registered and falls back to DefaultTypeDescriber Shared Instance if no surrogate is registered and uses Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings1, Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2_Surrogate for Cesil.Tests.SurrogateTypeDescriberTests+_ToStrings2"
                };

            Assert.Contains(str2, str2Legal);
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
            var surrogateB = SurrogateTypeDescriberBuilder.CreateBuilder(SurrogateTypeDescriberFallbackBehavior.UseFallback, lower);
            var surrogate = surrogateB.ToSurrogateTypeDescriber();

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
            Assert.Equal(EmitDefaultValue.Yes, SurrogateTypeDescriber.GetEquivalentEmitFor(true));
            Assert.Equal(EmitDefaultValue.No, SurrogateTypeDescriber.GetEquivalentEmitFor(false));

            Assert.Equal(BindingFlags.Public | BindingFlags.Static, SurrogateTypeDescriber.GetEquivalentFlagsFor(true, true));
            Assert.Equal(BindingFlags.Public | BindingFlags.Instance, SurrogateTypeDescriber.GetEquivalentFlagsFor(true, false));
            Assert.Equal(BindingFlags.NonPublic | BindingFlags.Static, SurrogateTypeDescriber.GetEquivalentFlagsFor(false, true));
            Assert.Equal(BindingFlags.NonPublic | BindingFlags.Instance, SurrogateTypeDescriber.GetEquivalentFlagsFor(false, false));

            Assert.Equal(MemberRequired.Yes, SurrogateTypeDescriber.GetEquivalentRequiredFor(true));
            Assert.Equal(MemberRequired.No, SurrogateTypeDescriber.GetEquivalentRequiredFor(false));
        }
    }
#pragma warning restore IDE1006
}
