using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Cesil.Tests
{
    public class PublicInterfaceTests
    {
        private static IEnumerable<TypeInfo> AllTypes()
        {
            var ts = typeof(Configuration).Assembly.GetTypes();
            foreach (var t in ts)
            {
                yield return t.GetTypeInfo();
            }
        }

        private static IEnumerable<TypeInfo> AllPrivateTypes()
        {
            foreach (var t in AllTypes())
            {
                if (!t.IsPublic)
                {
                    yield return t;
                }
            }
        }

        private static IEnumerable<TypeInfo> AllPubicTypes()
        {
            foreach (var t in AllTypes())
            {
                if (t.IsPublic)
                {
                    yield return t;
                }
            }
        }

        private static IEnumerable<MethodInfo> AllPublicMethods()
        {
            var types = AllPubicTypes();

            foreach (var t in types)
            {
                var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                var mtds = t.GetMethods(flags);

                foreach (var mtd in mtds)
                {
                    // protected (potentially), but on a type that's sealed
                    if (t.IsSealed && mtd.IsFamily) continue;

                    var declared = mtd.DeclaringType;

                    if (!declared.FullName.StartsWith("Cesil.")) continue;

                    yield return mtd;
                }
            }
        }

        private static IEnumerable<TypeInfo> AllShouldBeEquatableTypes()
        {
            var types = AllPubicTypes();

            foreach (var t in types)
            {
                if (t.IsEnum) continue;
                if (t.IsAbstract && t.IsSealed) continue;   // ie. is static, apparently?
                if (t.IsInterface) continue;
                if (t.BaseType == typeof(MulticastDelegate)) continue;
                if (t.IsAbstract) continue;

                var shouldBeEquatable =
                    t.GetCustomAttribute<NotEquatableAttribute>() == null &&
                    t.GetCustomAttribute<IntentionallyExtensibleAttribute>() == null;

                if (!shouldBeEquatable) continue;

                yield return t;
            }
        }

        [Fact]
        public void DeliberatelyExtensible()
        {
            foreach (var t in AllPubicTypes())
            {
                if (t.IsAbstract) continue;
                if (t.IsInterface) continue;
                if (t.BaseType == typeof(MulticastDelegate)) continue;

                if (t.IsSealed) continue;

                var c = t.GetCustomAttribute<IntentionallyExtensibleAttribute>();
                Assert.True(c != null, $"Missing explanation {t.FullName}");
            }
        }

        [Fact]
        public void NoArraysBoolsOrNumbers()
        {
            foreach (var mtd in AllPublicMethods())
            {
                var declaring = mtd.DeclaringType.GetTypeInfo();
                if (declaring.BaseType == typeof(Delegate)) continue;
                if (declaring.BaseType == typeof(MulticastDelegate)) continue;

                var name = mtd.Name;

                var isRuntimeInterface =
                    name.Equals(nameof(object.Equals)) ||
                    name.Equals(nameof(object.GetHashCode)) ||
                    name.Equals("op_Equality") ||
                    name.Equals("op_Inequality");

                if (isRuntimeInterface) continue;

                if (mtd.IsSpecialName)
                {
                    // probably part of a property?
                    if(name.StartsWith("get_") || name.StartsWith("set_"))
                    {
                        // yeah, it's a property
                        var propName = name.Substring(4);
                        var prop = mtd.DeclaringType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                        if(prop != null)
                        {
                            var skip = prop.GetCustomAttribute<IntentionallyExposedPrimitiveAttribute>() != null;
                            if (skip) continue;
                        }
                    }
                }

                var ret = mtd.ReturnParameter;
                if(ret != null)
                {
                    var skip = ret.GetCustomAttribute<IntentionallyExposedPrimitiveAttribute>() != null;
                    if (skip) continue;

                    var rType = ret.ParameterType;
                    rType = Nullable.GetUnderlyingType(rType) ?? rType;

                    Assert.False(rType.IsArray, $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is an array");
                    Assert.False(rType == typeof(bool), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a bool");
                    Assert.False(rType == typeof(byte), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a byte");
                    Assert.False(rType == typeof(sbyte), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a sbyte");
                    Assert.False(rType == typeof(short), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a short");
                    Assert.False(rType == typeof(ushort), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a ushort");
                    Assert.False(rType == typeof(int), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a int");
                    Assert.False(rType == typeof(uint), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a uint");
                    Assert.False(rType == typeof(long), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a long");
                    Assert.False(rType == typeof(ulong), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a ulong");
                    Assert.False(rType == typeof(float), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a float");
                    Assert.False(rType == typeof(double), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a double");
                    Assert.False(rType == typeof(decimal), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} is a decimal");

                    if (rType.IsGenericType && !rType.IsGenericTypeDefinition)
                    {
                        var args = rType.GetGenericArguments();
                        foreach (var rSubType in args)
                        {
                            var rSubTypeFinal = Nullable.GetUnderlyingType(rSubType) ?? rSubType;

                            Assert.False(rSubTypeFinal.IsArray, $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has an array type parameter");
                            Assert.False(rSubTypeFinal == typeof(bool), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has a bool type parameter");
                            Assert.False(rSubTypeFinal == typeof(byte), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has a byte type parameter");
                            Assert.False(rSubTypeFinal == typeof(sbyte), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has a sbyte type parameter");
                            Assert.False(rSubTypeFinal == typeof(short), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has a short type parameter");
                            Assert.False(rSubTypeFinal == typeof(ushort), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has an ushort type parameter");
                            Assert.False(rSubTypeFinal == typeof(int), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has an int type parameter");
                            Assert.False(rSubTypeFinal == typeof(uint), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has an uint type parameter");
                            Assert.False(rSubTypeFinal == typeof(long), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has a long type parameter");
                            Assert.False(rSubTypeFinal == typeof(ulong), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has an ulong type parameter");
                            Assert.False(rSubTypeFinal == typeof(float), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has a float type parameter");
                            Assert.False(rSubTypeFinal == typeof(double), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has an double type parameter");
                            Assert.False(rSubTypeFinal == typeof(decimal), $"Return of {mtd.DeclaringType.Name}.{mtd.Name} has an decimal type parameter");
                        }
                    }
                }
                
                var ps = mtd.GetParameters();
                for (var i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];

                    var skip = p.GetCustomAttribute<IntentionallyExposedPrimitiveAttribute>() != null;
                    if (skip) continue;

                    var pType = p.ParameterType;
                    pType = Nullable.GetUnderlyingType(pType) ?? pType;

                    Assert.False(pType.IsArray, $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is an array");
                    Assert.False(pType == typeof(bool), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a bool");
                    Assert.False(pType == typeof(byte), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a byte");
                    Assert.False(pType == typeof(sbyte), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a sbyte");
                    Assert.False(pType == typeof(short), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a short");
                    Assert.False(pType == typeof(ushort), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a ushort");
                    Assert.False(pType == typeof(int), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a int");
                    Assert.False(pType == typeof(uint), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a uint");
                    Assert.False(pType == typeof(long), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a long");
                    Assert.False(pType == typeof(ulong), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a ulong");
                    Assert.False(pType == typeof(float), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a float");
                    Assert.False(pType == typeof(double), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a double");
                    Assert.False(pType == typeof(decimal), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) is a decimal");

                    if(pType.IsGenericType && !pType.IsGenericTypeDefinition)
                    {
                        var args = pType.GetGenericArguments();
                        foreach(var pSubType in args)
                        {
                            var pSubTypeFinal = Nullable.GetUnderlyingType(pSubType) ?? pSubType;

                            Assert.False(pSubTypeFinal.IsArray, $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has an array type parameter");
                            Assert.False(pSubTypeFinal == typeof(bool), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has a bool type parameter");
                            Assert.False(pSubTypeFinal == typeof(byte), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has a byte type parameter");
                            Assert.False(pSubTypeFinal == typeof(sbyte), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has a sbyte type parameter");
                            Assert.False(pSubTypeFinal == typeof(short), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has a short type parameter");
                            Assert.False(pSubTypeFinal == typeof(ushort), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has an ushort type parameter");
                            Assert.False(pSubTypeFinal == typeof(int), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has an int type parameter");
                            Assert.False(pSubTypeFinal == typeof(uint), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has an uint type parameter");
                            Assert.False(pSubTypeFinal == typeof(long), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has a long type parameter");
                            Assert.False(pSubTypeFinal == typeof(ulong), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has an ulong type parameter");
                            Assert.False(pSubTypeFinal == typeof(float), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has a float type parameter");
                            Assert.False(pSubTypeFinal == typeof(double), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has an double type parameter");
                            Assert.False(pSubTypeFinal == typeof(decimal), $"Parameter #{i} ({p.Name} on {mtd.DeclaringType.Name}.{mtd.Name}) has an decimal type parameter");
                        }
                    }
                }
            }
        }

        [Fact]
        public void EquatableWithHashCodes()
        {
            var equatable = AllShouldBeEquatableTypes().ToHashSet();

            // anything public needs to be equatable, plus a == and != 
            //  *OR*
            // have a good reason not to, and then it had better NOT have those things
            foreach (var t in AllPubicTypes())
            {
                if (t.IsEnum) continue;
                if (t.IsAbstract && t.IsSealed) continue;   // ie. is static, apparently?
                if (t.IsInterface) continue;
                if (t.BaseType == typeof(MulticastDelegate)) continue;
                if (t.IsAbstract) continue;

                var shouldBeEquatable = equatable.Contains(t);

                var hasEquatable = false;
                foreach (var impl in t.GetInterfaces())
                {
                    if (!impl.IsGenericType) continue;

                    var gen = impl.GetGenericTypeDefinition();
                    if (gen != typeof(IEquatable<>)) continue;

                    var arg = impl.GetGenericArguments();
                    if (arg[0] != t) continue;

                    hasEquatable = true;
                }

                if (shouldBeEquatable)
                {
                    Assert.True(hasEquatable, $"{t.Name} should be equatable");
                }
                else
                {
                    Assert.False(hasEquatable, $"{t.Name} should not be equatable");
                }

                var equals = t.GetMethod(nameof(object.Equals), new[] { typeof(object) });
                var implementedEquals = equals.DeclaringType.GetTypeInfo();

                if (shouldBeEquatable)
                {
                    Assert.True(t.Equals(implementedEquals), $"Should directly implement .Equals ({t.Name})");
                }
                else
                {
                    Assert.False(t.Equals(implementedEquals), $"Should NOT directly implement .Equals ({t.Name})");
                }

                var getHashCode = t.GetMethod(nameof(object.GetHashCode));
                var implementedGetHashCode = getHashCode.DeclaringType.GetTypeInfo();

                if (shouldBeEquatable)
                {
                    Assert.True(t.Equals(implementedGetHashCode), $"Should directly implement .GetHashCode ({t.Name})");
                }
                else
                {
                    Assert.False(t.Equals(implementedGetHashCode), $"Should NOT directly implement .GetHashCode ({t.Name})");
                }

                var opEq = t.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static);
                var opNeq = t.GetMethod("op_Inequality", BindingFlags.Public | BindingFlags.Static);
                if (shouldBeEquatable)
                {
                    Assert.True(opEq != null, $"Should directly implement == ({t.Name})");
                    var eqPs = opEq.GetParameters();
                    Assert.Collection(
                        eqPs,
                        p1 => Assert.True(t.Equals(p1.ParameterType), $"Should directly implement == ({t.Name})"),
                        p2 => Assert.True(t.Equals(p2.ParameterType), $"Should directly implement == ({t.Name})")
                    );

                    Assert.True(opNeq != null, $"Should directly implement != ({t.Name})");
                    var neqPs = opNeq.GetParameters();
                    Assert.Collection(
                        neqPs,
                        p1 => Assert.True(t.Equals(p1.ParameterType), $"Should directly implement != ({t.Name})"),
                        p2 => Assert.True(t.Equals(p2.ParameterType), $"Should directly implement != ({t.Name})")
                    );
                }
                else
                {
                    Assert.True(opEq == null, $"Should NOT directly implement == ({t.Name})");
                    Assert.True(opNeq == null, $"Should NOT directly implement != ({t.Name})");
                }
            }
        }

        private class _EqualityNullSafe
        {
#pragma warning disable CS0649
            public int Foo;
#pragma warning restore CS0649
        }

        [Fact]
        public void EqualityNullSafe()
        {
            foreach (var t in AllShouldBeEquatableTypes())
            {
                // null makes no sense for these
                if (t.IsValueType) continue;

                if (t == typeof(Options))
                {
                    var ex = Options.Default;
                    var exNull1 = default(Options);
                    var exNull2 = default(Options);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(DeserializableMember))
                {
                    var ex = DeserializableMember.ForField(typeof(_EqualityNullSafe).GetField(nameof(_EqualityNullSafe.Foo)));
                    var exNull1 = default(DeserializableMember);
                    var exNull2 = default(DeserializableMember);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(SerializableMember))
                {
                    var ex = SerializableMember.ForField(typeof(_EqualityNullSafe).GetField(nameof(_EqualityNullSafe.Foo)));
                    var exNull1 = default(SerializableMember);
                    var exNull2 = default(SerializableMember);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(DynamicRowConverter))
                {
                    var ex =
                        DynamicRowConverter.ForDelegate(
                            (object _, in ReadContext __, out int res) =>
                            {
                                res = 1;
                                return true;
                            }
                        );
                    var exNull1 = default(DynamicRowConverter);
                    var exNull2 = default(DynamicRowConverter);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(Formatter))
                {
                    var ex =
                        Formatter.ForDelegate(
                            (int _, in WriteContext __, IBufferWriter<char> ___) =>
                            {
                                return true;
                            }
                        );
                    var exNull1 = default(Formatter);
                    var exNull2 = default(Formatter);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(Getter))
                {
                    var ex = Getter.ForField(typeof(_EqualityNullSafe).GetField(nameof(_EqualityNullSafe.Foo)));
                    var exNull1 = default(Getter);
                    var exNull2 = default(Getter);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(InstanceBuilder))
                {
                    var ex = InstanceBuilder.ForParameterlessConstructor(typeof(_EqualityNullSafe).GetConstructor(Type.EmptyTypes));
                    var exNull1 = default(InstanceBuilder);
                    var exNull2 = default(InstanceBuilder);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(Parser))
                {
                    var ex =
                        Parser.ForDelegate(
                            (ReadOnlySpan<char> _, in ReadContext __, out int x) =>
                            {
                                x = 1;
                                return true;
                            }
                        ); ;
                    var exNull1 = default(Parser);
                    var exNull2 = default(Parser);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(Reset))
                {
                    var ex = Reset.ForDelegate(() => { });
                    var exNull1 = default(Reset);
                    var exNull2 = default(Reset);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(Setter))
                {
                    var ex =
                        Setter.ForDelegate(
                            (int _) => { }
                        );
                    var exNull1 = default(Setter);
                    var exNull2 = default(Setter);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(ShouldSerialize))
                {
                    var ex =
                        ShouldSerialize.ForDelegate(
                            () => true
                        );
                    var exNull1 = default(ShouldSerialize);
                    var exNull2 = default(ShouldSerialize);
                    Assert.NotNull(ex);
                    Assert.Null(exNull1);
                    Assert.Null(exNull2);
                    Assert.False(ex.Equals(exNull1));
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else
                {
                    Assert.True(false, $"({t.Name}) doesn't have a test for null checks");
                }
            }
        }

        [Fact]
        public void HelpfulToString()
        {
            // anything public needs a nice ToString()
            foreach (var t in AllPubicTypes())
            {
                if (t.IsEnum) continue;
                if (t.IsAbstract && t.IsSealed) continue;   // ie. is static, apparently?
                if (t.IsInterface) continue;
                if (t.BaseType == typeof(MulticastDelegate)) continue;
                if (t.IsAbstract) continue;

                var toStr = t.GetMethod(nameof(object.ToString));
                var implementedToString = toStr.DeclaringType.GetTypeInfo();

                Assert.Equal(t, implementedToString);
            }

            // anything that's PRIVATE but implements a PUBLIC interface needs a nice ToString()
            var pubInterfaces = AllPubicTypes().Where(t => t.IsInterface).ToList();
            foreach (var t in AllPrivateTypes())
            {
                if (t.IsEnum) continue;
                if (t.IsAbstract && t.IsSealed) continue;   // ie. is static, apparently?
                if (t.IsInterface) continue;
                if (t.BaseType == typeof(MulticastDelegate)) continue;
                if (t.IsAbstract) continue;
                if (t.CustomAttributes.Any(c => c.AttributeType == typeof(CompilerGeneratedAttribute))) continue;

                var impledInterfaces =
                    t.GetInterfaces()
                      .Except(new[] { typeof(IDisposable), typeof(IAsyncDisposable) })  // these don't count
                      .Select(i => i.IsGenericType && !i.IsGenericTypeDefinition ? i.GetGenericTypeDefinition() : i)
                      .ToList();
                var needsToStr =
                    impledInterfaces.Any(i => i.FullName.StartsWith("System.") || pubInterfaces.Contains(i));

                if (!needsToStr) continue;

                var toStr = t.GetMethod(nameof(object.ToString));
                var implementedToString = toStr.DeclaringType.GetTypeInfo();

                Assert.Equal(t, implementedToString);
            }
        }

        [Fact]
        public void EnumsSmallNonZeroAndUnique()
        {
            foreach(var t in AllPubicTypes())
            {
                if (!t.IsEnum) continue;

                var underlying = Enum.GetUnderlyingType(t);
                Assert.True(typeof(byte) == underlying, t.Name);

                var vals = Enum.GetValues(t).Cast<byte>().ToArray();

                Assert.Equal(vals.Length, vals.Distinct().Count());

                foreach(var v in vals)
                {
                    Assert.False(0 == v, $"{t.Name} has a 0 value, which will make debugging a pain");
                }
            }
        }
    }
}
