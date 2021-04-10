using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    // todo: in future Roslyn versions, we can replace this with ISyntaxContextReceiver
    //       which will remove the need for AttributeMembers being created with a Compilation
    internal sealed class AttributeTracker : ISyntaxReceiver
    {
        internal enum AttributeTarget : byte
        {
            Unknown = 0,
            Assembly,
            Module,
            Field,
            Event,
            Method,
            Parameter,
            Property,
            Return,
            Type,
        }

        private ImmutableArray<(AttributeSyntax Attribute, TypeDeclarationSyntax Type, AttributeTarget? RetargettedTo)>.Builder AttributedTypes;
        private ImmutableArray<(AttributeSyntax Attribute, RecordDeclarationSyntax Record, AttributeTarget? RetargettedTo)>.Builder AttributedRecords;
        private ImmutableArray<(AttributeSyntax Attribute, PropertyDeclarationSyntax Property, AttributeTarget? RetargettedTo)>.Builder AttributedProperties;
        private ImmutableArray<(AttributeSyntax Attribute, VariableDeclaratorSyntax Field, AttributeTarget? RetargettedTo)>.Builder AttributedFields;
        private ImmutableArray<(AttributeSyntax Attribute, BaseMethodDeclarationSyntax Method, AttributeTarget? RetargettedTo)>.Builder AttributedMethods;
        private ImmutableArray<(AttributeSyntax Attribute, ConstructorDeclarationSyntax Constructor, AttributeTarget? RetargettedTo)>.Builder AttributedConstructors;
        private ImmutableArray<(AttributeSyntax Attribute, ParameterSyntax Parameter, AttributeTarget? RetargettedTo)>.Builder AttributedConstructorParameters;

        internal AttributeTracker()
        {
            AttributedTypes = ImmutableArray.CreateBuilder<(AttributeSyntax Attribute, TypeDeclarationSyntax Type, AttributeTarget? RetargettedTo)>();
            AttributedRecords = ImmutableArray.CreateBuilder<(AttributeSyntax Attribute, RecordDeclarationSyntax Type, AttributeTarget? RetargettedTo)>();
            AttributedProperties = ImmutableArray.CreateBuilder<(AttributeSyntax Attribute, PropertyDeclarationSyntax Property, AttributeTarget? RetargettedTo)>();
            AttributedFields = ImmutableArray.CreateBuilder<(AttributeSyntax Attribute, VariableDeclaratorSyntax Field, AttributeTarget? RetargettedTo)>();
            AttributedMethods = ImmutableArray.CreateBuilder<(AttributeSyntax Attribute, BaseMethodDeclarationSyntax Method, AttributeTarget? RetargettedTo)>();
            AttributedConstructors = ImmutableArray.CreateBuilder<(AttributeSyntax Attribute, ConstructorDeclarationSyntax Constructor, AttributeTarget? RetargettedTo)>();
            AttributedConstructorParameters = ImmutableArray.CreateBuilder<(AttributeSyntax Attribute, ParameterSyntax Parameter, AttributeTarget? RetargettedTo)>();
        }

        internal AttributedMembers GetMembers(Compilation comp)
        {
            var cache = ImmutableDictionary<SyntaxTree, SemanticModel>.Empty;

            var types = AttributedTypes.ToImmutable();
            var records = AttributedRecords.ToImmutable();
            var props = AttributedProperties.ToImmutable();
            var fields = AttributedFields.ToImmutable();
            var methods = AttributedMethods.ToImmutable();
            var cons = AttributedConstructors.ToImmutable();
            var conParams = AttributedConstructorParameters.ToImmutable();

            var allInterestingDecls =
                types
                    .Select(x => x.Type)
                    .Concat(
                        records.Select(x => x.Record)
                    );

            var allMembers =
                types
                    .Select(t => (t.Attribute, Syntax: (SyntaxNode)t.Type, t.RetargettedTo))
                    .Concat(
                        records.Select(t => (t.Attribute, Syntax: (SyntaxNode)t.Record, t.RetargettedTo))
                    )
                    .Concat(
                        props.Select(t => (t.Attribute, Syntax: (SyntaxNode)t.Property, t.RetargettedTo))
                    )
                    .Concat(
                        fields.Select(t => (t.Attribute, Syntax: (SyntaxNode)t.Field, t.RetargettedTo))
                    )
                    .Concat(
                        methods.Select(t => (t.Attribute, Syntax: (SyntaxNode)t.Method, t.RetargettedTo))
                    )
                    .Concat(
                        cons.Select(t => (t.Attribute, Syntax: (SyntaxNode)t.Constructor, t.RetargettedTo))
                    )
                    .Concat(
                        conParams.Select(t => (t.Attribute, Syntax: (SyntaxNode)t.Parameter, t.RetargettedTo))
                    );

            var allAttrs = allMembers.Select(a => a.Attribute);

            var resolvedTypes = Resolve(comp, types, ref cache);
            var resolvedRecords = Resolve(comp, records, ref cache);
            var resolvedProps = Resolve(comp, props, ref cache);
            var resolvedFields = Resolve(comp, fields, ref cache);
            var resolvedMethods = Resolve(comp, methods, ref cache);
            var resolvedCons = Resolve(comp, cons, ref cache);
            var resolvedConParams = Resolve(comp, conParams, ref cache);

            var attrTypeLookup =
                resolvedTypes
                    .Select(x => (x.Attribute, x.AttributeType))
                    .Concat(
                        resolvedRecords.Select(x => (x.Attribute, x.AttributeType))
                    )
                    .Concat(
                        resolvedProps.Select(x => (x.Attribute, x.AttributeType))
                    )
                    .Concat(
                        resolvedFields.Select(x => (x.Attribute, x.AttributeType))
                    )
                    .Concat(
                        resolvedMethods.Select(x => (x.Attribute, x.AttributeType))
                    )
                    .Concat(
                        resolvedCons.Select(x => (x.Attribute, x.AttributeType))
                    )
                    .Concat(
                        resolvedConParams.Select(x => (x.Attribute, x.AttributeType))
                    )
                    .ToImmutableDictionary(t => t.Attribute, t => t.AttributeType);

            return
                new AttributedMembers(
                    comp.Assembly,
                    resolvedTypes,
                    resolvedRecords,
                    resolvedCons,
                    CreateTypeLookup(comp, allInterestingDecls, ref cache),
                    CreateMemberLookup(comp, allMembers, ref cache),
                    CreateAttributeConstantLookup(comp, allAttrs, ref cache),
                    attrTypeLookup
                );

            static ImmutableDictionary<AttributeSyntax, ImmutableDictionary<string, ImmutableArray<(SyntaxNode Node, Optional<object?> Value)>>> CreateAttributeConstantLookup(
                Compilation comp,
                IEnumerable<AttributeSyntax> attrs,
                ref ImmutableDictionary<SyntaxTree, SemanticModel> semanticCache
            )
            {
                var ret = ImmutableDictionary.CreateBuilder<AttributeSyntax, ImmutableDictionary<string, ImmutableArray<(SyntaxNode Node, Optional<object?> Value)>>>();

                foreach (var attr in attrs)
                {
                    if (attr.ArgumentList == null)
                    {
                        continue;
                    }

                    var constVals = ImmutableDictionary.CreateBuilder<string, ImmutableArray<(SyntaxNode Node, Optional<object?> Value)>>();

                    var model = GetOrAddToCache(comp, attr, ref semanticCache);

                    foreach (var arg in attr.ArgumentList.Arguments)
                    {
                        var name = arg.NameEquals?.Name;
                        if (name == null)
                        {
                            continue;
                        }

                        var nameStr = name.Identifier.ValueText;

                        var exp = arg.Expression;
                        Optional<object?> val;
                        if (exp is TypeOfExpressionSyntax typeofExp)
                        {
                            var typeInfo = model.GetTypeInfo(typeofExp.Type);

                            val = new Optional<object?>(typeInfo.Type);
                        }
                        else
                        {
                            val = model.GetConstantValue(exp);
                        }

                        if (!constVals.TryGetValue(nameStr, out var nameValues))
                        {
                            nameValues = ImmutableArray<(SyntaxNode Node, Optional<object?> Value)>.Empty;
                            constVals.Add(nameStr, nameValues);
                        }

                        constVals[nameStr] = nameValues.Add((exp, val));
                    }

                    ret.Add(attr, constVals.ToImmutable());
                }

                return ret.ToImmutable();
            }

            static ImmutableDictionary<ISymbol, ImmutableArray<(AttributeSyntax Syntax, INamedTypeSymbol Type)>> CreateMemberLookup(
                Compilation comp,
                IEnumerable<(AttributeSyntax Attribute, SyntaxNode Syntax, AttributeTarget? RetargettedTo)> members,
                ref ImmutableDictionary<SyntaxTree, SemanticModel> semanticCache
            )
            {
                var ret = ImmutableDictionary.CreateBuilder<ISymbol, ImmutableArray<(AttributeSyntax Syntax, INamedTypeSymbol Type)>>(SymbolEqualityComparer.Default);

                var retargetted = RetargetAttributes(comp, members, ref semanticCache);

                foreach (var grp in retargetted.GroupBy(m => m.Symbol, SymbolEqualityComparer.Default))
                {
                    var member = grp.Key;

                    var attrs = ImmutableArray.CreateBuilder<(AttributeSyntax Syntax, INamedTypeSymbol Type)>();
                    foreach (var (_, attr) in grp)
                    {
                        var model = GetOrAddToCache(comp, attr, ref semanticCache);

                        var attrTypeInfo = model.GetTypeInfo(attr);
                        var attrType = attrTypeInfo.Type;
                        if (attrType is INamedTypeSymbol namedType)
                        {
                            attrs.Add((attr, namedType));
                        }
                    }

                    ret.Add(member, attrs.ToImmutable());
                }

                return ret.ToImmutable();
            }

            static ImmutableDictionary<TypeDeclarationSyntax, INamedTypeSymbol> CreateTypeLookup(
                Compilation comp,
                IEnumerable<TypeDeclarationSyntax> decls,
                ref ImmutableDictionary<SyntaxTree, SemanticModel> semanticCache
            )
            {
                var ret = ImmutableDictionary.CreateBuilder<TypeDeclarationSyntax, INamedTypeSymbol>();

                foreach (var node in decls)
                {
                    var model = GetOrAddToCache(comp, node, ref semanticCache);

                    var typeInfo = model.GetDeclaredSymbol(node);

                    if (typeInfo is INamedTypeSymbol namedType)
                    {
                        ret.Add(node, namedType);
                    }
                }

                return ret.ToImmutable();
            }

            static ImmutableArray<(AttributeSyntax Attribute, INamedTypeSymbol AttributeType, T Node)> Resolve<T>(
                Compilation comp,
                ImmutableArray<(AttributeSyntax Attribute, T Node, AttributeTarget? RetargettedTo)> syntax,
                ref ImmutableDictionary<SyntaxTree, SemanticModel> semanticCache
            )
                where T : SyntaxNode
            {
                var ret = ImmutableArray.CreateBuilder<(AttributeSyntax Attribute, INamedTypeSymbol AttributeType, T Node)>(syntax.Length);

                foreach (var (attr, node, retargettedTo) in syntax)
                {
                    var model = GetOrAddToCache(comp, attr, ref semanticCache);

                    var attrTypeInfo = model.GetTypeInfo(attr);

                    if (attrTypeInfo.Type is INamedTypeSymbol attrType)
                    {
                        ret.Add((attr, attrType, node));
                    }
                }

                return ret.ToImmutable();
            }
        }

        private static SemanticModel GetOrAddToCache(Compilation comp, SyntaxNode node, ref ImmutableDictionary<SyntaxTree, SemanticModel> semanticCache)
        {
            var tree = node.SyntaxTree;

            if (!semanticCache.TryGetValue(tree, out var model))
            {
                model = comp.GetSemanticModel(tree);

                semanticCache = semanticCache.Add(tree, model);
            }

            return model;
        }

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is TypeDeclarationSyntax typeDecl)
            {
                if (typeDecl is RecordDeclarationSyntax recordDecl)
                {
                    RecordAttributes(recordDecl, AttributeTarget.Type, recordDecl.AttributeLists, AttributedRecords);

                    var ps = recordDecl.ParameterList;

                    if (ps != null)
                    {
                        foreach (var pDecl in ps.Parameters)
                        {
                            RecordAttributes(pDecl, AttributeTarget.Parameter, pDecl.AttributeLists, AttributedConstructorParameters);
                        }
                    }
                }
                else
                {
                    RecordAttributes(typeDecl, AttributeTarget.Type, typeDecl.AttributeLists, AttributedTypes);
                }
            }
            else if (syntaxNode is PropertyDeclarationSyntax propDecl)
            {
                RecordAttributes(propDecl, AttributeTarget.Property, propDecl.AttributeLists, AttributedProperties);
            }
            else if (syntaxNode is FieldDeclarationSyntax fieldDecl)
            {
                foreach (var varDecl in fieldDecl.Declaration.Variables)
                {
                    RecordAttributes(varDecl, AttributeTarget.Field, fieldDecl.AttributeLists, AttributedFields);
                }
            }
            else if (syntaxNode is BaseMethodDeclarationSyntax methodDecl)
            {
                // constructors need special handling, but are also a kind of method
                if (methodDecl is ConstructorDeclarationSyntax consDecl)
                {
                    RecordAttributes(consDecl, AttributeTarget.Method, consDecl.AttributeLists, AttributedConstructors);

                    foreach (var pDecl in consDecl.ParameterList.Parameters)
                    {
                        RecordAttributes(pDecl, AttributeTarget.Parameter, pDecl.AttributeLists, AttributedConstructorParameters);
                    }
                }
                else
                {
                    RecordAttributes(methodDecl, AttributeTarget.Method, methodDecl.AttributeLists, AttributedMethods);
                }
            }
        }

        // internal for testing purposes
        internal static ImmutableArray<(ISymbol Symbol, AttributeSyntax Attribute)> RetargetAttributes(
                Compilation comp,
                IEnumerable<(AttributeSyntax Attribute, SyntaxNode Syntax, AttributeTarget? RetargettedTo)> members,
                ref ImmutableDictionary<SyntaxTree, SemanticModel> semanticCache
            )
        {
            var mappedToDeclaredSymbol = ImmutableArray.CreateBuilder<(ISymbol Symbol, AttributeSyntax Syntax)>();
            foreach (var (attr, syntax, retargetted) in members)
            {
                var model = GetOrAddToCache(comp, syntax, ref semanticCache);

                var member = model.GetDeclaredSymbol(syntax);
                if (member == null)
                {
                    continue;
                }

                if (retargetted == null)
                {
                    mappedToDeclaredSymbol.Add((member, attr));
                    continue;
                }

                var retargetVal = retargetted.Value;

                if (member is IPropertySymbol prop)
                {
                    // on a property, we could re-target to the field (will still error probably, but "makes sense")
                    if (retargetVal == AttributeTarget.Field)
                    {
                        var backingField = prop.ContainingType.GetMembers().OfType<IFieldSymbol>().SingleOrDefault(f => prop.Equals(f.AssociatedSymbol, SymbolEqualityComparer.Default));
                        if (backingField != null)
                        {
                            mappedToDeclaredSymbol.Add((backingField, attr));
                        }
                    }
                }
                else if (member is IParameterSymbol param)
                {
                    // on a parameter, we could re-target to the PROPERTY declared implicitly on a record
                    if (retargetVal == AttributeTarget.Property)
                    {
                        var onType = param.ContainingType;
                        var (isRecord, _, props) = onType.IsRecord();
                        if (isRecord)
                        {
                            var correspondingProperty = props.SingleOrDefault(p => p.Name == param.Name);

                            if (correspondingProperty != null)
                            {
                                mappedToDeclaredSymbol.Add((correspondingProperty, attr));
                            }
                        }
                    }
                }

                // if we found something else, just ignore it
            }

            return mappedToDeclaredSymbol.ToImmutable();
        }

        // internal for testing purposes
        internal static void RecordAttributes<T>(
                T node,
                AttributeTarget defaultTarget,
                SyntaxList<AttributeListSyntax> attrLists,
                ImmutableArray<(AttributeSyntax Attribute, T Item, AttributeTarget? RetargettedTo)>.Builder recordTo
            )
        {
            foreach (var attrList in attrLists)
            {
                var declaredTarget = attrList.Target;
                var trueTarget = defaultTarget;

                if (declaredTarget != null)
                {
                    trueTarget =
                        declaredTarget.Identifier.ValueText switch
                        {
                            "assembly" => AttributeTarget.Assembly,
                            "module" => AttributeTarget.Module,
                            "field" => AttributeTarget.Field,
                            "event" => AttributeTarget.Event,
                            "method" => AttributeTarget.Method,
                            "param" => AttributeTarget.Parameter,
                            "property" => AttributeTarget.Property,
                            "return" => AttributeTarget.Return,
                            "type" => AttributeTarget.Type,
                            _ => AttributeTarget.Unknown
                        };
                }

                var isIgnoredTarget =
                    trueTarget == AttributeTarget.Unknown ||
                    trueTarget == AttributeTarget.Assembly ||
                    trueTarget == AttributeTarget.Module ||
                    trueTarget == AttributeTarget.Event ||
                    trueTarget == AttributeTarget.Return;

                if (isIgnoredTarget)
                {
                    continue;
                }

                foreach (var attr in attrList.Attributes)
                {
                    var retargetted = trueTarget != defaultTarget ? trueTarget : default(AttributeTarget?);

                    recordTo.Add((attr, node, retargetted));
                }
            }
        }
    }
}
