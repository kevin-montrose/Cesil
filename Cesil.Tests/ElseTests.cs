using System.Collections.Immutable;
using Xunit;

namespace Cesil.Tests
{
    public class ElseTests
    {
        private sealed class _Simple : IElseSupporting<_Simple>
        {
            public int Val { get; }
            public ImmutableArray<_Simple> Fallbacks { get; }

            public _Simple(int val) : this(val, ImmutableArray<_Simple>.Empty) { }

            private _Simple(int val, ImmutableArray<_Simple> fallbacks)
            {
                Val = val;
                Fallbacks = fallbacks;
            }

            public _Simple Clone(ImmutableArray<_Simple> newFallbacks)
            => new _Simple(Val, newFallbacks);
        }

        [Fact]
        public void Simple()
        {
            var a = new _Simple(1);
            var b = new _Simple(2);
            var c = new _Simple(3);
            var d = new _Simple(4);

            var abcd = a.DoElse(b).DoElse(c).DoElse(d);
            Assert.NotSame(a, abcd);
            Assert.NotSame(b, abcd);
            Assert.NotSame(c, abcd);
            Assert.NotSame(d, abcd);
            Assert.Equal(1, abcd.Val);
            Assert.Collection(
                abcd.Fallbacks,
                v => Assert.Equal(2, v.Val),
                v => Assert.Equal(3, v.Val),
                v => Assert.Equal(4, v.Val)
            );

            var dcba = d.DoElse(c).DoElse(b).DoElse(a);
            Assert.NotSame(a, dcba);
            Assert.NotSame(b, dcba);
            Assert.NotSame(c, dcba);
            Assert.NotSame(d, dcba);
            Assert.Equal(4, dcba.Val);
            Assert.Collection(
                dcba.Fallbacks,
                v => Assert.Equal(3, v.Val),
                v => Assert.Equal(2, v.Val),
                v => Assert.Equal(1, v.Val)
            );

            // 4 cases
            //  - left has no fallbacks, right has no fallbacks
            //  - left has no fallbacks, right has fallbacks
            //  - left has fallbacks, right has no fallbacks
            //  - both left and right have fallbacks

            // case 1
            {
                var res = a.DoElse(b);
                Assert.NotSame(a, res);
                Assert.NotSame(b, res);
                Assert.Equal(1, res.Val);
                Assert.Collection(
                    res.Fallbacks,
                    v => Assert.Equal(2, v.Val)
                );
            }

            // case 2
            {
                var res = a.DoElse(abcd);
                Assert.NotSame(a, res);
                Assert.NotSame(abcd, res);

                Assert.Equal(1, res.Val);
                Assert.Collection(
                    res.Fallbacks,
                    v => Assert.Equal(1, v.Val),
                    v => Assert.Equal(2, v.Val),
                    v => Assert.Equal(3, v.Val),
                    v => Assert.Equal(4, v.Val)
                );
            }

            // case 3
            {
                var res = dcba.DoElse(a);
                Assert.NotSame(a, res);
                Assert.NotSame(dcba, res);

                Assert.Equal(4, res.Val);
                Assert.Collection(
                    res.Fallbacks,
                    v => Assert.Equal(3, v.Val),
                    v => Assert.Equal(2, v.Val),
                    v => Assert.Equal(1, v.Val),
                    v => Assert.Equal(1, v.Val)
                );
            }

            // case 4
            {
                var res = abcd.DoElse(dcba);
                Assert.NotSame(abcd, res);
                Assert.NotSame(dcba, res);

                Assert.Equal(1, res.Val);
                Assert.Collection(
                    res.Fallbacks,
                    v => Assert.Equal(2, v.Val),
                    v => Assert.Equal(3, v.Val),
                    v => Assert.Equal(4, v.Val),
                    v => Assert.Equal(4, v.Val),
                    v => Assert.Equal(3, v.Val),
                    v => Assert.Equal(2, v.Val),
                    v => Assert.Equal(1, v.Val)
                );
            }
        }
    }
}
