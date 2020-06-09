using System;
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
            var node = context.Node;
            if (!(node is PostfixUnaryExpressionSyntax exp))
            {
                throw new InvalidOperationException($"Expected {nameof(PostfixUnaryExpressionSyntax)}");
            }

            // if not null forgiveness, bail
            if (exp.OperatorToken.ValueText != "!")
            {
                return;
            }

            var diag = Diagnostic.Create(Diagnostics.NullForgiveness, node.GetLocation());
            context.ReportDiagnostic(diag);
        }
    }
}
