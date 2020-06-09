using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cesil.Analyzers
{
    /// <summary>
    /// Check that public members are only declared on types that are themselves public.
    /// 
    /// Exceptions include:
    ///  - implementing interface members (which are defacto-public)
    ///  - overriding members (we don't control the visibility)
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PublicMemberAnalyzer : AnalyzerBase<object?>
    {
        public PublicMemberAnalyzer() : base(false, Diagnostics.PublicMember, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration) { }

        protected override void OnSyntaxNode(SyntaxNodeAnalysisContext context, object? state)
        {
            var node = context.Node;
            if (!(node is TypeDeclarationSyntax typeDecl))
            {
                throw new InvalidOperationException($"Expected {nameof(TypeDeclarationSyntax)}");
            }

            var isPublic = typeDecl.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword);
            if (isPublic)
            {
                // nothing to do if it's public
                return;
            }

            var model = context.SemanticModel;

            var namedType = model.GetDeclaredSymbol(typeDecl);
            if (namedType == null)
            {
                // can't parse it, bail
                return;
            }

            var interfaceMembers = ImmutableHashSet.CreateBuilder<ISymbol>();

            foreach (var i in namedType.AllInterfaces)
            {
                foreach (var m in i.GetMembers())
                {
                    var implementing = namedType.FindImplementationForInterfaceMember(m);
                    if (implementing != null)
                    {
                        interfaceMembers.Add(implementing);
                    }
                }
            }

            var canBePublic = interfaceMembers.ToImmutable();

            foreach (var member in typeDecl.Members)
            {
                OnMemberDeclaration(context, model, canBePublic, member);
            }
        }

        private static void OnMemberDeclaration(SyntaxNodeAnalysisContext context, SemanticModel model, ImmutableHashSet<ISymbol> canBePublic, MemberDeclarationSyntax member)
        {
            SyntaxToken publicMod = default;
            var isMemPublic = false;
            var isMemOverride = false;
            var isMemOperator = member is ConversionOperatorDeclarationSyntax;
            foreach (var modifier in member.Modifiers)
            {
                var kind = modifier.Kind();

                if (!isMemPublic && kind == SyntaxKind.PublicKeyword)
                {
                    publicMod = modifier;
                    isMemPublic = true;
                }

                if (kind == SyntaxKind.OverrideKeyword)
                {
                    isMemOverride = true;
                    break;
                }
            }

            if (!isMemPublic)
            {
                // not public, don't care
                return;
            }

            if (isMemOverride)
            {
                // can't change the visibility, just roll with it
                return;
            }

            if (isMemOperator)
            {
                // operators must be public
                return;
            }

            var memberSym = model.GetDeclaredSymbol(member);
            if (memberSym != null)
            {
                if (canBePublic.Contains(memberSym))
                {
                    // it's public, but it's _allowed_ to be public
                    return;
                }
            }

            var diag = Diagnostic.Create(Diagnostics.PublicMember, publicMod.GetLocation());
            context.ReportDiagnostic(diag);
        }
    }
}
