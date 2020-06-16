using System.Collections.Immutable;
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
    public sealed class BindingFlagsAnalyzer : AnalyzerBase<BindingFlagsAnalyzer.State>
    {
        public sealed class State
        {
            public ImmutableArray<SourceSpan> BindingFlagsConstantsRoots { get; }
            public INamedTypeSymbol BindingFlagsConstants { get; }
            public ImmutableHashSet<string> BindingFlagsConstantsNames { get; }

            public INamedTypeSymbol BindingFlags { get; }
            public ImmutableHashSet<string> BindingFlagsNames { get; }



            public State(INamedTypeSymbol bindingFlagsConstants, INamedTypeSymbol bindingFlags, ImmutableArray<SourceSpan> bindingFlagsConstantsRoots)
            {
                BindingFlagsConstants = bindingFlagsConstants;
                BindingFlags = bindingFlags;
                BindingFlagsConstantsRoots = bindingFlagsConstantsRoots;

                {
                    var bfcns = ImmutableHashSet.CreateBuilder<string>();
                    foreach (var member in bindingFlagsConstants.GetMembers())
                    {
                        bfcns.Add(member.Name);
                    }
                    BindingFlagsConstantsNames = bfcns.ToImmutable();
                }

                {
                    var bfns = ImmutableHashSet.CreateBuilder<string>();
                    foreach (var member in bindingFlags.GetMembers())
                    {
                        bfns.Add(member.Name);
                    }
                    BindingFlagsNames = bfns.ToImmutable();
                }

            }
        }

        public BindingFlagsAnalyzer() :
            base(
                false,
                new[] { Diagnostics.BindingFlagsConstants, Diagnostics.UsingStaticBindingFlagsConstants },
                SyntaxKind.SimpleMemberAccessExpression
            )
        { }

        protected override State OnCompilationStart(Compilation compilation)
        {
            var bindingFlagsConstants = compilation.GetTypeByMetadataNameNonNull("Cesil.BindingFlagsConstants");
            var bindingFlags = compilation.GetTypeByMetadataNameNonNull("System.Reflection.BindingFlags");

            var bindingFlagsRoot = bindingFlagsConstants.GetSourceSpans();

            return new State(bindingFlagsConstants, bindingFlags, bindingFlagsRoot);
        }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, State state)
        {
            var accessSyntax = context.Node.Expect<SyntaxNode, MemberAccessExpressionSyntax>();

            var inBindingFlagsConstants = state.BindingFlagsConstantsRoots.ContainsNode(accessSyntax);
            if (inBindingFlagsConstants)
            {
                // don't flag _in_ BindingFlagsConstants
                return;
            }

            var rightHand = accessSyntax.Name.Identifier.ValueText;

            var mightBeBindingFlags = state.BindingFlagsNames.Contains(rightHand);
            var mightBeBindingFlagsContants = state.BindingFlagsConstantsNames.Contains(rightHand);

            if (!mightBeBindingFlags && !mightBeBindingFlagsContants)
            {
                // nothing we care about, bail before touching the semantic model
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
            if (mightBeBindingFlags && state.BindingFlags.Equals(namedSym, SymbolEqualityComparer.Default))
            {
                accessSyntax.ReportDiagnostic(Diagnostics.BindingFlagsConstants, context);
            }

            // if you use BindingFlagsConstant, use it with a using static
            if (mightBeBindingFlagsContants && state.BindingFlagsConstants.Equals(namedSym, SymbolEqualityComparer.Default))
            {
                accessSyntax.ReportDiagnostic(Diagnostics.UsingStaticBindingFlagsConstants, context);
            }
        }
    }
}
