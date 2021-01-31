using System;
using System.Collections.Immutable;
using System.Reflection;
using Xunit;

namespace Cesil.Tests
{
    public class AheadOfTimeTypeDescriberTests
    {
#pragma warning disable CS0618 // having to fake up some generated types, so using things we shouldn't normally use
        [GeneratedSourceVersion(AheadOfTimeTypeDescriber.CURRENT_CESIL_VERSION, typeof(_BadInstanceProviders_Type1), GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer)]
        class _BadInstanceProviders_Generated1
        {
            [ConstructorInstanceProvider(typeof(string), typeof(string), 0)]
            [ConstructorInstanceProvider(typeof(int), typeof(string), 1)]
            public static bool __InstanceProvider(out _BadInstanceProviders_Type1 val)
            {
                val = default;
                return false;
            }
        }
#pragma warning restore CS0618

        class _BadInstanceProviders_Type1
        {

        }

#pragma warning disable CS0618 // having to fake up some generated types, so using things we shouldn't normally use
        [GeneratedSourceVersion(AheadOfTimeTypeDescriber.CURRENT_CESIL_VERSION, typeof(_BadInstanceProviders_Type2), GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer)]
        class _BadInstanceProviders_Generated2
        {
            [ConstructorInstanceProvider(typeof(_BadInstanceProviders_Type2), typeof(string), 0)]
            public static bool __InstanceProvider(out _BadInstanceProviders_Type2 val)
            {
                val = default;
                return false;
            }
        }
#pragma warning restore CS0618

        class _BadInstanceProviders_Type2
        {
            public _BadInstanceProviders_Type2(int a) { }
        }

        [Fact]
        public void BadInstanceProviders()
        {
            // cons provided for multiple types
            {
                var t = typeof(_BadInstanceProviders_Type1).GetTypeInfo();
                var exc = Assert.Throws<ImpossibleException>(() => TypeDescribers.AheadOfTime.GetInstanceProvider(t));

                Assert.Contains($"Generated type {typeof(_BadInstanceProviders_Generated1)} (for {t}) claims multiple constructors for an InstanceProvider.", exc.Message);
            }

            // cons provided doesn't match existing constructor
            {
                var t = typeof(_BadInstanceProviders_Type2).GetTypeInfo();
                var exc = Assert.Throws<ImpossibleException>(() => TypeDescribers.AheadOfTime.GetInstanceProvider(t));

                Assert.Contains($"Generated type {typeof(_BadInstanceProviders_Generated2)} (for {t}) claims a constructor for an InstanceProvider that could not be found.", exc.Message);
            }
        }

#pragma warning disable CS0618 // having to fake up some generated types, so using things we shouldn't normally use
        [GeneratedSourceVersion(AheadOfTimeTypeDescriber.CURRENT_CESIL_VERSION, typeof(_BadDeserializableMembers_Type1), GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer)]
        class _BadDeserializableMembers_Generated1
        {
            public static ImmutableArray<string> __ColumnNames { get; } = ImmutableArray.Create("Foo");

            [ConstructorInstanceProvider(typeof(_BadDeserializableMembers_Type1), typeof(string), 0)]
            public static bool __InstanceProvider(out _BadDeserializableMembers_Type1 val)
            {
                val = default;
                return false;
            }

            [SetterBackedByConstructorParameter(-1)]
            public void __Column_0() { }
        }
#pragma warning restore CS0618

        class _BadDeserializableMembers_Type1
        {
            public _BadDeserializableMembers_Type1(string p) { }
        }

#pragma warning disable CS0618 // having to fake up some generated types, so using things we shouldn't normally use
        [GeneratedSourceVersion(AheadOfTimeTypeDescriber.CURRENT_CESIL_VERSION, typeof(_BadDeserializableMembers_Type2), GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer)]
        class _BadDeserializableMembers_Generated2
        {
            public static ImmutableArray<string> __ColumnNames { get; } = ImmutableArray.Create("Foo");

            [ConstructorInstanceProvider(typeof(_BadDeserializableMembers_Type2), typeof(string), 0)]
            public static bool __InstanceProvider(out _BadDeserializableMembers_Type2 val)
            {
                val = default;
                return false;
            }

            [SetterBackedByConstructorParameter(100)]
            public void __Column_0() { }
        }
#pragma warning restore CS0618

        class _BadDeserializableMembers_Type2
        {
            public _BadDeserializableMembers_Type2(string p) { }
        }

#pragma warning disable CS0618 // having to fake up some generated types, so using things we shouldn't normally use
        [GeneratedSourceVersion(AheadOfTimeTypeDescriber.CURRENT_CESIL_VERSION, typeof(_BadDeserializableMembers_Type3), GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer)]
        class _BadDeserializableMembers_Generated3
        {
            public static ImmutableArray<string> __ColumnNames { get; } = ImmutableArray.Create("Foo");

