using System;
using Xunit;

namespace Cesil.Tests
{
    public class PoisonableTests
    {
        private sealed class SimplePoisonable : PoisonableBase
        {
            public void InvokeAssert()
            {
                var config = Configuration.For<SimplePoisonable>();

                AssertNotPoisoned(config);
            }
        }

        [Fact]
        public void WeirdAggregate()
        {
            var a = new SimplePoisonable();
            a.SetPoison(new AggregateException("Foo"));
            Assert.Throws<InvalidOperationException>(() => a.InvokeAssert());
        }
    }
}
