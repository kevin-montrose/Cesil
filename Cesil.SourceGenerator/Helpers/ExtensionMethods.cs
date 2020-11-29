using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cesil.SourceGenerator
{
    internal static class ExtensionMethods
    {
        internal static string EscapeCSharp(this string text)
        =>SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(text)).ToFullString();

        internal static T? ParentOrSelfOfType<T>(this SyntaxNode self)
            where T : SyntaxNode
        {
            var cur = self;

            while (cur != null)
            {
                if (cur is T asT)
                {
                    return asT;
                }

                cur = cur.Parent;
            }

            return null;
        }

        internal static bool IsNormalParameterOfType(this IParameterSymbol p, Compilation compilation, ITypeSymbol type)
        {
            if(p.RefKind != RefKind.None)
            {
                return false;
            }

            var pType = p.Type;

            // without an in/ref/out/etc. conversions need to be checked
            var conversion = compilation.ClassifyConversion(type, pType);
            var canConvert = conversion.IsImplicit || conversion.IsIdentity;

            return canConvert;
        }

        internal static bool IsInWriteContext(this IParameterSymbol p, CesilTypes types)
        => IsInOfType(p, types.WriteContext);

        internal static bool IsInReadContext(this IParameterSymbol p, CesilTypes types)
        => IsInOfType(p, types.ReadContext);

        private static bool IsInOfType(this IParameterSymbol p, ITypeSymbol type)
        {
            if (p.RefKind != RefKind.In)
            {
                return false;
            }

            var pType = p.Type;

            return pType.Equals(type, SymbolEqualityComparer.Default);
        }
    }
}
