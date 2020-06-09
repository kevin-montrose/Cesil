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
    /// Flag uses of `BindingFlags`, suggesting use of `BindingFlagsConstants` instead.
    /// 
    /// Also suggests adding a `using static ... BindingFlagsConstants` if one is not present.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BindingFlagsAnalyzer : AnalyzerBase<BindingFlagsAnalyzer.Context>
    {
        public sealed class Context
        {
            public INamedTypeSymbol BindingFlagsConstants { get; }
            public INamedTypeSymbol BindingFlags { get; }
            public ImmutableArray<SyntaxNode> BindingFlagsConstantsRoots { get; }

            public Context(INamedTypeSymbol bindingFlagsConstants, INamedTypeSymbol bindingFlags, ImmutableArray<SyntaxNode> bindingFlagsConstantsRoots)
            {
                BindingFlagsConstants = bindingFlagsConstants;
                BindingFlags = bindingFlags;
                BindingFlagsConstantsRoots = bindingFlagsConstantsRoots;
            }
        }

        public BindingFlagsAnalyzer() :
            base(
                false,
                new[] { Diagnostics.BindingFlagsConstants, Diagnostics.UsingStaticBindingFlagsConstants },
                SyntaxKind.SimpleMemberAccessExpression
            )
        { }

        [SuppressMessage("MicrosoftCodeAnalysisPerformance", "RS1012:Start action has no registered actions.", Justification = "Handled in AnalyzerBase")]
        protected override Context OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var comp = context.Compilation;

            var bindingFlagsConstants = comp.GetTypeByMetadataName("Cesil.BindingFlagsConstants");
            if (bindingFlagsConstants == null)
            {
                throw new InvalidOperationException($"Expected BindingFlagsConstants");
            }

            var bindingFlags = comp.GetTypeByMetadataName("System.Reflection.BindingFlags");
            if (bindingFlags == null)
            {
                throw new InvalidOperationException($"Expected BindingFlags");
            }

            var bindingFlagsRoot =
                ImmutableArray.CreateRange(
                    bindingFlagsConstants
                        .DeclaringSyntaxReferences
                        .Select(
                            r =>
                            {
                                var tree = r.SyntaxTree;
                                var root = tree.GetRoot();

                                return root;
                            }
                        )
                    );

            return new Context(bindingFlagsConstants, bindingFlags, bindingFlagsRoot);
        }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, Context state)
        {
            var node = context.Node;
            if (!(node is MemberAccessExpressionSyntax accessSyntax))
            {
                throw new InvalidOperationException($"Expected {nameof(MemberAccessExpressionSyntax)} or {nameof(UsingDirectiveSyntax)}");
            }

            var inBindingFlagsConstants = state.BindingFlagsConstantsRoots.Any(root => root.Contains(node));
            if (inBindingFlagsConstants)
            {
                // don't flag _in_ BindingFlagsConstants
                return;
            }

            var model = context.SemanticModel;
            var leftHand = accessSyntax.Expression;

            var refersToInfo = model.GetSymbolInfo(leftHand);
            var refersToSym = refersToInfo.Symbol;
            if (refersToSym == null || !(refersToSym is INamedTypeSymbol namedSym))
            {
                // can't parse, bail
                return;
            }

            // don't use BindingFlags
            if (state.BindingFlags.Equals(namedSym, SymbolEqualityComparer.Default))
            {
                var diag = Diagnostic.Create(Diagnostics.BindingFlagsConstants, node.GetLocation());
                context.ReportDiagnostic(diag);
            }

            // if you use BindingFlagsConstant, use it with a using static
            if (state.BindingFlagsConstants.Equals(namedSym, SymbolEqualityComparer.Default))
            {
                var diag = Diagnostic.Create(Diagnostics.UsingStaticBindingFlagsConstants, node.GetLocation());
                context.ReportDiagnostic(diag);
            }
        }
    }
}
