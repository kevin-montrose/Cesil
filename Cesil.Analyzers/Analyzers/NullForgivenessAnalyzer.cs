using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class NullForgivenessAnalyzer : AnalyzerBase<object?>
    {
        public NullForgivenessAnalyzer() : base(false, Diagnostics.NullForgiveness, SyntaxKind.SuppressNullableWarningExpression) { }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, object? state)
        {
            var exp = context.Node.Expect<SyntaxNode, PostfixUnaryExpressionSyntax>();

            exp.ReportDiagnostic(Diagnostics.NullForgiveness, context);
        }
    }
}
