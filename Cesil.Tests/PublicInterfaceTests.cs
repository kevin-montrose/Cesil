using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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

        private static IEnumerable<ConstructorInfo> AllPublicConstructors()
        {
            foreach(var t in AllPubicTypes())
            {
                foreach(var cons in t.GetConstructors())
                {
                    if(cons.IsPublic)
                    {
                        yield return cons;
                    }
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

#if RELEASE
        [Fact]
        public void ReleaseHasNoITestableAsyncProvider()
        {
            foreach(var t in AllTypes())
            {
                Assert.False(t.ImplementedInterfaces.Any(i => i == typeof(ITestableAsyncProvider)), t.Name);
            }
        }
#endif

        [Fact]
        public void ThrowOnlyNoInlining()
        {
            foreach(var mtd in typeof(Throw).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (mtd.DeclaringType != typeof(Throw)) continue;

                Assert.True(mtd.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining));
            }
        }


        [Fact]
        public void DisposableHelperAllAggressivelyInlined()
        {
            foreach (var mtd in typeof(DisposableHelper).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (mtd.DeclaringType != typeof(DisposableHelper)) continue;

                Assert.True(mtd.MethodImplementationFlags.HasFlag(MethodImplAttributes.AggressiveInlining));
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
            foreach(var cons in AllPublicConstructors())
            {
                var ps = cons.GetParameters();
                for(var i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];

                    var skip = p.GetCustomAttribute<IntentionallyExposedPrimitiveAttribute>() != null;
                    if (skip) continue;

                    var pType = p.ParameterType;
                    pType = Nullable.GetUnderlyingType(pType) ?? pType;

                    Assert.False(pType.IsArray, $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is an array");
                    Assert.False(pType == typeof(bool), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a bool");
                    Assert.False(pType == typeof(byte), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a byte");
                    Assert.False(pType == typeof(sbyte), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a sbyte");
                    Assert.False(pType == typeof(short), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a short");
                    Assert.False(pType == typeof(ushort), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a ushort");
                    Assert.False(pType == typeof(int), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a int");
                    Assert.False(pType == typeof(uint), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a uint");
                    Assert.False(pType == typeof(long), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a long");
                    Assert.False(pType == typeof(ulong), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a ulong");
                    Assert.False(pType == typeof(float), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a float");
                    Assert.False(pType == typeof(double), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a double");
                    Assert.False(pType == typeof(decimal), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) is a decimal");

                    if (pType.IsGenericType && !pType.IsGenericTypeDefinition)
                    {
                        var args = pType.GetGenericArguments();
                        foreach (var pSubType in args)
                        {
                            var pSubTypeFinal = Nullable.GetUnderlyingType(pSubType) ?? pSubType;

                            Assert.False(pSubTypeFinal.IsArray, $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has an array type parameter");
                            Assert.False(pSubTypeFinal == typeof(bool), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has a bool type parameter");
                            Assert.False(pSubTypeFinal == typeof(byte), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has a byte type parameter");
                            Assert.False(pSubTypeFinal == typeof(sbyte), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has a sbyte type parameter");
                            Assert.False(pSubTypeFinal == typeof(short), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has a short type parameter");
                            Assert.False(pSubTypeFinal == typeof(ushort), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has an ushort type parameter");
                            Assert.False(pSubTypeFinal == typeof(int), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has an int type parameter");
                            Assert.False(pSubTypeFinal == typeof(uint), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has an uint type parameter");
                            Assert.False(pSubTypeFinal == typeof(long), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has a long type parameter");
                            Assert.False(pSubTypeFinal == typeof(ulong), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has an ulong type parameter");
                            Assert.False(pSubTypeFinal == typeof(float), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has a float type parameter");
                            Assert.False(pSubTypeFinal == typeof(double), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has an double type parameter");
                            Assert.False(pSubTypeFinal == typeof(decimal), $"Parameter #{i} ({p.Name} on {cons.DeclaringType.Name}) has an decimal type parameter");
                        }
                    }
                }
            }

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
                    if (name.StartsWith("get_") || name.StartsWith("set_"))
                    {
                        // yeah, it's a property
                        var propName = name.Substring(4);
                        var prop = mtd.DeclaringType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                        if (prop != null)
                        {
                            var skip = prop.GetCustomAttribute<IntentionallyExposedPrimitiveAttribute>() != null;
                            if (skip) continue;
                        }
                    }
                }

                var ret = mtd.ReturnParameter;
                if (ret != null)
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

                    if (pType.IsGenericType && !pType.IsGenericTypeDefinition)
                    {
                        var args = pType.GetGenericArguments();
                        foreach (var pSubType in args)
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
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(DeserializableMember))
                {
                    var ex = DeserializableMember.ForField(typeof(_EqualityNullSafe).GetField(nameof(_EqualityNullSafe.Foo)));
                    var exNull1 = default(DeserializableMember);
                    var exNull2 = default(DeserializableMember);
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(SerializableMember))
                {
                    var ex = SerializableMember.ForField(typeof(_EqualityNullSafe).GetField(nameof(_EqualityNullSafe.Foo)));
                    var exNull1 = default(SerializableMember);
                    var exNull2 = default(SerializableMember);
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
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
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
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
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(Getter))
                {
                    var ex = Getter.ForField(typeof(_EqualityNullSafe).GetField(nameof(_EqualityNullSafe.Foo)));
                    var exNull1 = default(Getter);
                    var exNull2 = default(Getter);
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(InstanceBuilder))
                {
                    var ex = InstanceBuilder.ForParameterlessConstructor(typeof(_EqualityNullSafe).GetConstructor(Type.EmptyTypes));
                    var exNull1 = default(InstanceBuilder);
                    var exNull2 = default(InstanceBuilder);
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
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
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(Reset))
                {
                    var ex = Reset.ForDelegate(() => { });
                    var exNull1 = default(Reset);
                    var exNull2 = default(Reset);
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
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
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
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
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else
                {
                    Assert.True(false, $"({t.Name}) doesn't have a test for null checks");
                }
            }

            static void CommonNonOperatorChecks<T>(T ex, T exNull1, T exNull2)
                where T: class, IEquatable<T>
            {
                Assert.NotNull(ex);
                Assert.True(ex.Equals(ex));
                Assert.True(ex.Equals((object)ex));
                Assert.Null(exNull1);
                Assert.Null(exNull2);
                Assert.False(ex.Equals(exNull1));
                Assert.False(ex.Equals((object)exNull1));
            }
        }

        private class _HelpfulToString
        {
            public string Foo { get; set; }

            public _HelpfulToString() { }
            public _HelpfulToString(dynamic row) { }
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

                InvokeToString(t);
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

                InvokeToString(t);
            }

            // actually call the ToString(), make sure it doesn't throw, and make sure it mentions the _type name_
            static void InvokeToString(TypeInfo t)
            {
                string msg;
                if (t == typeof(Reader<>))
                {
                    msg = InvokeToString_Reader();
                }
                else if (t == typeof(Writer<>))
                {
                    msg = InvokeToString_Writer();
                }
                else if (t == typeof(AsyncReader<>))
                {
                    msg = InvokeToString_AsyncReader().Result;
                }
                else if (t == typeof(AsyncWriter<>))
                {
                    msg = InvokeToString_AsyncWriter().Result;
                }
                else if (t == typeof(AsyncDynamicWriter))
                {
                    msg = InvokeToString_AsyncDynamicWriter().Result;
                }
                else if (t == typeof(AsyncDynamicReader))
                {
                    msg = InvokeToString_AsyncDynamicReader().Result;
                }
                else if (t == typeof(DynamicReader))
                {
                    msg = InvokeToString_DynamicReader();
                }
                else if (t == typeof(DynamicWriter))
                {
                    msg = InvokeToString_DynamicWriter();
                }
                else if (t == typeof(ColumnIdentifier))
                {
                    msg = InvokeToString_ColumnIdentifier();
                }
                else if (t == typeof(ReadContext))
                {
                    msg = InvokeToString_ReadContext();
                }
                else if (t == typeof(WriteContext))
                {
                    msg = InvokeToString_WriteContext();
                }
                else if (t == typeof(ReadResult<>))
                {
                    msg = InvokeToString_ReadResult().Result;
                }
                else if (t == typeof(ReadWithCommentResult<>))
                {
                    msg = InvokeToString_ReadWithCommentResult();
                }
                else if (t == typeof(ConcreteBoundConfiguration<>))
                {
                    msg = InvokeToString_ConcreteBoundConfiguration();
                }
                else if (t == typeof(DynamicBoundConfiguration))
                {
                    msg = InvokeToString_DynamicBoundConfiguration();
                }
                else if (t == typeof(Options))
                {
                    msg = InvokeToString_Options();
                }
                else if (t == typeof(OptionsBuilder))
                {
                    msg = InvokeToString_OptionsBuilder();
                }
                else if (t == typeof(DefaultTypeDescriber))
                {
                    msg = InvokeToString_DefaultTypeDescriber();
                }
                else if (t == typeof(ManualTypeDescriber))
                {
                    msg = InvokeToString_ManualTypeDescriber();
                }
                else if (t == typeof(SurrogateTypeDescriber))
                {
                    msg = InvokeToString_SurrogateTypeDescriber();
                }
                else if (t == typeof(DeserializableMember))
                {
                    msg = InvokeToString_DeserializableMember();
                }
                else if (t == typeof(SerializableMember))
                {
                    msg = InvokeToString_SerializableMember();
                }
                else if (t == typeof(DynamicCellValue))
                {
                    msg = InvokeToString_DynamicCellValue();
                }
                else if (t == typeof(DynamicRowConverter))
                {
                    msg = InvokeToString_DynamicRowConverter();
                }
                else if (t == typeof(Formatter))
                {
                    msg = InvokeToString_Formatter();
                }
                else if (t == typeof(Getter))
                {
                    msg = InvokeToString_Getter();
                }
                else if (t == typeof(InstanceBuilder))
                {
                    msg = InvokeToString_InstanceBuilder();
                }
                else if (t == typeof(Parser))
                {
                    msg = InvokeToString_Parser();
                }
                else if (t == typeof(Reset))
                {
                    msg = InvokeToString_Reset();
                }
                else if (t == typeof(Setter))
                {
                    msg = InvokeToString_Setter();
                }
                else if (t == typeof(ShouldSerialize))
                {
                    msg = InvokeToString_ShouldSerialize();
                }
                else if (t == typeof(AsyncEnumerable<>))
                {
                    msg = InvokeToString_AsyncEnumerable().Result;
                }
                else if (t == typeof(AsyncEnumerator<>))
                {
                    msg = InvokeToString_AsyncEnumerator().Result;
                }
                else if (t == typeof(Enumerable<>))
                {
                    msg = InvokeToString_Enumerable();
                }
                else if (t == typeof(Enumerator<>))
                {
                    msg = InvokeToString_Enumerator();
                }
                else if (t == typeof(DynamicCell))
                {
                    msg = InvokeToString_DynamicCell();
                }
                else if (t == typeof(DynamicRow))
                {
                    msg = InvokeToString_DynamicRow();
                }
                else if (t == typeof(DynamicRowEnumerable<>))
                {
                    msg = InvokeToString_DynamicRowEnumerable();
                }
                else if (t == typeof(DynamicRowEnumerator<>))
                {
                    msg = InvokeToString_DynamicRowEnumerator();
                }
                else if (t == typeof(DynamicRowEnumerableNonGeneric))
                {
                    msg = InvokeToString_DynamicRowEnumerableNonGeneric();
                }
                else if (t == typeof(DynamicRowEnumeratorNonGeneric))
                {
                    msg = InvokeToString_DynamicRowEnumeratorNonGeneric();
                }
                else if (t == typeof(MaxSizedBufferWriter))
                {
                    msg = InvokeToString_MaxSizedBufferWriter();
                }
                else if (t == typeof(DynamicRow.DynamicColumnEnumerator))
                {
                    msg = InvokeToString_DynamicColumnEnumerator();
                }
                else if (t == typeof(DynamicRow.DynamicColumnEnumerable))
                {
                    msg = InvokeToString_DynamicColumnEnumerable();
                }
                else if (t == typeof(HeadersReader<>.HeaderEnumerator))
                {
                    msg = InvokeToString_HeaderEnumerator();
                }
                else if (t == typeof(DynamicRowMemberNameEnumerable))
                {
                    msg = InvokeToString_DynamicRowMemberNameEnumerable();
                }
                else if (t == typeof(DynamicRowMemberNameEnumerator))
                {
                    msg = InvokeToString_DynamicRowMemberNameEnumerator();
                }
                else
                {
                    Assert.True(false, $"No test for ToString() on {t}");
                    // just for control flow, won't be reached
                    return;
                }

                Assert.NotNull(msg);

                string shouldStartWith = t.Name;
                var backtickIx = shouldStartWith.IndexOf('`');
                if (backtickIx != -1)
                {
                    shouldStartWith = shouldStartWith.Substring(0, backtickIx);
                }

                shouldStartWith += " ";

                Assert.StartsWith(shouldStartWith, msg);
            }

            static string InvokeToString_DynamicRowMemberNameEnumerator()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0] as DynamicRow;

                    var e = new DynamicRowMemberNameEnumerable(row);

                    return e.GetEnumerator().ToString();
                }
            }

            static string InvokeToString_DynamicRowMemberNameEnumerable()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0] as DynamicRow;

                    var e = new DynamicRowMemberNameEnumerable(row);

                    return e.ToString();
                }
            }

            static string InvokeToString_HeaderEnumerator()
            {
                var config = Configuration.For<_HelpfulToString>();

                var e = new HeadersReader<_HelpfulToString>.HeaderEnumerator(0, ReadOnlyMemory<char>.Empty);

                return e.ToString();
            }

            static string InvokeToString_DynamicColumnEnumerable()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0] as DynamicRow;

                    var e = new DynamicRow.DynamicColumnEnumerable(row);

                    return e.ToString();
                }
            }

            static string InvokeToString_DynamicColumnEnumerator()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0] as DynamicRow;

                    using (var e = new DynamicRow.DynamicColumnEnumerator(row))
                    {
                        return e.ToString();
                    }
                }
            }

            static string InvokeToString_MaxSizedBufferWriter()
            {
                using (var r = new MaxSizedBufferWriter(MemoryPool<char>.Shared, 100))
                {
                    return r.ToString();
                }
            }

            static string InvokeToString_DynamicRowEnumeratorNonGeneric()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0];

                    var e = (System.Collections.IEnumerable)row;

                    var i = e.GetEnumerator();
                    return i.ToString();
                }
            }

            static string InvokeToString_DynamicRowEnumerableNonGeneric()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0];

                    var e = (System.Collections.IEnumerable)row;

                    return e.ToString();
                }
            }

            static string InvokeToString_DynamicRowEnumerator()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0];

                    var e = (IEnumerable<string>)row;

                    using (var i = e.GetEnumerator())
                    {
                        return i.ToString();
                    }
                }
            }

            static string InvokeToString_DynamicRowEnumerable()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0];

                    var e = (IEnumerable<string>)row;

                    return e.ToString();
                }
            }

            static string InvokeToString_DynamicRow()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0];

                    return (row as DynamicRow).ToString();
                }
            }

            static string InvokeToString_DynamicCell()
            {
                var config = Configuration.ForDynamic(Options.DynamicDefault.NewBuilder().WithReadHeader(ReadHeaders.Never).Build());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0];
                    var cell = row[0];

                    return (cell as DynamicCell).ToString();
                }
            }

            static string InvokeToString_Enumerator()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var e = csv.EnumerateAll();
                    using (var i = e.GetEnumerator())
                    {
                        return i.ToString();
                    }
                }
            }

            static string InvokeToString_Enumerable()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var e = csv.EnumerateAll();
                    return e.ToString();
                }
            }

            static async Task<string> InvokeToString_AsyncEnumerator()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringReader("foo"))
                await using (var csv = config.CreateAsyncReader(str))
                {
                    var e = csv.EnumerateAllAsync();
                    await using (var i = e.GetAsyncEnumerator())
                    {
                        return i.ToString();
                    }
                }
            }

            static async Task<string> InvokeToString_AsyncEnumerable()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringReader("foo"))
                await using (var csv = config.CreateAsyncReader(str))
                {
                    var e = csv.EnumerateAllAsync();
                    return e.ToString();
                }
            }

            static string InvokeToString_ShouldSerialize()
            {
                var ss = ShouldSerialize.ForDelegate(() => true);

                return ss.ToString();
            }

            static string InvokeToString_Setter()
            {
                var s = Setter.ForMethod(typeof(_HelpfulToString).GetProperty(nameof(_HelpfulToString.Foo)).SetMethod);

                return s.ToString();
            }

            static string InvokeToString_Reset()
            {
                var r = Reset.ForDelegate(() => { });

                return r.ToString();
            }

            static string InvokeToString_Parser()
            {
                var p = Parser.GetDefault(typeof(string).GetTypeInfo());

                return p.ToString();
            }

            static string InvokeToString_InstanceBuilder()
            {
                var i = InstanceBuilder.ForParameterlessConstructor(typeof(_HelpfulToString).GetConstructor(Type.EmptyTypes));

                return i.ToString();
            }

            static string InvokeToString_Getter()
            {
                var g = Getter.ForMethod(typeof(_HelpfulToString).GetProperty(nameof(_HelpfulToString.Foo)).GetMethod);

                return g.ToString();
            }

            static string InvokeToString_Formatter()
            {
                var f = Formatter.GetDefault(typeof(string).GetTypeInfo());

                return f.ToString();
            }

            static string InvokeToString_DynamicRowConverter()
            {
                var d = DynamicRowConverter.ForConstructorTakingDynamic(typeof(_HelpfulToString).GetConstructor(new[] { typeof(object) }));

                return d.ToString();
            }

            static string InvokeToString_DynamicCellValue()
            {
                var d = DynamicCellValue.Create("foo", "bar", Formatter.GetDefault(typeof(string).GetTypeInfo()));

                return d.ToString();
            }

            static string InvokeToString_SerializableMember()
            {
                var ser = SerializableMember.ForProperty(typeof(_HelpfulToString).GetProperty(nameof(_HelpfulToString.Foo)));

                return ser.ToString();
            }

            static string InvokeToString_DeserializableMember()
            {
                var des = DeserializableMember.ForProperty(typeof(_HelpfulToString).GetProperty(nameof(_HelpfulToString.Foo)));

                return des.ToString();
            }

            static string InvokeToString_SurrogateTypeDescriber()
            {
                return (new SurrogateTypeDescriber(TypeDescribers.Default)).ToString();
            }

            static string InvokeToString_ManualTypeDescriber()
            {
                return (new ManualTypeDescriber()).ToString();
            }

            static string InvokeToString_DefaultTypeDescriber()
            {
                return TypeDescribers.Default.ToString();
            }

            static string InvokeToString_OptionsBuilder()
            {
                return Options.Default.NewBuilder().ToString();
            }

            static string InvokeToString_Options()
            {
                return Options.Default.ToString();
            }

            static string InvokeToString_DynamicBoundConfiguration()
            {
                var config = Configuration.ForDynamic();

                return config.ToString();
            }

            static string InvokeToString_ConcreteBoundConfiguration()
            {
                var config = Configuration.For<_HelpfulToString>();

                return config.ToString();
            }

            static async Task<string> InvokeToString_ReadResult()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringReader("foo"))
                await using (var csv = config.CreateAsyncReader(str))
                {
                    var res = await csv.TryReadAsync();
                    return res.ToString();
                }
            }

            static string InvokeToString_ReadWithCommentResult()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.TryReadWithComment();
                    return res.ToString();
                }
            }

            static string InvokeToString_WriteContext()
            {
                var c = WriteContext.WritingColumn(4, (ColumnIdentifier)19, null);
                return c.ToString();
            }

            static string InvokeToString_ReadContext()
            {
                var c = ReadContext.ConvertingColumn(2, (ColumnIdentifier)4, "foo");
                return c.ToString();
            }

            static string InvokeToString_ColumnIdentifier()
            {
                var c = ColumnIdentifier.Create(1, "foo");
                return c.ToString();
            }

            static string InvokeToString_Reader()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    return csv.ToString();
                }
            }

            static string InvokeToString_DynamicReader()
            {
                var config = Configuration.ForDynamic();

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    return csv.ToString();
                }
            }

            static string InvokeToString_Writer()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringWriter())
                using (var csv = config.CreateWriter(str))
                {
                    return csv.ToString();
                }
            }

            static string InvokeToString_DynamicWriter()
            {
                var config = Configuration.ForDynamic();

                using (var str = new StringWriter())
                using (var csv = config.CreateWriter(str))
                {
                    return csv.ToString();
                }
            }

            static async Task<string> InvokeToString_AsyncReader()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringReader("foo"))
                await using (var csv = config.CreateAsyncReader(str))
                {
                    return csv.ToString();
                }
            }

            static async Task<string> InvokeToString_AsyncDynamicReader()
            {
                var config = Configuration.ForDynamic();

                using (var str = new StringReader("foo"))
                await using (var csv = config.CreateAsyncReader(str))
                {
                    return csv.ToString();
                }
            }

            static async Task<string> InvokeToString_AsyncWriter()
            {
                var config = Configuration.For<_HelpfulToString>();

                using (var str = new StringWriter())
                await using (var csv = config.CreateAsyncWriter(str))
                {
                    return csv.ToString();
                }
            }

            static async Task<string> InvokeToString_AsyncDynamicWriter()
            {
                var config = Configuration.ForDynamic();

                using (var str = new StringWriter())
                await using (var csv = config.CreateAsyncWriter(str))
                {
                    return csv.ToString();
                }
            }
        }

        [Fact]
        public void EnumsSmallNonZeroAndUnique()
        {
            foreach (var t in AllPubicTypes())
            {
                if (!t.IsEnum) continue;

                var underlying = Enum.GetUnderlyingType(t);
                Assert.True(typeof(byte) == underlying, t.Name);

                var vals = Enum.GetValues(t).Cast<byte>().ToArray();

                Assert.Equal(vals.Length, vals.Distinct().Count());

                foreach (var v in vals)
                {
                    Assert.False(0 == v, $"{t.Name} has a 0 value, which will make debugging a pain");
                }
            }
        }
    }
}
