using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class BuiltInTypes
    {
        internal readonly INamedTypeSymbol Char;

        private BuiltInTypes(INamedTypeSymbol c)
        {
            Char = c;
        }

        internal static BuiltInTypes Create(Compilation compilation)
        {
            var c = compilation.GetSpecialType(SpecialType.System_Char);

            return new BuiltInTypes(c);
        }
    }
}
