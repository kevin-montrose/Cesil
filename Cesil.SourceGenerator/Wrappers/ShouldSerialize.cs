using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class ShouldSerialize
    {
        internal readonly IMethodSymbol Method;
        internal readonly bool IsStatic;
        internal readonly bool TakesRow;
        internal readonly bool TakesContext;

        internal ShouldSerialize(IMethodSymbol method, bool isStatic, bool takesRow, bool takesContext)
        {
            Method = method;
            IsStatic = isStatic;
            TakesRow = takesRow;
            TakesContext = takesContext;
        }
    }
}
