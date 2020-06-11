using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Analyzers
{
    internal static class ExtensionMethods
    {
        // todo: can GetTreeRoots() and ContainsNode() be replaced with a simple range check?
        //       ie. does this text occur in one of these files XYZ in ranges ABC?

        /// <summary>
        /// Gets all the root nodes of the syntax that defines parts of the given symbol,
        ///   and wrap them up in SourceSpans for easy checking.
        /// 
        /// Note that this isn't the root of the SyntaxTrees that contain the definition of the symbol,
        ///   it is the top most "declaration node" for the given symbols.
        ///   
        /// This can produce multiple symbols due to partials and whatnot.
        /// </summary>
        public static ImmutableArray<SourceSpan> GetSourceSpans(this INamedTypeSymbol symbol)
        => ImmutableArray.CreateRange(symbol.DeclaringSyntaxReferences.Select(r => SourceSpan.Create(r.GetSyntax())));

        /// <summary>
        /// Return true if any of the SourceSpans in spans contains the given node.
        /// </summary>
        public static bool ContainsNode(this ImmutableArray<SourceSpan> spans, SyntaxNode node)
        {
            var span = SourceSpan.Create(node);

            foreach(var other in spans)
            {
                if (other.Contains(span))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Raise a diagnostic on the given node.
        /// </summary>
        public static void ReportDiagnostic(this SyntaxNode node, DiagnosticDescriptor diagnostic, SyntaxNodeAnalysisContext context)
        {
            var diag = Diagnostic.Create(diagnostic, node.GetLocation());
            context.ReportDiagnostic(diag);
        }

        /// <summary>
        /// Raise a diagnostic on the given token.
        /// </summary>
        public static void ReportDiagnostic(this SyntaxToken node, DiagnosticDescriptor diagnostic, SyntaxNodeAnalysisContext context)
        {
            var diag = Diagnostic.Create(diagnostic, node.GetLocation());
            context.ReportDiagnostic(diag);
        }
    }
}
