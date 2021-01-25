using Microsoft.CodeAnalysis;

namespace Cesil.SourceGenerator
{
    internal sealed class BuiltInTypes
    {
        internal readonly INamedTypeSymbol Bool;
        internal readonly INamedTypeSymbol Char;

        internal readonly INamedTypeSymbol NullableOfT;

        private BuiltInTypes(INamedTypeSymbol b, INamedTypeSymbol c, INamedTypeSymbol n)
        {
            Bool = b;
            Char = c;
            NullableOfT = n;
        }

        internal static BuiltInTypes Create(Compilation compilation)
        {
            var b = compilation.GetSpecialType(SpecialType.System_Boolean);
            var c = compilation.GetSpecialType(SpecialType.System_Char);
            var n = compilation.GetSpecialType(SpecialType.System_Nullable_T);

            return new BuiltInTypes(b, c, n);
        }
    }
}
