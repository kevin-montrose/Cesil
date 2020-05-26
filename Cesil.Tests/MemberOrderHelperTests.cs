using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cesil.Tests
{
    public class MemberOrderHelperTests
    {
        [Fact]
        public void Simple()
        {
            // single elements
            {
                // no order
                {
                    var inst = MemberOrderHelper<string>.Create();
                    inst.Add(null, "foo");

                    Assert.Collection(
                        inst,
                        a =>
                        {
                            Assert.Equal("foo", a);
                        }
                    );
                }

                // negative
                {
                    var inst = MemberOrderHelper<string>.Create();
                    inst.Add(-1, "foo");

                    Assert.Collection(
                        inst,
                        a =>
                        {
                            Assert.Equal("foo", a);
                        }
                    );
                }

                // positive
                {
                    var inst = MemberOrderHelper<string>.Create();
                    inst.Add(1, "foo");

                    Assert.Collection(
                        inst,
                        a =>
                        {
                            Assert.Equal("foo", a);
                        }
                    );
                }
            }

            // two elements
            {
                // no order
                {
                    var inst = MemberOrderHelper<string>.Create();
                    inst.Add(null, "foo");
                    inst.Add(null, "bar");

                    Assert.Collection(
                        inst,
                        a => Assert.Equal("foo", a),
                        a => Assert.Equal("bar", a)
                    );
                }

                // 1 ordered
                {
                    var inst = MemberOrderHelper<string>.Create();
                    inst.Add(1, "foo");
                    inst.Add(null, "bar");

                    Assert.Collection(
                        inst,
                        a => Assert.Equal("foo", a),
                        a => Assert.Equal("bar", a)
                    );
                }

                // 1 ordered (reverse)
                {
                    var inst = MemberOrderHelper<string>.Create();
                    inst.Add(null, "foo");
                    inst.Add(-1, "bar");

                    Assert.Collection(
                        inst,
                        a => Assert.Equal("bar", a),
                        a => Assert.Equal("foo", a)
                    );
                }

                // both ordered
                {
                    var inst = MemberOrderHelper<string>.Create();
                    inst.Add(-2, "foo");
                    inst.Add(-1, "bar");

                    Assert.Collection(
                        inst,
                        a => Assert.Equal("foo", a),
                        a => Assert.Equal("bar", a)
                    );
                }

                // both ordered (reversed)
                {
                    var inst = MemberOrderHelper<string>.Create();
                    inst.Add(2, "foo");
                    inst.Add(1, "bar");

                    Assert.Collection(
                        inst,
                        a => Assert.Equal("bar", a),
                        a => Assert.Equal("foo", a)
                    );
                }
            }
        }

        [Fact]
        public void Random()
        {
            var random = new Random(2020_05_19);

            // all ordered
            for (var i = 0; i < 10_000; i++)
            {
                var items = new List<(int Order, string Value)>();
                var length = random.Next(20) + 1;
                var helper = MemberOrderHelper<string>.Create();

                for (var j = 0; j < length; j++)
                {
                    var val = random.Next();
                    var item = (Order: val, Value: val + "__");

                    items.Add(item);

                    helper.Add(item.Order, item.Value);
                }

                items.Sort((a, b) => a.Order.CompareTo(b.Order));

                var expected = string.Join(", ", items.Select(t => t.Value));
                var got = string.Join(", ", helper.Select(t => t));

                Assert.Equal(expected, got);
            }

            // none ordered
            for (var i = 0; i < 10_000; i++)
            {
                var items = new List<string>();
                var length = random.Next(20) + 1;
                var helper = MemberOrderHelper<string>.Create();

                for (var j = 0; j < length; j++)
                {
                    var item = j + "__";

                    items.Add(item);

                    helper.Add(null, item);
                }

                var expected = string.Join(", ", items);
                var got = string.Join(", ", helper.Select(t => t));

                Assert.Equal(expected, got);
            }

            // some ordered
            foreach (var percUnordered in new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 })
            {
                for (var i = 0; i < 1_000; i++)
                {
                    var items = new List<(int? Order, string Value)>();
                    var length = random.Next(20) + 1;
                    var helper = MemberOrderHelper<string>.Create();

                    for (var j = 0; j < length; j++)
                    {
                        var hasOrder = random.NextDouble() > percUnordered;
                        var order = hasOrder ? random.Next() : default(int?);

                        var val = order != null ? order + "__" : "null__" + j;

                        var item = (Order: order, Value: val);

                        items.Add(item);

                        helper.Add(item.Order, item.Value);
                    }

                    items.Sort(
                        (a, b) =>
                        {
                            if (a.Order == null)
                            {
                                if (b.Order == null)
                                {
                                    return CompareNulls(a.Value, b.Value);
                                }

                                return 1;
                            }
                            else
                            {
                                if (b.Order == null) return -1;

                                return a.Order.Value.CompareTo(b.Order.Value);
                            }
                        }
                    );

                    var expected = string.Join(", ", items.Select(t => t.Value));
                    var got = string.Join(", ", helper.Select(t => t));

                    Assert.Equal(expected, got);
                }

                // need to preserve _insertion_ order 
                //   which List.Sort isn't guranteed to do
                static int CompareNulls(string nullA, string nullB)
                {
                    var aStr = nullA.Substring("null__".Length);
                    var a = int.Parse(aStr);

                    var bStr = nullB.Substring("null__".Length);
                    var b = int.Parse(bStr);

                    return a.CompareTo(b);
                }
            }
        }
    }
}
