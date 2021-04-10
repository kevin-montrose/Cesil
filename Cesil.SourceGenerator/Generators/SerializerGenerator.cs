using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Cesil.SourceGenerator.Constants;

namespace Cesil.SourceGenerator
{
    [Generator]
    internal sealed class SerializerGenerator : GeneratorBase<SerializerTypes, SerializableMember>
    {
        internal ImmutableArray<Formatter> NeededDefaultFormatters = ImmutableArray<Formatter>.Empty;

        internal override IEnumerable<(string FileName, string Source)> GenerateSource(
            ImmutableDictionary<INamedTypeSymbol, ImmutableArray<SerializableMember>> toGenerate
        )
        {
            const string DEFAULT_FORMATTERS_FILE_NAME = "__Cesil_Generated_DefaultFormatters.cs";

            NeededDefaultFormatters = Members.SelectMany(m => m.Value.Select(v => v.Formatter).Where(f => f.IsDefault)).Distinct().ToImmutableArray();

            var defaultFormatterFullyQualifiedTypeName = "--NONE--";
            var defaultFormatters = GenerateDefaultFormatterType(NeededDefaultFormatters);
            if (defaultFormatters != null)
            {
                string defaultFormattersSource;
                (defaultFormatterFullyQualifiedTypeName, defaultFormattersSource) = defaultFormatters.Value;

                yield return (DEFAULT_FORMATTERS_FILE_NAME, defaultFormattersSource);
            }

            foreach (var kv in Members)
            {
                var rowType = kv.Key;
                var columns = kv.Value;

                var inOrder = Utils.SortColumns(columns, a => a.Order);

                var source = GenerateSerializerType(defaultFormatterFullyQualifiedTypeName, rowType, inOrder);

                var generatedFileName = "__Cesil_Generated_Serializer_" + rowType.ToFullyQualifiedName().Replace(".", "_") + ".cs";

                yield return (generatedFileName, source);
            }
        }

