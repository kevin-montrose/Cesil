
using Xunit;

namespace Cesil.Tests
{
    public class IntrustiveLinkedListTests
    {
        private class _Item : IIntrusiveLinkedList<_Item>
        {
            public int Value { get; set; }

            public _Item Next { get; set; }
            public _Item Previous { get; set; }
        }

        [Fact]
        public void AddAfter()
        {
            _Item head = null;
            _Item tail = null;

            // build the list
            for (var i = 0; i <= 10_000; i++)
            {
                var item = new _Item { Value = i };

                if (head == null)
                {
                    head.AddHead(ref head, item);
                    tail = head;
                }
                else
                {
                    tail.AddAfter(item);
                    tail = item;
                }
            }

            // read forward
            {
                var cur = head;
                for (var i = 0; i <= 10_000; i++)
                {
                    Assert.Equal(i, cur.Value);
                    cur = cur.Next;
                }

                Assert.Null(cur);
            }

            // read backwards
            {
                var cur = tail;
                for (var i = 10_000; i >= 0; i--)
                {
                    Assert.Equal(i, cur.Value);
                    cur = cur.Previous;
                }

                Assert.Null(cur);
            }
        }

        [Fact]
        public void AddHead()
        {
            _Item head = null;
            _Item tail = null;

            // build the list
            for (var i = 0; i <= 10_000; i++)
            {
                var item = new _Item { Value = i };
                tail = tail ?? item;

                head.AddHead(ref head, item);
            }

            // read forward
            {
                var cur = head;
                for (var i = 10_000; i >= 0; i--)
                {
                    Assert.Equal(i, cur.Value);
                    cur = cur.Next;
                }

                Assert.Null(cur);
            }

            // read backwards
            {
                var cur = tail;
                for (var i = 0; i <= 10_000; i++)
                {
                    Assert.Equal(i, cur.Value);
                    cur = cur.Previous;
                }

                Assert.Null(cur);
            }
        }

        [Fact]
        public void Remove()
        {
            _Item head = null;
            _Item tail = null;

            _Item oneZeroZero = null;
            _Item twoZeroZero = null;
            _Item threeThreeThree = null;

            // build the list
            for (var i = 0; i <= 10_000; i++)
            {
                var item = new _Item { Value = i };

                if (i == 100)
                {
                    oneZeroZero = item;
                }

                if (i == 200)
                {
                    twoZeroZero = item;
                }

                if (i == 333)
                {
                    threeThreeThree = item;
                }

                tail = tail ?? item;

                head.AddHead(ref head, item);
            }

            // remove 100
            {
                head.Remove(ref head, oneZeroZero);
                Assert.Null(oneZeroZero.Previous);
                Assert.Null(oneZeroZero.Next);

                // read forward
                {
                    var cur = head;
                    for (var i = 10_000; i >= 0; i--)
                    {
                        if (i == 100) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Next;
                    }

                    Assert.Null(cur);
                }

                // read backwards
                {
                    var cur = tail;
                    for (var i = 0; i <= 10_000; i++)
                    {
                        if (i == 100) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Previous;
                    }

                    Assert.Null(cur);
                }
            }

            // remove 200
            {
                head.Remove(ref head, twoZeroZero);
                Assert.Null(twoZeroZero.Previous);
                Assert.Null(twoZeroZero.Next);

                // read forward
                {
                    var cur = head;
                    for (var i = 10_000; i >= 0; i--)
                    {
                        if (i == 100) continue;
                        if (i == 200) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Next;
                    }

                    Assert.Null(cur);
                }

                // read backwards
                {
                    var cur = tail;
                    for (var i = 0; i <= 10_000; i++)
                    {
                        if (i == 100) continue;
                        if (i == 200) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Previous;
                    }

                    Assert.Null(cur);
                }
            }

            // remove 333
            {
                head.Remove(ref head, threeThreeThree);
                Assert.Null(threeThreeThree.Previous);
                Assert.Null(threeThreeThree.Next);

                // read forward
                {
                    var cur = head;
                    for (var i = 10_000; i >= 0; i--)
                    {
                        if (i == 100) continue;
                        if (i == 200) continue;
                        if (i == 333) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Next;
                    }

                    Assert.Null(cur);
                }

                // read backwards
                {
                    var cur = tail;
                    for (var i = 0; i <= 10_000; i++)
                    {
                        if (i == 100) continue;
                        if (i == 200) continue;
                        if (i == 333) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Previous;
                    }

                    Assert.Null(cur);
                }
            }

            // remove head
            {
                var oldHead = head;

                head.Remove(ref head, head);
                Assert.Null(oldHead.Previous);
                Assert.Null(oldHead.Next);

                Assert.NotNull(head);
                Assert.Equal(9_999, head.Value);

                // read forward
                {
                    var cur = head;
                    for (var i = 10_000; i >= 0; i--)
                    {
                        if (i == 10_000) continue;
                        if (i == 100) continue;
                        if (i == 200) continue;
                        if (i == 333) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Next;
                    }

                    Assert.Null(cur);
                }

                // read backwards
                {
                    var cur = tail;
                    for (var i = 0; i <= 10_000; i++)
                    {
                        if (i == 10_000) continue;
                        if (i == 100) continue;
                        if (i == 200) continue;
                        if (i == 333) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Previous;
                    }

                    Assert.Null(cur);
                }
            }

            // remove tail
            {
                var oldTail = tail;
                var newTail = tail.Previous;

                head.Remove(ref head, tail);
                Assert.Null(oldTail.Previous);
                Assert.Null(oldTail.Next);

                tail = newTail;
                Assert.Equal(1, tail.Value);

                // read forward
                {
                    var cur = head;
                    for (var i = 10_000; i >= 0; i--)
                    {
                        if (i == 10_000) continue;
                        if (i == 0) continue;
                        if (i == 100) continue;
                        if (i == 200) continue;
                        if (i == 333) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Next;
                    }

                    Assert.Null(cur);
                }

                // read backwards
                {
                    var cur = tail;
                    for (var i = 0; i <= 10_000; i++)
                    {
                        if (i == 10_000) continue;
                        if (i == 0) continue;
                        if (i == 100) continue;
                        if (i == 200) continue;
                        if (i == 333) continue;

                        Assert.Equal(i, cur.Value);
                        cur = cur.Previous;
                    }

                    Assert.Null(cur);
                }
            }

            // remove everything
            {
                while (head != null)
                {
                    var oldHead = head;
                    head.Remove(ref head, head);
                    Assert.Null(oldHead.Previous);
                    Assert.Null(oldHead.Next);
                }

                Assert.Null(head);
            }
        }
    }
}
