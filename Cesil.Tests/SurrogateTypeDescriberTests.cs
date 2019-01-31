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
        class _Simple_Real
        {
            public string Foo { get; set; }
        }

        class _Simple_Surrogate
        {
            [DataMember(Name = "bar")]
            public string Foo { get; set; }
        }

        [Fact]
        public void Simple_Deserialize()
        {
            var surrogate = new SurrogateTypeDescriber(false);
            surrogate.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo());

            // maps!
            {
                var res = surrogate.EnumerateMembersToDeserialize(typeof(_Simple_Real).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.Null(a.Field);
                        Assert.Equal(DeserializableMember.GetDefaultParser(typeof(string).GetTypeInfo()), a.Parser);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Real).GetProperty(nameof(_Simple_Real.Foo)).SetMethod, a.Setter);
                        Assert.False(a.IsRequired);
                    }
                );
            }

            // doesn't map
            {
                var res = surrogate.EnumerateMembersToDeserialize(typeof(_Simple_Surrogate).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.Null(a.Field);
                        Assert.Equal(DeserializableMember.GetDefaultParser(typeof(string).GetTypeInfo()), a.Parser);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Surrogate).GetProperty(nameof(_Simple_Surrogate.Foo)).SetMethod, a.Setter);
                        Assert.False(a.IsRequired);
                    }
                );
            }
        }

        [Fact]
        public void Simple_Serialize()
        {
            var surrogate = new SurrogateTypeDescriber(false);
            surrogate.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo());

            // maps!
            {
                var res = surrogate.EnumerateMembersToSerialize(typeof(_Simple_Real).GetTypeInfo());

                Assert.Collection(
                    res,
                    a =>
                    {
                        Assert.True(a.EmitDefaultValue);
                        Assert.Null(a.Field);
                        Assert.Equal(SerializableMember.GetDefaultFormatter(typeof(string).GetTypeInfo()), a.Formatter);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Real).GetProperty(nameof(_Simple_Real.Foo)).GetMethod, a.Getter);
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
                        Assert.Null(a.Field);
                        Assert.Equal(SerializableMember.GetDefaultFormatter(typeof(string).GetTypeInfo()), a.Formatter);
                        Assert.Equal("bar", a.Name);
                        Assert.Equal(typeof(_Simple_Surrogate).GetProperty(nameof(_Simple_Surrogate.Foo)).GetMethod, a.Getter);
                        Assert.Null(a.ShouldSerialize);
                    }
                );
            }
        }

#pragma warning disable 0649
        class _Errors_Field
        {
            public string Foo;
        }


        class _Errors_Field_Missing
        {
            [DataMember]
            public string Bar;
        }

        class _Errors_Field_Mismatch
        {
            [DataMember]
            public int Foo;
        }
