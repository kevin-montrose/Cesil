using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    public sealed class SerializerGenerator : ISourceGenerator
    {
        public void Execute(SourceGeneratorContext context)
        {
            var compilation = context.Compilation;

            var rootAttr = compilation.GetTypeByMetadataName("Cesil.GenerateSerializableAttribute");
            if (rootAttr == null)
            {
                var diag = Diagnostic.Create(Diagnostics.NoCesilReference, null);
                context.ReportDiagnostic(diag);
                return;
            }

            var toGenerate = GetTypesToGenerate(compilation, rootAttr);

            if (toGenerate.IsEmpty)
            {
                return;
            }

            var cesilMemberAttr = compilation.GetTypeByMetadataName("Cesil.GenerateSerializableMemberAttribute");
            if(cesilMemberAttr == null)
            {
                var diag = Diagnostic.Create(Diagnostics.NoCesilReference, null);
                context.ReportDiagnostic(diag);
                return;
            }

            // this might be legitimately not included
            var dataMemberAttr = compilation.GetTypeByMetadataName("System.Runtime.Serialization.DataMemberAttribute");

            foreach (var decl in toGenerate)
            {
                var serializerSource = GenerateSerializer(context, compilation, decl, cesilMemberAttr, dataMemberAttr);
            }
        }

        private static string? GenerateSerializer(SourceGeneratorContext context, Compilation compilation, TypeDeclarationSyntax decl, INamedTypeSymbol cesilAttr, INamedTypeSymbol? dataMemberAttr)
        {
            var model = compilation.GetSemanticModel(decl.SyntaxTree);
            var typeInfo = model.GetTypeInfo(decl);
            var type = typeInfo.Type;
            if (type == null)
            {
                var diag = Diagnostic.Create(Diagnostics.GenericError, decl.GetLocation(), "Could not identify type");
                context.ReportDiagnostic(diag);
                return null;
            }

            var namedType = type as INamedTypeSymbol;
            if (namedType == null)
            {
                var diag = Diagnostic.Create(Diagnostics.GenericError, decl.GetLocation(), "Type identified, but not named");
                context.ReportDiagnostic(diag);
                return null;
            }

            
        }

        private static ImmutableArray<SerializableMember> GetSerializableMembers(Compilation compilation, INamedTypeSymbol namedType, INamedTypeSymbol cesilAttr, INamedTypeSymbol? dataMemberAttr)
        {
            var ret = ImmutableArray.CreateBuilder<SerializableMember>();

            foreach (var member in namedType.GetMembers())
            {
                var serializableMember = GetSerializableMember(compilation, member, cesilAttr, dataMemberAttr);
                if(serializableMember == null)
                {
                    continue;
                }

                ret.Add(serializableMember);
            }

            return ret.ToImmutable();
        }

        private static SerializableMember? GetSerializableMember(Compilation compilation, ISymbol member, INamedTypeSymbol cesilAttr, INamedTypeSymbol? dataMemberAttr)
        {
            if (member is IPropertySymbol prop)
            {
                var configAttrs = GetConfigurationAttributes(compilation, member, cesilAttr, dataMemberAttr);

                var isVisible =
                    member.DeclaredAccessibility == Accessibility.Public |
                    !configAttrs.IsEmpty;

                // either visible or annotated to include
                if (!isVisible)
                {
                    return null;
                }
                
                return SerializableMember.ForProperty(compilation, prop, configAttrs);
            }
            else if (member is IFieldSymbol field)
            {
                var configAttrs = GetConfigurationAttributes(compilation, member, cesilAttr, dataMemberAttr);

                // must be annotated to include
                if (configAttrs.IsEmpty)
                {
                    return null;
                }

                return SerializableMember.ForField(compilation, field, configAttrs);
            }
            else if (member is IMethodSymbol method)
            {
                var configAttrs = GetConfigurationAttributes(compilation, member, cesilAttr, dataMemberAttr);
                
                // must be annotated to include
                if (configAttrs.IsEmpty)
                {
                    return null;
                }

                return SerializableMember.ForMethod(compilation, method, configAttrs);
            }

            return null;
        }

        private static ImmutableArray<AttributeSyntax> GetConfigurationAttributes(Compilation compilation, ISymbol member, INamedTypeSymbol cesilAttr, INamedTypeSymbol? dataMemberAttr)
        {
            var relevantAttributes = ImmutableArray.CreateBuilder<AttributeSyntax>();

            foreach (var syntaxRef in member.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                var syntaxModel = compilation.GetSemanticModel(syntax.SyntaxTree);

                SyntaxList<AttributeListSyntax> attrLists;
                if (syntax is MethodDeclarationSyntax method)
                {
                    attrLists = method.AttributeLists;
                }
                else if (syntax is FieldDeclarationSyntax field)
                {
                    attrLists = field.AttributeLists;
                }
                else if (syntax is PropertyDeclarationSyntax prop)
                {
                    attrLists = prop.AttributeLists;
                }
                else
                {
                    throw new Exception("This shouldn't be possible");
                }

                foreach (var attrList in attrLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        var attrTypeInfo = syntaxModel.GetTypeInfo(attr);

                        var attrType = attrTypeInfo.Type;
                        if (attrType == null)
                        {
                            continue;
                        }

                        if (attrType.Equals(cesilAttr, SymbolEqualityComparer.Default))
                        {
                            relevantAttributes.Add(attr);
                            continue;
                        }

                        if (dataMemberAttr != null && attrType.Equals(dataMemberAttr, SymbolEqualityComparer.Default))
                        {
                            relevantAttributes.Add(attr);
                        }
                    }
                }
            }

            return relevantAttributes.ToImmutable();
        }

        private static ImmutableArray<TypeDeclarationSyntax> GetTypesToGenerate(Compilation compilation, ITypeSymbol generateAttr)
        {
            var ret = ImmutableArray.CreateBuilder<TypeDeclarationSyntax>();

            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);

                var root = tree.GetRoot();
                var decls = root.DescendantNodesAndSelf().OfType<TypeDeclarationSyntax>();
                foreach (var decl in decls)
                {
                    var attrLists = decl.AttributeLists;
                    foreach (var attrList in attrLists)
                    {
                        foreach (var attr in attrList.Attributes)
                        {
                            var attrTypeInfo = model.GetTypeInfo(attr);

                            var attrType = attrTypeInfo.Type;
                            if (attrType == null)
                            {
                                continue;
                            }

                            if (attrType.Equals(generateAttr, SymbolEqualityComparer.Default))
                            {
                                ret.Add(decl);
                            }
                        }
                    }
                }
            }

            return ret.ToImmutable();
        }

        public void Initialize(InitializationContext context)
        {
            // nothing to do
        }
    }
}
