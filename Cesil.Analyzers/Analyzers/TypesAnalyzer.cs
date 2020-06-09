using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Analyzers
{
    /// <summary>
    /// Flag `typeof` expressions on concrete types, suggesting using a member on Types instead.
    /// 
    /// Generic (that is, none concrete) types can still get typeof()'d, as that's really the best
    ///    option available.  Replacing that in the IL with a method call would be a bit much.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TypesAnalyzer : AnalyzerBase<ImmutableArray<SyntaxNode>>
    {
        public TypesAnalyzer() : base(false, Diagnostics.Types, SyntaxKind.TypeOfExpression) { }

        [SuppressMessage("MicrosoftCodeAnalysisPerformance", "RS1012:Start action has no registered actions.", Justification = "Handled in AnalyzerBase")]
        protected override ImmutableArray<SyntaxNode> OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var comp = context.Compilation;

            var typesClass = comp.GetTypeByMetadataName("Cesil.Types");
            if (typesClass == null)
            {
                throw new InvalidOperationException("Expected Types");
            }

            var ret =
                ImmutableArray.CreateRange(
                    typesClass
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

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> typeRoots)
        {
            var node = context.Node;

            if (!(node is TypeOfExpressionSyntax typeofSyntax))
            {
                throw new InvalidOperationException($"Expected {nameof(TypeOfExpressionSyntax)}");
            }

            var inTypes = typeRoots.Any(root => root.Contains(node));

            if (inTypes)
            {
                // we're _in_ the Types class, no reason to flag here
                return;
            }

            var model = context.SemanticModel;

            var type = model.GetTypeInfo(typeofSyntax.Type);

            // can't figure out, or it's not a generic type?  flag it
            var mark = type.Type == null || type.Type.TypeKind != TypeKind.TypeParameter;

            if (mark)
            {
                var diag = Diagnostic.Create(Diagnostics.Types, typeofSyntax.GetLocation());
                context.ReportDiagnostic(diag);
            }
        }
    }
}