#pragma warning restore 0649

        class _Errors_Property
        {
            public string Fizz { get; set; }
        }

        class _Errors_Property_Missing
        {
            public string Buzz { get; set; }
        }

        class _Errors_Property_Mismatch
        {
            public int Fizz { get; set; }
        }
        
        class _Errors_ExplicitSetter
        {
            public void SetVal(string val) { }
        }

        class _Errors_ExplicitSetter_Mismatch
        {
            public void SetVal(int val) { }
        }

        class _Errors_ExplicitStaticSetter
        {
            public static void SetVal(string val) { }
        }

        class _Errors_ExplicitStaticSetter_Mismatch
        {
            public static void SetVal(int val) { }
        }

        class _Errors_ExplicitStaticSetter_ArityMismatch
        {
            public static void SetVal(_Errors_ExplicitStaticSetter foo, int val) { }
        }

        class _Errors_ExplicitGetter
        {
            public string GetVal() => "";
        }

        class _Errors_ExplicitGetter_Mismatch
        {
            public int GetVal() => 0;
        }

        class _Errors_ExplicitStaticGetter
        {
            public static string GetVal() => "";
        }

        class _Errors_ExplicitStaticGetter_Mismatch
        {
            public static int GetVal() => 0;
        }

        class _Errors_ExplicitStaticGetter_ArityMismatch: _Errors_ExplicitStaticGetter
        {
            public static string GetVal(_Errors_ExplicitStaticGetter row) => "";
        }

        [Fact]
        public void Errors()
        {
            // null inner describer
            {
                Assert.ThrowsAny<Exception>(() => new SurrogateTypeDescriber(null, false));
            }

            // null forType
            {
                var s = new SurrogateTypeDescriber(false);
                Assert.ThrowsAny<Exception>(() => s.AddSurrogateType(null, typeof(_Simple_Real).GetTypeInfo()));
            }

            // null surrogateType
            {
                var s = new SurrogateTypeDescriber(false);
                Assert.ThrowsAny<Exception>(() => s.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), null));
            }

            // same registration
            {
                var s = new SurrogateTypeDescriber(false);
                Assert.ThrowsAny<Exception>(() => s.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Real).GetTypeInfo()));
            }

            // double registration
            {
                var s = new SurrogateTypeDescriber(false);
                s.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo());

                Assert.ThrowsAny<Exception>(() => s.AddSurrogateType(typeof(_Simple_Real).GetTypeInfo(), typeof(_Simple_Surrogate).GetTypeInfo()));
            }

            // no registration
            {
                var s = new SurrogateTypeDescriber(true);
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToSerialize(typeof(object).GetTypeInfo()).ToList());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToDeserialize(typeof(object).GetTypeInfo()).ToList());
            }

            // field missing
            {
                var s = new SurrogateTypeDescriber(false);
                s.AddSurrogateType(typeof(_Errors_Field).GetTypeInfo(), typeof(_Errors_Field_Missing).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Field).GetTypeInfo()).ToList());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Field).GetTypeInfo()).ToList());
            }

            // field type mismatch
            {
                var s = new SurrogateTypeDescriber(false);
                s.AddSurrogateType(typeof(_Errors_Field).GetTypeInfo(), typeof(_Errors_Field_Mismatch).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Field).GetTypeInfo()).ToList());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Field).GetTypeInfo()).ToList());
            }

            // prop missing
            {
                var s = new SurrogateTypeDescriber(false);
                s.AddSurrogateType(typeof(_Errors_Property).GetTypeInfo(), typeof(_Errors_Property_Missing).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Property).GetTypeInfo()).ToList());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Property).GetTypeInfo()).ToList());
            }

            // prop type mismatch
            {
                var s = new SurrogateTypeDescriber(false);
                s.AddSurrogateType(typeof(_Errors_Property).GetTypeInfo(), typeof(_Errors_Property_Mismatch).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToSerialize(typeof(_Errors_Property).GetTypeInfo()).ToList());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_Property).GetTypeInfo()).ToList());
            }

            // explicit setter mismatch
            {
                var i = new ManualTypeDescriber(false);
                i.AddExplicitSetter(typeof(_Errors_ExplicitSetter_Mismatch).GetTypeInfo(), "Val", typeof(_Errors_ExplicitSetter_Mismatch).GetMethod("SetVal"));

                var s = new SurrogateTypeDescriber(i, false);
                s.AddSurrogateType(typeof(_Errors_ExplicitSetter).GetTypeInfo(), typeof(_Errors_ExplicitSetter_Mismatch).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_ExplicitSetter).GetTypeInfo()).ToList());
            }

            // explicit static setter mismatch
            {
                var i = new ManualTypeDescriber(false);
                i.AddExplicitSetter(typeof(_Errors_ExplicitStaticSetter_Mismatch).GetTypeInfo(), "Val", typeof(_Errors_ExplicitStaticSetter_Mismatch).GetMethod("SetVal"));

                var s = new SurrogateTypeDescriber(i, false);
                s.AddSurrogateType(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticSetter_Mismatch).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo()).ToList());
            }

            // explicit static setter arity mismatch
            {
                var i = new ManualTypeDescriber(false);
                i.AddExplicitSetter(typeof(_Errors_ExplicitStaticSetter_ArityMismatch).GetTypeInfo(), "Val", typeof(_Errors_ExplicitStaticSetter_ArityMismatch).GetMethod("SetVal"));

                var s = new SurrogateTypeDescriber(i, false);
                s.AddSurrogateType(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticSetter_ArityMismatch).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToDeserialize(typeof(_Errors_ExplicitStaticSetter).GetTypeInfo()).ToList());
            }

            // explicit getter mismatch
            {
                var i = new ManualTypeDescriber(false);
                i.AddExplicitGetter(typeof(_Errors_ExplicitGetter_Mismatch).GetTypeInfo(), "Val", typeof(_Errors_ExplicitGetter_Mismatch).GetMethod("GetVal"));

                var s = new SurrogateTypeDescriber(i, false);
                s.AddSurrogateType(typeof(_Errors_ExplicitGetter).GetTypeInfo(), typeof(_Errors_ExplicitGetter_Mismatch).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToSerialize(typeof(_Errors_ExplicitGetter).GetTypeInfo()).ToList());
            }

            // explicit static getter mismatch
            {
                var i = new ManualTypeDescriber(false);
                i.AddExplicitGetter(typeof(_Errors_ExplicitStaticGetter_Mismatch).GetTypeInfo(), "Val", typeof(_Errors_ExplicitStaticGetter_Mismatch).GetMethod("GetVal"));

                var s = new SurrogateTypeDescriber(i, false);
                s.AddSurrogateType(typeof(_Errors_ExplicitStaticGetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticGetter_Mismatch).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToSerialize(typeof(_Errors_ExplicitStaticGetter).GetTypeInfo()).ToList());
            }

            // explicit static getter arity mismatch
            {
                var i = new ManualTypeDescriber(false);
                i.AddExplicitGetter(typeof(_Errors_ExplicitStaticGetter_ArityMismatch).GetTypeInfo(), "Val", typeof(_Errors_ExplicitStaticGetter_ArityMismatch).GetMethod("GetVal"));

                var s = new SurrogateTypeDescriber(i, false);
                s.AddSurrogateType(typeof(_Errors_ExplicitStaticGetter).GetTypeInfo(), typeof(_Errors_ExplicitStaticGetter_ArityMismatch).GetTypeInfo());
                Assert.ThrowsAny<Exception>(() => s.EnumerateMembersToSerialize(typeof(_Errors_ExplicitStaticGetter).GetTypeInfo()).ToList());
            }
        }
    }
#pragma warning restore IDE1006
}
