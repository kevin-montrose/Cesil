using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Analyzers
{
    /// <summary>
    /// Flag all `throw` statements or expressions, suggesting using the Throw.XXX() class instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ThrowAnalyzer : AnalyzerBase<ImmutableArray<SyntaxNode>>
    {
        public ThrowAnalyzer() : base(false, Diagnostics.Throw, SyntaxKind.ThrowExpression, SyntaxKind.ThrowStatement) { }

        [SuppressMessage("MicrosoftCodeAnalysisPerformance", "RS1012:Start action has no registered actions.", Justification = "Handled in AnalyzerBase")]
        protected override ImmutableArray<SyntaxNode> OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var comp = context.Compilation;
            var throwClass = comp.GetTypeByMetadataName("Cesil.Throw");
            if (throwClass == null)
            {
                throw new InvalidOperationException("Expected Throw");
            }

            var ret =
                ImmutableArray.CreateRange(
                    throwClass
                        .DeclaringSyntaxReferences
                        .Select(
                            syntaxRef =>
                            {
                                var tree = syntaxRef.SyntaxTree;
                                var root = tree.GetRoot();

                                return root;
                            }
                        )
                );

            return ret;
        }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> throwRoots)
        {
            var node = context.Node;

            var inThrow = throwRoots.Any(root => root.Contains(node));

            // no need to check within the class we're suggesting...
            if (inThrow)
            {
                return;
            }

            var diag = Diagnostic.Create(Diagnostics.Throw, node.GetLocation());

            context.ReportDiagnostic(diag);
        }
    }
}
