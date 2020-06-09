using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Analyzers
{
    /// <summary>
    /// Looks for direct use of IsComplete, IsCanceled, IsFaulted, or IsCompletedSuccessfully
    ///   on Task or ValueTask
    ///   
    /// If any of those are used, flag them with a suggestion to use the IsCompletedSuccessfully()
    ///   extension method instead
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class IsCompletedSuccessfullyAnalyzer : AnalyzerBase<IsCompletedSuccessfullyAnalyzer.State>
    {
        public sealed class State
        {
            public INamedTypeSymbol AsyncTestHelper { get; }
            public ImmutableHashSet<IPropertySymbol> ForbiddenMembers { get; }

            internal State(INamedTypeSymbol asyncTestHelper, ImmutableHashSet<IPropertySymbol> forbiddenMembers)
            {
                AsyncTestHelper = asyncTestHelper;
                ForbiddenMembers = forbiddenMembers;
            }
        }

        public IsCompletedSuccessfullyAnalyzer() : base(false, Diagnostics.IsCompletedSuccessfully, SyntaxKind.SimpleMemberAccessExpression) { }

        [SuppressMessage("MicrosoftCodeAnalysisPerformance", "RS1012:Start action has no registered actions.", Justification = "Handled in AnalyzerBase")]
        protected override State OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var comp = context.Compilation;

            // single class is allowed to use the forbidden members without explanation
            var asyncTestHelper = comp.GetTypeByMetadataName("Cesil.AsyncTestHelper");
            if (asyncTestHelper == null)
            {
                throw new InvalidOperationException("Expected AsyncTestHelper");
            }

            // get the stuff that we want to flag ONCE per compilation
            var task = comp.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var taskT = comp.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
            var valueTask = comp.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
            var valueTaskT = comp.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

            var ret = ImmutableHashSet.CreateBuilder<IPropertySymbol>();

            foreach (var sym in new[] { task, taskT, valueTask, valueTaskT })
            {
                if (sym == null)
                {
                    throw new InvalidOperationException("Symbol not defined");
                }

                var members = sym.GetMembers();
                foreach (var mem in members)
                {
                    if (mem.Kind != SymbolKind.Property) continue;

                    var name = mem.Name;

                    var include =
                        name == nameof(Task.IsCompleted) ||
                        name == nameof(Task.IsCanceled) ||
                        name == nameof(Task.IsFaulted) ||
                        name == nameof(ValueTask.IsCompletedSuccessfully);

                    if (include)
                    {
                        var prop = (IPropertySymbol)mem;
                        ret.Add(prop);
                    }
                }
            }

            return new State(asyncTestHelper, ret.ToImmutable());
        }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, State state)
        {
            if (!(context.Node is MemberAccessExpressionSyntax expr))
            {
                throw new InvalidOperationException($"Expected {nameof(MemberAccessExpressionSyntax)}");
            }

            var model = context.SemanticModel;

            var mark = false;

            var bound = model.GetSymbolInfo(expr);
            if (bound.Symbol is IPropertySymbol prop)
            {
                var originalProp = prop.OriginalDefinition;
                mark = state.ForbiddenMembers.Contains(originalProp);
            }

            if (mark)
            {
                var inAsyncTestHelper =
                    state
                        .AsyncTestHelper
                        .DeclaringSyntaxReferences
                        .Any(
                            syntaxRef =>
                            {
                                var tree = syntaxRef.SyntaxTree;
                                var root = tree.GetRoot();

                                return root.Contains(expr);
                            }
                        );

                if (inAsyncTestHelper)
                {
                    // no need to check uses in AsyncTestHelper, since that's what we suggest you use instead
                    return;
                }

                var loc = expr.GetLocation();
                var diag = Diagnostic.Create(
                    Diagnostics.IsCompletedSuccessfully,
                    loc
                );
                context.ReportDiagnostic(diag);
            }
        }
    }
}
