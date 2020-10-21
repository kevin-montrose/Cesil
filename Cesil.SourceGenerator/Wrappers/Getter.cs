using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class Getter
    {
        internal readonly IMethodSymbol? Method;
        internal readonly IPropertySymbol? Property;
        internal readonly IFieldSymbol? Field;

        internal Getter(IMethodSymbol method)
        {
            Method = method;
            Property = null;
            Field = null;
        }

        internal Getter(IPropertySymbol prop)
        {
            Method = null;
            Property = prop;
            Field = null;
        }

        internal Getter(IFieldSymbol field)
        {
            Method = null;
            Property = null;
            Field = field;
        }
    }
}
