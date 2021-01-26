using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    internal abstract class GeneratorBase<TNeededTypes, TMemberDescriber> : ISourceGenerator
        where TNeededTypes : class
        where TMemberDescriber : class
    {
        internal TNeededTypes? NeededTypes;

        internal ImmutableArray<TypeDeclarationSyntax> ToGenerateFor = ImmutableArray<TypeDeclarationSyntax>.Empty;

        internal ImmutableDictionary<INamedTypeSymbol, ImmutableArray<TMemberDescriber>> Members = ImmutableDictionary<INamedTypeSymbol, ImmutableArray<TMemberDescriber>>.Empty;

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            if (!TryCreateNeededTypes(compilation, context, out NeededTypes))
            {
                return;
            }

            var types = Utils.NonNull(NeededTypes);

            ToGenerateFor = GetTypesToGenerateFor(compilation, types);

            if (ToGenerateFor.IsEmpty)
            {
                return;
            }

            Members = GetMembersToGenerateFor(context, compilation, ToGenerateFor, types);

            GenerateSource(compilation, context, types, Members);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // nothing to do
        }

        internal abstract bool TryCreateNeededTypes(Compilation compilation, GeneratorExecutionContext context, out TNeededTypes? neededTypes);

        internal abstract ImmutableArray<TypeDeclarationSyntax> GetTypesToGenerateFor(Compilation compilation, TNeededTypes types);

        internal abstract ImmutableDictionary<INamedTypeSymbol, ImmutableArray<TMemberDescriber>> GetMembersToGenerateFor(
            GeneratorExecutionContext context,
            Compilation compilation,
            ImmutableArray<TypeDeclarationSyntax> toGenerateFor,
            TNeededTypes types
        );

        internal abstract void GenerateSource(
            Compilation compilation,
            GeneratorExecutionContext context,
            TNeededTypes types,
            ImmutableDictionary<INamedTypeSymbol, ImmutableArray<TMemberDescriber>> toGenerate
        );

        internal static ImmutableArray<AttributeSyntax> GetConfigurationAttributes(
            Compilation compilation,
            ITypeSymbol attributeType,
            FrameworkTypes frameworkTypes,
            ISymbol member
        )
        {
            var relevantAttributes = ImmutableArray.CreateBuilder<AttributeSyntax>();

            foreach (var syntaxRef in member.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                var syntaxModel = compilation.GetSemanticModel(syntax.SyntaxTree);

                var method = syntax.ParentOrSelfOfType<MethodDeclarationSyntax>();
                var field = syntax.ParentOrSelfOfType<FieldDeclarationSyntax>();
                var prop = syntax.ParentOrSelfOfType<PropertyDeclarationSyntax>();
                var parameter = syntax.ParentOrSelfOfType<ParameterSyntax>();

                // property attribute usage allows indexers to be annotated... so need
                //   to read them here so we can report errors later
                var indexer = syntax.ParentOrSelfOfType<IndexerDeclarationSyntax>();

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
                else if (indexer != null)
                {
                    attrLists = indexer.AttributeLists;
                }
                else if (parameter != null)
                {
                    attrLists = parameter.AttributeLists;
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

                        if (attrType.Equals(attributeType, SymbolEqualityComparer.Default))
                        {
                            relevantAttributes.Add(attr);
                            continue;
                        }

                        if (frameworkTypes.DataMemberAttribute != null && attrType.Equals(frameworkTypes.DataMemberAttribute, SymbolEqualityComparer.Default))
                        {
                            relevantAttributes.Add(attr);
                        }
                    }
                }
            }

            return relevantAttributes.ToImmutable();
        }

        internal static bool IsIgnored(ISymbol member, FrameworkTypes types)
        {
            var ignoreDataMember = types.IgnoreDataMemberAttribute;

            if (ignoreDataMember == null)
            {
                return false;
            }

            var attrs = member.GetAttributes();
            return attrs.Any(a => a.AttributeClass?.Equals(ignoreDataMember, SymbolEqualityComparer.Default) ?? false);
        }

        internal static void AddHeader(StringBuilder sb, string action, string? forType = null)
        {
            sb.AppendLine("//***************************************************************************//");
            sb.AppendLine("// This file was automatically generated and should not be modified by hand. ");
            sb.AppendLine("//");
            sb.Append("// Library: ");
            sb.AppendLine(nameof(Cesil) + "." + nameof(Cesil.SourceGenerator));
            sb.AppendLine("//");
            sb.Append("// Purpose: ");
            sb.AppendLine(action);
            sb.AppendLine("//");
            if (forType != null)
            {
                sb.Append("// For: ");
                sb.AppendLine(forType);
                sb.AppendLine("//");
            }
            sb.Append("// On: ");
            sb.AppendLine(DateTime.UtcNow.ToString("u"));
            sb.AppendLine("//***************************************************************************//");
            sb.AppendLine();
        }
    }
}
