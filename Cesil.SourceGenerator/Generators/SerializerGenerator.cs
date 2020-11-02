using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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

            foreach (var kv in Members)
            {
                var rowType = kv.Key;
                var columns = kv.Value;

                var inOrder = columns.Sort(
                    (a, b) =>
                    {
                        var aCurIx = columns.IndexOf(a);
                        var bCurIx = columns.IndexOf(b);

                        // if equal, preserve discovered order
                        if (a.Order == b.Order)
                        {
                            return aCurIx.CompareTo(bCurIx);
                        }

                        // sort nulls to the end
                        if (a.Order == null)
                        {
                            return 1;
                        }

                        if (b.Order == null)
                        {
                            return -1;
                        }

                        return a.Order.Value.CompareTo(b.Order.Value);
                    }
                );

                var source = GenerateSerializerType(compilation, rowType, inOrder);

                context.AddSource($"Cesil_Generated_{rowType.Name}.cs", SourceText.From(source));
            }
        }

        private static string GenerateSerializerType(Compilation compilation, INamedTypeSymbol rowType, ImmutableArray<SerializableMember> columns)
        {
            var fullyQualifiedFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

            var fullyQualifiedRowType = rowType.ToDisplayString(fullyQualifiedFormat);

            var sb = new StringBuilder();

            sb.AppendLine("namespace Cesil.SourceGenerator.Generated");
            sb.AppendLine("{");

            sb.AppendLine("  internal sealed class Generated_" + rowType.Name);
            sb.AppendLine("  {");

            sb.Append("    public static readonly System.String[] ColumnNames = new System.String[] { ");

            for (var i = 0; i < columns.Length; i++)
            {
                var col = columns[i];

                if (i != 0)
                {
                    sb.Append(", ");
                }

                var escaped = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(col.Name)).ToFullString();
                sb.Append(escaped);
            }
            sb.AppendLine(" };");

            for (var i = 0; i < columns.Length; i++)
            {
                var col = columns[i];

                sb.AppendLine();
                sb.AppendLine("    public static System.Boolean Column_" + i + "(System.Object rowObj, in Cesil.WriteContext ctx, System.Buffers.IBufferWriter<char> writer)");
                sb.AppendLine("    {");
                sb.AppendLine("      var row = (" + fullyQualifiedRowType + ")rowObj;");

                if (col.ShouldSerialize != null)
                {
                    var ss = col.ShouldSerialize;

                    var mtdName = ss.Method.Name;

                    string invokeStatement;
                    if (ss.IsStatic)
                    {
                        var onType = ss.Method.ContainingType.ToDisplayString(fullyQualifiedFormat);

                        invokeStatement = onType + "." + mtdName + "(";

                        if (ss.TakesRow)
                        {
                            invokeStatement += "row";


                            if (ss.TakesContext)
                            {
                                invokeStatement += ", in ctx";
                            }
                        }
                        else
                        {
                            if (ss.TakesContext)
                            {
                                invokeStatement += "in ctx";
                            }
                        }

                        invokeStatement += ")";
                    }
                    else
                    {
                        invokeStatement = "row." + mtdName + "(";

                        if (ss.TakesContext)
                        {
                            invokeStatement += "in ctx";
                        }

                        invokeStatement += ")";
                    }

                    sb.AppendLine("      var shouldSerialize = " + invokeStatement + ";");
                    sb.AppendLine("      if(!shouldSerialize) { return true; }");
                }

                var getter = col.Getter;

                string getStatement;

                if (getter.Field != null)
                {
                    var f = getter.Field;
                    if (f.IsStatic)
                    {
                        getStatement = fullyQualifiedRowType + "." + f.Name;
                    }
                    else
                    {
                        getStatement = "row." + f.Name;
                    }
                }
                else if (getter.Property != null)
                {
                    var p = getter.Property;
                    if (p.IsStatic)
                    {
                        getStatement = fullyQualifiedRowType + "." + p.Name;
                    }
                    else
                    {
                        getStatement = "row." + p.Name;
                    }
                }
                else if (getter.Method != null)
                {
                    var mtd = getter.Method;
                    if (mtd.IsStatic)
                    {
                        getStatement = fullyQualifiedRowType + "." + mtd.Name;
                    }
                    else
                    {
                        getStatement = "row." + mtd.Name;
                    }

                    getStatement += "(";
                    if (getter.MethodTakesRow)
                    {
                        getStatement += "row";
                        if (getter.MethodTakesContext)
                        {
                            getStatement += ", in ctx";
                        }
                    }
                    else
                    {
                        if (getter.MethodTakesContext)
                        {
                            getStatement += "in ctx";
                        }
                    }

                    getStatement += ")";
                }
                else
                {
                    throw new Exception("Shouldn't be possible");
                }

                sb.AppendLine("      var value = " + getStatement + ";");

                var formatter = col.Formatter;
                var formatterType = formatter.Method.ContainingType.ToDisplayString(fullyQualifiedFormat);

                var formatterStatement = formatterType + "." + formatter.Method.Name + "(value, in ctx, buffer)";

                sb.AppendLine("      var res = " + formatterStatement + ";");
                sb.AppendLine();
                sb.AppendLine("      return res;");
                sb.AppendLine("    }");
            }

            sb.AppendLine("  }");
            sb.AppendLine("}");

            var generatedCS = sb.ToString();

            var parsed = SyntaxFactory.ParseCompilationUnit(generatedCS);
            var normalized = parsed.NormalizeWhitespace();
            var ret = normalized.ToFullString();

            return ret;
        }

        private static bool TryCreateNeededTypes(Compilation compilation, SourceGeneratorContext context, [MaybeNullWhen(returnValue: false)] out SerializerTypes neededTypes)
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
                    member.DeclaredAccessibility == Accessibility.Public ||
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
