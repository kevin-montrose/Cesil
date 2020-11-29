using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using Microsoft.CodeAnalysis.Text;
using System.Text;

using static Cesil.SourceGenerator.Constants;

namespace Cesil.SourceGenerator
{
    [Generator]
    internal sealed class DeserializerGenerator : GeneratorBase<DeserializerTypes, DeserializableMember>
    {
        internal ImmutableDictionary<TypeDeclarationSyntax, AttributeSyntax> TypeDeclarationsToDeserializeAttribute = ImmutableDictionary<TypeDeclarationSyntax, AttributeSyntax>.Empty;
        internal ImmutableDictionary<TypeDeclarationSyntax, (ConstructorDeclarationSyntax Constructor, AttributeSyntax InstanceProviderAttribute)> TypeDeclarationsToConstructorsWithAttribute = ImmutableDictionary<TypeDeclarationSyntax, (ConstructorDeclarationSyntax Constructor, AttributeSyntax InstanceProviderAttribute)>.Empty;
        internal ImmutableArray<Parser> NeededDefaultParsers = ImmutableArray<Parser>.Empty;

        internal bool SkipGeneration;
        internal ImmutableDictionary<INamedTypeSymbol, InstanceProvider> InstanceProviders = ImmutableDictionary<INamedTypeSymbol, InstanceProvider>.Empty;

        internal override void GenerateSource(Compilation compilation, GeneratorExecutionContext context, DeserializerTypes types, ImmutableDictionary<INamedTypeSymbol, ImmutableArray<DeserializableMember>> toGenerate)
        {
            NeededDefaultParsers = Members.SelectMany(m => m.Value.Select(v => v.Parser).Where(p => p.IsDefault)).Distinct().ToImmutableArray();

            var defaultParsersFullyQualifiedTypeName = "--NONE--";
            var defaultParsers = GenerateDefaultParserType(NeededDefaultParsers);
            if (defaultParsers != null)
            {
                string defaultParsersSource;
                (defaultParsersFullyQualifiedTypeName, defaultParsersSource) = defaultParsers.Value;

                var defaultParsersFileName = "__Cesil_Generated_DefaultParsers.cs";

                defaultParsersSource = Utils.RemoveUnusedUsings(compilation, defaultParsersSource);

                context.AddSource(defaultParsersFileName, SourceText.From(defaultParsersSource, Encoding.UTF8));
            }

            foreach (var kv in Members)
            {
                var rowType = kv.Key;
                var columns = kv.Value;
                var instanceProvider = InstanceProviders[rowType];

                var inOrder = Utils.SortColumns(columns, a => a.Order);

                var source = GenerateDeserializerType(defaultParsersFullyQualifiedTypeName, rowType, instanceProvider, inOrder);
                source = Utils.RemoveUnusedUsings(compilation, source);

                var generatedFileName = "__Cesil_Generated_Deserializer_" + rowType.ToFullyQualifiedName().Replace(".", "_") + ".cs";

                context.AddSource(generatedFileName, SourceText.From(source, Encoding.UTF8));
            }
        }

