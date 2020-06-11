using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Analyzers
{
    /// <summary>
    /// Abstracts over attaching a debugger (which is a pain),
    ///   gathering some state at compilation start to use for the rest
    ///   of the analyzer, and attaching to specific syntax kinds for
    ///   the real work.
    /// </summary>
    public abstract class AnalyzerBase<TCompilationState> : DiagnosticAnalyzer
    {
        private readonly bool AttachDebugger;
        private readonly ImmutableArray<SyntaxKind> InspectsSyntax;

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

        protected AnalyzerBase(bool debug, DiagnosticDescriptor raisesDiagnostic, SyntaxKind inspect, params SyntaxKind[] inspectRest)
            : this(debug, new[] { raisesDiagnostic }, inspect, inspectRest)
        {
            AttachDebugger = debug;
            SupportedDiagnostics = ImmutableArray.Create(raisesDiagnostic);

            var inspects = ImmutableArray.CreateBuilder<SyntaxKind>();
            inspects.Add(inspect);
            inspects.AddRange(inspectRest);
            InspectsSyntax = inspects.ToImmutable();
        }

        protected AnalyzerBase(bool debug, DiagnosticDescriptor[] raisesDiagnostic, SyntaxKind inspect, params SyntaxKind[] inspectRest)
        {
            AttachDebugger = debug;

            if (raisesDiagnostic.Length == 0)
            {
                throw new ArgumentException("Cannot be empty", nameof(raisesDiagnostic));
            }
            SupportedDiagnostics = ImmutableArray.CreateRange(raisesDiagnostic);

            var inspects = ImmutableArray.CreateBuilder<SyntaxKind>();
            inspects.Add(inspect);
            inspects.AddRange(inspectRest);
            InspectsSyntax = inspects.ToImmutable();
        }

        public sealed override void Initialize(AnalysisContext context)
        {
            // helper for conveniently attaching a debugger
            DoAttachDebugger(AttachDebugger);

            // concurrent for performance sake's, but only if we're not debugging
            if (!AttachDebugger)
            {
                context.EnableConcurrentExecution();
            }

            // ignore generated code
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // hook up anything that needs to run on compilation start
            context.RegisterCompilationStartAction(
                context =>
                {
                    // grab the state
                    var state = OnCompilationStart(context.Compilation);

                    // visit whatever nodes
                    context.RegisterSyntaxNodeAction(
                        nodeContext =>
                        {
                            OnSyntaxNode(nodeContext, state);
                        },
                        InspectsSyntax
                    );
                }
            );
        }

        protected virtual TCompilationState OnCompilationStart(Compilation compilation)
        => default!;

        protected abstract void OnSyntaxNode(SyntaxNodeAnalysisContext context, TCompilationState state);

        [Conditional("DEBUG")]
        private static void DoAttachDebugger(bool doAttach)
        {
            if (!doAttach)
            {
                return;
            }

            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
        }
    }
}
