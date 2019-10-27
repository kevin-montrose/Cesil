
using Xunit;

namespace Cesil.Tests
{
    public class IntrustiveLinkedListTests
    {
        private class _Item : IIntrusiveLinkedList<_Item>
        {
            public int Value { get; set; }

            public bool HasNext => _Next != null;

            private _Item _Next;
            public _Item Next
            {
                get => Utils.NonNull(_Next);
                set => _Next = value;
            }
            public void ClearNext() => Next = null;

            public bool HasPrevious => _Previous != null;
            private _Item _Previous;
            public _Item Previous
            {
                get => Utils.NonNull(_Previous);
                set => _Previous = value;
            }
            public void ClearPrevious() => Previous = null;
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
                    if (cur.HasNext)
                    {
                        cur = cur.Next;
                    }
                    else
                    {
                        cur = null;
                    }
                }

                Assert.Null(cur);
            }

            // read backwards
            {
                var cur = tail;
                for (var i = 10_000; i >= 0; i--)
                {
                    Assert.Equal(i, cur.Value);
                    if (cur.HasPrevious)
                    {
                        cur = cur.Previous;
                    }
                    else
                    {
                        cur = null;
                    }
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
                    if (cur.HasNext)
                    {
                        cur = cur.Next;
                    }
                    else
                    {
                        cur = null;
                    }
                }

                Assert.Null(cur);
            }

            // read backwards
            {
                var cur = tail;
                for (var i = 0; i <= 10_000; i++)
                {
                    Assert.Equal(i, cur.Value);
                    if (cur.HasPrevious)
                    {
                        cur = cur.Previous;
                    }
                    else
                    {
                        cur = null;
                    }
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
                Assert.False(oneZeroZero.HasPrevious);
                Assert.False(oneZeroZero.HasNext);

                // read forward
                {
                    var cur = head;
                    for (var i = 10_000; i >= 0; i--)
                    {
                        if (i == 100) continue;

                        Assert.Equal(i, cur.Value);
                        if (cur.HasNext)
                        {
                            cur = cur.Next;
                        }
                        else
                        {
                            cur = null;
                        }
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

                        if (cur.HasPrevious)
                        {
                            cur = cur.Previous;
                        }
                        else
                        {
                            cur = null;
                        }
                    }

                    Assert.Null(cur);
                }
            }

            // remove 200
            {
                head.Remove(ref head, twoZeroZero);
                Assert.False(twoZeroZero.HasPrevious);
                Assert.False(twoZeroZero.HasNext);

                // read forward
                {
                    var cur = head;
                    for (var i = 10_000; i >= 0; i--)
                    {
                        if (i == 100) continue;
                        if (i == 200) continue;

                        Assert.Equal(i, cur.Value);

                        if (cur.HasNext)
                        {
                            cur = cur.Next;
                        }
                        else
                        {
                            cur = null;
                        }
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

                        if (cur.HasPrevious)
                        {
                            cur = cur.Previous;
                        }
                        else
                        {
                            cur = null;
                        }
                    }

                    Assert.Null(cur);
                }
            }

            // remove 333
            {
                head.Remove(ref head, threeThreeThree);
                Assert.False(threeThreeThree.HasPrevious);
                Assert.False(threeThreeThree.HasNext);

                // read forward
                {
                    var cur = head;
                    for (var i = 10_000; i >= 0; i--)
                    {
                        if (i == 100) continue;
                        if (i == 200) continue;
                        if (i == 333) continue;

                        Assert.Equal(i, cur.Value);

                        if (cur.HasNext)
                        {
                            cur = cur.Next;
                        }
                        else
                        {
                            cur = null;
                        }
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

                        if (cur.HasPrevious)
                        {
                            cur = cur.Previous;
                        }
                        else
                        {
                            cur = null;
                        }
                    }

                    Assert.Null(cur);
                }
            }

            // remove head
            {
                var oldHead = head;

                head.Remove(ref head, head);
                Assert.False(oldHead.HasPrevious);
                Assert.False(oldHead.HasNext);

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

                        if (cur.HasNext)
                        {
                            cur = cur.Next;
                        }
                        else
                        {
                            cur = null;
                        }
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

                        if (cur.HasPrevious)
                        {
                            cur = cur.Previous;
                        }
                        else
                        {
                            cur = null;
                        }
                    }

                    Assert.Null(cur);
                }
            }

            // remove tail
            {
                var oldTail = tail;
                var newTail = tail.Previous;

                head.Remove(ref head, tail);
                Assert.False(oldTail.HasPrevious);
                Assert.False(oldTail.HasNext);

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

                        if (cur.HasNext)
                        {
                            cur = cur.Next;
                        }
                        else
                        {
                            cur = null;
                        }
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

                        if (cur.HasPrevious)
                        {
                            cur = cur.Previous;
                        }
                        else
                        {
                            cur = null;
                        }
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
                    Assert.False(oldHead.HasPrevious);
                    Assert.False(oldHead.HasNext);
                }

                Assert.Null(head);
            }
        }
    }
}