        private static string GenerateDeserializerType(
            string defaultParsersFullyQualifiedTypeName,
            INamedTypeSymbol rowType,
            InstanceProvider instanceProvider,
            ImmutableArray<DeserializableMember> columns
        )
        {
            var fullyQualifiedRowType = rowType.ToFullyQualifiedName();
            var generatedTypeName = "__Cesil_Generated_Deserializer_" + fullyQualifiedRowType.Replace(".", "_");

            var isValueType = rowType.IsValueType;
            var needsHold = instanceProvider.IsConstructor && !instanceProvider.IsDefault;

            var sb = new StringBuilder();

            sb.AppendLine("#nullable disable warnings");
            sb.AppendLine("#nullable enable annotations");
            sb.AppendLine();
            sb.AppendLine("namespace Cesil.SourceGenerator.Generated");
            sb.AppendLine("{");

            sb.AppendLine("  #pragma warning disable CS0618 // only Obsolete to prevent direct use");
            sb.AppendLine("  [Cesil.GeneratedSourceVersionAttribute(\"" + EXPECTED_CESIL_VERSION + "\", typeof(" + fullyQualifiedRowType + "), Cesil.GeneratedSourceVersionAttribute.GeneratedTypeKind.Deserializer)]");
            sb.AppendLine("  #pragma warning restore CS0618");
            sb.AppendLine("  internal sealed class " + generatedTypeName);
            sb.AppendLine("  {");

            // names in declaration order
            sb.Append("    public static System.Collections.Immutable.ImmutableArray<System.String> __ColumnNames { get; } = System.Collections.Immutable.ImmutableArray.CreateRange(new System.String[] { ");

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

            // for the current reader, how to map columns by ordinal to methods
            sb.AppendLine();
            sb.AppendLine("    private System.Memory<System.Int32> __CurrentColumnMap_Memory = System.Memory<System.Int32>.Empty;");
            sb.AppendLine("    public ref System.Memory<System.Int32> __CurrentColumnMap => ref __CurrentColumnMap_Memory;");

            // the current row
            sb.AppendLine();
            sb.AppendLine("    private System.Boolean __CurrentRowPopulated = false;");
            sb.AppendLine("    private " + fullyQualifiedRowType + " __CurrentRow = default;");

            sb.AppendLine();
            sb.AppendLine("    public System.Boolean __RowStarted { get; private set; }");

            // add instance provider
            var instanceProviderMethod = AddInstanceProvider(sb, fullyQualifiedRowType, instanceProvider);
            AddTryPreAllocate(sb, isValueType, needsHold, fullyQualifiedRowType, instanceProviderMethod);

            var tryMoveFromHold = needsHold ? AddTryMoveFromHoldMethod(sb, fullyQualifiedRowType, instanceProvider, columns) : "";

            // where data comes in
            sb.AppendLine();
            sb.AppendLine("  public System.Boolean __ColumnAvailable(System.ReadOnlySpan<char> data, in Cesil.ReadContext ctx)");
            sb.AppendLine("  {");
            sb.AppendLine("    var columnMap = __CurrentColumnMap_Memory.Span;");
            sb.AppendLine("    if(ctx.Column.Index >= columnMap.Length)");
            sb.AppendLine("    {");
            sb.AppendLine("      return true; // extra columns are 'fine' here");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    var actualRowIx = columnMap[ctx.Column.Index];");
            sb.AppendLine();
            sb.AppendLine("    System.Boolean result;");
            sb.AppendLine("    switch(actualRowIx)");
            sb.AppendLine("    {");
            for (var i = 0; i < columns.Length; i++)
            {
                var colMethodName = GetColumnMethodName(i);
                sb.AppendLine("       case " + i + ": result = " + colMethodName + "(data, in ctx);");
                sb.AppendLine("       break;");
            }
            sb.AppendLine("       default: result = true; // extra columns are 'fine' here");
            sb.AppendLine("       break;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    if(!result)");
            sb.AppendLine("    {");
            sb.AppendLine("      return false;");
            sb.AppendLine("    }");

            if (needsHold)
            {
                sb.AppendLine();
                sb.AppendLine("    " + tryMoveFromHold + "();");
            }

            sb.AppendLine();
            sb.AppendLine("    return true;");
            sb.AppendLine("  }");

            // method for starting a row
            AddStartRow(sb, needsHold, columns);

            // method for finishing a row
            AddFinishRow(sb, needsHold, fullyQualifiedRowType, columns);

            // emit methods for handling individual columns
            for (var i = 0; i < columns.Length; i++)
            {
                var col = columns[i];

                var setter = AddSetterMethod(sb, fullyQualifiedRowType, i, col.Setter);

                var resetCanTakeRow = setter != null;   // if the setter goes to a constructor, reset has to be treated specially

                var reset = AddResetMethod(sb, fullyQualifiedRowType, i, resetCanTakeRow, col.Reset);
                var parser = AddParserMethod(sb, defaultParsersFullyQualifiedTypeName, i, col.Parser);

                AddColumnMethod(sb, needsHold, col, i, resetCanTakeRow, reset, parser, col.Setter, setter);
            }

            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#nullable restore");

            return sb.ToString();

            static void AddColumnMethod(StringBuilder sb, bool mayNeedHold, DeserializableMember col, int index, bool resetCanTakeRow, string? reset, string parser, Setter setter, string? setterMtd)
            {
                var colMethodName = GetColumnMethodName(index);

                sb.AppendLine();

                if (mayNeedHold)
                {
                    var isHeldFieldName = GetColumnIsHeldFieldName(index);
                    var heldValueFieldName = GetColumnHeldValueFieldName(index);
                    var heldContextFieldName = GetColumnHeldContextFieldName(index);

                    sb.AppendLine("  private System.Boolean " + isHeldFieldName + " = false;");
                    sb.AppendLine("  private " + col.Setter.ValueType.ToFullyQualifiedName() + " " + heldValueFieldName + " = default;");
                    sb.AppendLine("  private Cesil.ReadContext? " + heldContextFieldName + " = null;");
                }

                string? resetStatement;
                if (reset != null)
                {
                    if (resetCanTakeRow)
                    {
                        resetStatement = reset + "(ref __CurrentRow, in ctx);";
                    }
                    else
                    {
                        resetStatement = reset + "(in ctx);";
                    }
                }
                else
                {
                    resetStatement = null;
                }

                if (col.IsRequired)
                {
                    // mark column as required
                    var fieldName = GetColumnRequiredFieldName(index);

                    sb.AppendLine("    private System.Boolean " + fieldName + " = false;");
                    sb.AppendLine();
                    sb.AppendLine("    #pragma warning disable CS0618 // only Obsolete to prevent direct use");
                    sb.AppendLine("    [Cesil.IsRequiredAttribute]");
                    sb.AppendLine("    #pragma warning restore CS0618");
                }

                if (setterMtd == null)
                {
                    // store enough to find the parameter on the constructor
                    var position = Utils.NonNullValue(setter.ParameterPosition);
                    sb.AppendLine("    #pragma warning disable CS0618 // only Obsolete to prevent direct use");
                    sb.AppendLine("    [Cesil.SetterBackedByConstructorParameterAttribute(" + position + ")]");
                    sb.AppendLine("    #pragma warning restore CS0618");
                }

                sb.AppendLine("    public System.Boolean " + colMethodName + "(System.ReadOnlySpan<char> data, in Cesil.ReadContext ctx)");
                sb.AppendLine("    {");

                sb.AppendLine("       if(!" + parser + "(data, in ctx, out var parsed))");
                sb.AppendLine("       {");
                sb.AppendLine("         return false;");
                sb.AppendLine("       }");
                sb.AppendLine();

                if (mayNeedHold)
                {
                    var isHeldFieldName = GetColumnIsHeldFieldName(index);
                    var heldValueFieldName = GetColumnHeldValueFieldName(index);
                    var heldContextFieldName = GetColumnHeldContextFieldName(index);

                    sb.AppendLine("       if(__CurrentRowPopulated)");
                    sb.AppendLine("       {");
                    if (setterMtd != null)
                    {
                        if (resetStatement != null)
                        {
                            sb.AppendLine("         " + resetStatement);
                        }

                        sb.AppendLine("         " + setterMtd + "(ref __CurrentRow, parsed, in ctx);");
                    }
                    else
                    {
                        sb.AppendLine("         throw new System.InvalidOperationException(\"Column is backed by a constructor parameter and cannot be set at this time\");");
                    }
                    sb.AppendLine("       }");
                    sb.AppendLine("       else");
                    sb.AppendLine("       {");
                    sb.AppendLine("         " + heldValueFieldName + " = parsed;");
                    sb.AppendLine("         " + isHeldFieldName + " = true;");
                    sb.AppendLine("         " + heldContextFieldName + " = ctx;");
                    sb.AppendLine("       }");
                }
                else
                {
                    if (resetStatement != null)
                    {
                        sb.AppendLine("       " + resetStatement);
                    }

                    if (setterMtd != null)
                    {
                        sb.AppendLine("       " + setterMtd + "(ref __CurrentRow, parsed, in ctx);");
                    }
                    else
                    {
                        sb.AppendLine("         throw new System.InvalidOperationException(\"Column is backed by a constructor parameter and cannot be set at this time\");");
                    }
                }

                if (col.IsRequired)
                {
                    var fieldName = GetColumnRequiredFieldName(index);
                    sb.AppendLine("       " + fieldName + " = true;");
                }

                sb.AppendLine("       return true;");

                sb.AppendLine("    }");
            }

            static string AddTryMoveFromHoldMethod(StringBuilder sb, string fullyQualifiedRowType, InstanceProvider instanceProvider, ImmutableArray<DeserializableMember> columns)
            {
                const string METHOD_NAME = "__TryMoveFromHold";

                sb.AppendLine();
                sb.AppendLine("    private void " + METHOD_NAME + "()");
                sb.AppendLine("    {");

                // don't do anything if we're already populated
                sb.AppendLine("      if(__CurrentRowPopulated)");
                sb.AppendLine("      {");
                sb.AppendLine("        return;");
                sb.AppendLine("      }");

                // check to see if enough things are set that we _can_ populate
                var terms = ImmutableArray.CreateBuilder<string>();
                for (var i = 0; i < columns.Length; i++)
                {
                    var col = columns[i];
                    if (col.Setter.Parameter == null)
                    {
                        continue;
                    }

                    var heldVar = GetColumnIsHeldFieldName(i);

                    terms.Add(heldVar);
                }

                sb.AppendLine();
                sb.AppendLine("      var canMove = " + string.Join(" && ", terms.ToImmutableArray()) + ";");

                sb.AppendLine();
                sb.AppendLine("      if(!canMove)");
                sb.AppendLine("      {");
                sb.AppendLine("         return;");
                sb.AppendLine("      }");

                // create the row using any setters that 
                var initTerms = ImmutableArray.CreateBuilder<string>();
                var consPs = Utils.NonNull(instanceProvider.Method).Parameters;
                foreach (var p in consPs)
                {
                    int? colIxRaw = null;
                    Reset? reset = null;
                    for (var i = 0; i < columns.Length; i++)
                    {
                        var col = columns[i];
                        if (col.Setter.Parameter == null)
                        {
                            continue;
                        }

                        if (col.Setter.Parameter.Equals(p, SymbolEqualityComparer.Default))
                        {
                            colIxRaw = i;
                            reset = col.Reset;
                            break;
                        }
                    }
                    var colIx = Utils.NonNullValue(colIxRaw);

                    var colVar = GetColumnHeldValueFieldName(colIx);

                    initTerms.Add(colVar);

                    // run the reset before invoking the constructor
                    if (reset != null)
                    {
                        var resetMethod = GetResetMethodName(colIx);
                        var heldContextFieldName = GetColumnHeldContextFieldName(colIx);
                        sb.AppendLine("      " + resetMethod + "(" + heldContextFieldName + ".Value);");   // none of these can take a row, since they're backed by constructor params
                    }
                }

                sb.AppendLine();
                sb.AppendLine("      __CurrentRow = new " + fullyQualifiedRowType + "(" + string.Join(", ", initTerms.ToImmutable()) + ");");
                sb.AppendLine("      __CurrentRowPopulated = true;");


                // move anything that's already present
                for (var i = 0; i < columns.Length; i++)
                {
                    var col = columns[i];
                    if (col.Setter.Parameter != null)
                    {
                        continue;
                    }

                    var isHeld = GetColumnIsHeldFieldName(i);
                    var heldContextFieldName = GetColumnHeldContextFieldName(i);

                    sb.AppendLine();
                    sb.AppendLine("      if(" + isHeld + ")");
                    sb.AppendLine("      {");

                    if (col.Reset != null)
                    {
                        var resetMethod = GetResetMethodName(i);
                        sb.AppendLine("        " + resetMethod + "(ref __CurrentRow, " + heldContextFieldName + ".Value);");   // all of these can take a row, since they are NOT backed by a constructor param
                    }

                    var setterMethod = GetSetterMethodName(i);
                    var colVar = GetColumnHeldValueFieldName(i);

                    sb.AppendLine("        " + setterMethod + "(ref __CurrentRow, " + colVar + ", " + heldContextFieldName + ".Value);");
                    sb.AppendLine("      }");
                }

                sb.AppendLine();
                sb.AppendLine("      return;");
                sb.AppendLine("    }");

                return METHOD_NAME;
            }

            static string? AddResetMethod(StringBuilder sb, string fullyQualifiedRowType, int colIx, bool canTakeRow, Reset? reset)
            {
                if (reset == null)
                {
                    return null;
                }

                var mtdName = reset.Method.Name;

                string invokeStatement;
                if (reset.IsStatic)
                {
                    var onType = reset.Method.ContainingType.ToFullyQualifiedName();

                    invokeStatement = onType + "." + mtdName + "(";

                    if (reset.TakesRow)
                    {
                        invokeStatement += "row";


                        if (reset.TakesContext)
                        {
                            invokeStatement += ", in ctx";
                        }
                    }
                    else
                    {
                        if (reset.TakesContext)
                        {
                            invokeStatement += "in ctx";
                        }
                    }

                    invokeStatement += ")";
                }
                else
                {
                    invokeStatement = "row." + mtdName + "(";

                    if (reset.TakesContext)
                    {
                        invokeStatement += "in ctx";
                    }

                    invokeStatement += ")";
                }

                var resetMethodName = GetResetMethodName(colIx);

                sb.AppendLine();
                sb.AppendLine("    [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                if (canTakeRow)
                {
                    sb.AppendLine("    public static void " + resetMethodName + "(ref " + fullyQualifiedRowType + " row, in Cesil.ReadContext ctx)");
                }
                else
                {
                    sb.AppendLine("    public static void " + resetMethodName + "(in Cesil.ReadContext ctx)");
                }
                sb.AppendLine("    {");
                sb.AppendLine("      " + invokeStatement + ";");
                sb.AppendLine("    }");

                return resetMethodName;
            }

            static string GetResetMethodName(int ix)
            {
                return "__Column_" + ix + "_Reset";
            }

            static string GetColumnIsHeldFieldName(int ix)
            {
                return "__Column_" + ix + "_IsHeld";
            }

            static string GetColumnHeldValueFieldName(int ix)
            {
                return "__Column_" + ix + "_HeldValue";
            }

            static string GetColumnHeldContextFieldName(int ix)
            {
                return "__Column_" + ix + "_Context";
            }

            static string GetColumnMethodName(int ix)
            {
                return "__Column_" + ix;
            }

            static string GetColumnRequiredFieldName(int ix)
            {
                return "__Column_" + ix + "_Set";
            }

            static string? AddSetterMethod(StringBuilder sb, string fullyQualifiedRowType, int colIx, Setter setter)
            {
                if (setter.Parameter != null)
                {
                    return null;
                }

                string invokeStatement;
                if (setter.Field != null)
                {
                    if (setter.Field.IsStatic)
                    {
                        invokeStatement = setter.Field.ContainingType.ToFullyQualifiedName() + "." + setter.Field.Name + " = value";
                    }
                    else
                    {
                        invokeStatement = "row." + setter.Field.Name + " = value";
                    }
                }
                else if (setter.Property != null)
                {
                    if (setter.Property.IsStatic)
                    {
                        invokeStatement = setter.Property.ContainingType.ToFullyQualifiedName() + "." + setter.Property.Name + " = value";
                    }
                    else
                    {
                        invokeStatement = "row." + setter.Property.Name + " = value";
                    }
                }
                else if (setter.Method != null)
                {
                    if (setter.Method.IsStatic)
                    {
                        invokeStatement = setter.Method.ContainingType.ToFullyQualifiedName() + "." + setter.Method.Name + "(";
                    }
                    else
                    {
                        invokeStatement = "row." + setter.Method.Name + "(";
                    }

                    var needsComma = false;
                    if (setter.MethodTakesRow)
                    {
                        needsComma = true;
                        if (setter.MethodTakesRowByRef)
                        {
                            invokeStatement += "ref row";
                        }
                        else
                        {
                            invokeStatement += "row";
                        }
                    }

                    if (needsComma)
                    {
                        invokeStatement += ", ";
                    }

                    invokeStatement += "value";

                    if (setter.MethodTakesContext)
                    {
                        invokeStatement += ", in ctx";
                    }

                    invokeStatement += ")";
                }
                else
                {
                    throw new Exception("Shouldn't be possible");
                }

                var fullyQualifiedValueTypeName = setter.ValueType.ToFullyQualifiedName();

                var setterMethodName = GetSetterMethodName(colIx);

                sb.AppendLine();
                sb.AppendLine("    [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine("    public static void " + setterMethodName + "(ref " + fullyQualifiedRowType + " row, " + fullyQualifiedValueTypeName + " value, in Cesil.ReadContext ctx)");
                sb.AppendLine("    {");
                sb.AppendLine("      " + invokeStatement + ";");
                sb.AppendLine("    }");

                return setterMethodName;
            }

            static string GetSetterMethodName(int colIx)
            {
                return "__Column_" + colIx + "_Setter";
            }

            static string AddParserMethod(StringBuilder sb, string defaultParsersFullyQualifiedTypeName, int colIx, Parser parser)
            {
                if (parser.IsDefault)
                {
                    var forType = Utils.NonNull(parser.ForDefaultType);

                    if (parser.DefaultIsMethod)
                    {
                        var defaultParserMethodName = GetDefaultParserMethodName(forType);

                        return defaultParsersFullyQualifiedTypeName + "." + defaultParserMethodName;
                    }
                    else
                    {
                        var defaultFormatterClassName = GetDefaultParserClassName(forType);

                        return defaultParsersFullyQualifiedTypeName + "." + defaultFormatterClassName + ".__TryParse";
                    }
                }

                var createsType = Utils.NonNull(parser.CreatesType);
                var method = Utils.NonNull(parser.Method);

                var createsTypeName = createsType.ToFullyQualifiedName();
                if (createsType.NullableAnnotation == NullableAnnotation.Annotated && createsType.TypeKind != TypeKind.Struct)
                {
                    createsTypeName += "?";
                }

                var parserType = method.ContainingType.ToFullyQualifiedName();

                var parserStatement = parserType + "." + method.Name + "(data, in ctx, out value)";

                var parserMethodName = "__Column_" + colIx + "_Parser";

                sb.AppendLine();
                sb.AppendLine("    [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine("    public static System.Boolean " + parserMethodName + "(System.ReadOnlySpan<char> data, in Cesil.ReadContext ctx, out " + createsTypeName + " value)");
                sb.AppendLine("    {");
                sb.AppendLine("      var ret = " + parserStatement + ";");
                sb.AppendLine("      return ret;");
                sb.AppendLine("    }");

                return parserMethodName;
            }

            static string AddInstanceProvider(StringBuilder sb, string fullyQualifiedRowType, InstanceProvider instanceProvider)
            {
                const string METHOD_NAME = "__InstanceProvider";

                sb.AppendLine();

                if (instanceProvider.IsConstructor && !instanceProvider.IsDefault)
                {
                    var cons = Utils.NonNull(instanceProvider.Method);

                    var onType = cons.ContainingType;
                    var ps = cons.Parameters;

                    for (var i = 0; i < ps.Length; i++)
                    {
                        sb.AppendLine("  #pragma warning disable CS0618 // only Obsolete to prevent direct use");
                        sb.AppendLine("  [Cesil.ConstructorInstanceProviderAttribute(typeof(" + onType.ToFullyQualifiedName() + "), typeof(" + ps[i].Type.ToFullyQualifiedName() + "), " + i + ")]");
                        sb.AppendLine("  #pragma warning restore CS0618");
                    }
                }

                sb.AppendLine("  public static System.Boolean " + METHOD_NAME + "(in Cesil.ReadContext ctx, out " + fullyQualifiedRowType + " instance)");
                sb.AppendLine("  {");

                if (instanceProvider.IsConstructor)
                {
                    var decl = instanceProvider.RowType.ToFullyQualifiedName();

                    if (instanceProvider.IsDefault)
                    {
                        sb.AppendLine("    instance = new " + decl + "();");
                        sb.AppendLine("    return true; ");
                    }
                    else
                    {
                        sb.AppendLine("    throw new System.InvalidOperationException(\"InstanceProvider is actually a constructor, this method is just a placeholder.\");");
                    }
                }
                else
                {
                    var mtd = Utils.NonNull(instanceProvider.Method);

                    var invokeExpr = mtd.ContainingType.ToFullyQualifiedName() + "." + mtd.Name + "(in ctx, out instance)";

                    sb.AppendLine("    if (!" + invokeExpr + ")");
                    sb.AppendLine("    {");
                    sb.AppendLine("      instance = default;");
                    sb.AppendLine("      return false;");
                    sb.AppendLine("    }");
                    sb.AppendLine("    return true; ");
                }

                sb.AppendLine("  }");

                return METHOD_NAME;
            }

            static void AddTryPreAllocate(StringBuilder sb, bool isValueType, bool needsHold, string fullyQualifiedRowType, string instanceProviderMethod)
            {
                sb.AppendLine();
                sb.AppendLine("  public System.Boolean TryPreAllocate(in Cesil.ReadContext ctx, System.Boolean checkPrealloc, ref " + fullyQualifiedRowType + " prealloced)");
                sb.AppendLine("  {");

                if (needsHold)
                {
                    sb.AppendLine("    __CurrentRow = default;");
                    sb.AppendLine("    __CurrentRowPopulated = false;");
                    sb.AppendLine("    return false;");
                }
                else
                {
                    string prealloced;
                    if (isValueType)
                    {
                        prealloced = "";
                    }
                    else
                    {
                        prealloced = " && prealloced != null";
                    }

                    sb.AppendLine("    if (checkPrealloc" + prealloced + ")");
                    sb.AppendLine("    {");
                    sb.AppendLine("      __CurrentRow = prealloced;");
                    sb.AppendLine("      __CurrentRowPopulated = true;");
                    sb.AppendLine("      return true;");
                    sb.AppendLine("    }");
                    sb.AppendLine("    else if (!" + instanceProviderMethod + "(in ctx, out prealloced))");
                    sb.AppendLine("    {");
                    sb.AppendLine("      throw new System.InvalidOperationException(\"Failed to obtain instance of " + fullyQualifiedRowType + "\");");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                    sb.AppendLine("    __CurrentRow = prealloced;");
                    sb.AppendLine("    __CurrentRowPopulated = true;");
                    sb.AppendLine("    return true; ");
                }

                sb.AppendLine("  }");
            }

            static void AddStartRow(StringBuilder sb, bool needsHold, ImmutableArray<DeserializableMember> columns)
            {
                var hasRequiredColumns = columns.Any(c => c.IsRequired);

                sb.AppendLine();
                sb.AppendLine("  public void StartRow(in Cesil.ReadContext _)");
                sb.AppendLine("  {");
                if (needsHold)
                {
                    sb.AppendLine("    if (__RowStarted)");
                    sb.AppendLine("    {");
                    sb.AppendLine("      throw new System.InvalidOperationException(\"Row already started\");");
                    sb.AppendLine("    }");
                }
                else
                {
                    sb.AppendLine("    if (!__CurrentRowPopulated)");
                    sb.AppendLine("    {");
                    sb.AppendLine("      throw new System.Exception(\"Row should already be pre-allocated\");");
                    sb.AppendLine("    }");
                }
                sb.AppendLine();
                sb.AppendLine("    __RowStarted = true;");

                if (hasRequiredColumns)
                {
                    sb.AppendLine();
                    for (var i = 0; i < columns.Length; i++)
                    {
                        var col = columns[i];
                        if (!col.IsRequired)
                        {
                            continue;
                        }

                        var field = GetColumnRequiredFieldName(i);
                        sb.AppendLine("    " + field + " = false;");
                    }
                }

                sb.AppendLine("  }");
            }

            static void AddFinishRow(StringBuilder sb, bool mayNeedHold, string fullyQualifiedRowType, ImmutableArray<DeserializableMember> columns)
            {
                var hasRequiredColumns = columns.Any(c => c.IsRequired);

                sb.AppendLine();
                sb.AppendLine("  public " + fullyQualifiedRowType + " FinishRow()");
                sb.AppendLine("  {");
                sb.AppendLine("    if (!__CurrentRowPopulated)");
                sb.AppendLine("    {");
                sb.AppendLine("      throw new System.Exception(\"No current row available, shouldn't be trying to finish a row\");");
                sb.AppendLine("    }");

                if (hasRequiredColumns)
                {
                    for (var i = 0; i < columns.Length; i++)
                    {
                        var col = columns[i];
                        if (!col.IsRequired)
                        {
                            continue;
                        }

                        var field = GetColumnRequiredFieldName(i);
                        sb.AppendLine();
                        sb.AppendLine("    if (!" + field + ")");
                        sb.AppendLine("    {");
                        var colName = col.Name.EscapeCSharp();
                        colName = colName.Substring(1, colName.Length - 2);
                        sb.AppendLine("      throw new System.Runtime.Serialization.SerializationException(\"Column [" + colName + "] is required, but was not found in row\");");
                        sb.AppendLine("    }");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("    var ret = __CurrentRow;");
                sb.AppendLine("    __CurrentRow = default;");
                sb.AppendLine("    __CurrentRowPopulated = false;");
                sb.AppendLine("    __RowStarted = false;");

                if (mayNeedHold)
                {
                    for (var i = 0; i < columns.Length; i++)
                    {
                        sb.AppendLine();

                        var isHeldField = GetColumnIsHeldFieldName(i);
                        sb.AppendLine("    " + isHeldField + " = false;");

                        var heldValueField = GetColumnHeldValueFieldName(i);
                        sb.AppendLine("    " + heldValueField + " = default!;");

                        var heldContextField = GetColumnHeldContextFieldName(i);
                        sb.AppendLine("    " + heldContextField + " = null;");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("    return ret;");
                sb.AppendLine("  }");
            }
        }

        private static (string FullyQualifiedTypeName, string Source)? GenerateDefaultParserType(ImmutableArray<Parser> parsers)
        {
            if (parsers.IsEmpty)
            {
                return null;
            }

            var sb = new StringBuilder();

            sb.AppendLine("#nullable disable warnings");
            sb.AppendLine("#nullable enable annotations");
            sb.AppendLine("#pragma warning disable CS0162 // ignore unreachable code, this can happen because of inlining values that are known at source generation time");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine();
            sb.AppendLine("namespace Cesil.SourceGenerator.Generated");
            sb.AppendLine("{");

            var defaultParsersName = "__DefaultParsers";

            sb.AppendLine("  internal static class " + defaultParsersName);
            sb.AppendLine("  {");

            foreach (var parser in parsers)
            {
                var forType = Utils.NonNull(parser.ForDefaultType);
                var code = Utils.NonNull(parser.DefaultCode);

                if (parser.DefaultIsMethod)
                {
                    var parserMethodName = GetDefaultParserMethodName(forType);
                    var parsedMethod = (MethodDeclarationSyntax)Utils.NonNull(SyntaxFactory.ParseMemberDeclaration(code));
                    var parsedMethodBody = Utils.NonNull(parsedMethod.Body);

                    sb.AppendLine();
                    sb.AppendLine("    internal static System.Boolean " + parserMethodName + "(System.ReadOnlySpan<char> span, in Cesil.ReadContext ctx, out " + parser.ForDefaultType + " val)");
                    sb.AppendLine(parsedMethodBody.ToFullString());
                }
                else
                {
                    var formatterClassName = GetDefaultParserClassName(forType);
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

            return (defaultParsersName, source);
        }

        private static string GetDefaultParserClassName(string forType)
        {
            var formatterClassName = "__Class_Parser_" + forType.Replace(".", "_").TrimEnd('?');
            if (forType.EndsWith("?"))
            {
                formatterClassName += "_Nullable";
            }

            return formatterClassName;
        }

        private static string GetDefaultParserMethodName(string forType)
        {
            var formatterMethodName = "__Parser_" + forType.Replace(".", "_").TrimEnd('?');
            if (forType.EndsWith("?"))
            {
                formatterMethodName += "_Nullable";
            }

            return formatterMethodName;
        }

        internal override ImmutableDictionary<INamedTypeSymbol, ImmutableArray<DeserializableMember>> GetMembersToGenerateFor(
            GeneratorExecutionContext context,
            Compilation compilation,
            ImmutableArray<TypeDeclarationSyntax> toGenerateFor,
            DeserializerTypes types
        )
        {
            var ret = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, ImmutableArray<DeserializableMember>>();
            var instanceProviders = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, InstanceProvider>();

            foreach (var decl in toGenerateFor)
            {
                var model = compilation.GetSemanticModel(decl.SyntaxTree);
                var namedType = model.GetDeclaredSymbol(decl);
                if (namedType == null)
                {
                    var diag = Diagnostics.GenericError(decl.GetLocation(), "Type identified, but not named");
                    context.ReportDiagnostic(diag);
                    continue;
                }

                var members = GetDeserializableMembers(context, compilation, types, namedType);

                var diags = ImmutableArray<Diagnostic>.Empty;

                var pairedAttr = TypeDeclarationsToDeserializeAttribute[decl];

                (ConstructorDeclarationSyntax Constructor, AttributeSyntax InstanceProviderAttribute)? constructorIp;
                if (TypeDeclarationsToConstructorsWithAttribute.TryGetValue(decl, out var constructorIpRaw))
                {
                    constructorIp = constructorIpRaw;
                }
                else
                {
                    constructorIp = null;
                }

                var provider =
                    GetInstanceProvider(
                        compilation,
                        types,
                        namedType,
                        pairedAttr,
                        constructorIp,
                        ref diags
                    );

                foreach (var diag in diags)
                {
                    context.ReportDiagnostic(diag);
                }

                if (provider == null || members.IsEmpty)
                {
                    continue;
                }

                ret.Add(namedType, members);
                instanceProviders.Add(namedType, provider);
            }

            InstanceProviders = instanceProviders.ToImmutable();

            return ret.ToImmutable();
        }

        private static InstanceProvider? GetInstanceProvider(
            Compilation compilation,
            DeserializerTypes types,
            INamedTypeSymbol namedType,
            AttributeSyntax attr,
            (ConstructorDeclarationSyntax Constructor, AttributeSyntax InstanceProviderAttribute)? constructorWithAttribute,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var attrLoc = attr.GetLocation();

            var settings =
                Utils.GetMethodFromAttribute(
                    compilation,
                    "InstanceProviderType",
                    Diagnostics.InstanceProviderTypeSpecifiedMultipleTimes,
                    "InstanceProviderMethodName",
                    Diagnostics.InstanceProviderMethodNameSpecifiedMultipleTimes,
                    Diagnostics.InstanceProviderBothMustBeSet,
                    attrLoc,
                    ImmutableArray.Create(attr),
                    ref diags
                );

            if (!diags.IsEmpty)
            {
                return null;
            }

            if (settings != null && constructorWithAttribute != null)
            {
                var diag = Diagnostics.InstanceProviderConstructorAndMethodProvided(attrLoc, namedType);
                diags = diags.Add(diag);

                return null;
            }

            if (settings == null && constructorWithAttribute == null)
            {
                // default constructor
                return InstanceProvider.ForDefault(compilation, namedType, ref diags);
            }
            else if (settings != null)
            {
                // explicit method
                var (type, mtdName) = settings.Value;

                return InstanceProvider.ForMethod(compilation, types, attrLoc, namedType, type, mtdName, ref diags);
            }
            else if (constructorWithAttribute != null)
            {
                // explicit constructor
                var (cons, _) = constructorWithAttribute.Value;

                // ... but it's the default constructor, so just move on
                if (cons.ParameterList.Parameters.Count == 0)
                {
                    return InstanceProvider.ForDefault(compilation, namedType, ref diags);
                }

                return InstanceProvider.ForConstructorWithParameters(compilation, types, namedType, cons, ref diags);
            }
            else
            {
                throw new Exception("Shouldn't be possible");
            }
        }

        private static ImmutableArray<DeserializableMember> GetDeserializableMembers(
            GeneratorExecutionContext context,
            Compilation compilation,
            DeserializerTypes types,
            INamedTypeSymbol namedType
        )
        {
            var hasErrors = false;
            var ret = ImmutableArray.CreateBuilder<DeserializableMember>();

            foreach (var member in namedType.GetMembers())
            {
                var res = GetDeserializableMember(compilation, types, namedType, member);
                if (res == null)
                {
                    continue;
                }

                var (serializableMembers, diags) = res.Value;

                if (!serializableMembers.IsEmpty)
                {
                    ret.AddRange(serializableMembers);
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
                return ImmutableArray<DeserializableMember>.Empty;
            }

            return ret.ToImmutable();
        }

        private static (ImmutableArray<DeserializableMember> Members, ImmutableArray<Diagnostic> Diagnostics)? GetDeserializableMember(
            Compilation compilation,
            DeserializerTypes types,
            INamedTypeSymbol deserializingType,
            ISymbol member
        )
        {
            if (member is IPropertySymbol prop)
            {
                if (IsIgnored(member, types.Framework))
                {
                    return null;
                }

                var configAttrs = GetConfigurationAttributes(compilation, types.OurTypes.DeserializerMemberAttribute, types.Framework, member);

                // skip properties without setters _if_ there are no attributes (if there are, we need to raise an error later)
                if (configAttrs.IsEmpty && prop.SetMethod == null)
                {
                    return null;
                }

                var isVisible =
                    (member.DeclaredAccessibility == Accessibility.Public && !member.IsStatic) ||
                    !configAttrs.IsEmpty;

                // neither visible or annotated to include
                if (!isVisible)
                {
                    return null;
                }

                var propMember = DeserializableMember.ForProperty(compilation, types, deserializingType, prop, configAttrs);

                var memberRet = ImmutableArray<DeserializableMember>.Empty;

                if (propMember.Member != null)
                {
                    memberRet = memberRet.Add(propMember.Member);
                }

                return (memberRet, propMember.Diagnostics);
            }
            else if (member is IFieldSymbol field)
            {
                var configAttrs = GetConfigurationAttributes(compilation, types.OurTypes.DeserializerMemberAttribute, types.Framework, member);

                // must be annotated to include
                if (configAttrs.IsEmpty)
                {
                    return null;
                }

                var fieldMember = DeserializableMember.ForField(compilation, types, deserializingType, field, configAttrs);

                var memberRet = ImmutableArray<DeserializableMember>.Empty;

                if (fieldMember.Member != null)
                {
                    memberRet = memberRet.Add(fieldMember.Member);
                }

                return (memberRet, fieldMember.Diagnostics);
            }
            else if (member is IMethodSymbol method)
            {
                var diags = ImmutableArray<Diagnostic>.Empty;

                var haveMemberAttr = 0;

                var ps = method.Parameters;
                foreach (var p in ps)
                {
                    var attrs = p.GetAttributes();
                    if (attrs.Any(a => a.AttributeClass?.Equals(types.OurTypes.DeserializerMemberAttribute, SymbolEqualityComparer.Default) ?? false))
                    {
                        haveMemberAttr++;
                        if (method.MethodKind != MethodKind.Constructor)
                        {
                            var pLoc = p.Locations.FirstOrDefault();

                            var diag = Diagnostics.DeserializableMemberOnNonConstructorParameter(pLoc, deserializingType, method);
                            diags = diags.Add(diag);
                        }
                    }
                }

                if (!diags.IsEmpty)
                {
                    return (ImmutableArray<DeserializableMember>.Empty, diags);
                }

                if (method.MethodKind == MethodKind.Constructor)
                {
                    var consLoc = method.Locations.FirstOrDefault();

                    var consAttrs = method.GetAttributes();
                    var consIsAnnotated = consAttrs.Any(c => c.AttributeClass?.Equals(types.OurTypes.DeserializerInstanceProviderAttribute, SymbolEqualityComparer.Default) ?? false);
                    if (!consIsAnnotated)
                    {
                        if (haveMemberAttr > 0)
                        {
                            var diag = Diagnostics.ConstructorHasMembersButIsntInstanceProvider(consLoc, deserializingType);
                            diags = diags.Add(diag);

                            return (ImmutableArray<DeserializableMember>.Empty, diags);
                        }

                        // doesn't participate, ignore it
                        return null;
                    }

                    if (haveMemberAttr != ps.Length)
                    {
                        var diag = Diagnostics.AllConstructorParametersMustBeMembers(consLoc, deserializingType);
                        diags = diags.Add(diag);
                    }

                    if (diags.IsEmpty)
                    {
                        var ret = ImmutableArray<DeserializableMember>.Empty;
                        var subDiags = ImmutableArray<Diagnostic>.Empty;

                        foreach (var p in ps)
                        {
                            var configAttrs = GetConfigurationAttributes(compilation, types.OurTypes.DeserializerMemberAttribute, types.Framework, p);
                            var (paramMember, memberDiags) = DeserializableMember.ForConstructorParameter(compilation, types, deserializingType, p, configAttrs);

                            if (!memberDiags.IsEmpty)
                            {
                                subDiags = subDiags.AddRange(memberDiags);
                            }

                            if (paramMember != null)
                            {
                                ret = ret.Add(paramMember);
                            }
                        }

                        return (ret, subDiags);
                    }

                    return (ImmutableArray<DeserializableMember>.Empty, diags);
                }

                if (method.MethodKind == MethodKind.Ordinary)
                {
                    var configAttrs = GetConfigurationAttributes(compilation, types.OurTypes.DeserializerMemberAttribute, types.Framework, member);

                    // must be annotated to include
                    if (configAttrs.IsEmpty)
                    {
                        return null;
                    }

                    var mtdMember = DeserializableMember.ForMethod(compilation, types, deserializingType, method, configAttrs);
                    var memberRet = ImmutableArray<DeserializableMember>.Empty;

                    if (mtdMember.Member != null)
                    {
                        memberRet = memberRet.Add(mtdMember.Member);
                    }

                    return (memberRet, mtdMember.Diagnostics);
                }
            }

            return null;
        }

        internal override ImmutableArray<TypeDeclarationSyntax> GetTypesToGenerateFor(Compilation compilation, DeserializerTypes types)
        {
            var ret = ImmutableArray.CreateBuilder<TypeDeclarationSyntax>();
            var attrTracker = ImmutableDictionary.CreateBuilder<TypeDeclarationSyntax, AttributeSyntax>();
            var consAttrTracker = ImmutableDictionary.CreateBuilder<TypeDeclarationSyntax, (ConstructorDeclarationSyntax Constructor, AttributeSyntax InstanceProviderAttribute)>();

            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);

                var root = tree.GetRoot();
                var decls = root.DescendantNodesAndSelf().OfType<TypeDeclarationSyntax>();
                foreach (var decl in decls)
                {
                    var tracking = false;

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

                            if (attrType.Equals(types.OurTypes.GenerateDeserializerAttribute, SymbolEqualityComparer.Default))
                            {
                                tracking = true;
                                ret.Add(decl);
                                attrTracker.Add(decl, attr);
                            }
                        }
                    }

                    if (!tracking)
                    {
                        continue;
                    }

                    var consDecls = decl.DescendantNodesAndSelf().OfType<ConstructorDeclarationSyntax>();
                    foreach (var consDecl in consDecls)
                    {
                        var consAttrLists = consDecl.AttributeLists;
                        foreach (var attrList in consAttrLists)
                        {
                            foreach (var attr in attrList.Attributes)
                            {
                                var attrTypeInfo = model.GetTypeInfo(attr);

                                var attrType = attrTypeInfo.Type;
                                if (attrType == null)
                                {
                                    continue;
                                }

                                if (attrType.Equals(types.OurTypes.DeserializerInstanceProviderAttribute, SymbolEqualityComparer.Default))
                                {
                                    consAttrTracker.Add(decl, (consDecl, attr));
                                }
                            }
                        }
                    }
                }
            }

            TypeDeclarationsToDeserializeAttribute = attrTracker.ToImmutable();
            TypeDeclarationsToConstructorsWithAttribute = consAttrTracker.ToImmutable();

            return ret.ToImmutable();
        }

        internal override bool TryCreateNeededTypes(Compilation compilation, GeneratorExecutionContext context, out DeserializerTypes? neededTypes)
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

            neededTypes = new DeserializerTypes(builtIn, framework, types);
            return true;
        }
    }
}
