using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    internal sealed class AttributedMembers
    {
        public IAssemblySymbol CompilingAssembly { get; }

        public ImmutableArray<(AttributeSyntax Attribute, INamedTypeSymbol AttributeType, TypeDeclarationSyntax Type)> AttributedTypes { get; }
        public ImmutableArray<(AttributeSyntax Attribute, INamedTypeSymbol AttributeType, RecordDeclarationSyntax Record)> AttributedRecords { get; }
        public ImmutableArray<(AttributeSyntax Attribute, INamedTypeSymbol AttributeType, ConstructorDeclarationSyntax Constructor)> AttributedConstructors { get; }

        public ImmutableDictionary<TypeDeclarationSyntax, INamedTypeSymbol> TypeDeclarationsToNamedTypes { get; }

        public ImmutableDictionary<ISymbol, ImmutableArray<(AttributeSyntax Syntax, INamedTypeSymbol AttributeType)>> AttributedSymbolsToAttributes { get; }

        public ImmutableDictionary<AttributeSyntax, ImmutableDictionary<string, ImmutableArray<(SyntaxNode Node, Optional<object?> Value)>>> AttributeToConstantValues { get; }
        
        public ImmutableDictionary<AttributeSyntax, INamedTypeSymbol> AttributeSyntaxToAttributeType { get; }

        internal AttributedMembers(
            IAssemblySymbol compiling,
            ImmutableArray<(AttributeSyntax Attribute, INamedTypeSymbol AttributeType, TypeDeclarationSyntax Type)> types,
            ImmutableArray<(AttributeSyntax Attribute, INamedTypeSymbol AttributeType, RecordDeclarationSyntax Record)> records,
            ImmutableArray<(AttributeSyntax Attribute, INamedTypeSymbol AttributeType, ConstructorDeclarationSyntax Constructor)> constructors,
            ImmutableDictionary<TypeDeclarationSyntax, INamedTypeSymbol> typeDeclarationsToNamedTypes,
            ImmutableDictionary<ISymbol, ImmutableArray<(AttributeSyntax Syntax, INamedTypeSymbol AttributeType)>> attributedSymbolsToAttributes,
            ImmutableDictionary<AttributeSyntax, ImmutableDictionary<string, ImmutableArray<(SyntaxNode Node, Optional<object?> Value)>>> attributeConstantValues,
            ImmutableDictionary<AttributeSyntax, INamedTypeSymbol> attributeSyntaxToAttributeType
        )
        {
            CompilingAssembly = compiling;

            AttributedTypes = types;
            AttributedRecords = records;
            AttributedConstructors = constructors;

            TypeDeclarationsToNamedTypes = typeDeclarationsToNamedTypes;
            AttributedSymbolsToAttributes = attributedSymbolsToAttributes;
            AttributeToConstantValues = attributeConstantValues;
            AttributeSyntaxToAttributeType = attributeSyntaxToAttributeType;
        }

        internal ImmutableDictionary<TypeDeclarationSyntax, AttributeSyntax> GetAttributedDeclarations(INamedTypeSymbol attr)
        {
            var ret = ImmutableDictionary.CreateBuilder<TypeDeclarationSyntax, AttributeSyntax>();

            foreach (var t in AttributedTypes)
            {
                if (t.AttributeType.Equals(attr, SymbolEqualityComparer.Default))
                {
                    ret.Add(t.Type, t.Attribute);
                }
            }

            foreach (var t in AttributedRecords)
            {
                if (t.AttributeType.Equals(attr, SymbolEqualityComparer.Default))
                {
                    ret.Add(t.Record, t.Attribute);
                }
            }

            return ret.ToImmutable();
        }

        internal ImmutableDictionary<TypeDeclarationSyntax, (ConstructorDeclarationSyntax Constructor, AttributeSyntax Attribute)> GetAttributedConstructors(INamedTypeSymbol attr)
        {
            var ret = ImmutableDictionary.CreateBuilder<TypeDeclarationSyntax, (ConstructorDeclarationSyntax Constructor, AttributeSyntax Attribute)>();

            foreach (var (attrSyntax, attrType, cons) in AttributedConstructors)
            {
                if (attr.Equals(attrType, SymbolEqualityComparer.Default))
                {
                    var typeDecl = cons.ParentOrSelfOfType<TypeDeclarationSyntax>();
                    typeDecl = Utils.NonNull(typeDecl);

                    ret.Add(typeDecl, (cons, attrSyntax));
                }
            }

            return ret.ToImmutable();
        }
    }
}
