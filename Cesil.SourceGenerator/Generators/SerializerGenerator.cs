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
        private const string EXPECTED_CESIL_VERSION = "0.7.0";

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

                var source = GenerateSerializerType(rowType, inOrder);

                context.AddSource($"Cesil_Generated_{rowType.Name}.cs", SourceText.From(source));
            }
        }

        private static string GenerateSerializerType(INamedTypeSymbol rowType, ImmutableArray<SerializableMember> columns)
        {
            var fullyQualifiedFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

            var fullyQualifiedRowType = rowType.ToDisplayString(fullyQualifiedFormat);

            var sb = new StringBuilder();

            sb.AppendLine("namespace Cesil.SourceGenerator.Generated");
            sb.AppendLine("{");

            sb.AppendLine("  [Cesil.GeneratedSourceVersionAttribute(\"" + EXPECTED_CESIL_VERSION + "\", typeof(" + fullyQualifiedRowType + "), 1)]");
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

                var shouldSerialize = AddShouldSerializeMethod(sb, i, fullyQualifiedFormat, fullyQualifiedRowType, col.ShouldSerialize);
                var getter = AddGetterMethod(sb, i, fullyQualifiedFormat, fullyQualifiedRowType, col.Getter);
                var formatter = AddFormatterMethod(sb, i, fullyQualifiedFormat, col.Formatter);

                sb.AppendLine();
                if (!col.EmitDefaultValue)
                {
                    sb.AppendLine("    [Cesil.DoesNotEmitDefaultValueAttribute]");
                }
                sb.AppendLine("    public static System.Boolean __Column_" + i + "(System.Object rowObj, in Cesil.WriteContext ctx, System.Buffers.IBufferWriter<char> writer)");
                sb.AppendLine("    {");
                sb.AppendLine("      var row = (" + fullyQualifiedRowType + ")rowObj;");

                if (shouldSerialize != null)
                {
                    sb.AppendLine("      var shouldSerialize = " + shouldSerialize + "(row, in ctx);");
                    sb.AppendLine("      if(!shouldSerialize) { return true; }");
                }

                sb.AppendLine("      var value = " + getter + "(row, in ctx);");

                if (!col.EmitDefaultValue)
                {
                    AddEmitDefaultEarlyReturn(sb, fullyQualifiedFormat, col.Formatter.TakesType);
                }

                sb.AppendLine("      var res = " + formatter + "(value, in ctx, writer);");
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

            // add some code to return true IF the value we see is a default value
            static void AddEmitDefaultEarlyReturn(StringBuilder sb, SymbolDisplayFormat fullyQualifiedFormat, ITypeSymbol type)
            {
                var typeFullyQualifiedName = type.ToDisplayString(fullyQualifiedFormat);

                var ops = type.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.BuiltinOperator);
                var eqOp = ops.Any(o => o.MetadataName == "op_Equality");

                bool isPrimitive;
                switch (type.SpecialType)
                {
                    case SpecialType.System_Boolean:
                    case SpecialType.System_Char:
                    case SpecialType.System_SByte:
                    case SpecialType.System_Byte:
                    case SpecialType.System_Int16:
                    case SpecialType.System_UInt16:
                    case SpecialType.System_Int32:
                    case SpecialType.System_UInt32:
                    case SpecialType.System_Int64:
                    case SpecialType.System_UInt64:
                    case SpecialType.System_Single:
                    case SpecialType.System_Double:
                    case SpecialType.System_Decimal:
                        isPrimitive = true;
                        break;

                    default:
                        isPrimitive = false;
                        break;
                }

                var defaultExp = "default(" + typeFullyQualifiedName + ")";

                string isDefaultExp;

                if (type.IsReferenceType || type.TypeKind == TypeKind.Enum || isPrimitive || eqOp)
                {
                    isDefaultExp = "value == " + defaultExp;
                }
                else
                {
                    isDefaultExp = "value.Equals(" + defaultExp + ")";
                }

                sb.AppendLine("      if(" + isDefaultExp + ") { return true; }");
            }

            // add a method that does the real "formatter" work and returns the name of that method
            static string AddFormatterMethod(StringBuilder sb, int colIx, SymbolDisplayFormat fullyQualifiedFormat, Formatter formatter)
            {
                var takingType = formatter.TakesType.ToDisplayString(fullyQualifiedFormat);
                var formatterType = formatter.Method.ContainingType.ToDisplayString(fullyQualifiedFormat);

                var formatterStatement = formatterType + "." + formatter.Method.Name + "(value, in ctx, writer)";

                var formatterMethodName = "__Column_" + colIx + "_Formatter";

                sb.AppendLine();
                sb.AppendLine("    [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine("    public static System.Boolean " + formatterMethodName + "(" + takingType + " value, in Cesil.WriteContext ctx, System.Buffers.IBufferWriter<char> writer)");
                sb.AppendLine("    {");
                sb.AppendLine("      var ret = " + formatterStatement + ";");
                sb.AppendLine("      return ret;");
                sb.AppendLine("    }");

                return formatterMethodName;
            }

            // add a method that does the real "getter" work and returns the name of that method
            static string AddGetterMethod(StringBuilder sb, int colIx, SymbolDisplayFormat fullyQualifiedFormat, string fullyQualifiedRowType, Getter getter)
            {
                string getStatement;

                var returnedFullyQualifiedTypeName = getter.ForType.ToDisplayString(fullyQualifiedFormat);

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

                var getterMethodName = "__Column_" + colIx + "_Getter";

                sb.AppendLine();
                sb.AppendLine("    [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine("    public static " + returnedFullyQualifiedTypeName + " " + getterMethodName + "(" + fullyQualifiedRowType + " row, in Cesil.WriteContext ctx)");
                sb.AppendLine("    {");
                sb.AppendLine("      var ret = " + getStatement + ";");
                sb.AppendLine("      return ret;");
                sb.AppendLine("    }");

                return getterMethodName;
            }

            // add a method that does the real "should serialize" work and returns the name of that method, if there is one
            static string? AddShouldSerializeMethod(StringBuilder sb, int colIx, SymbolDisplayFormat fullyQualifiedFormat, string fullyQualifiedRowType, ShouldSerialize? shouldSerialize)
            {
                if (shouldSerialize == null)
                {
                    return null;
                }

                var mtdName = shouldSerialize.Method.Name;

                string invokeStatement;
                if (shouldSerialize.IsStatic)
                {
                    var onType = shouldSerialize.Method.ContainingType.ToDisplayString(fullyQualifiedFormat);

                    invokeStatement = onType + "." + mtdName + "(";

                    if (shouldSerialize.TakesRow)
                    {
                        invokeStatement += "row";


                        if (shouldSerialize.TakesContext)
                        {
                            invokeStatement += ", in ctx";
                        }
                    }
                    else
                    {
                        if (shouldSerialize.TakesContext)
                        {
                            invokeStatement += "in ctx";
                        }
                    }

                    invokeStatement += ")";
                }
                else
                {
                    invokeStatement = "row." + mtdName + "(";

                    if (shouldSerialize.TakesContext)
                    {
                        invokeStatement += "in ctx";
                    }

                    invokeStatement += ")";
                }

                var shouldSerializeMethodName = "__Column_" + colIx + "_ShouldSerialize";

                sb.AppendLine();
                sb.AppendLine("    [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine("    public static System.Boolean " + shouldSerializeMethodName + "(" + fullyQualifiedRowType + " row, in Cesil.WriteContext ctx)");
                sb.AppendLine("    {");
                sb.AppendLine("      var ret = " + invokeStatement + ";");
                sb.AppendLine("      return ret;");
                sb.AppendLine("    }");

                return shouldSerializeMethodName;
            }
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
