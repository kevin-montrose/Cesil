using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Tests
{
    internal sealed class DefaultAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private sealed class DefaultAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            public DefaultAnalyzerConfigOptions()
            {
            }

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string value)
            {
                value = null;
                return false;
            }
        }

        internal DefaultAnalyzerConfigOptionsProvider() { }

        public override AnalyzerConfigOptions GlobalOptions { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        => new DefaultAnalyzerConfigOptions();

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        => new DefaultAnalyzerConfigOptions();
    }
}
