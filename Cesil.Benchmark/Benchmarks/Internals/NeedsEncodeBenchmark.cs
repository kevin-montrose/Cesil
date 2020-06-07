using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace Cesil.Benchmark
{
    [BenchmarkCategory("Internals")]
    public class NeedsEncodeBenchmark
    {
        [ParamsSource(nameof(StringLengths))]
        public int StringLength { get; set; }

        [ParamsSource(nameof(PercentsNeedEncoding))]
        public int PercentNeedEncoding { get; set; }

        public IEnumerable<int> StringLengths => new[] { 1, 10, 16, 100, 112, 1_000, 1_008, 10_000 };
        public IEnumerable<int> PercentsNeedEncoding => new[] { 0, 10, 50, 100 };

        private List<string> Strings;

        private string BasicContains_C1;
        private char? BasicContains_C2;
        private char? BasicContains_C3;

        private NeedsEncodeHelper NeedsEncodeHelper;

        [GlobalSetup]
        public unsafe void Initialize()
        {
            const int NUM_STRINGS = 100;

            var strRet = new List<string>();

            var r = MakeRandom();

            for (var i = 0; i < NUM_STRINGS; i++)
            {
                strRet.Add(GenerateString(r, PercentNeedEncoding, StringLength, ',', '"', null));
            }

            Strings = strRet;

            BasicContains_C1 = ",";
            BasicContains_C2 = '"';
            BasicContains_C3 = null;

            NeedsEncodeHelper = new NeedsEncodeHelper(BasicContains_C1, BasicContains_C2, BasicContains_C3);

            static string GenerateString(Random r, int percent, int length, char c1, char? c2, char? c3)
            {
                if (length == 0) return "";

                var ret = new char[length];
                for (var i = 0; i < ret.Length; i++)
                {
                    var c = (char)r.Next(128);

                    var needsEncode =
                        c == '\r' ||
                        c == '\n' ||
                        c == c1 ||
                        c == c2 ||
                        c == c3;

                    if (needsEncode)
                    {
                        i--;
                        continue;
                    }

                    ret[i] = c;
                }

                var needsEncoding = r.Next(100) < percent;

                if (needsEncoding)
                {
                    var ix = r.Next(ret.Length);

                    var max = 3 + (c2.HasValue ? 1 : 0) + (c3.HasValue ? 1 : 0);
                    switch (r.Next(max))
                    {
                        case 0: ret[ix] = '\r'; break;
                        case 1: ret[ix] = '\n'; break;
                        case 2: ret[ix] = c1; break;
                        case 3: ret[ix] = c2.Value; break;
                        case 4: ret[ix] = c3.Value; break;
                        default: throw new Exception();
                    }
                }

                return new string(ret);
            }

            static Random MakeRandom()
            {
                return new Random(2020_03_01);
            }
        }

        // called via reflection
        public void InitializeAndTest()
        {
            foreach (var len in StringLengths)
            {
                foreach (var percent in PercentsNeedEncoding)
                {
                    Strings = null;
                    PercentNeedEncoding = percent;
                    StringLength = len;
                    Initialize();

                    for (var i = 0; i < Strings.Count; i++)
                    {
                        var span = Strings[i].AsSpan();

                        var bc = BasicContains(span);
                        var p = Probabilistic(span);
                        var a = Avx2(span);
                        var c = Combo(span);

                        if (bc != p) throw new Exception();
                        if (bc != a) throw new Exception();
                        if (bc != c) throw new Exception();
                    }
                }
            }
        }

        [Benchmark(Baseline = true)]
        public void BasicContains()
        {
            foreach (var str in Strings)
            {
                var span = str.AsSpan();
                BasicContains(span);
            }
        }

        [Benchmark]
        public void Probabilistic()
        {
            foreach (var str in Strings)
            {
                var span = str.AsSpan();
                Probabilistic(span);
            }
        }

        [Benchmark]
        public void Avx2()
        {
            foreach (var str in Strings)
            {
                var span = str.AsSpan();
                Avx2(span);
            }
        }

        [Benchmark]
        public void Combo()
        {
            foreach (var str in Strings)
            {
                var span = str.AsSpan();
                Combo(span);
            }
        }

        private int BasicContains(ReadOnlySpan<char> str)
        {
            var ret = str.IndexOf('\r');
            if (ret != -1) return ret;

            ret = str.IndexOf('\n');
            if (ret != -1) return ret;

            ret = str.IndexOf(BasicContains_C1);
            if (ret != -1) return ret;

            if (BasicContains_C2.HasValue)
            {
                ret = str.IndexOf(BasicContains_C2.Value);
                if (ret != -1) return ret;
            }

            if (BasicContains_C3.HasValue)
            {
                ret = str.IndexOf(BasicContains_C3.Value);
                if (ret != -1) return ret;
            }

            return -1;
        }

        private unsafe int Probabilistic(ReadOnlySpan<char> str)
        {
            fixed (char* charPtr = str)
            {
                return NeedsEncodeHelper.ProbabilisticContainsChar(charPtr, str.Length);
            }
        }

        private unsafe int Avx2(ReadOnlySpan<char> str)
        {
            fixed (char* charPtr = str)
            {
                return NeedsEncodeHelper.Avx2ContainsChar(charPtr, str.Length);
            }
        }

        private unsafe int Combo(ReadOnlySpan<char> str)
        {
            fixed (char* charPtr = str)
            {
                return NeedsEncodeHelper.ContainsCharRequiringEncoding(charPtr, str.Length);
            }
        }
    }
}
