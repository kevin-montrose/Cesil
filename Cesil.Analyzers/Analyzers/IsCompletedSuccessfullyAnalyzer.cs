using System;
using System.Collections.Immutable;
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
            public ImmutableArray<SourceSpan> AsyncTestHelperRoots { get; }
            public ImmutableHashSet<IPropertySymbol> ForbiddenMembers { get; }

            internal State(ImmutableArray<SourceSpan> asyncTestHelperRoots, ImmutableHashSet<IPropertySymbol> forbiddenMembers)
            {
                AsyncTestHelperRoots = asyncTestHelperRoots;
                ForbiddenMembers = forbiddenMembers;
            }
        }

        public IsCompletedSuccessfullyAnalyzer() : base(false, Diagnostics.IsCompletedSuccessfully, SyntaxKind.SimpleMemberAccessExpression) { }

        protected override State OnCompilationStart(Compilation compilation)
        {
            // single class is allowed to use the forbidden members without explanation
            var asyncTestHelper = compilation.GetTypeByMetadataName("Cesil.AsyncTestHelper");
            if (asyncTestHelper == null)
            {
                throw new InvalidOperationException("Expected AsyncTestHelper");
            }

            var asyncTestHelperRoots = asyncTestHelper.GetSourceSpans();

            // get the stuff that we want to flag ONCE per compilation
            var task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var taskT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
            var valueTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
            var valueTaskT = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

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

            return new State(asyncTestHelperRoots, ret.ToImmutable());
        }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, State state)
        {
            var node = context.Node;

            if (!(node is MemberAccessExpressionSyntax expr))
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
                var inAsyncTestHelper = state.AsyncTestHelperRoots.ContainsNode(node);
                if (inAsyncTestHelper)
                {
                    // no need to check uses in AsyncTestHelper, since that's what we suggest you use instead
                    return;
                }

                node.ReportDiagnostic(Diagnostics.IsCompletedSuccessfully, context);
            }
        }
    }
}
