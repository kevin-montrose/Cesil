using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class Setter
    {
        internal readonly ITypeSymbol ValueType;

        internal readonly IMethodSymbol? Method;
        internal readonly bool MethodTakesRow;
        internal readonly bool MethodTakesRowByRef;
        internal readonly bool MethodTakesContext;

        internal readonly IPropertySymbol? Property;
        internal readonly IFieldSymbol? Field;
        internal readonly IParameterSymbol? Parameter;
        
        internal int? ParameterPosition => Parameter?.Ordinal;

        internal Setter(IMethodSymbol method, ITypeSymbol valueType, bool takesRow, bool takesRowByRef, bool takesContext)
        {
            Method = method;
            MethodTakesRow = takesRow;
            MethodTakesRowByRef = takesRowByRef;
            MethodTakesContext = takesContext;

            ValueType = valueType;

            Property = null;
            Field = null;
            Parameter = null;
        }

        internal Setter(IPropertySymbol prop)
        {
            Method = null;
            Property = prop;
            Field = null;
            Parameter = null;

            ValueType = prop.Type;
        }

        internal Setter(IFieldSymbol field)
        {
            Method = null;
            Property = null;
            Field = field;
            Parameter = null;

            ValueType = field.Type;
        }

        internal Setter(IParameterSymbol parameter)
        {
            Method = null;
            Property = null;
            Field = null;
            Parameter = parameter;

            ValueType = parameter.Type;
        }
    }
}
