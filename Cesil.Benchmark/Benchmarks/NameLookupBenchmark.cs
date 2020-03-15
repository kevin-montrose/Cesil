using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    public class NameLookupBenchmark
    {
        private delegate bool LookupDelegate(string val, out int res);

        [ParamsSource(nameof(Classes))]
        public string Class { get; set; }

        [ParamsSource(nameof(NameSets))]
        public string NameSet { get; set; }

        public IEnumerable<string> NameSets => new[] { nameof(WideRow), nameof(NarrowRow<object>), "CommonEnglish" };

        public IEnumerable<string> Classes =>
            new[] 
            {
                nameof(Dictionary<string, int>),
                nameof(System.Array),
                nameof(Cesil.NameLookup)
            };

        private List<string> Names;
        private List<string> MissingNames;

        private Dictionary<string, int> Dictionary;
        private string[] Array;
        private NameLookup NameLookup;

        private LookupDelegate LookupFunc;

        [GlobalSetup]
        public void Initialize()
        {
            var names = GetNames(NameSet);
            var rand = new Random(2020_03_22);
            Names = names.Select(n => (Name: n, Order: rand.Next())).OrderBy(t => t.Order).Select(t => t.Name).ToList();

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

            InitializeClass(Class);

            LookupFunc = MakeLookupFunc(Class);
        }

        internal void InitializeAndTest()
        {
            foreach (var nameSet in NameSets)
            {
                var success = new Dictionary<string, List<(string Name, bool Found, int Index)>>();
                var failure = new Dictionary<string, List<(string Name, bool Found, int Index)>>();
                foreach (var @class in Classes)
                {

                    Class = @class;
                    NameSet = nameSet;

                    Initialize();

                    var successForClass = new List<(string Name, bool Found, int Index)>();
                    var failureForClass = new List<(string Name, bool Found, int Index)>();

                    foreach (var name in Names)
                    {
                        var found = LookupFunc(name, out var index);
                        successForClass.Add((name, found, index));
                    }

                    foreach (var name in MissingNames)
                    {
                        var found = LookupFunc(name, out var index);
                        failureForClass.Add((name, found, index));
                    }

                    success[@class] = successForClass;
                    failure[@class] = failureForClass;
                }

                // check that we get the same results for everything with Names
                var baseSuccess = success[nameof(System.Array)];
                foreach (var kv in success)
                {
                    if (kv.Key == nameof(System.Array)) continue;

                    if (kv.Value.Count != baseSuccess.Count) throw new Exception();

                    for (var i = 0; i < baseSuccess.Count; i++)
                    {
                        var a = baseSuccess[i];
                        var b = kv.Value[i];

                        if (a.Name != b.Name || a.Index != b.Index || a.Found != b.Found) throw new Exception();
                    }
                }

                // check that we get the same results for everything with MissingNames
                var baseFailure = failure[nameof(System.Array)];
                foreach (var kv in failure)
                {
                    if (kv.Key == nameof(System.Array)) continue;

                    if (kv.Value.Count != baseFailure.Count) throw new Exception();

                    for (var i = 0; i < baseFailure.Count; i++)
                    {
                        var a = baseFailure[i];
                        var b = kv.Value[i];

                        if (a.Name != b.Name || a.Index != b.Index || a.Found != b.Found) throw new Exception();
                    }
                }
            }
        }

        [Benchmark]
        public void Create()
        {
            InitializeClass(Class);
        }

        [Benchmark]
        public void LookupSuccess()
        {
            // intentionally only looking at cases where the key is present, as that is
            //   the expected case for NameLookup
            for (var iter = 0; iter < 100; iter++)
            {
                for (var i = 0; i < Names.Count; i++)
                {
                    var name = Names[i];
                    LookupFunc(name, out _);
                }
            }
        }

        [Benchmark]
        public void LookupFailure()
        {
            // for curiousity, what does it look like when all the keys aren't present?
            for (var iter = 0; iter < 100; iter++)
            {
                for (var i = 0; i < MissingNames.Count; i++)
                {
                    var name = MissingNames[i];
                    LookupFunc(name, out _);
                }
            }
        }

        private LookupDelegate MakeLookupFunc(string @class)
        {
            switch (@class)
            {
                case nameof(Dictionary<string, int>):
                    return
                        (string val, out int res) =>
                        {
                            if (Dictionary.TryGetValue(val, out res))
                            {
                                return true;
                            }

                            res = -1;
                            return false;
                        };
                case nameof(System.Array):
                    return
                        (string val, out int res) =>
                        {
                            for (var i = 0; i < Array.Length; i++)
                            {
                                if (Array[i].Equals(val))
                                {
                                    res = i;
                                    return true;
                                }
                            }

                            res = -1;
                            return false;
                        };
                case nameof(Cesil.NameLookup):
                    return (string val, out int res) => NameLookup.TryLookup(val, out res);
                default:
                    throw new Exception();
            }
        }

        private void InitializeClass(string @class)
        {
            switch (@class)
            {
                case nameof(Dictionary<string, int>):
                    Dictionary = new Dictionary<string, int>();
                    for (var i = 0; i < Names.Count; i++)
                    {
                        Dictionary[Names[i]] = i;
                    }
                    break;
                case nameof(System.Array):
                    Array = Names.ToArray();
                    break;
                case nameof(Cesil.NameLookup):
                case nameof(Cesil.NameLookup) + "Unsafe":
                    NameLookup = NameLookup.Create(Names, MemoryPool<char>.Shared);
                    break;
                default:
                    throw new Exception();
            }
        }

        private static List<string> GetNames(string key)
        {
            switch (key)
            {
                case nameof(WideRow):
                    return typeof(WideRow).GetProperties().Select(p => p.Name).ToList();
                case nameof(NarrowRow<object>):
                    return new List<string> { nameof(NarrowRow<object>.Column) };
                case "CommonEnglish":
                    return new List<string> { "a", "able", "about", "after", "all", "an", "and", "as", "ask", "at", "bad", "be", "big", "but", "by", "call", "case", "child", "come", "company", "day", "different", "do", "early", "eye", "fact", "feel", "few", "find", "first", "for", "from", "get", "give", "go", "good", "government", "great", "group", "hand", "have", "he", "her", "high", "his", "I", "important", "in", "into", "it", "know", "large", "last", "leave", "life", "little", "long", "look", "make", "man", "my", "new", "next", "not", "number", "of", "old", "on", "one", "or", "other", "over", "own", "part", "person", "place", "point", "problem", "public", "right", "same", "say", "see", "seem", "she", "small", "take", "tell", "that", "the", "their", "there", "they", "thing", "think", "this", "time", "to", "try", "up", "use", "want", "way", "week", "will", "with", "woman", "work", "world", "would", "year", "you", "young" };
                default:
                    throw new Exception();
            }
        }
    }
}
