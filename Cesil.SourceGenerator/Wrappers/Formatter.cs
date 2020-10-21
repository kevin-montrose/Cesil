using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class Formatter
    {
        internal readonly IMethodSymbol Method;

        internal Formatter(IMethodSymbol method)
        {
            Method = method;
        }
    }
}