            public static bool __InstanceProvider(out _BadDeserializableMembers_Type2 val)
            {
                val = default;
                return false;
            }

            [SetterBackedByInitOnlyProperty("DoesNotExist", BindingFlags.Public | BindingFlags.Instance)]
            public void __Column_0() { }
        }
#pragma warning restore CS0618

        class _BadDeserializableMembers_Type3
        {
            
        }

        [Fact]
        public void BadDeserializableMembers()
        {
            // constructor param index < 0
            {
                var t = typeof(_BadDeserializableMembers_Type1).GetTypeInfo();
                var exc = Assert.Throws<ImpossibleException>(() => TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(t).ToImmutableArray());

                Assert.Contains("Setter for column 0 claims to be backed by constructor parameter, but its position is out of bounds (index=-1)", exc.Message);
            }

            // constructor param index > len
            {
                var t = typeof(_BadDeserializableMembers_Type2).GetTypeInfo();
                var exc = Assert.Throws<ImpossibleException>(() => TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(t).ToImmutableArray());

                Assert.Contains("Setter for column 0 claims to be backed by constructor parameter, but its position is out of bounds (index=100)", exc.Message);
            }

            // backed by init only property, but can't find it
            {
                var t = typeof(_BadDeserializableMembers_Type3).GetTypeInfo();
                var exc = Assert.Throws<ImpossibleException>(() => TypeDescribers.AheadOfTime.EnumerateMembersToDeserialize(t).ToImmutableArray());

                Assert.Contains("Setter for column 0 claims to be backed by init-only property DoesNotExist with bindings (Instance, Public), but it could not be found", exc.Message);
            }
        }

#pragma warning disable CS0618 // having to fake up some generated types, so using things we shouldn't normally use
        [GeneratedSourceVersion("!!!", typeof(_BadVersion_Type), GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer)]
        class _BadVersion_Generated
        {
            public static ImmutableArray<string> __ColumnNames { get; } = ImmutableArray.Create("Foo");

            public static bool __InstanceProvider(out _BadDeserializableMembers_Type2 val)
            {
                val = default;
                return false;
            }

            public void __Column_0() { }
        }
#pragma warning restore CS0618

        class _BadVersion_Type
        {

        }

        [Fact]
        public void BadVersion()
        {
            var t = typeof(_BadVersion_Type).GetTypeInfo();
            var exc = Assert.Throws<ImpossibleException>(() => TypeDescribers.AheadOfTime.GetInstanceProvider(t));

            Assert.Contains($"Found a generated type (Cesil.Tests.AheadOfTimeTypeDescriberTests+_BadVersion_Generated) with an unexpected version (!!!), suggesting the generated source does not match the version ({AheadOfTimeTypeDescriber.CURRENT_CESIL_VERSION}) of Cesil in use.", exc.Message);
        }

#pragma warning disable CS0618 // having to fake up some generated types, so using things we shouldn't normally use
        [GeneratedSourceVersion(AheadOfTimeTypeDescriber.CURRENT_CESIL_VERSION, typeof(_MultipleGeneratedTypes_Type), GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer)]
        class _MultipleGeneratedTypes_Generated1
        {
            public static ImmutableArray<string> __ColumnNames { get; } = ImmutableArray.Create("Foo");

            public static bool __InstanceProvider(out _BadDeserializableMembers_Type2 val)
            {
                val = default;
                return false;
            }

            public void __Column_0() { }
        }

        [GeneratedSourceVersion(AheadOfTimeTypeDescriber.CURRENT_CESIL_VERSION, typeof(_MultipleGeneratedTypes_Type), GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer)]
        class _MultipleGeneratedTypes_Generated2
        {
            public static ImmutableArray<string> __ColumnNames { get; } = ImmutableArray.Create("Foo");

            public static bool __InstanceProvider(out _BadDeserializableMembers_Type2 val)
            {
                val = default;
                return false;
            }

            public void __Column_0() { }
        }
#pragma warning restore CS0618

        class _MultipleGeneratedTypes_Type
        {

        }

        [Fact]
        public void MultipleGeneratedTypes()
        {
            var t = typeof(_MultipleGeneratedTypes_Type).GetTypeInfo();
            var exc = Assert.Throws<ImpossibleException>(() => TypeDescribers.AheadOfTime.GetInstanceProvider(t));

            Assert.Contains("Found multiple generated types for Cesil.Tests.AheadOfTimeTypeDescriberTests+_MultipleGeneratedTypes_Type", exc.Message);
        }

        [Fact]
        public void UnsupportedOperations()
        {
            Assert.Throws<NotSupportedException>(() => TypeDescribers.AheadOfTime.GetCellsForDynamicRow(default, null, default));
            Assert.Throws<NotSupportedException>(() => TypeDescribers.AheadOfTime.GetDynamicCellParserFor(default, null));
            Assert.Throws<NotSupportedException>(() => TypeDescribers.AheadOfTime.GetDynamicRowConverter(default, null, null));
        }
    }
}