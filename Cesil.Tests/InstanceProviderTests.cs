using System.Collections.Generic;
using System.Reflection;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class InstanceProviderTests
    {
        private class _BridgeDelegate
        {
            public int Ix { get; }
            public string Foo { get; set; }

            protected _BridgeDelegate(int ix)
            {
                Ix = ix;
            }
        }

        private class _BridgeDelegate_Subclass : _BridgeDelegate
        {
            public _BridgeDelegate_Subclass(int ix) : base(ix) { }
        }

        private class _BridgeDelegate_Describer : ITypeDescriber
        {
            public IEnumerable<DeserializableMember> EnumerateMembersToDeserialize(TypeInfo forType)
            {
                var foo = DeserializableMember.ForProperty(typeof(_BridgeDelegate).GetProperty(nameof(_BridgeDelegate.Foo)));

                return new[] { foo };
            }

            public IEnumerable<SerializableMember> EnumerateMembersToSerialize(TypeInfo forType)
            {
                var ix = SerializableMember.ForProperty(typeof(_BridgeDelegate).GetProperty(nameof(_BridgeDelegate.Ix)));
                var foo = SerializableMember.ForProperty(typeof(_BridgeDelegate).GetProperty(nameof(_BridgeDelegate.Foo)));

                return new[] { ix, foo };
            }

            public InstanceProvider GetInstanceProvider(TypeInfo forType)
            {
                InstanceProviderDelegate<_BridgeDelegate_Subclass> x = (in ReadContext _, out _BridgeDelegate_Subclass foo) =>
                {
                    foo = new _BridgeDelegate_Subclass(123); return true;
                };

                return InstanceProvider.ForDelegate(x);
            }

            public IEnumerable<DynamicCellValue> GetCellsForDynamicRow(in WriteContext ctx, object row)
                => TypeDescribers.Default.GetCellsForDynamicRow(in ctx, row);

            public Parser GetDynamicCellParserFor(in ReadContext ctx, TypeInfo targetType)
            => TypeDescribers.Default.GetDynamicCellParserFor(in ctx, targetType);

            public DynamicRowConverter GetDynamicRowConverter(in ReadContext ctx, IEnumerable<ColumnIdentifier> columns, TypeInfo targetType)
            => TypeDescribers.Default.GetDynamicRowConverter(in ctx, columns, targetType);

        }

        [Fact]
        public void BridgeDelegate()
        {
            var describer = new _BridgeDelegate_Describer();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer).ToOptions();

            RunSyncReaderVariants<_BridgeDelegate>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("A\r\nB"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var x = csv.ReadAll();

                        Assert.Collection(
                            x,
                            a =>
                            {
                                Assert.Equal(123, a.Ix);
                                Assert.Equal("A", a.Foo);
                            },
                            b =>
                            {
                                Assert.Equal(123, b.Ix);
                                Assert.Equal("B", b.Foo);
                            }
                        );
                    }
                }
            );
        }

        private class _MethodBacking_Type
        {
            public int Foo { get; set; }
            public int Bar { get; set; }

            public _MethodBacking_Type(int def)
            {
                Foo = def;
            }
        }

        private static bool _MethodBacking_Method(in ReadContext ctx, out _MethodBacking_Type m)
        {
            m = new _MethodBacking_Type(123);
            return true;
        }

        [Fact]
        public void MethodBacking()
        {
            InstanceProvider builder = (InstanceProvider)typeof(InstanceProviderTests).GetMethod(nameof(_MethodBacking_Method), BindingFlags.Static | BindingFlags.NonPublic);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithInstanceProvider(builder);
            describer.WithDeserializableProperty(typeof(_MethodBacking_Type).GetProperty(nameof(_MethodBacking_Type.Bar)));

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).ToOptions();

            RunSyncReaderVariants<_MethodBacking_Type>(
                opts,
                (config, getReader) =>
                {
                    using (var reader = getReader("Foo,Bar\r\n0,678"))
                    using (var csv = config.CreateReader(reader))
                    {
                        var x = csv.ReadAll();

                        Assert.Collection(
                            x,
                            a =>
                            {
                                Assert.Equal(123, a.Foo);
                                Assert.Equal(678, a.Bar);
                            }
                        );
                    }
                }
            );
        }
    }
}
