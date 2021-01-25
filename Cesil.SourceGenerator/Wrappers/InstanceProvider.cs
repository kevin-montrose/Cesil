using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    internal sealed class InstanceProvider
    {
        internal readonly bool IsDefault;

        internal readonly bool IsConstructor;
        internal readonly ITypeSymbol RowType;

        internal readonly IMethodSymbol? Method;

        internal InstanceProvider(bool isConstructor, IMethodSymbol method, ITypeSymbol rowType)
        {
            IsDefault = false;
            IsConstructor = isConstructor;

            Method = method;
            RowType = rowType;
        }

        internal InstanceProvider(ITypeSymbol rowType)
        {
            IsDefault = true;
            IsConstructor = true;

            RowType = rowType;
        }

        internal static InstanceProvider? ForDefault(Compilation compilation, INamedTypeSymbol type, ref ImmutableArray<Diagnostic> diags)
        {
            var defaultCons = type.InstanceConstructors.SingleOrDefault(p => p.Parameters.Length == 0);

            if (defaultCons != null)
            {
                var accessible =
                    defaultCons.DeclaredAccessibility == Accessibility.Public ||
                    (compilation.Assembly.Equals(defaultCons.ContainingAssembly, SymbolEqualityComparer.Default) &&
                     defaultCons.DeclaredAccessibility == Accessibility.Internal);

                if (!accessible)
                {
                    var diag = Diagnostics.MethodNotPublicOrInternal(defaultCons.Locations.FirstOrDefault(), defaultCons);
                    diags = diags.Add(diag);

                    return null;
                }

                return new InstanceProvider(type);
            }
            else
            {
                var diag = Diagnostics.NoInstanceProvider(type.Locations.FirstOrDefault(), type);
                diags = diags.Add(diag);

                return null;
            }
        }

        internal static InstanceProvider? ForMethod(
            Compilation compilation,
            DeserializerTypes types,
            Location? loc,
            INamedTypeSymbol rowType,
            INamedTypeSymbol hostType,
            string mtdName,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var instanceProviderMtd = Utils.GetMethod(hostType, mtdName, loc, ref diags);
            if (instanceProviderMtd == null)
            {
                return null;
            }

            var instanceProviderLoc = instanceProviderMtd.Locations.FirstOrDefault();

            var methodReturnType = instanceProviderMtd.ReturnType;
            if (methodReturnType.SpecialType != SpecialType.System_Boolean)
            {
                var diag = Diagnostics.MethodMustReturnBool(instanceProviderLoc, instanceProviderMtd);
                diags = diags.Add(diag);

                return null;
            }

            if (!instanceProviderMtd.IsStatic)
            {
                var diag = Diagnostics.MethodNotStatic(instanceProviderLoc, instanceProviderMtd);
                diags = diags.Add(diag);

                return null;
            }

            var accessible =
                instanceProviderMtd.DeclaredAccessibility == Accessibility.Public ||
                (compilation.Assembly.Equals(instanceProviderMtd.ContainingAssembly, SymbolEqualityComparer.Default) &&
                 instanceProviderMtd.DeclaredAccessibility == Accessibility.Internal);

            if (!accessible)
            {
                var diag = Diagnostics.MethodNotPublicOrInternal(instanceProviderLoc, instanceProviderMtd);
                diags = diags.Add(diag);

                return null;
            }

            var ps = instanceProviderMtd.Parameters;
            if (ps.Length != 2)
            {
                var diag = Diagnostics.BadInstanceProviderParameters(instanceProviderLoc, instanceProviderMtd);
                diags = diags.Add(diag);

                return null;
            }

            var p0 = ps[0];
            if (!p0.IsInReadContext(types.OurTypes))
            {
                var diag = Diagnostics.BadInstanceProviderParameters(instanceProviderLoc, instanceProviderMtd);
                diags = diags.Add(diag);

                return null;
            }

            var p1 = ps[1];
            if (p1.RefKind != RefKind.Out)
            {
                var diag = Diagnostics.BadInstanceProviderParameters(instanceProviderLoc, instanceProviderMtd);
                diags = diags.Add(diag);

                return null;
            }

            var conversion = compilation.ClassifyConversion(p1.Type, rowType);
            var canConvert = conversion.IsImplicit || conversion.IsIdentity;
            if (!canConvert)
            {
                var diag = Diagnostics.BadInstanceProviderParameters(instanceProviderLoc, instanceProviderMtd);
                diags = diags.Add(diag);

                return null;
            }

            return new InstanceProvider(false, instanceProviderMtd, p1.Type);
        }

        internal static InstanceProvider? ForConstructorWithParameters(Compilation compilation, DeserializerTypes types, INamedTypeSymbol rowType, ConstructorDeclarationSyntax cons, ref ImmutableArray<Diagnostic> diags)
        {
            var model = compilation.GetSemanticModel(cons.SyntaxTree);

            var hasErrors = false;

            foreach (var p in cons.ParameterList.Parameters)
            {
                var pAttrs = p.AttributeLists;
                var foundMemberAttr = false;
                foreach (var attrList in pAttrs)
                {
                    foreach (var pAttr in attrList.Attributes)
                    {
                        var attrTypeInfo = model.GetTypeInfo(pAttr);

                        var attrType = attrTypeInfo.Type;
                        if (attrType == null)
                        {
                            continue;
                        }

                        if (attrType.Equals(types.OurTypes.DeserializerMemberAttribute, SymbolEqualityComparer.Default))
                        {
                            foundMemberAttr = true;
                            break;
                        }
                    }

                    if (foundMemberAttr)
                    {
                        break;
                    }
                }

                if (!foundMemberAttr)
                {
                    // no need to raise a diagnostic here, it'll be found elsewhere
                    hasErrors = true;
                }
            }

            if (hasErrors)
            {
                return null;
            }

            var decl = Utils.NonNull(model.GetDeclaredSymbol(cons));

            return new InstanceProvider(true, decl, rowType);
        }
    }
}
