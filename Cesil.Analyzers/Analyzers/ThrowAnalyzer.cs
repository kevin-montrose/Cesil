using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Analyzers
{
    /// <summary>
    /// Flag all `throw` statements or expressions, suggesting using the Throw.XXX() class instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ThrowAnalyzer : AnalyzerBase<ImmutableArray<SourceSpan>>
    {
        public ThrowAnalyzer() : base(false, Diagnostics.Throw, SyntaxKind.ThrowExpression, SyntaxKind.ThrowStatement) { }

        protected override ImmutableArray<SourceSpan> OnCompilationStart(Compilation compilation)
        {
            var throwClass = compilation.GetTypeByMetadataName("Cesil.Throw");
            if (throwClass == null)
            {
                throw new InvalidOperationException("Expected Throw");
            }

            var ret = throwClass.GetSourceSpans();

            return ret;
        }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, ImmutableArray<SourceSpan> throwRoots)
        {
            var node = context.Node;

            var inThrow = throwRoots.ContainsNode(node);

            // no need to check within the class we're suggesting...
            if (inThrow)
            {
                return;
            }

            node.ReportDiagnostic(Diagnostics.Throw, context);
        }
    }
}
