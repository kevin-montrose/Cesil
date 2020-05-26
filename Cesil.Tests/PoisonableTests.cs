using System;
using Xunit;

namespace Cesil.Tests
{
    public class PoisonableTests
    {
        private sealed class _Asserts : PoisonableBase
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void Asserts()
        {
            var config = Configuration.For<_Asserts>();

            // weird aggregate
            {
                var a = new _Asserts();
                a.SetPoison(new AggregateException("Foo"));
                Assert.Throws<InvalidOperationException>(() => a.AssertNotPoisoned(config));
            }

            // cancellation
            {
                var a = new _Asserts();
                a.SetPoison(new OperationCanceledException("Foo"));
                Assert.Throws<InvalidOperationException>(() => a.AssertNotPoisoned(config));
            }

            // normal aggregate
            {
                var a = new _Asserts();
                a.SetPoison(new AggregateException("Foo", new InvalidOperationException()));
                Assert.Throws<InvalidOperationException>(() => a.AssertNotPoisoned(config));
            }
        }
    }
}
