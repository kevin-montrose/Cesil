using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class ShouldSerialize
    {
        internal readonly IMethodSymbol Method;

        internal ShouldSerialize(IMethodSymbol method)
        {
            Method = method;
        }
    }
}
