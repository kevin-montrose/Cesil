using System;
using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal static class Utils
    {
        private static readonly SymbolDisplayFormat FullyQualifiedFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        internal static string ToFullyQualifiedName(this ITypeSymbol symbol)
        => symbol.ToDisplayString(FullyQualifiedFormat);

        internal static T NonNull<T>(T? item)
            where T : class
        {
            if (item == null)
            {
                throw new InvalidOperationException("Found null value when that shouldn't be possible");
            }

            return item;
        }
    }
}
