using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cesil.Tests
{
    public class RequiredSetTests
    {
        [Fact]
        public void Explore()
        {
            var sizes = new HashSet<int>();
            var relevantSizes = new[] { 0, 1, 4, 8 };
            for (var i = 0; i < relevantSizes.Length; i++)
            {
                for (var j = 0; j < relevantSizes.Length; j++)
                {
                    for (var k = 0; k < relevantSizes.Length; k++)
                    {
                        sizes.Add(relevantSizes[i] + relevantSizes[j] + relevantSizes[k]);

                        sizes.Add(relevantSizes[i] * 2 + relevantSizes[j] + relevantSizes[k]);
                        sizes.Add(relevantSizes[i] + relevantSizes[j] * 2 + relevantSizes[k]);
                        sizes.Add(relevantSizes[i] + relevantSizes[j] + relevantSizes[k] * 2);

                        sizes.Add(relevantSizes[i] * 3 + relevantSizes[j] + relevantSizes[k]);
                        sizes.Add(relevantSizes[i] + relevantSizes[j] * 3 + relevantSizes[k]);
                        sizes.Add(relevantSizes[i] + relevantSizes[j] + relevantSizes[k] * 3);

                        sizes.Add(relevantSizes[i] * 4 + relevantSizes[j] + relevantSizes[k]);
                        sizes.Add(relevantSizes[i] + relevantSizes[j] * 4 + relevantSizes[k]);
                        sizes.Add(relevantSizes[i] + relevantSizes[j] + relevantSizes[k] * 4);
                    }
                }
            }

            var rand = new Random(0x1234_5678);

            var testSizes = sizes.Except(new[] { 0 }).OrderBy(_ => _).ToList();

            foreach (var size in testSizes)
            {
                if (size == 0) continue;

                for (var z = 0; z < 3; z++)
                {
                    var required = new HashSet<int>();

                    var toMakeRequired = 0;
                    while (toMakeRequired == 0)
                    {
                        toMakeRequired = rand.Next(size + 1);
                    }

                    for (var i = 0; i < toMakeRequired; i++)
                    {
                        required.Add(rand.Next(size));
                    }

                    using (var tracker = new RequiredSet(MemoryPool<char>.Shared, size))
                    {
                        foreach (var r in required)
                        {
                            tracker.SetIsRequired(r);
                        }

                        Assert.False(tracker.CheckRequiredAndClear(out var firstMissing));
                        Assert.Equal(required.Min(), firstMissing);

                        foreach (var r in required)
                        {
                            tracker.MarkSet(r);
                        }

                        Assert.True(tracker.CheckRequiredAndClear(out _));

                        foreach (var toSkip in required)
                        {
                            foreach (var r in required.Except(new[] { toSkip }))
                            {
                                tracker.MarkSet(r);
                            }

                            Assert.False(tracker.CheckRequiredAndClear(out var missing));

                            Assert.Equal(toSkip, missing);
                        }
                    }
                }
            }
        }
    }
}
