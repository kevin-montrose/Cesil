using System.Collections.Immutable;
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
    public sealed class TypesAnalyzer : AnalyzerBase<ImmutableArray<SourceSpan>>
    {
        public TypesAnalyzer() : base(false, Diagnostics.Types, SyntaxKind.TypeOfExpression) { }

        protected override ImmutableArray<SourceSpan> OnCompilationStart(Compilation compilation)
        {
            var typesClass = compilation.GetTypeByMetadataNameNonNull("Cesil.Types");

            var ret = typesClass.GetSourceSpans();

            return ret;
        }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, ImmutableArray<SourceSpan> typeRoots)
        {
            var typeofSyntax = context.Node.Expect<SyntaxNode, TypeOfExpressionSyntax>();

            var inTypes = typeRoots.ContainsNode(typeofSyntax);

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
                typeofSyntax.ReportDiagnostic(Diagnostics.Types, context);
            }
        }
    }
}
