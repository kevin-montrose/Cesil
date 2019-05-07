using System.Collections.Generic;
using System.Reflection;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class InstanceBuilderTests
    {
        class _BridgeDelegate
        {
            public int Ix { get; }
            public string Foo { get; set; }

            protected _BridgeDelegate(int ix)
            {
                Ix = ix;
            }
        }

        class _BridgeDelegate_Subclass: _BridgeDelegate
        {
            public _BridgeDelegate_Subclass(int ix) : base(ix) { }
        }

        class _BridgeDelegate_Describer : ITypeDescriber
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

            public InstanceBuilder GetInstanceBuilder(TypeInfo forType)
            {
                InstanceBuilderDelegate<_BridgeDelegate_Subclass> x = (out _BridgeDelegate_Subclass foo) => { foo = new _BridgeDelegate_Subclass(123); return true; };

                return InstanceBuilder.ForDelegate(x);
            }
        }

        [Fact]
        public void BridgeDelegate()
        {
            var describer = new _BridgeDelegate_Describer();

            var opts = Options.Default.NewBuilder().WithTypeDescriber(describer).Build();

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
    }
}
