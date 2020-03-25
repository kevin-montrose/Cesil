using System;
using System.Buffers;
using System.Linq;
using Xunit;

namespace Cesil.Tests
{
    public class UnmanagedLookupArrayTests
    {
        [Fact]
        public void SimpleHeldMembers()
        {
            var rand = new Random(2020_02_18);
            for (var i = 0; i < 1_000; i++)
            {
                var size = rand.Next(100);

                var memberIndexes = Enumerable.Range(0, size + 1).Select(_ => rand.Next()).ToList();

                // all simple
                {
                    using (var mem = new UnmanagedLookupArray<(int? Simple, int? Held)>(MemoryPool<char>.Shared, size))
                    {
                        for (var j = 0; j < size; j++)
                        {
                            mem.Set(j, (memberIndexes[j], null));
                        }

                        for (var j = 0; j < size; j++)
                        {
                            mem.Get(j, default, out var m);
                            Assert.Equal(memberIndexes[j], m.Simple.Value);
                            Assert.False(m.Held.HasValue);
                        }
                    }
                }

                // all held
                {
                    using (var mem = new UnmanagedLookupArray<(int? Simple, int? Held)>(MemoryPool<char>.Shared, size))
                    {
                        for (var j = 0; j < size; j++)
                        {
                            mem.Set(j, (null, memberIndexes[j]));
                        }

                        for (var j = 0; j < size; j++)
                        {
                            mem.Get(j, default, out var m);
                            Assert.False(m.Simple.HasValue);
                            Assert.Equal(memberIndexes[j], m.Held.Value);
                        }
                    }
                }

                // all missing
                {
                    using (var mem = new UnmanagedLookupArray<(int? Simple, int? Held)>(MemoryPool<char>.Shared, size))
                    {
                        for (var j = 0; j < size; j++)
                        {
                            mem.Set(j, (null, null));
                        }

                        for (var j = 0; j < size; j++)
                        {
                            mem.Get(j, default, out var m);
                            Assert.False(m.Simple.HasValue);
                            Assert.False(m.Held.HasValue);
                        }
                    }
                }

                // mix
                {
                    var assignments =
                        memberIndexes.Select(
                            mix =>
                            {
                                var assignment = rand.Next(3);

                                return (MemberIndex: mix, Simple: assignment == 0, Held: assignment == 1, Missing: assignment == 2);
                            }
                        )
                        .ToList();

                    using (var mem = new UnmanagedLookupArray<(int? Simple, int? Held)>(MemoryPool<char>.Shared, size))
                    {
                        for (var j = 0; j < size; j++)
                        {
                            var assignment = assignments[j];
                            if (assignment.Simple)
                            {
                                mem.Set(j, (assignment.MemberIndex, null));
                            }
                            else if (assignment.Held)
                            {
                                mem.Set(j, (null, assignment.MemberIndex));
                            }
                            else if (assignment.Missing)
                            {
                                mem.Set(j, (null, null));
                            }
                            else
                            {
                                throw new Exception();
                            }
                        }

                        for (var j = 0; j < size; j++)
                        {
                            mem.Get(j, default, out var m);

                            var res = assignments[j];
                            if (res.Simple)
                            {
                                Assert.Equal(res.MemberIndex, m.Simple.Value);
                                Assert.False(m.Held.HasValue);
                            }
                            else if (res.Held)
                            {
                                Assert.False(m.Simple.HasValue);
                                Assert.Equal(res.MemberIndex, m.Held.Value);
                            }
                            else if (res.Missing)
                            {
                                Assert.False(m.Simple.HasValue);
                                Assert.False(m.Held.HasValue);
                            }
                            else
                            {
                                throw new Exception();
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void SimpleLookup()
        {
            var rand = new Random(2020_02_18);
            for (var i = 0; i < 1_000; i++)
            {
                var size = rand.Next(100);

                var memberIndexes = Enumerable.Range(0, size + 1).Select(_ => rand.Next()).ToList();

                // all set
                {
                    using (var mem = new UnmanagedLookupArray<int>(MemoryPool<char>.Shared, size))
                    {
                        for (var j = 0; j < size; j++)
                        {
                            mem.Set(j, memberIndexes[j]);
                        }

                        for (var j = 0; j < size; j++)
                        {
                            mem.Get(j, -1, out var val);
                            Assert.Equal(memberIndexes[j], val);
                        }
                    }
                }

                // some missing (kind of a silly distinction, but whatever)
                {
                    var assignments =
                        memberIndexes.Select(
                            mix =>
                            {
                                var assignment = rand.Next(2);

                                return (MemberIndex: mix, Set: assignment == 0, Missing: assignment == 1);
                            }
                        )
                        .ToList();

                    using (var mem = new UnmanagedLookupArray<int>(MemoryPool<char>.Shared, size))
                    {
                        for (var j = 0; j < size; j++)
                        {
                            var assignment = assignments[j];

                            if (assignment.Set)
                            {
                                mem.Set(j, memberIndexes[j]);
                            }
                            else
                            {
                                mem.Set(j, -1);
                            }
                        }

                        for (var j = 0; j < size; j++)
                        {
                            var assignment = assignments[j];

                            mem.Get(j, -1, out var val);

                            if (assignment.Set)
                            {
                                Assert.Equal(memberIndexes[j], val);
                            }
                            else
                            {
                                Assert.Equal(-1, val);
                            }

                        }
                    }
                }
            }
        }
    }
}