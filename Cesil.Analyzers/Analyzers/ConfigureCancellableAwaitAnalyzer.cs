﻿using System;
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
            var awaitHelper = compilation.GetTypeByMetadataName("Cesil.AwaitHelper");
            if (awaitHelper == null)
            {
                throw new InvalidOperationException("Expected AwaitHelper");
            }

            var members = ImmutableHashSet.CreateBuilder<IMethodSymbol>();
            foreach (var mem in awaitHelper.GetMembers("ConfigureCancellableAwait"))
            {
                if (!(mem is IMethodSymbol mtd)) continue;

                members.Add(mtd);
            }

            var methodsMustBeCalled = members.ToImmutable();

            return methodsMustBeCalled;
        }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, ImmutableHashSet<IMethodSymbol> methodsMustBeCalled)
        {
            var node = context.Node;
            if (!(node is AwaitExpressionSyntax await))
            {
                throw new InvalidOperationException($"Expected {nameof(AwaitExpressionSyntax)}");
            }

            var model = context.SemanticModel;

            var mark = true;

            var rightHand = await.Expression;
            if (rightHand is InvocationExpressionSyntax invoke)
            {
                var symInfo = model.GetSymbolInfo(invoke.Expression);
                if (symInfo.Symbol is IMethodSymbol mtdSym)
                {
                    if (methodsMustBeCalled.Contains(mtdSym.OriginalDefinition))
                    {
                        mark = false;
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