using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Analyzers
{
    /// <summary>
    /// Check that every `await` is on a value that has been passed through ConfigureCancellableAwait.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConfigureCancellableAwaitAnalyzer : AnalyzerBase<ImmutableHashSet<IMethodSymbol>>
    {
        public ConfigureCancellableAwaitAnalyzer() : base(false, Diagnostics.ConfigureCancellableAwait, SyntaxKind.AwaitExpression) { }

        protected override ImmutableHashSet<IMethodSymbol> OnCompilationStart(Compilation compilation)
        {
            var awaitHelper = compilation.GetTypeByMetadataNameNonNull("Cesil.AwaitHelper");

            var methodsMustBeCalled =
                ImmutableHashSet.CreateRange(
                    awaitHelper.GetMembers("ConfigureCancellableAwait").OfType<IMethodSymbol>()
                );

            return methodsMustBeCalled;
        }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, ImmutableHashSet<IMethodSymbol> methodsMustBeCalled)
        {
            var await = context.Node.Expect<SyntaxNode, AwaitExpressionSyntax>();

            var model = context.SemanticModel;

            var mark = true;

            var rightHand = await.Expression;
            if (rightHand is InvocationExpressionSyntax invoke)
            {
                var exp = invoke.Expression;

                // only bother touching the semantic model _if_ the method name involved is ConfigureCancellableAwait
                //   note that we don't check the class name, because it could be aliased to something else
                bool couldBeConfigureCancellableAwait;
                if (exp is IdentifierNameSyntax ident)
                {
                    var methodName = ident.Identifier.ValueText;
                    couldBeConfigureCancellableAwait = methodName == "ConfigureCancellableAwait";
                }
                else if (exp is MemberAccessExpressionSyntax memberAccess)
                {
                    var methodName = memberAccess.Name.Identifier.ValueText;
                    couldBeConfigureCancellableAwait = methodName == "ConfigureCancellableAwait";
                }
                else
                {
                    couldBeConfigureCancellableAwait = false;
                }

                if (couldBeConfigureCancellableAwait)
                {
                    var symInfo = model.GetSymbolInfo(exp);
                    if (symInfo.Symbol is IMethodSymbol mtdSym)
                    {
                        if (methodsMustBeCalled.Contains(mtdSym.OriginalDefinition))
                        {
                            mark = false;
                        }
                    }
                }
            }

            if (mark)
            {
                rightHand.ReportDiagnostic(Diagnostics.ConfigureCancellableAwait, context);
            }
        }
    }
}
