using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    /// <summary>
    /// Generates classes that can be used to read paired types.
    /// 
    /// Basically fakes ITypeDescriber, but at compile time.  Accordingly, this is all done with attributes.
    /// 
    /// Types to read have [GeneratedReader] on them.
    /// 
    /// InstanceProviders are found either:
    ///  - default constructor
    ///  - constructor with [InstanceProvider]
    ///  - method with [InstanceProvider]
    ///  
    /// Parsers are found either:
    ///  - well known types, default implementation
    ///  - method with [Parser]
    /// 
    /// Resets are found either:
    ///  - named Reset(Name)
    ///  - method with [Reset]
    ///  
    /// Setters are found either:
    ///  - public properties
    ///  - method with [Setter]
    /// </summary>
    [Generator]
    public sealed class RowConstructorGenerator : ISourceGenerator
    {
        private const string GENERATE_ATTRIBUTE_NAME = "GenerateReaderAttribute";

        private const string INSTANCE_PROVIDER_ATTRIBUTE_NAME = "InstanceProviderAttribute";
        private const string PARSER_ATTRIBUTE_NAME = "ParserAttribute";
        private const string RESET_ATTRIBUTE_NAME = "ResetAttribute";
        private const string SETTER_ATTRIBUTE_NAME = "SetterAttribute";

        private readonly DiagnosticDescriptor NO_CESIL_REFERENCE = new DiagnosticDescriptor("CES1000", "Cesil not referenced", "Could not find a type referenced by Cesil, are you missing a reference?", "Generator", DiagnosticSeverity.Error, true);
        private readonly DiagnosticDescriptor NO_INSTANCE_PROVIDER = new DiagnosticDescriptor("CES1001", "No InstanceProvider Found", "Could not find a constructor or method to provide instances for {0}", "Generator", DiagnosticSeverity.Error, true);

        public void Initialize(InitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new RowConstructorSyntaxReceiver(GENERATE_ATTRIBUTE_NAME));
        }

        public void Execute(SourceGeneratorContext context)
        {
            var receiver = context.SyntaxReceiver as RowConstructorSyntaxReceiver;
            if (receiver == null)
            {
                throw new InvalidOperationException($"Expected {nameof(RowConstructorSyntaxReceiver)}, found: {context.SyntaxReceiver}");
            }

            var attr = context.Compilation.GetTypeByMetadataName($"Cesil.{GENERATE_ATTRIBUTE_NAME}");
            if (attr == null)
            {
                var diag = Diagnostic.Create(NO_CESIL_REFERENCE, null);
                context.ReportDiagnostic(diag);
                return;
            }

            var nodes = receiver.GetDeclarations();

            foreach (var node in nodes)
            {
                if (!InstanceProvider.TryGet(node, out var ip))
                {
                    var diag = Diagnostic.Create(NO_INSTANCE_PROVIDER, node.Identifier.GetLocation(), new[] { node.Identifier.ValueText });
                    context.ReportDiagnostic(diag);
                    continue;
                }
            }
        }
    }

    internal sealed class RowConstructorSyntaxReceiver : ISyntaxReceiver
    {
        private readonly string AttributeName;
        private readonly string AttributeNameWithAttribute;

        private ImmutableArray<TypeDeclarationSyntax>.Builder Nodes;

        internal RowConstructorSyntaxReceiver(string attributeName)
        {
            if (!attributeName.EndsWith("Attribute"))
            {
                throw new ArgumentException($"Attribute name should end with Attribute, found: {attributeName}", nameof(attributeName));
            }

            AttributeNameWithAttribute = attributeName;
            AttributeName = $"{attributeName}Attribute";

            Nodes = ImmutableArray.CreateBuilder<TypeDeclarationSyntax>();
        }

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax classDecl && HasAttribute(classDecl.AttributeLists))
            {
                Nodes.Add(classDecl);
            }
            else if (syntaxNode is StructDeclarationSyntax structDecl && HasAttribute(structDecl.AttributeLists))
            {
                Nodes.Add(structDecl);
            }
        }

        internal bool HasAttribute(SyntaxList<AttributeListSyntax> list)
        {
            foreach (var attrList in list)
            {
                if (attrList == null) continue;

                foreach (var attr in attrList.Attributes)
                {
                    if (IsPotentiallyOurAttribute(attr.Name))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool IsPotentiallyOurAttribute(NameSyntax name)
        {
            return name switch
            {
                AliasQualifiedNameSyntax alias => SameAlias(alias),
                QualifiedNameSyntax qualified => SameQualified(qualified),
                GenericNameSyntax generic => SameGeneric(generic),
                IdentifierNameSyntax ident => SameIdentifier(ident),
                _ => throw new ArgumentException($"Unexpected {nameof(NameSyntax)}: {name}")
            };

            bool SameAlias(AliasQualifiedNameSyntax alias)
            => SameIdentifier(alias.Alias);

            bool SameQualified(QualifiedNameSyntax qualified)
            {
                return IsPotentiallyOurAttribute(qualified.Right);
            }

            bool SameGeneric(GenericNameSyntax generic)
            {
                var name = generic.Identifier.ValueText;

                return name.Equals(AttributeName) || name.Equals(AttributeNameWithAttribute);
            }

            bool SameIdentifier(IdentifierNameSyntax ident)
            {
                var name = ident.Identifier.ValueText;

                return name.Equals(AttributeName) || name.Equals(AttributeNameWithAttribute);
            }
        }

        internal ImmutableArray<TypeDeclarationSyntax> GetDeclarations()
        {
            var ret = Nodes.ToImmutable();

            Nodes.Clear();

            return ret;
        }
    }
}
