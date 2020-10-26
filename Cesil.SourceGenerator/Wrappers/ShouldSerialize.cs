using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class ShouldSerialize
    {
        internal readonly IMethodSymbol Method;
        internal readonly bool IsStatic;
        internal readonly bool TakesContext;

        internal ShouldSerialize(IMethodSymbol method, bool isStatic, bool takesContext)
        {
            Method = method;
            IsStatic = isStatic;
            TakesContext = takesContext;
        }
    }
}
