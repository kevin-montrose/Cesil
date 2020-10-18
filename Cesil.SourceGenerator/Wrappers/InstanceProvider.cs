using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    internal sealed class InstanceProvider
    {
        internal readonly ConstructorDeclarationSyntax? Constructor;
        internal readonly MethodDeclarationSyntax? Method;

        internal InstanceProvider(ConstructorDeclarationSyntax constructor)
        {
            Constructor = constructor;
        }

        internal InstanceProvider(MethodDeclarationSyntax method)
        {
            Method = method;
        }
    }
}
