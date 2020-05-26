using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ShieldGenerator
{
    public class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Expected parameter");
                return -1;
            }

            switch (args[0])
            {
                case "coverage": return GenerateCodeCoverage();
                default:
                    Console.WriteLine($"Unexpected parameter '{args[0]}");
                    return -1;
            }
        }

        private static int GenerateCodeCoverage()
        {
            string coverageResultsDir;
            {
                var cur = Environment.CurrentDirectory;
                while (true)
                {
                    coverageResultsDir = Path.Combine(cur, "TestCoverageResults");

                    if (Directory.Exists(coverageResultsDir)) break;

                    if (Path.GetPathRoot(cur) == cur)
                    {
                        Console.WriteLine("Couldn't find TestCoverageResults director");
                        return -1;
                    }

                    cur = Path.GetDirectoryName(cur);
                }
            }

            var coverageResults = Path.Combine(coverageResultsDir, "index.htm");
            if (!File.Exists(coverageResults))
            {
                Console.WriteLine("Couldn't find Coverage.xml");
                return -1;
            }

            var linesBadgePath = Path.Combine(coverageResultsDir, "linesCovered.json");
            var branchesBadgePath = Path.Combine(coverageResultsDir, "branchesCovered.json");

            var html = File.ReadAllText(coverageResults);
            var tableIx = html.IndexOf(@"<table class=""overview");
            var endTableIx = html.IndexOf(@"</table>", tableIx) + "</table>".Length;
            var table = html[tableIx..endTableIx];
            var tableWithOutTags = Regex.Replace(table, @"\<.*?\>", "\r\n");
            var collapsed = Regex.Replace(tableWithOutTags, @"(\r\n)+", "\r\n").Trim();
            var lines = collapsed.Split("\r\n");
            var coveredLinesIx = Array.IndexOf(lines, "Covered lines:") + 1;
            var coverableLinesIx = Array.IndexOf(lines, "Coverable lines:") + 1;
            var coveredBranchesIx = Array.IndexOf(lines, "Covered branches:") + 1;
            var coverableBranchesIx = Array.IndexOf(lines, "Total branches:") + 1;

            var totalLines = double.Parse(lines[coverableLinesIx]);
            var totalVisitedLines = double.Parse(lines[coveredLinesIx]);

            var totalBranches = double.Parse(lines[coverableBranchesIx]);
            var totalVisitedBranches = double.Parse(lines[coveredBranchesIx]);

            if (totalLines == 0)
            {
                Console.WriteLine("Couldn't find any profiled lines");
                return -1;
            }

            if (totalBranches == 0)
            {
                Console.WriteLine("Couldn't find any profiled branches");
                return -1;
            }

            var lineCoverage = Math.Round(totalVisitedLines / totalLines * 100.0, 1);
            var branchCoverage = Math.Round(totalVisitedBranches / totalBranches * 100.0, 1);

            var linesCoverageJSON = $@"{{
  ""schemaVersion"": 1,
  ""label"": ""Line Coverage"",
  ""message"": ""{lineCoverage}%"",
  ""color"": ""{GetColor(lineCoverage)}"",
  ""namedLogo"": ""GitHub""
}}";

            var branchCoverageJSON = $@"{{
  ""schemaVersion"": 1,
  ""label"": ""Branch Coverage"",
  ""message"": ""{branchCoverage}%"",
  ""color"": ""{GetColor(branchCoverage)}"",
  ""namedLogo"": ""GitHub""
}}";

            File.WriteAllText(linesBadgePath, linesCoverageJSON);
            File.WriteAllText(branchesBadgePath, branchCoverageJSON);

            return 0;

            static string GetColor(double perc)
            {
                if (perc < 60) return "#d1383d";
                if (perc < 80) return "#cea51b";
                return "#48a868";
            }
        }
    }
}
