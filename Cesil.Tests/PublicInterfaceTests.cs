using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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
                // skip things generated for code coverage
                if (t.FullName.StartsWith("Coverlet")) continue;

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

        private static IEnumerable<TypeInfo> AllPublicDelegates()
        {
            var types = AllPubicTypes();

            foreach (var t in types)
            {
                if (t.BaseType == typeof(MulticastDelegate))
                {
                    yield return t;
                }
            }
        }

        private static IEnumerable<ConstructorInfo> AllPublicConstructors()
        {
            foreach (var t in AllPubicTypes())
            {
                foreach (var cons in t.GetConstructors())
                {
                    if (cons.IsPublic)
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

        [Fact]
        public void PrivateTypesHaveNonPublicMembers()
        {
            // this doesn't really matter (internal, protected, or private types'
            //   public members are still hidden) but makes grep'ing for public
            //   easier.

            foreach (var t in AllPrivateTypes())
            {
                // ignore interfaces, delegates, enums, and compiler generated types
                if (t.IsInterface) continue;
                if (t.BaseType == typeof(MulticastDelegate)) continue;
                if (t.IsEnum) continue;
                if (t.Name.Contains("<")) continue;

                if (t == typeof(DynamicRowConstructor))
                {
                    Console.WriteLine();
                }

                var pubMethods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                Check(t, pubMethods);
                var pubFields = t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                Check(t, pubFields);
                var pubProps = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                Check(t, pubProps);
                var pubEvents = t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                Check(t, pubEvents);
                var pubCons = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                Check(t, pubCons);
            }

            static void Check(TypeInfo onType, IEnumerable<MemberInfo> members)
            {
                members = RemoveInherited(onType, members);

                if (!members.Any()) return;

                var type = members.FirstOrDefault()?.MemberType;

                var mems = string.Join(", ", members.Select(t => t.Name));

                var msg = $"{onType.FullName} has public {type}: {mems}";

                Assert.True(false, msg);
            }

            static IEnumerable<MemberInfo> RemoveInherited(TypeInfo onType, IEnumerable<MemberInfo> members)
            {
                var onTypeMethods = onType.GetMethods();

                var fromInterfaces = new HashSet<MemberInfo>();
                foreach (var i in onType.ImplementedInterfaces)
                {
                    var map = onType.GetInterfaceMap(i);

                    foreach (var implMtd in map.TargetMethods)
                    {
                        // default methods
                        if (implMtd.DeclaringType == map.InterfaceType) continue;

                        var implName = implMtd.Name;
                        var dotIx = implName.LastIndexOf('.');
                        if (dotIx != -1)
                        {
                            implName = implName.Substring(dotIx + 1);
                        }

                        var match =
                            onTypeMethods
                                .SingleOrDefault(
                                    m =>
                                    {
                                        if (m.Name != implName) return false;
                                        if (m.ReturnType != implMtd.ReturnType) return false;

                                        var mParams = m.GetParameters();
                                        var implParams = implMtd.GetParameters();

                                        if (mParams.Length != implParams.Length) return false;

                                        for (var i = 0; i < implParams.Length; i++)
                                        {
                                            if (mParams[i].ParameterType != implParams[i].ParameterType) return false;
                                        }

                                        return true;
                                    }
                                );

                        if (match != null)
                        {
                            fromInterfaces.Add(match);
                        }
                    }
                }

                foreach (var m in members)
                {
                    bool implOfInterface;

                    if (m is PropertyInfo p)
                    {
                        var methodsCovered = true;

                        if (p.GetMethod != null && p.GetMethod.IsPublic)
                        {
                            methodsCovered &= fromInterfaces.Contains(p.GetMethod);
                        }

                        if (p.SetMethod != null && p.SetMethod.IsPublic)
                        {
                            methodsCovered &= fromInterfaces.Contains(p.SetMethod);
                        }

                        implOfInterface = methodsCovered;
                    }
                    else
                    {
                        implOfInterface = fromInterfaces.Contains(m);
                    }

                    if (implOfInterface) continue;

                    if (m.DeclaringType?.FullName?.StartsWith("System.") ?? false) continue;
                    if (m.DeclaringType?.FullName?.StartsWith("Microsoft.") ?? false) continue;
                    if (m.DeclaringType.IsPublic) continue;

                    if (m is MethodInfo mtd)
                    {
                        if (mtd.Name.StartsWith("op_")) continue;

                        var baseMtd = mtd.GetBaseDefinition();

                        if (baseMtd.DeclaringType?.FullName?.StartsWith("System.") ?? false) continue;
                        if (baseMtd.DeclaringType?.FullName?.StartsWith("Microsoft.") ?? false) continue;
                        if (baseMtd.DeclaringType.IsPublic) continue;
                    }

                    yield return m;
                }
            }
        }

#if RELEASE
        [Fact]
        public void ReleaseHasNoITestableAsyncProvider()
        {
            foreach (var t in AllTypes())
            {
                Assert.False(t.ImplementedInterfaces.Any(i => i == typeof(ITestableAsyncProvider)), t.Name);
            }
        }
#endif

        [Fact]
        public void ThrowOnlyNoInlining()
        {
            foreach (var mtd in typeof(Throw).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (mtd.DeclaringType != typeof(Throw)) continue;

                Assert.True(mtd.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining));
            }
        }

        [Fact]
        public void ThrowOnlyDoesNotReturn()
        {
            foreach (var mtd in typeof(Throw).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (mtd.DeclaringType != typeof(Throw)) continue;

                var attr = mtd.GetCustomAttribute<DoesNotReturnAttribute>();
                Assert.NotNull(attr);
            }
        }


        [Fact]
        public void DisposableHelperAllAggressivelyInlined()
        {
            foreach (var mtd in typeof(DisposableHelper).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (mtd.DeclaringType != typeof(DisposableHelper)) continue;

                if (mtd.GetCustomAttribute<ConditionalAttribute>()?.ConditionString == "DEBUG") continue;

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
            foreach (var cons in AllPublicConstructors())
            {
                var ps = cons.GetParameters();
                for (var i = 0; i < ps.Length; i++)
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
                else if (t == typeof(InstanceProvider))
                {
                    var ex = InstanceProvider.ForParameterlessConstructor(typeof(_EqualityNullSafe).GetConstructor(Type.EmptyTypes));
                    var exNull1 = default(InstanceProvider);
                    var exNull2 = default(InstanceProvider);
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
                    var ex = Reset.ForDelegate((in ReadContext _) => { });
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
                            (int _, in ReadContext __) => { }
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
                            (in WriteContext _) => true
                        );
                    var exNull1 = default(ShouldSerialize);
                    var exNull2 = default(ShouldSerialize);
                    CommonNonOperatorChecks(ex, exNull1, exNull2);
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(ManualTypeDescriber))
                {
                    var ex = ManualTypeDescriber.CreateBuilder().ToManualTypeDescriber();
                    var exNull1 = default(ManualTypeDescriber);
                    var exNull2 = default(ManualTypeDescriber);

                    CommonNonOperatorChecks(ex, exNull1, exNull2);
                    Assert.False(ex == exNull1);
                    Assert.False(exNull1 == ex);
                    Assert.True(exNull1 == exNull2);
                }
                else if (t == typeof(SurrogateTypeDescriber))
                {
                    var ex = SurrogateTypeDescriberBuilder.CreateBuilder().ToSurrogateTypeDescriber();
                    var exNull1 = default(SurrogateTypeDescriber);
                    var exNull2 = default(SurrogateTypeDescriber);

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
                where T : class, IEquatable<T>
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
#pragma warning disable CS0649
            public int Bar1;
            public int Bar2;
#pragma warning restore CS0649

            public string Foo { get; set; }

            public _HelpfulToString() { }
#pragma warning disable IDE0060
            public _HelpfulToString(dynamic row) { }
#pragma warning restore IDE0060
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
                string msg2 = null;
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
                    msg = InvokeToString_ManualTypeDescriber1();
                    msg2 = InvokeToString_ManualTypeDescriber2();
                }
                else if (t == typeof(ManualTypeDescriberBuilder))
                {
                    msg = InvokeToString_ManualTypeDescriberBuilder();
                }
                else if (t == typeof(SurrogateTypeDescriber))
                {
                    msg = InvokeToString_SurrogateTypeDescriber();
                }
                else if (t == typeof(SurrogateTypeDescriberBuilder))
                {
                    msg = InvokeToString_SurrogateTypeDescriberBuilder();
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
                else if (t == typeof(InstanceProvider))
                {
                    msg = InvokeToString_InstanceProvider();
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
                else if (t == typeof(Enumerable<>))
                {
                    msg = InvokeToString_Enumerable();
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
                else if (t == typeof(AsyncEnumerableAdapter<>))
                {
                    msg = InvokeToString_AsyncEnumerableAdapter();
                }
                else if (t == typeof(EmptyMemoryOwner))
                {
                    msg = InvokeToString_EmptyMemoryOwner();
                }
                else if (t == typeof(PassthroughRowEnumerable))
                {
                    msg = InvokeToString_PassthroughRowEnumerable();
                }
                else if (t == typeof(PassthroughRowEnumerator))
                {
                    msg = InvokeToString_PassthroughRowEnumerator();
                }
                else if (t == typeof(ImpossibleException))
                {
                    msg = InvokeToString_ImpossibleException();
                }
                else if (t == typeof(MemberOrderHelper<>))
                {
                    msg = InvokeToString_MemberOrderHelper();
                }
                else if (t == typeof(MemoryPoolProviders.DefaultMemoryPoolProvider))
                {
                    msg = InvokeToString_DefaultMemoryPoolProvider();
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

                if (msg2 != null)
                {
                    Assert.StartsWith(shouldStartWith, msg2);
                }
            }

            static string InvokeToString_DefaultMemoryPoolProvider()
            {
                return MemoryPoolProviders.Default.ToString();
            }

            static string InvokeToString_MemberOrderHelper()
            {
                var a = MemberOrderHelper<string>.Create();

                return a.ToString();
            }

            static string InvokeToString_ImpossibleException()
            {
                var exc = ImpossibleException.Create("Some reason!", "a", "b", 3);

                return exc.ToString();
            }

            static string InvokeToString_PassthroughRowEnumerable()
            {
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    IEnumerable<dynamic> row = res[0];
                    var p = (PassthroughRowEnumerable)row;

                    return p.ToString();
                }
            }

            static string InvokeToString_PassthroughRowEnumerator()
            {
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    IEnumerable<dynamic> row = res[0];
                    var p = (PassthroughRowEnumerable)row;
                    var e = p.GetEnumerator();
                    var ee = (PassthroughRowEnumerator)e;

                    return ee.ToString();
                }
            }

            static string InvokeToString_EmptyMemoryOwner()
            {
                var m = EmptyMemoryOwner.Singleton;
                return m.ToString();
            }

            static string InvokeToString_AsyncEnumerableAdapter()
            {
                var e = new AsyncEnumerableAdapter<string>(new string[0]);
                return e.ToString();
            }

            static string InvokeToString_DynamicRowMemberNameEnumerator()
            {
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

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
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

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

                var e = new HeadersReader<_HelpfulToString>.HeaderEnumerator(0, ReadOnlyMemory<char>.Empty, WhitespaceTreatments.Preserve);

                return e.ToString();
            }

            static string InvokeToString_DynamicColumnEnumerable()
            {
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

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
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

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
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

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
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

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
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

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
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

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
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

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
                var config = Configuration.ForDynamic(Options.CreateBuilder(Options.DynamicDefault).WithReadHeader(ReadHeader.Never).ToOptions());

                using (var str = new StringReader("foo"))
                using (var csv = config.CreateReader(str))
                {
                    var res = csv.ReadAll();
                    var row = res[0];
                    var cell = row[0];

                    return (cell as DynamicCell).ToString();
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
                var ss = ShouldSerialize.ForDelegate((in WriteContext _) => true);

                return ss.ToString();
            }

            static string InvokeToString_Setter()
            {
                var s = Setter.ForMethod(typeof(_HelpfulToString).GetProperty(nameof(_HelpfulToString.Foo)).SetMethod);

                return s.ToString();
            }

            static string InvokeToString_Reset()
            {
                var r = Reset.ForDelegate((in ReadContext _) => { });

                return r.ToString();
            }

            static string InvokeToString_Parser()
            {
                var p = Parser.GetDefault(typeof(string).GetTypeInfo());

                return p.ToString();
            }

            static string InvokeToString_InstanceProvider()
            {
                var i = InstanceProvider.ForParameterlessConstructor(typeof(_HelpfulToString).GetConstructor(Type.EmptyTypes));

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
                var sb = SurrogateTypeDescriberBuilder.CreateBuilder();
                var s = sb.ToSurrogateTypeDescriber();

                return s.ToString();
            }

            static string InvokeToString_SurrogateTypeDescriberBuilder()
            {
                var sb = SurrogateTypeDescriberBuilder.CreateBuilder();
                sb.WithSurrogateType(typeof(string).GetTypeInfo(), typeof(object).GetTypeInfo());
                sb.WithSurrogateType(typeof(int).GetTypeInfo(), typeof(long).GetTypeInfo());

                return sb.ToString();
            }

            static string InvokeToString_ManualTypeDescriberBuilder()
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder();
                return m.ToString();
            }

            static string InvokeToString_ManualTypeDescriber1()
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
                var b1 = InstanceProvider.ForDelegate((in ReadContext _, out string x) => { x = ""; return true; });
                var b2 = InstanceProvider.ForDelegate((in ReadContext _, out int x) => { x = 0; return true; });

                var f1 = typeof(_HelpfulToString).GetField(nameof(_HelpfulToString.Bar1));
                var f2 = typeof(_HelpfulToString).GetField(nameof(_HelpfulToString.Bar2));

                m.WithInstanceProvider(b1);
                m.WithInstanceProvider(b2);
                m.WithDeserializableField(f1);
                m.WithDeserializableField(f2);
                m.WithSerializableField(f1);
                m.WithSerializableField(f2);

                var b = m.ToManualTypeDescriber();

                return b.ToString();
            }

            static string InvokeToString_ManualTypeDescriber2()
            {
                var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.Throw);
                var b1 = InstanceProvider.ForDelegate((in ReadContext _, out string x) => { x = ""; return true; });
                var b2 = InstanceProvider.ForDelegate((in ReadContext _, out int x) => { x = 0; return true; });

                var f1 = typeof(_HelpfulToString).GetField(nameof(_HelpfulToString.Bar1));
                var f2 = typeof(_HelpfulToString).GetField(nameof(_HelpfulToString.Bar2));

                m.WithInstanceProvider(b1);
                m.WithInstanceProvider(b2);
                m.WithDeserializableField(f1);
                m.WithDeserializableField(f2);
                m.WithSerializableField(f1);
                m.WithSerializableField(f2);

                var b = m.ToManualTypeDescriber();

                return b.ToString();
            }

            static string InvokeToString_DefaultTypeDescriber()
            {
                return TypeDescribers.Default.ToString();
            }

            static string InvokeToString_OptionsBuilder()
            {
                return Options.CreateBuilder(Options.Default).ToString();
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
                var c = WriteContext.WritingColumn(Options.Default, 4, (ColumnIdentifier)19, null);
                return c.ToString();
            }

            static string InvokeToString_ReadContext()
            {
                var c = ReadContext.ConvertingColumn(Options.Default, 2, (ColumnIdentifier)4, "foo");
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

                if (!t.CustomAttributes.Any(x => x.AttributeType == typeof(FlagsAttribute)))
                {
                    foreach (var v in vals)
                    {
                        Assert.False(0 == v, $"{t.Name} has a 0 value, which will make debugging a pain");
                    }
                }
            }
        }

        [Fact]
        public void NullableReferencesInPublicTypes()
        {
            foreach (var t in AllPubicTypes())
            {
                // skip delegates
                if (t.BaseType == typeof(MulticastDelegate)) continue;

                foreach (var prop in t.GetProperties())
                {
                    var pType = prop.PropertyType.GetTypeInfo();
                    if (pType.IsValueType) continue;

                    var thing = $"{t.Name}.{prop.Name} property";

                    var pAttrs = prop.CustomAttributes.ToList();
                    var propIsNullable = IsNullable(thing, t, null, pAttrs);

                    if (propIsNullable)
                    {
                        var allowed = pAttrs.SingleOrDefault(p => p.AttributeType == typeof(NullableExposedAttribute));
                        Assert.True(allowed != null, $"{thing} nullable without documentation");
                    }
                }

                foreach (var c in t.GetConstructors())
                {
                    foreach (var p in c.GetParameters())
                    {
                        if (p.ParameterType.IsValueType) continue;

                        var thing = $"{t.Name} constructor parameter {p.Name}";

                        var pAttrs = p.CustomAttributes.ToList();

                        var parameterIsNullable = p.Attributes.HasFlag(ParameterAttributes.Optional) || IsNullable(thing, t, null, pAttrs);

                        if (parameterIsNullable)
                        {
                            var allowed = pAttrs.SingleOrDefault(p => p.AttributeType == typeof(NullableExposedAttribute));
                            Assert.True(allowed != null, $"{thing} nullable without documentation");
                        }
                    }
                }

                // remove implementations of interfaces that come from the BCL
                var relevantMethods =
                    t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .Where(m => m.IsPublic || m.IsFamily)
                        .ToList();
                foreach (var i in t.GetInterfaces())
                {
                    if (i.FullName.StartsWith("System."))
                    {
                        if (!t.IsInterface)
                        {
                            var iMap = t.GetInterfaceMap(i);
                            relevantMethods.RemoveAll(r => iMap.TargetMethods.Contains(r));
                        }
                    }
                }

                // remove operators
                relevantMethods.RemoveAll(r => r.Name.StartsWith("op_"));

                // remove any method declared in a base class in the BCL
                relevantMethods.RemoveAll(
                    r =>
                    {
                        var baseMtd = r.GetBaseDefinition();
                        if (baseMtd == null) return false;

                        return baseMtd.DeclaringType.FullName.StartsWith("System.");
                    }
                );

                foreach (var m in relevantMethods)
                {
                    // skip properties, they're handled elsewhere
                    if (m.Name.StartsWith("get_") || m.Name.StartsWith("set_")) continue;

                    if (!m.ReturnType.IsValueType)
                    {
                        var thing = $"{t.Name}.{m.Name} return";

                        var rAttrs = m.ReturnParameter.CustomAttributes.ToList();

                        var returnIsNullable = IsNullable(thing, t, m, rAttrs);
                        if (returnIsNullable)
                        {
                            var allowed = rAttrs.SingleOrDefault(p => p.AttributeType == typeof(NullableExposedAttribute));
                            Assert.True(allowed != null, $"{thing} nullable without documentation");
                        }
                    }

                    foreach (var p in m.GetParameters())
                    {
                        if (p.ParameterType.IsValueType) continue;

                        var thing = $"{t.Name}.{m.Name} parameter {p.Name}";

                        var pAttrs = p.CustomAttributes.ToList();

                        var parameterIsNullable = p.Attributes.HasFlag(ParameterAttributes.Optional) || IsNullable(thing, t, m, pAttrs);

                        if (parameterIsNullable)
                        {
                            var allowed = pAttrs.SingleOrDefault(p => p.AttributeType == typeof(NullableExposedAttribute));
                            Assert.True(allowed != null, $"{thing} nullable without documentation");
                        }
                    }
                }
            }

            static bool IsNullable(string thing, TypeInfo inType, MethodInfo inMethod, IEnumerable<CustomAttributeData> data)
            {
                var hasAnnotation = false;

                var ns = data.Where(d => d.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute").ToList();
                foreach (var n in ns)
                {
                    hasAnnotation = true;

                    if (AnyIndicatesNullable(n.ConstructorArguments)) return true;
                }

                if (!hasAnnotation && inMethod != null)
                {
                    ns = inMethod.CustomAttributes.Where(d => d.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute").ToList();
                    foreach (var n in ns)
                    {
                        hasAnnotation = true;

                        if (AnyIndicatesNullable(n.ConstructorArguments)) return true;
                    }
                }

                if (!hasAnnotation && inType != null)
                {
                    ns = inType.CustomAttributes.Where(d => d.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute").ToList();
                    foreach (var n in ns)
                    {
                        hasAnnotation = true;

                        if (AnyIndicatesNullable(n.ConstructorArguments)) return true;
                    }
                }

                if (!hasAnnotation)
                {
                    throw new InvalidOperationException($"{thing} was missing a nullable annotation... that makes no sense");
                }

                return false;
            }

            static bool AnyIndicatesNullable(IList<CustomAttributeTypedArgument> args)
            {
                foreach (var arg in args)
                {
                    if (arg.ArgumentType == typeof(byte))
                    {
                        if (arg.Value.Equals((byte)2)) return true;
                    }
                    else if (arg.ArgumentType == typeof(byte[]))
                    {
                        var asByteArr = (byte[])arg.Value;

                        if (asByteArr.Any(b => b == 2)) return true;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unexpected constructor for [Nullable]");
                    }
                }

                return false;
            }
        }

        private class _ParameterNamesApproved<TRow, TCollection, TValue, TOutput, TInstance, TElement>
        { }

        private class NamedComparer : IEqualityComparer<TypeInfo>
        {
            public bool Equals(TypeInfo x, TypeInfo y)
            {
                if (x == y) return true;

                if (!x.IsGenericType || !y.IsGenericType) return false;

                var xGen = x.IsGenericTypeDefinition ? x : x.GetGenericTypeDefinition();
                var yGen = y.IsGenericTypeDefinition ? y : y.GetGenericTypeDefinition();

                if (xGen != yGen) return false;

                var xArgs = x.GetGenericArguments();
                var yArgs = y.GetGenericArguments();

                for (var i = 0; i < xArgs.Length; i++)
                {
                    var xArg = xArgs[i];
                    var yArg = yArgs[i];

                    if (xArg.IsGenericParameter != yArg.IsGenericParameter) return false;

                    if (xArg.IsGenericParameter)
                    {
                        var xStr = xArg.Name;
                        var yStr = yArg.Name;

                        if (xStr != yStr) return false;
                    }
                    else
                    {
                        if (xArg != yArg) return false;
                    }
                }

                return true;
            }

            public int GetHashCode(TypeInfo obj)
            {
                if (!obj.IsGenericType) return obj.FullName.GetHashCode();

                var args = obj.GetGenericArguments();

                var ret = obj.FullName + " (" + string.Join(", ", args.Select(a => a.FullName ?? a.Name)) + ")";

                return ret.GetHashCode();
            }
        }

        [Fact]
        public void ParameterNamesApproved()
        {
            var genArgs = typeof(_ParameterNamesApproved<,,,,,>).GetGenericArguments();
            Assert.True(genArgs.All(a => a.IsGenericParameter));

            // these should be descriptive, but aren't actually important for stability
            var legalGenericArgNames = genArgs.Select(t => t.Name).ToHashSet();

            var genLookups = genArgs.ToDictionary(t => t.Name, t => t);

            // special types
            var enumerableOfRow = typeof(IEnumerable<>).MakeGenericType(genLookups["TRow"]).GetTypeInfo();
            var asyncEnumerableOfRow = typeof(IAsyncEnumerable<>).MakeGenericType(genLookups["TRow"]).GetTypeInfo();

            // names of parameters end up in the contract, because of named parameters
            // so these actually need to be "vetted" to not suck
            var legal =
                new Dictionary<TypeInfo, string[]>(new NamedComparer())
                {
                    // basic types
                    [typeof(object).GetTypeInfo()] = new[] { "obj", "context", "row", "value" },
                    [typeof(int).GetTypeInfo()] = new[] { "index", "sizeHint" },
                    [typeof(int?).GetTypeInfo()] = new[] { "sizeHint" },
                    [typeof(string).GetTypeInfo()] = new[] { "name", "comment", "path", "data", "valueSeparator" },
                    [typeof(char?).GetTypeInfo()] = new[] { "commentStart", "escapeStart", "escape" },

                    // system types
                    [typeof(CancellationToken).GetTypeInfo()] = new[] { "cancellationToken" },
                    [typeof(Encoding).GetTypeInfo()] = new[] { "encoding" },
                    [typeof(IBufferWriter<char>).GetTypeInfo()] = new[] { "writer" },
                    [typeof(IBufferWriter<byte>).GetTypeInfo()] = new[] { "writer" },
                    [typeof(TextWriter).GetTypeInfo()] = new[] { "writer" },
                    [typeof(PipeWriter).GetTypeInfo()] = new[] { "writer" },
                    [typeof(ReadOnlySequence<char>).GetTypeInfo()] = new[] { "sequence" },
                    [typeof(ReadOnlySequence<byte>).GetTypeInfo()] = new[] { "sequence" },
                    [typeof(TextReader).GetTypeInfo()] = new[] { "reader" },
                    [typeof(PipeReader).GetTypeInfo()] = new[] { "reader" },
                    [typeof(ReadOnlySpan<char>).GetTypeInfo()] = new[] { "data", "comment" },
                    [typeof(ReadOnlyMemory<char>).GetTypeInfo()] = new[] { "comment", "name" },
                    [typeof(IEnumerable<dynamic>).GetTypeInfo()] = new[] { "rows" },
                    [typeof(IAsyncEnumerable<dynamic>).GetTypeInfo()] = new[] { "rows" },

                    // reflection types
                    [typeof(TypeInfo).GetTypeInfo()] = new[] { "forType", "targetType", "surrogateType" },
                    [typeof(MethodInfo).GetTypeInfo()] = new[] { "method" },
                    [typeof(PropertyInfo).GetTypeInfo()] = new[] { "property" },
                    [typeof(FieldInfo).GetTypeInfo()] = new[] { "field" },
                    [typeof(ConstructorInfo).GetTypeInfo()] = new[] { "constructor" },
                    [typeof(ParameterInfo).GetTypeInfo()] = new[] { "parameter" },

                    // custom types
                    [typeof(Options).GetTypeInfo()] = new[] { "options" },
                    [typeof(ColumnIdentifier).GetTypeInfo()] = new[] { "column" },
                    [typeof(ITypeDescriber).GetTypeInfo()] = new[] { "typeDescriber", "surrogateTypeDescriber", "fallbackTypeDescriber" },
                    [typeof(ManualTypeDescriber).GetTypeInfo()] = new[] { "typeDescriber" },
                    [typeof(SurrogateTypeDescriber).GetTypeInfo()] = new[] { "typeDescriber" },
                    [typeof(WriteContext).GetTypeInfo()] = new[] { "context" },
                    [typeof(ReadContext).GetTypeInfo()] = new[] { "context" },
                    [typeof(RowEnding).GetTypeInfo()] = new[] { "rowEnding" },
                    [typeof(ReadHeader).GetTypeInfo()] = new[] { "readHeader" },
                    [typeof(WriteHeader).GetTypeInfo()] = new[] { "writeHeader" },
                    [typeof(WriteTrailingRowEnding).GetTypeInfo()] = new[] { "writeTrailingNewLine" },
                    [typeof(IEnumerable<ColumnIdentifier>).GetTypeInfo()] = new[] { "columns", "columnsForSetters", "columnsForParameters" },
                    [typeof(ManualTypeDescriberFallbackBehavior).GetTypeInfo()] = new[] { "fallbackBehavior" },
                    [typeof(SurrogateTypeDescriberFallbackBehavior).GetTypeInfo()] = new[] { "fallbackBehavior" },
                    [typeof(EmitDefaultValue).GetTypeInfo()] = new[] { "emitDefault" },
                    [typeof(MemberRequired).GetTypeInfo()] = new[] { "required" },
                    [typeof(DynamicRowDisposal).GetTypeInfo()] = new[] { "dynamicRowDisposal" },
                    [typeof(WhitespaceTreatments).GetTypeInfo()] = new[] { "whitespaceTreatment" },
                    [typeof(ExtraColumnTreatment).GetTypeInfo()] = new[] { "extraColumnTreatment" },
                    [typeof(IMemoryPoolProvider).GetTypeInfo()] = new[] { "memoryPoolProvider" },
                    [typeof(NullHandling).GetTypeInfo()] = new[] { "nullHandling" },

                    // wrapper types
                    [typeof(DynamicCellValue).GetTypeInfo()] = new[] { "value" },
                    [typeof(Formatter).GetTypeInfo()] = new[] { "formatter", "fallbackFormatter" },
                    [typeof(Getter).GetTypeInfo()] = new[] { "getter" },
                    [typeof(Setter).GetTypeInfo()] = new[] { "setter" },
                    [typeof(IEnumerable<Setter>).GetTypeInfo()] = new[] { "setters" },
                    [typeof(Parser).GetTypeInfo()] = new[] { "parser", "fallbackParser" },
                    [typeof(Reset).GetTypeInfo()] = new[] { "reset" },
                    [typeof(ShouldSerialize).GetTypeInfo()] = new[] { "shouldSerialize" },
                    [typeof(InstanceProvider).GetTypeInfo()] = new[] { "instanceProvider", "fallbackProvider" },
                    [typeof(DynamicRowConverter).GetTypeInfo()] = new[] { "rowConverter", "fallbackConverter" },
                    [typeof(SerializableMember).GetTypeInfo()] = new[] { "serializableMember" },
                    [typeof(DeserializableMember).GetTypeInfo()] = new[] { "deserializableMember" },
                    [typeof(Span<DynamicCellValue>).GetTypeInfo()] = new[] { "cells" },

                    // delegates
                    [typeof(FormatterDelegate<>).GetTypeInfo()] = new[] { "del" },
                    [typeof(GetterDelegate<,>).GetTypeInfo()] = new[] { "del" },
                    [typeof(StaticGetterDelegate<>).GetTypeInfo()] = new[] { "del" },
                    [typeof(InstanceProviderDelegate<>).GetTypeInfo()] = new[] { "del" },
                    [typeof(ParserDelegate<>).GetTypeInfo()] = new[] { "del" },
                    [typeof(ResetDelegate<>).GetTypeInfo()] = new[] { "del" },
                    [typeof(ResetByRefDelegate<>).GetTypeInfo()] = new[] { "del" },
                    [typeof(StaticResetDelegate).GetTypeInfo()] = new[] { "del" },
                    [typeof(StaticSetterDelegate<>).GetTypeInfo()] = new[] { "del" },
                    [typeof(SetterDelegate<,>).GetTypeInfo()] = new[] { "del" },
                    [typeof(SetterByRefDelegate<,>).GetTypeInfo()] = new[] { "del" },
                    [typeof(ShouldSerializeDelegate<>).GetTypeInfo()] = new[] { "del" },
                    [typeof(StaticShouldSerializeDelegate).GetTypeInfo()] = new[] { "del" },
                    [typeof(DynamicRowConverterDelegate<>).GetTypeInfo()] = new[] { "del" },

                    // generic args
                    [enumerableOfRow] = new[] { "rows" },
                    [asyncEnumerableOfRow] = new[] { "rows" },
                };

            var failing = new StringBuilder();

            foreach (var c in AllPublicConstructors())
            {
                var t = c.DeclaringType;

                // skip delegates
                if (t.BaseType == typeof(MulticastDelegate)) continue;

                foreach (var p in c.GetParameters())
                {
                    var pType = p.ParameterType.GetTypeInfo();
                    if (pType.IsByRef)
                    {
                        pType = pType.GetElementType().GetTypeInfo();
                    }

                    if (pType.IsGenericType && !pType.IsGenericTypeDefinition && pType.BaseType == typeof(MulticastDelegate))
                    {
                        pType = pType.GetGenericTypeDefinition().GetTypeInfo();
                    }

                    // already dealt with elsewhere
                    if (pType.IsGenericParameter) continue;

                    var pName = p.Name;

                    if (!legal.TryGetValue(pType, out var legalNames))
                    {
                        failing.AppendLine($"{pName} of {pType.Name} on constructor of {t.Name}; no entry for {pType.Name}");
                    }
                    else
                    {
                        if (!legalNames.Contains(pName))
                        {
                            failing.AppendLine($"{pName} of {pType.Name} on constructor of {t.Name}; '{pName}' is not approved");
                        }
                    }
                }
            }

            foreach (var m in AllPublicMethods())
            {
                var t = m.DeclaringType;

                // skip delegates
                if (t.BaseType == typeof(MulticastDelegate)) continue;

                // skip operators
                if (m.Name.StartsWith("op_")) continue;

                if (m.IsGenericMethodDefinition)
                {
                    var args = m.GetGenericArguments();
                    foreach (var a in args)
                    {
                        var aName = a.Name;

                        if (!legalGenericArgNames.Contains(aName))
                        {
                            failing.AppendLine($"{aName} generic arg on method {m.Name} of {t.Name}; not a legal generic parameter name");
                        }
                    }
                }

                foreach (var p in m.GetParameters())
                {
                    var pType = p.ParameterType.GetTypeInfo();
                    if (pType.IsByRef)
                    {
                        pType = pType.GetElementType().GetTypeInfo();
                    }

                    if (pType.IsGenericType && !pType.IsGenericTypeDefinition && pType.BaseType == typeof(MulticastDelegate))
                    {
                        pType = pType.GetGenericTypeDefinition().GetTypeInfo();
                    }

                    // already dealt with elsewhere
                    if (pType.IsGenericParameter) continue;

                    var pName = p.Name;

                    if (!legal.TryGetValue(pType, out var legalNames))
                    {
                        failing.AppendLine($"{pName} of {pType.Name} on method {m.Name} of {t.Name}; no entry for {pType.Name}");
                    }
                    else
                    {
                        if (!legalNames.Contains(pName))
                        {
                            failing.AppendLine($"{pName} of {pType.Name} on method {m.Name} of {t.Name}; '{pName}' is not approved");
                        }
                    }
                }
            }

            foreach (var d in AllPublicDelegates())
            {
                if (d.IsGenericTypeDefinition)
                {
                    var args = d.GetGenericArguments();
                    foreach (var a in args)
                    {
                        var aName = a.Name;

                        if (!legalGenericArgNames.Contains(aName))
                        {
                            failing.AppendLine($"{aName} generic arg on delegate {d.Name}; not a legal generic parameter name");
                        }
                    }
                }

                var invokeMtd = d.GetMethod("Invoke");

                foreach (var p in invokeMtd.GetParameters())
                {
                    var pType = p.ParameterType.GetTypeInfo();
                    if (pType.IsByRef)
                    {
                        pType = pType.GetElementType().GetTypeInfo();
                    }

                    if (pType.IsGenericType && !pType.IsGenericTypeDefinition && pType.BaseType == typeof(MulticastDelegate))
                    {
                        pType = pType.GetGenericTypeDefinition().GetTypeInfo();
                    }

                    // already dealt with elsewhere
                    if (pType.IsGenericParameter) continue;

                    var pName = p.Name;

                    if (!legal.TryGetValue(pType, out var legalNames))
                    {
                        failing.AppendLine($"{pName} of {pType.Name} on delegate {d.Name}; no entry for {pType.Name}");
                    }
                    else
                    {
                        if (!legalNames.Contains(pName))
                        {
                            failing.AppendLine($"{pName} of {pType.Name} on delegate {d.Name}; '{pName}' is not approved");
                        }
                    }
                }
            }

            foreach (var t in AllPubicTypes())
            {
                // skip delegates
                if (t.BaseType == typeof(MulticastDelegate)) continue;

                if (t.IsGenericTypeDefinition)
                {
                    var args = t.GetGenericArguments();
                    foreach (var a in args)
                    {
                        var aName = a.Name;

                        if (!legalGenericArgNames.Contains(aName))
                        {
                            failing.AppendLine($"{aName} generic arg on type {t.Name}; not a legal generic parameter name");
                        }
                    }
                }
            }

            var str = failing.ToString();
            Assert.Equal("", str);
        }

        [Fact]
        public void Builders()
        {
            // patterned after Immutable collections builder pattern

            var pubTypes = AllPubicTypes();

            var builders = pubTypes.Where(p => p.Name.EndsWith("Builder")).ToList();

            var builderToBuilt = builders.ToDictionary(b => b, b => pubTypes.Single(x => x.Name == b.Name.Substring(0, b.Name.Length - "Builder".Length)));

            foreach (var kv in builderToBuilt)
            {
                var builder = kv.Key;
                var built = kv.Value;

                var builderMtds = builder.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).ToList();
                var builtMtds = built.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                // remove types it doesn't implement
                builderMtds.RemoveAll(m => m.DeclaringType != builder);

                // remove ToString(), it's fine
                builderMtds.RemoveAll(m => m.Name == nameof(object.ToString));

                // nuke all the property _getters_; setters are still bad news
                builderMtds.RemoveAll(m => m.Name.StartsWith("get_"));

                var staticBuilderMtds = builderMtds.Where(b => b.IsStatic).ToList();
                var instanceBuilderMtds = builderMtds.Where(b => !b.IsStatic).ToList();

                var staticBuiltMtds = builtMtds.Where(b => b.IsStatic).ToArray();

                var builderProps = builder.GetProperties();

                // there's a method on the builder called CreateBuilder, that returns the builder, and takes an instance of the built
                Assert.Contains(
                    staticBuilderMtds,
                    m =>
                    {
                        if (m.Name != "CreateBuilder") return false;
                        if (m.ReturnType != builder) return false;

                        var mParams = m.GetParameters();
                        if (mParams.Length != 1) return false;

                        return mParams[0].ParameterType == built;
                    }
                );

                // all static methods on the builder return the builder, are named CreateBuilder, and there's a paired method on the built type
                Assert.All(
                    staticBuilderMtds,
                    m =>
                    {
                        Assert.Equal("CreateBuilder", m.Name);
                        Assert.Equal(builder, m.ReturnType);

                        var mParams = m.GetParameters();

                        var paired =
                            staticBuiltMtds.SingleOrDefault(
                                s =>
                                {
                                    if (s.Name != m.Name) return false;

                                    if (s.ReturnType != m.ReturnType) return false;

                                    var sParams = s.GetParameters();

                                    if (sParams.Length != mParams.Length) return false;

                                    for (var i = 0; i < sParams.Length; i++)
                                    {
                                        if (sParams[i].ParameterType != mParams[i].ParameterType) return false;
                                    }

                                    return true;
                                }
                            );

                        Assert.NotNull(paired);
                    }
                );

                // there's a single method called To<Built>() on the builder that returns the built
                Assert.Contains(
                    instanceBuilderMtds,
                    m =>
                    {
                        var name = "To" + built.Name;
                        if (name != m.Name) return false;

                        var p = m.GetParameters();
                        if (p.Length != 0) return false;

                        return m.ReturnType == built;
                    }
                );

                // all methods on builder return the builder
                var builtInstanceExceptToXXX = instanceBuilderMtds.Where(m => m.Name != "To" + built.Name).ToList();
                Assert.All(
                    builtInstanceExceptToXXX,
                    m =>
                    {
                        Assert.Equal(builder, m.ReturnType);
                        Assert.StartsWith("With", m.Name);
                    }
                );

                Assert.All(
                    builderProps,
                    p =>
                    {
                        var with = "With" + p.Name;
                        var mtd = instanceBuilderMtds.SingleOrDefault(m => m.Name == with);
                        Assert.NotNull(mtd);
                    }
                );
            }
        }

        [Fact]
        public void AllTypesAreBeforeFieldInit()
        {
            // What we want to avoid are checks everywhere before
            //   touching static members.
            //
            // This looks like .beforefieldinit, which the compiler
            //   will slap on all static types that DON'T have a 
            //   static constructor.
            //
            // For kicks, this also checks to make sure all static
            //   fields are readonly or const.

            foreach (var type in AllTypes())
            {
                // ignore interfaces, delegates, enums, and compiler generated types
                if (type.IsInterface) continue;
                if (type.BaseType == typeof(MulticastDelegate)) continue;
                if (type.IsEnum) continue;
                if (type.Name.Contains("<")) continue;

                // skip bits that we don't declare (Roslyn likes to add random attributes, for example)
                if (type.Namespace == null || !type.Namespace.StartsWith("Cesil")) continue;

                Assert.True(type.Attributes.HasFlag(TypeAttributes.BeforeFieldInit), $"{type.FullName} is not .beforefieldinit");

                var staticFields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                Assert.All(
                    staticFields,
                    f =>
                    {
                        var isReadOnly = f.IsInitOnly;
                        var isConst = f.IsLiteral;

                        var allowedDeclaration = isReadOnly || isConst;

                        Assert.True(allowedDeclaration, $"Static field {f.Name} on {type.FullName} is not readonly or const");
                    }
                );
            }
        }

        [Fact]
        public void Attributes()
        {
            new NullableExposedAttribute("shouldn't throw");
            Assert.Throws<ArgumentNullException>(() => new NullableExposedAttribute(null));

            new DoesNotEscapeAttribute("shouldn't throw");
            Assert.Throws<ArgumentNullException>(() => new DoesNotEscapeAttribute(null));

            new IntentionallyExposedPrimitiveAttribute("shouldn't throw");
            Assert.Throws<ArgumentNullException>(() => new IntentionallyExposedPrimitiveAttribute(null));

            new IntentionallyExtensibleAttribute("shouldn't throw");
            Assert.Throws<ArgumentNullException>(() => new IntentionallyExtensibleAttribute(null));

            new NotEquatableAttribute("shouldn't throw");
            Assert.Throws<ArgumentNullException>(() => new NotEquatableAttribute(null));

            new ExcludeFromCoverageAttribute("shouldn't throw");
            Assert.Throws<ArgumentNullException>(() => new ExcludeFromCoverageAttribute(null));
        }

        [Fact]
        public void LogHelperConditionals()
        {
            var logHelper = typeof(LogHelper).GetTypeInfo();
            foreach (var mtd in logHelper.GetMethods(BindingFlagsConstants.All))
            {
                // check public or internal methods that are declared by LogHelper
                var check = mtd.DeclaringType.GetTypeInfo() == logHelper && (mtd.IsAssembly || mtd.IsPublic);
                if (!check) continue;

                var cond = mtd.GetCustomAttribute<ConditionalAttribute>();
                Assert.NotNull(cond);
            }
        }
    }
}
