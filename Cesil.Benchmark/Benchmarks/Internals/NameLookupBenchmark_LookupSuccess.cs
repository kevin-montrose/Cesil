using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    [BenchmarkCategory("Internals")]
    public class NameLookupBenchmark_LookupSuccess
    {
        private const int ITERS = 100;

        [ParamsSource(nameof(NameSets))]
        public string NameSet { get; set; }

        public IEnumerable<string> NameSets => new[] { nameof(WideRow), nameof(NarrowRow<object>), "CommonEnglish" };

        private List<string> Names;
        private List<string> MissingNames;

        private Dictionary<string, int> DictionaryLookup;
        private string[] ArrayLookup;
        private NameLookup BinarySearch;
        private NameLookup AdaptiveRadixTrie;

        [GlobalSetup]
        public void Initialize()
        {
            var rand = new Random(2020_05_22);
            Names = Benchmark.NameSet.GetNameSet(NameSet);

            MissingNames = new List<string>();
            while (MissingNames.Count < Names.Count)
            {
                var baseName = Names[rand.Next(Names.Count)];
                string newName;

                switch (rand.Next(3))
                {
                    // remove a char
                    case 0:
                        var removeIx = rand.Next(baseName.Length);
                        newName = baseName.Substring(0, removeIx) + baseName.Substring(removeIx + 1);
                        break;

                    // dupe a char
                    case 1:
                        var addIx = rand.Next(baseName.Length);
                        newName = baseName.Substring(0, addIx) + baseName[addIx] + baseName.Substring(addIx);
                        break;

                    // trim tail
                    case 2:
                        var trimIx = rand.Next(baseName.Length);
                        newName = baseName.Substring(0, trimIx);
                        break;
                    default:
                        throw new Exception();
                }

                if (Names.Contains(newName)) continue;

                MissingNames.Add(newName);
            }

            InitializeClass(nameof(Dictionary<string, int>));
            InitializeClass(nameof(System.Array));
            InitializeClass(nameof(NameLookup.Algorithm.BinarySearch));
            InitializeClass(nameof(NameLookup.Algorithm.AdaptiveRadixTrie));
        }

        // called via reflection
        public void InitializeAndTest()
        {
            foreach (var nameSet in NameSets)
            {
                NameSet = nameSet;
                Initialize();

                foreach (var name in Names)
                {
                    var res1 = DictionaryLookup.TryGetValue(name, out var ix1);
                    var ix2 = System.Array.IndexOf(ArrayLookup, name);
                    var res2 = ix2 != -1;
                    var res3 = BinarySearch.TryLookup(name, out var ix3);
                    var res4 = AdaptiveRadixTrie.TryLookup(name, out var ix4);

                    if (res1 != res2) throw new Exception();
                    if (res1 != res3) throw new Exception();
                    if (res1 != res4) throw new Exception();

                    if (ix1 != ix2) throw new Exception();
                    if (ix1 != ix3) throw new Exception();
                    if (ix1 != ix4) throw new Exception();
                }

                foreach (var name in MissingNames)
                {
                    var res1 = DictionaryLookup.TryGetValue(name, out var ix1);
                    ix1 = res1 ? ix1 : -1;
                    var ix2 = System.Array.IndexOf(ArrayLookup, name);
                    var res2 = ix2 != -1;
                    var res3 = BinarySearch.TryLookup(name, out var ix3);
                    var res4 = AdaptiveRadixTrie.TryLookup(name, out var ix4);

                    if (res1 != res2) throw new Exception();
                    if (res1 != res3) throw new Exception();
                    if (res1 != res4) throw new Exception();

                    if (ix1 != ix2) throw new Exception();
                    if (ix1 != ix3) throw new Exception();
                    if (ix1 != ix4) throw new Exception();
                }
            }
        }

        [Benchmark(Baseline = true)]
        public void Dictionary()
        {
            //intentionally only looking at cases where the key is present, as that is
            //   the expected case for NameLookup
            for (var iter = 0; iter < ITERS; iter++)
            {
                for (var i = 0; i < Names.Count; i++)
                {
                    var name = Names[i];
                    DictionaryLookup.TryGetValue(name, out _);
                }
            }
        }

        private Dictionary<string, int> DictionaryImpl()
        {
            var ret = new Dictionary<string, int>();
            for (var i = 0; i < Names.Count; i++)
            {
                ret[Names[i]] = i;
            }

            return ret;
        }

        [Benchmark]
        public void Array()
        {
            //intentionally only looking at cases where the key is present, as that is
            //   the expected case for NameLookup
            for (var iter = 0; iter < ITERS; iter++)
            {
                for (var i = 0; i < Names.Count; i++)
                {
                    var name = Names[i];
                    System.Array.IndexOf(ArrayLookup, name);
                }
            }
        }

        private string[] ArrayImpl()
        {
            return Names.ToArray();
        }

        [Benchmark]
        public void NameLookup_BinarySearch()
        {
            //intentionally only looking at cases where the key is present, as that is
            //   the expected case for NameLookup
            for (var iter = 0; iter < ITERS; iter++)
            {
                for (var i = 0; i < Names.Count; i++)
                {
                    var name = Names[i];
                    BinarySearch.TryLookup(name, out _);
                }
            }
        }

        private NameLookup NameLookup_BinarySearchImpl()
        {
            var withIx = Names.Select((n, ix) => (Name: n, Index: ix));
            var inOrder = withIx.OrderBy(o => o.Name, StringComparer.Ordinal);

            if (!NameLookup.TryCreateBinarySearch(inOrder, MemoryPool<char>.Shared, out var owner, out var mem))
            {
                throw new Exception();
            }
            var ret = new NameLookup(NameLookup.Algorithm.BinarySearch, owner, mem);

            return ret;
        }

        [Benchmark]
        public void NameLookup_AdaptiveRadixTrie()
        {
            //intentionally only looking at cases where the key is present, as that is
            //   the expected case for NameLookup
            for (var iter = 0; iter < ITERS; iter++)
            {
                for (var i = 0; i < Names.Count; i++)
                {
                    var name = Names[i];
                    AdaptiveRadixTrie.TryLookup(name, out _);
                }
            }
        }

        private NameLookup NameLookup_AdaptiveRadixTrieImpl()
        {
            var withIx = Names.Select((n, ix) => (Name: n, Index: ix));
            var inOrder = withIx.OrderBy(o => o.Name, StringComparer.Ordinal);

            if (!NameLookup.TryCreateAdaptiveRadixTrie(inOrder, MemoryPool<char>.Shared, out var owner, out var mem))
            {
                throw new Exception();
            }
            var ret = new NameLookup(NameLookup.Algorithm.AdaptiveRadixTrie, owner, mem);

            return ret;
        }

        private void InitializeClass(string @class)
        {
            switch (@class)
            {
                case nameof(Dictionary<string, int>):
                    DictionaryLookup = DictionaryImpl();
                    break;
                case nameof(System.Array):
                    ArrayLookup = ArrayImpl();
                    break;
                case nameof(NameLookup.Algorithm.BinarySearch):
                    BinarySearch = NameLookup_BinarySearchImpl();
                    break;
                case nameof(NameLookup.Algorithm.AdaptiveRadixTrie):
                    AdaptiveRadixTrie = NameLookup_AdaptiveRadixTrieImpl();
                    break;
                default:
                    throw new Exception();
            }
        }
    }
}
