using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    public sealed class SerializerGenerator : ISourceGenerator
    {
        internal SerializerTypes? NeededTypes;

        internal ImmutableArray<TypeDeclarationSyntax> ToGenerateFor = ImmutableArray<TypeDeclarationSyntax>.Empty;

        internal ImmutableDictionary<INamedTypeSymbol, ImmutableArray<SerializableMember>> Members = ImmutableDictionary<INamedTypeSymbol, ImmutableArray<SerializableMember>>.Empty;

        public void Execute(SourceGeneratorContext context)
        {
            var compilation = context.Compilation;

            if (!TryCreateNeededTypes(compilation, context, out NeededTypes))
            {
                return;
            }

            ToGenerateFor = GetTypesToGenerateFor(compilation, NeededTypes);

            if (ToGenerateFor.IsEmpty)
            {
                return;
            }

            Members = GetMembersToGenerateFor(context, compilation, ToGenerateFor, NeededTypes);

            // todo: actually write some C#
        }

        private static bool TryCreateNeededTypes(Compilation compilation, SourceGeneratorContext context, [MaybeNullWhen(returnValue: false)]out SerializerTypes neededTypes)
        {
            var builtIn = BuiltInTypes.Create(compilation);

            if (!FrameworkTypes.TryCreate(compilation, builtIn, out var framework))
            {
                var diag = Diagnostic.Create(Diagnostics.NoSystemMemoryReference, null);
                context.ReportDiagnostic(diag);
                neededTypes = null;
                return false;
            }

            if (!CesilTypes.TryCreate(compilation, out var types))
            {
                var diag = Diagnostic.Create(Diagnostics.NoCesilReference, null);
                context.ReportDiagnostic(diag);
                neededTypes = null;
                return false;
            }

            neededTypes = new SerializerTypes(builtIn, framework, types);
            return true;
        }

        private static ImmutableDictionary<INamedTypeSymbol, ImmutableArray<SerializableMember>> GetMembersToGenerateFor(
            SourceGeneratorContext context,
            Compilation compilation,
            ImmutableArray<TypeDeclarationSyntax> toGenerateFor,
            SerializerTypes types
        )
        {
            var ret = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, ImmutableArray<SerializableMember>>();

            foreach (var decl in toGenerateFor)
            {
                var model = compilation.GetSemanticModel(decl.SyntaxTree);
                var namedType = model.GetDeclaredSymbol(decl);
                if (namedType == null)
                {
                    var diag = Diagnostic.Create(Diagnostics.GenericError, decl.GetLocation(), "Type identified, but not named");
                    context.ReportDiagnostic(diag);
                    continue;
                }

                var members = GetSerializableMembers(context, compilation, types, namedType);
                if (!members.IsEmpty)
                {
                    ret.Add(namedType, members);
                }
            }

            return ret.ToImmutable();
        }

        private static ImmutableArray<SerializableMember> GetSerializableMembers(
            SourceGeneratorContext context,
            Compilation compilation,
            SerializerTypes types,
            INamedTypeSymbol namedType
        )
        {
            var hasErrors = false;
            var ret = ImmutableArray.CreateBuilder<SerializableMember>();

            foreach (var member in namedType.GetMembers())
            {
                var res = GetSerializableMember(compilation, types, namedType, member);
                if (res == null)
                {
                    continue;
                }

                var (serializableMember, diags) = res.Value;

                if (serializableMember != null)
                {
                    ret.Add(serializableMember);
                }
                else
                {
                    hasErrors = true;

                    foreach (var diag in diags)
                    {
                        context.ReportDiagnostic(diag);
                    }
                }
            }

            if (hasErrors)
            {
                return ImmutableArray<SerializableMember>.Empty;
            }

            return ret.ToImmutable();
        }

        private static (SerializableMember? Member, ImmutableArray<Diagnostic> Diagnostics)? GetSerializableMember(
            Compilation compilation,
            SerializerTypes types,
            INamedTypeSymbol serializingType,
            ISymbol member
        )
        {
            if (member is IPropertySymbol prop)
            {
                var configAttrs = GetConfigurationAttributes(compilation, types, member);

                var isVisible =
                    member.DeclaredAccessibility == Accessibility.Public |
                    !configAttrs.IsEmpty;

                // either visible or annotated to include
                if (!isVisible)
                {
                    return null;
                }

                return SerializableMember.ForProperty(compilation, types, serializingType, prop, configAttrs);
            }
            else if (member is IFieldSymbol field)
            {
                var configAttrs = GetConfigurationAttributes(compilation, types, member);

                // must be annotated to include
                if (configAttrs.IsEmpty)
                {
                    return null;
                }

                return SerializableMember.ForField(compilation, types, serializingType, field, configAttrs);
            }
            else if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
            {
                var configAttrs = GetConfigurationAttributes(compilation, types, member);

                // must be annotated to include
                if (configAttrs.IsEmpty)
                {
                    return null;
                }

                return SerializableMember.ForMethod(compilation, types, serializingType, method, configAttrs);
            }

            return null;
        }

        private static ImmutableArray<AttributeSyntax> GetConfigurationAttributes(Compilation compilation, SerializerTypes types, ISymbol member)
        {
            var relevantAttributes = ImmutableArray.CreateBuilder<AttributeSyntax>();

            foreach (var syntaxRef in member.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                var syntaxModel = compilation.GetSemanticModel(syntax.SyntaxTree);

                var method = syntax.ParentOrSelfOfType<MethodDeclarationSyntax>();
                var field = syntax.ParentOrSelfOfType<FieldDeclarationSyntax>();
                var prop = syntax.ParentOrSelfOfType<PropertyDeclarationSyntax>();

                SyntaxList<AttributeListSyntax> attrLists;
                if (method != null)
                {
                    attrLists = method.AttributeLists;
                }
                else if (field != null)
                {
                    attrLists = field.AttributeLists;
                }
                else if (prop != null)
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

                        if (attrType.Equals(types.OurTypes.GenerateSerializableMemberAttribute, SymbolEqualityComparer.Default))
                        {
                            relevantAttributes.Add(attr);
                            continue;
                        }

                        if (types.Framework.DataMemberAttribute != null && attrType.Equals(types.Framework.DataMemberAttribute, SymbolEqualityComparer.Default))
                        {
                            relevantAttributes.Add(attr);
                        }
                    }
                }
            }

            return relevantAttributes.ToImmutable();
        }

        private static ImmutableArray<TypeDeclarationSyntax> GetTypesToGenerateFor(Compilation compilation, SerializerTypes types)
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

                            if (attrType.Equals(types.OurTypes.GenerateSerializableAttribute, SymbolEqualityComparer.Default))
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
