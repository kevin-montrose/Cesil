using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class Reset
    {
        internal readonly IMethodSymbol Method;
        internal readonly bool IsStatic;
        internal readonly bool TakesRow;
        internal readonly bool TakesRowByRef;
        internal readonly bool TakesContext;

        internal Reset(IMethodSymbol method, bool isStatic, bool takesRow, bool takesRowByRef, bool takesContext)
        {
            Method = method;
            IsStatic = isStatic;
            TakesRow = takesRow;
            TakesRowByRef = takesRowByRef;
            TakesContext = takesContext;
        }
    }
}
