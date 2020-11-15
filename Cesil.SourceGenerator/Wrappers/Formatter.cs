using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class Formatter
    {
        internal readonly IMethodSymbol Method;
        internal readonly ITypeSymbol TakesType;

        internal Formatter(IMethodSymbol method, ITypeSymbol takesType)
        {
            Method = method;
            TakesType = takesType;
        }
    }
}
