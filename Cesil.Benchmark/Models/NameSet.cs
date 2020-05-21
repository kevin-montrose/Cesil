using System;
using System.Collections.Generic;
using System.Linq;

namespace Cesil.Benchmark
{
    internal static class NameSet
    {
        public static List<string> GetNameSet(string name)
        {
            var names = GetNames(name);
            var rand = new Random(2020_05_22);
            var ret = names.Select(n => (Name: n, Order: rand.Next())).OrderBy(t => t.Order).Select(t => t.Name).ToList();

            return ret;
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
