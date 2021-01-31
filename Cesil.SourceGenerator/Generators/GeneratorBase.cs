using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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

            var relevantSyntax = ((AttributeTracker)Utils.NonNull(context.SyntaxReceiver)).GetMembers(compilation);

            ToGenerateFor = GetTypesToGenerateFor(relevantSyntax, types);

            if (ToGenerateFor.IsEmpty)
            {
                return;
            }

            Members = GetMembersToGenerateFor(context, compilation, ToGenerateFor, relevantSyntax, types);

            var generated = GenerateSource(Members);

            foreach(var (name, source) in generated)
            {
                var cleanSource = Utils.RemoveUnusedUsings(compilation, source);

                context.AddSource(name, SourceText.From(cleanSource, Encoding.UTF8));
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new AttributeTracker());
        }

        internal abstract bool TryCreateNeededTypes(Compilation compilation, GeneratorExecutionContext context, out TNeededTypes? neededTypes);

        internal abstract ImmutableArray<TypeDeclarationSyntax> GetTypesToGenerateFor(AttributedMembers attrMembers, TNeededTypes types);

        internal abstract ImmutableDictionary<INamedTypeSymbol, ImmutableArray<TMemberDescriber>> GetMembersToGenerateFor(
            GeneratorExecutionContext context,
            Compilation compilation,
            ImmutableArray<TypeDeclarationSyntax> toGenerateFor,
            AttributedMembers members,
            TNeededTypes types
        );

        internal abstract IEnumerable<(string FileName, string Source)> GenerateSource(
            ImmutableDictionary<INamedTypeSymbol, ImmutableArray<TMemberDescriber>> toGenerate
        );

        internal static ImmutableArray<AttributeSyntax> GetConfigurationAttributes(
            AttributedMembers attrMembers,
            ITypeSymbol attributeType,
            FrameworkTypes frameworkTypes,
            ISymbol member
        )
        {
            if (!attrMembers.AttributedSymbolsToAttributes.TryGetValue(member, out var attrs))
            {
                return ImmutableArray<AttributeSyntax>.Empty;
            }

            var relevantAttributes = ImmutableArray.CreateBuilder<AttributeSyntax>();

            foreach (var (attr, attrType) in attrs)
            {
                if (attributeType.Equals(attrType, SymbolEqualityComparer.Default))
                {
                    relevantAttributes.Add(attr);
                }

                if (frameworkTypes.DataMemberAttribute != null && frameworkTypes.DataMemberAttribute.Equals(attrType, SymbolEqualityComparer.Default))
                {
                    relevantAttributes.Add(attr);
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
            return attrs.Any(a => ignoreDataMember.Equals(a.AttributeClass, SymbolEqualityComparer.Default));
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
