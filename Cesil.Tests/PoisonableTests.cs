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
                AssertNotPoisoned();
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