        private static (string FullyQualifiedTypeName, string Source)? GenerateDefaultFormatterType(ImmutableArray<Formatter> formatters)
        {
            if (formatters.IsEmpty)
            {
                return null;
            }

            var sb = new StringBuilder();

            AddHeader(sb, "Formatting");

            sb.AppendLine("#nullable disable warnings");
            sb.AppendLine("#nullable enable annotations");
            sb.AppendLine("#pragma warning disable CS0162 // ignore unreachable code, this can happen because of inlining values that are known at source generation time");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Buffers;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine();
            sb.AppendLine("namespace Cesil.SourceGenerator.Generated");
            sb.AppendLine("{");

            var defaultFormatterName = "__DefaultFormatters";

            sb.AppendLine("  internal static class " + defaultFormatterName);
            sb.AppendLine("  {");

            foreach (var formatter in formatters)
            {
                var forType = Utils.NonNull(formatter.ForDefaultType);
                var code = Utils.NonNull(formatter.DefaultCode);

                if (formatter.DefaultIsMethod)
                {
                    var formatterMethodName = GetDefaultFormatterMethodName(forType);
                    var parsedMethod = (MethodDeclarationSyntax)Utils.NonNull(SyntaxFactory.ParseMemberDeclaration(code));
                    var parsedMethodBody = Utils.NonNull(parsedMethod.Body);

                    sb.AppendLine();
                    sb.AppendLine("    internal static System.Boolean " + formatterMethodName + "(" + formatter.ForDefaultType + " value, in Cesil.WriteContext ctx, System.Buffers.IBufferWriter<char> writer)");
                    sb.AppendLine("    {");
                    sb.AppendLine(parsedMethodBody.ToFullString());
                    sb.AppendLine("    }");
                }
                else
                {
                    var formatterClassName = GetDefaultFormatterClassName(forType);
                    var parsedClass = (ClassDeclarationSyntax)Utils.NonNull(SyntaxFactory.ParseMemberDeclaration(code));
                    var renamed = parsedClass.WithIdentifier(SyntaxFactory.ParseToken(formatterClassName));

                    sb.AppendLine();
                    sb.AppendLine(renamed.ToFullString());
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#pragma warning restore CS0162");
            sb.AppendLine("#nullable restore");

            var generatedCS = sb.ToString();

            var parsed = SyntaxFactory.ParseCompilationUnit(generatedCS);
            var normalized = parsed.NormalizeWhitespace();
            var source = normalized.ToFullString();

            return (defaultFormatterName, source);
        }

        private static string GetDefaultFormatterClassName(string forType)
        {
            var formatterClassName = "__Class_Formatter_" + forType.Replace(".", "_").TrimEnd('?');
            if (forType.EndsWith("?"))
            {
                formatterClassName += "_Nullable";
            }

            return formatterClassName;
        }

        private static string GetDefaultFormatterMethodName(string forType)
        {
            var formatterMethodName = "__Formatter_" + forType.Replace(".", "_").TrimEnd('?');
            if (forType.EndsWith("?"))
            {
                formatterMethodName += "_Nullable";
            }

            return formatterMethodName;
        }

        private static string GenerateSerializerType(string defaultFormatterFullyQualifiedTypeName, INamedTypeSymbol rowType, ImmutableArray<SerializableMember> columns)
        {
            var fullyQualifiedRowType = rowType.ToFullyQualifiedName();
            var generatedTypeName = "__Cesil_Generated_Serializer_" + fullyQualifiedRowType.Replace(".", "_");

            var sb = new StringBuilder();

            AddHeader(sb, "Serialize", fullyQualifiedRowType);

            sb.AppendLine("#nullable disable warnings");
            sb.AppendLine("#nullable enable annotations");
            sb.AppendLine();
            sb.AppendLine("namespace Cesil.SourceGenerator.Generated");
            sb.AppendLine("{");

            sb.AppendLine("  #pragma warning disable CS0618 // only Obsolete to prevent direct use");
            sb.AppendLine("  [Cesil.GeneratedSourceVersionAttribute(\"" + EXPECTED_CESIL_VERSION + "\", typeof(" + fullyQualifiedRowType + "), Cesil.GeneratedSourceVersionAttribute.GeneratedTypeKind.Serializer)]");
            sb.AppendLine("  #pragma warning restore CS0618");
            sb.AppendLine("  internal sealed class " + generatedTypeName);
            sb.AppendLine("  {");

            sb.Append("    public static readonly System.Collections.Immutable.ImmutableArray<System.String> ColumnNames = System.Collections.Immutable.ImmutableArray.CreateRange(new System.String[] { ");

            for (var i = 0; i < columns.Length; i++)
            {
                var col = columns[i];

                if (i != 0)
                {
                    sb.Append(", ");
                }

                var escaped = col.Name.EscapeCSharp();
                sb.Append(escaped);
            }
            sb.AppendLine(" });");

            for (var i = 0; i < columns.Length; i++)
            {
                var col = columns[i];

                var shouldSerialize = AddShouldSerializeMethod(sb, i, fullyQualifiedRowType, col.ShouldSerialize);
                var getter = AddGetterMethod(sb, i, fullyQualifiedRowType, col.Getter);
                var formatter = AddFormatterMethod(sb, defaultFormatterFullyQualifiedTypeName, i, col.Formatter);

                sb.AppendLine();
                if (!col.EmitDefaultValue)
                {
                    sb.AppendLine("    #pragma warning disable CS0618 // only Obsolete to prevent direct use");
                    sb.AppendLine("    [Cesil.DoesNotEmitDefaultValueAttribute]");
                    sb.AppendLine("    #pragma warning restore CS0618");
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
                    var takesType = col.Getter.ForType;
                    AddEmitDefaultEarlyReturn(sb, takesType);
                }

                sb.AppendLine("      var res = " + formatter + "(value, in ctx, writer);");
                sb.AppendLine();
                sb.AppendLine("      return res;");
                sb.AppendLine("    }");
            }

            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#nullable restore");

            var generatedCS = sb.ToString();

            var parsed = SyntaxFactory.ParseCompilationUnit(generatedCS);
            var normalized = parsed.NormalizeWhitespace();
            var ret = normalized.ToFullString();

            return ret;

            // add some code to return true IF the value we see is a default value
            static void AddEmitDefaultEarlyReturn(StringBuilder sb, ITypeSymbol type)
            {
                var typeFullyQualifiedName = type.ToFullyQualifiedName();

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
            static string AddFormatterMethod(StringBuilder sb, string defaultFormatterFullyQualifiedTypeName, int colIx, Formatter formatter)
            {
                if (formatter.IsDefault)
                {
                    var forType = Utils.NonNull(formatter.ForDefaultType);

                    if (formatter.DefaultIsMethod)
                    {
                        var defaultFormatterMethodName = GetDefaultFormatterMethodName(forType);

                        return defaultFormatterFullyQualifiedTypeName + "." + defaultFormatterMethodName;
                    }
                    else
                    {
                        var defaultFormatterClassName = GetDefaultFormatterClassName(forType);

                        return defaultFormatterFullyQualifiedTypeName + "." + defaultFormatterClassName + ".__TryFormat";
                    }
                }

                var takesType = Utils.NonNull(formatter.TakesType);
                var method = Utils.NonNull(formatter.Method);

                var takingType = takesType.ToFullyQualifiedName();
                if (takesType.NullableAnnotation == NullableAnnotation.Annotated && takesType.TypeKind != TypeKind.Struct)
                {
                    takingType += "?";
                }

                var formatterType = method.ContainingType.ToFullyQualifiedName();

                var formatterStatement = formatterType + "." + method.Name + "(value, in ctx, writer)";

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
            static string AddGetterMethod(StringBuilder sb, int colIx, string fullyQualifiedRowType, Getter getter)
            {
                string getStatement;

                var returnedFullyQualifiedTypeName = getter.ForType.ToFullyQualifiedName();
                if (getter.ForType.NullableAnnotation == NullableAnnotation.Annotated && getter.ForType.TypeKind != TypeKind.Struct)
                {
                    returnedFullyQualifiedTypeName += "?";
                }

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
                else
                {
                    var mtd = Utils.NonNull(getter.Method);
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
            static string? AddShouldSerializeMethod(StringBuilder sb, int colIx, string fullyQualifiedRowType, ShouldSerialize? shouldSerialize)
            {
                if (shouldSerialize == null)
                {
                    return null;
                }

                var mtdName = shouldSerialize.Method.Name;

                string invokeStatement;
                if (shouldSerialize.IsStatic)
                {
                    var onType = shouldSerialize.Method.ContainingType.ToFullyQualifiedName();

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

        internal override bool TryCreateNeededTypes(Compilation compilation, GeneratorExecutionContext context, out SerializerTypes? neededTypes)
        {
            var builtIn = BuiltInTypes.Create(compilation);

            if (!FrameworkTypes.TryCreate(compilation, builtIn, out var framework) || framework == null)
            {
                var diag = Diagnostics.NoSystemMemoryReference(null);
                context.ReportDiagnostic(diag);
                neededTypes = null;
                return false;
            }

            if (!CesilTypes.TryCreate(compilation, out var types) || types == null)
            {
                var diag = Diagnostics.NoCesilReference(null);
                context.ReportDiagnostic(diag);
                neededTypes = null;
                return false;
            }

            neededTypes = new SerializerTypes(builtIn, framework, types);
            return true;
        }

        internal override ImmutableDictionary<INamedTypeSymbol, ImmutableArray<SerializableMember>> GetMembersToGenerateFor(
            GeneratorExecutionContext context,
            Compilation compilation,
            ImmutableArray<TypeDeclarationSyntax> toGenerateFor,
            AttributedMembers attrMembers,
            SerializerTypes types
        )
        {
            var ret = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, ImmutableArray<SerializableMember>>();

            foreach (var decl in toGenerateFor)
            {
                var namedType = attrMembers.TypeDeclarationsToNamedTypes[decl];
              
                var members = GetSerializableMembers(context, attrMembers, compilation, types, namedType);
                if (!members.IsEmpty)
                {
                    ret.Add(namedType, members);
                }
            }

            return ret.ToImmutable();
        }

        private static ImmutableArray<SerializableMember> GetSerializableMembers(
            GeneratorExecutionContext context,
            AttributedMembers attrMembers,
            Compilation compilation,
            SerializerTypes types,
            INamedTypeSymbol namedType
        )
        {
            var hasErrors = false;
            var ret = ImmutableArray.CreateBuilder<SerializableMember>();

            var members = namedType.GetMembersIncludingInherited();
            foreach (var member in members)
            {
                var res = GetSerializableMember(compilation, attrMembers, types, namedType, member);
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
            AttributedMembers attrMembers,
            SerializerTypes types,
            INamedTypeSymbol serializingType,
            ISymbol member
        )
        {
            if (member is IPropertySymbol prop)
            {
                if (IsIgnored(member, types.Framework))
                {
                    return null;
                }

                var configAttrs = GetConfigurationAttributes(attrMembers, types.OurTypes.SerializerMemberAttribute, types.Framework, member);

                // skip properties if they have no getter _unless_ they're attributed (in which case there's an error we need to report)
                if (configAttrs.IsEmpty && prop.GetMethod == null)
                {
                    return null;
                }

                var include = prop.ShouldInclude(configAttrs);

                // neither visible or annotated to include
                if (!include)
                {
                    return null;
                }

                return SerializableMember.ForProperty(compilation, attrMembers, types, serializingType, prop, configAttrs);
            }
            else if (member is IFieldSymbol field)
            {
                var configAttrs = GetConfigurationAttributes(attrMembers, types.OurTypes.SerializerMemberAttribute, types.Framework, member);

                // must be annotated to include
                if (configAttrs.IsEmpty)
                {
                    return null;
                }

                return SerializableMember.ForField(compilation, attrMembers, types, serializingType, field, configAttrs);
            }
            else if (member is IMethodSymbol method)
            {
                var configAttrs = GetConfigurationAttributes(attrMembers, types.OurTypes.SerializerMemberAttribute, types.Framework, member);

                // must be annotated to include
                if (configAttrs.IsEmpty)
                {
                    return null;
                }

                return SerializableMember.ForMethod(compilation, attrMembers, types, serializingType, method, configAttrs);
            }

            return null;
        }

        internal override ImmutableArray<TypeDeclarationSyntax> GetTypesToGenerateFor(AttributedMembers members, SerializerTypes types)
        {
            var ret = ImmutableArray.CreateBuilder<TypeDeclarationSyntax>();

            SelectTypeDetails(members.AttributedTypes, types, ret);
            SelectTypeDetails(members.AttributedRecords, types, ret);

            return ret.ToImmutable();

            static void SelectTypeDetails<T>(
                ImmutableArray<(AttributeSyntax Attribute, INamedTypeSymbol AttributeType, T SyntaxDeclaration)> attributed,
                SerializerTypes types,
                ImmutableArray<TypeDeclarationSyntax>.Builder ret
            )
            where T : TypeDeclarationSyntax
            {
                foreach (var (_, attrType, typeDecl) in attributed)
                {
                    if (types.OurTypes.GenerateSerializerAttribute.Equals(attrType, SymbolEqualityComparer.Default))
                    {
                        ret.Add(typeDecl);
                    }
                }
            }
        }
    }
}
