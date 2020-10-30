using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class BuiltInTypes
    {
        internal readonly INamedTypeSymbol Bool;
        internal readonly INamedTypeSymbol Char;

        private BuiltInTypes(INamedTypeSymbol b, INamedTypeSymbol c)
        {
            Bool = b;
            Char = c;
        }

        internal static BuiltInTypes Create(Compilation compilation)
        {
            var b = compilation.GetSpecialType(SpecialType.System_Boolean);
            var c = compilation.GetSpecialType(SpecialType.System_Char);

            return new BuiltInTypes(b, c);
        }
    }
}
