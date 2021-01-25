using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    internal static class Utils
    {
        private static readonly SymbolDisplayFormat FullyQualifiedFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        internal static bool IsFlagsEnum(FrameworkTypes types, ITypeSymbol forEnum)
        {
            return forEnum.GetAttributes().Any(i => i.AttributeClass?.Equals(types.FlagsAttribute, SymbolEqualityComparer.Default) ?? false);
        }

        internal static string ToFullyQualifiedName(this ITypeSymbol symbol)
        => symbol.ToDisplayString(FullyQualifiedFormat);

        internal static T NonNull<T>(T? item)
            where T : class
        {
            if (item == null)
            {
                throw new InvalidOperationException("Found null value when that shouldn't be possible");
            }

            return item;
        }

        internal static T NonNullValue<T>(T? item)
            where T : struct
        {
            if (item == null)
            {
                throw new InvalidOperationException("Found null value when that shouldn't be possible");
            }

            return item.Value;
        }

        internal static ImmutableArray<T> GetConstantsWithName<T>(Compilation compilation, ImmutableArray<AttributeSyntax> attrs, string name, ref ImmutableArray<Diagnostic> diags)
        {
            var ret = ImmutableArray<T>.Empty;

            foreach (var attr in attrs)
            {
                var argList = attr.ArgumentList;
                if (argList == null) continue;

                var model = compilation.GetSemanticModel(attr.SyntaxTree);

                var values = argList.Arguments.Where(a => a.NameEquals != null && a.NameEquals.Name.Identifier.ValueText == name);
                foreach (var value in values)
                {
                    var trueValue = model.GetConstantValue(value.Expression);
                    if (!trueValue.HasValue)
                    {
                        var diag = Diagnostics.CouldNotExtractConstantValue(value.Expression.GetLocation());
                        diags = diags.Add(diag);
                        continue;
                    }

                    if (trueValue.Value is T asT)
                    {
                        ret = ret.Add(asT);
                    }
                    else
                    {
                        var actualType = trueValue.Value?.GetType()?.GetTypeInfo();
                        var diag = Diagnostics.UnexpectedConstantValueType(value.Expression.GetLocation(), new[] { typeof(T).GetTypeInfo() }, actualType);
                        diags = diags.Add(diag);
                        continue;
                    }
                }
            }

            return ret;
        }

        internal static (ImmutableArray<T1> First, ImmutableArray<T2> Second) GetConstantsWithName<T1, T2>(Compilation compilation, ImmutableArray<AttributeSyntax> attrs, string name, ref ImmutableArray<Diagnostic> diags)
        {
            var ret1 = ImmutableArray<T1>.Empty;
            var ret2 = ImmutableArray<T2>.Empty;

            foreach (var attr in attrs)
            {
                var argList = attr.ArgumentList;
                if (argList == null) continue;

                var model = compilation.GetSemanticModel(attr.SyntaxTree);

                var values = argList.Arguments.Where(a => a.NameEquals != null && a.NameEquals.Name.Identifier.ValueText == name);
                foreach (var value in values)
                {
                    var trueValue = model.GetConstantValue(value.Expression);
                    if (!trueValue.HasValue)
                    {
                        var diag = Diagnostics.CouldNotExtractConstantValue(value.Expression.GetLocation());
                        diags = diags.Add(diag);
                        continue;
                    }

                    if (trueValue.Value is T1 asT1)
                    {
                        ret1 = ret1.Add(asT1);
                    }
                    else if (trueValue.Value is T2 asT2)
                    {
                        ret2 = ret2.Add(asT2);
                    }
                    else
                    {
                        var actualType = trueValue.Value?.GetType()?.GetTypeInfo();
                        var diag = Diagnostics.UnexpectedConstantValueType(value.Expression.GetLocation(), new[] { typeof(T1).GetTypeInfo(), typeof(T2).GetTypeInfo() }, actualType);
                        diags = diags.Add(diag);
                        continue;
                    }
                }
            }

            return (ret1, ret2);
        }

        internal static int? GetOrderFromAttributes(
            Compilation compilation,
            Location? location,
            FrameworkTypes frameworkTypes,
            INamedTypeSymbol cesilMemberAttr,
            ImmutableArray<AttributeSyntax> attrs,
            ref ImmutableArray<Diagnostic> diags)
        {
            if (attrs.IsEmpty)
            {
                return null;
            }

            var values = ImmutableArray.CreateBuilder<int>();

            var dataMemberAttr = frameworkTypes.DataMemberAttribute;

            foreach (var attr in attrs)
            {
                var model = compilation.GetSemanticModel(attr.Name.SyntaxTree);
                var type = model.GetTypeInfo(attr.Name).Type;

                if (type == null)
                {
                    continue;
                }

                if (type.Equals(cesilMemberAttr, SymbolEqualityComparer.Default))
                {
                    var value = GetConstantsWithName<int>(compilation, ImmutableArray.Create(attr), "Order", ref diags);
                    foreach (var val in value)
                    {
                        values.Add(val);
                    }

                    continue;
                }

                if (dataMemberAttr != null && type.Equals(dataMemberAttr, SymbolEqualityComparer.Default))
                {
                    var value = GetConstantsWithName<int>(compilation, ImmutableArray.Create(attr), "Order", ref diags);
                    foreach (var val in value)
                    {
                        if (val == -1)
                        {
                            continue;
                        }
                        else
                        {
                            values.Add(val);
                        }
                    }

                    continue;
                }
            }

            var vs = values.ToImmutable();

            if (vs.IsEmpty)
            {
                return null;
            }
            else if (vs.Length == 1)
            {
                return vs[0];
            }
            else
            {
                var diag = Diagnostics.OrderSpecifiedMultipleTimes(location);
                diags = diags.Add(diag);

                return null;
            }
        }

        internal static (INamedTypeSymbol Type, string Method)? GetMethodFromAttribute(
            Compilation compilation,
            string typeNameProperty,
            Func<Location?, Diagnostic> multipleTypeDefinitionDiagnostic,
            string methodNameProperty,
            Func<Location?, Diagnostic> multipleMethodDefinitionDiagnostic,
            Func<Location?, Diagnostic> notBothSetDefinitionDiagnostic,
            Location? location,
            ImmutableArray<AttributeSyntax> attrs,
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var types = GetTypeConstantWithName(compilation, attrs, typeNameProperty, ref diags);
            if (types.Length > 1)
            {
                var diag = multipleTypeDefinitionDiagnostic(location);
                diags = diags.Add(diag);

                return null;
            }

            var type = types.SingleOrDefault();

            var methods = GetConstantsWithName<string>(compilation, attrs, methodNameProperty, ref diags);
            if (methods.Length > 1)
            {
                var diag = multipleMethodDefinitionDiagnostic(location);
                diags = diags.Add(diag);

                return null;
            }

            var method = methods.SingleOrDefault();

            if (type == null && method == null)
            {
                return null;
            }

            if (type == null || method == null)
            {
                var diag = notBothSetDefinitionDiagnostic(location);
                diags = diags.Add(diag);

                return null;
            }

            return (type, method);
        }

        private static ImmutableArray<INamedTypeSymbol> GetTypeConstantWithName(Compilation compilation, ImmutableArray<AttributeSyntax> attrs, string name, ref ImmutableArray<Diagnostic> diags)
        {
            var ret = ImmutableArray<INamedTypeSymbol>.Empty;

            foreach (var attr in attrs)
            {
                var argList = attr.ArgumentList;
                if (argList == null) continue;

                var model = compilation.GetSemanticModel(attr.SyntaxTree);

                var values = argList.Arguments.Where(a => a.NameEquals != null && a.NameEquals.Name.Identifier.ValueText == name);
                foreach (var value in values)
                {

                    if (value.Expression is TypeOfExpressionSyntax typeofExp)
                    {
                        var type = model.GetTypeInfo(typeofExp.Type);

                        if (type.Type is INamedTypeSymbol namedType)
                        {
                            ret = ret.Add(namedType);
                        }
                        else
                        {
                            var diag = Diagnostics.CouldNotExtractConstantValue(value.Expression.GetLocation());
                            diags = diags.Add(diag);
                            continue;
                        }
                    }
                }
            }

            return ret;
        }

        internal static IMethodSymbol? GetMethod(ITypeSymbol type, string mtd, Location? location, ref ImmutableArray<Diagnostic> diags)
        {
            var mtds = type.GetMembers().OfType<IMethodSymbol>().Where(m => m.Name == mtd).ToImmutableArray();
            if (mtds.Length == 0)
            {
                var diag = Diagnostics.CouldNotFindMethod(location, type.Name, mtd);
                diags = diags.Add(diag);

                return null;
            }
            else if (mtds.Length > 1)
            {
                var diag = Diagnostics.MultipleMethodsFound(location, type.Name, mtd);
                diags = diags.Add(diag);

                return null;
            }

            return mtds.Single();
        }

        internal static string? GetNameFromAttributes(Compilation compilation, Location? location, ImmutableArray<AttributeSyntax> attrs, ref ImmutableArray<Diagnostic> diags)
        {
            var names = Utils.GetConstantsWithName<string>(compilation, attrs, "Name", ref diags);

            if (names.Length > 1)
            {
                var diag = Diagnostics.NameSpecifiedMultipleTimes(location);
                diags = diags.Add(diag);

                return null;
            }

            return names.SingleOrDefault();
        }

        internal static string GetResourceText(string resourceName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var cesilDefaults = asm.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(cesilDefaults))
            {
                return reader.ReadToEnd();
            }
        }

        internal static string ExtractMethodBody(ClassDeclarationSyntax defaultTypeSyntax, string methodName)
        {
            var mtd = defaultTypeSyntax.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == methodName);

            var updatedMtd = InlineTailCalls(mtd, defaultTypeSyntax);
            updatedMtd = InlineIfInnerCalls(updatedMtd, defaultTypeSyntax);

            updatedMtd = updatedMtd.NormalizeWhitespace();

            var ret = updatedMtd.ToFullString();

            return ret;
        }

        internal static T ReplaceIn<T>(T toReplaceIn, ImmutableDictionary<ReturnStatementSyntax, (ParameterListSyntax Parameters, BlockSyntax Statements)> replaceWith)
            where T : SyntaxNode
        {
            var ret = toReplaceIn;

            foreach (var kv in replaceWith)
            {
                var toReplaceRet = kv.Key;
                var calledMethodParams = kv.Value.Parameters;
                var calledMethodBody = kv.Value.Statements;

                var toReplace = (InvocationExpressionSyntax)Utils.NonNull(toReplaceRet.Expression);

                // introduce locals to for the "parameters" we're removing
                var localBindings = ImmutableArray.CreateBuilder<StatementSyntax>();
                var updatedMethodBody = calledMethodBody;
                for (var i = 0; i < calledMethodParams.Parameters.Count; i++)
                {
                    var curParam = calledMethodParams.Parameters[i];

                    var newVar = "__parameter_" + i;
                    var arg = toReplace.ArgumentList.Arguments[i];
                    var assign = "var " + newVar + " = (" + arg.ToFullString() + ");";

                    var assignSyntax = SyntaxFactory.ParseStatement(assign);
                    localBindings.Add(assignSyntax);

                    var newVarSyntax = SyntaxFactory.IdentifierName(newVar);

                    var referToCurParam = updatedMethodBody.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Where(x => x.Identifier.ValueText == curParam.Identifier.ValueText).ToImmutableArray();

                    updatedMethodBody = updatedMethodBody.ReplaceNodes(referToCurParam, (_, __) => newVarSyntax);
                }

                var allStatements = localBindings.Concat(updatedMethodBody.Statements);
                var allStatementsList = SyntaxFactory.List(allStatements);
                var block = SyntaxFactory.Block(allStatementsList);

                // also avoid collisions by renaming any other variables introduced
                var variableDeclares = block.DescendantNodesAndSelf().OfType<VariableDeclaratorSyntax>().Select(v => v.Identifier.ValueText).Where(v => !v.StartsWith("__")).ToImmutableHashSet();
                var variableDesignates = block.DescendantNodesAndSelf().OfType<SingleVariableDesignationSyntax>().Select(v => v.Identifier.ValueText).Where(v => !v.StartsWith("__")).ToImmutableHashSet();

                var allVariables = variableDeclares.Union(variableDesignates);

                foreach (var variable in allVariables)
                {
                    var referToVariable = block.DescendantTokens().Where(t => t.ValueText == variable).ToImmutableArray();
                    var newVarToken = SyntaxFactory.ParseToken("__" + variable);

                    block = block.ReplaceTokens(referToVariable, (_, __) => newVarToken);
                }

                block = block.NormalizeWhitespace();

                ret = ret.ReplaceNode(toReplaceRet, block);
            }

            return ret;
        }

        internal static T InlineIfInnerCalls<T>(T toReplaceIn, TypeDeclarationSyntax referencesTo)
            where T : SyntaxNode
        {
            var ident = referencesTo.Identifier.ValueText;

            var needReplace =
                toReplaceIn
                    .DescendantNodesAndSelf()
                    .OfType<IfStatementSyntax>()
                    .Where(
                        ret =>
                        {
                            if (!(ret.Condition is InvocationExpressionSyntax i))
                            {
                                return false;
                            }

                            if (i.Expression is MemberAccessExpressionSyntax access)
                            {
                                if (access.Expression is SimpleNameSyntax name && name.Identifier.ValueText == ident)
                                {
                                    return true;
                                }
                            }

                            return false;
                        }
                    )
                    .ToImmutableArray();

            var toReplaceWith = ImmutableDictionary.CreateBuilder<IfStatementSyntax, BlockSyntax>();
            var toReplaceVariables = ImmutableDictionary.CreateBuilder<string, string>();

            foreach (var toReplaceRet in needReplace)
            {
                var toReplace = (InvocationExpressionSyntax)NonNull(toReplaceRet.Condition);

                var calledMethodName = ((MemberAccessExpressionSyntax)toReplace.Expression).Name.Identifier.ValueText;
                var calledMethod = referencesTo.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == calledMethodName);
                var calledMethodBody = NonNull(calledMethod.Body);

                var resVar = SyntaxFactory.IdentifierName("__resultOf_" + calledMethodName);
                var resVarDecl = SyntaxFactory.ParseStatement("System.Boolean " + resVar.Identifier + ";");

                var labelName = SyntaxFactory.IdentifierName("__callDone");
                var gotoCallDone = SyntaxFactory.GotoStatement(SyntaxKind.GotoStatement, labelName);
                var gotoLabel = SyntaxFactory.ParseStatement(labelName.Identifier.ValueText + ":");

                var paramsInMethod = calledMethod.ParameterList.Parameters.Select(p => p.Identifier.ValueText).ToImmutableArray();
                var argsToMethod = toReplace.ArgumentList.Arguments.Select(a => a).ToImmutableArray();

                var outParamIndexes = calledMethod.ParameterList.Parameters.Select((a, ix) => (Argument: a, Index: ix)).Where(t => t.Argument.Modifiers.Any(m => m.ValueText == "out")).Select(t => t.Index).ToImmutableArray();

                foreach (var ix in outParamIndexes)
                {
                    var arg = toReplace.ArgumentList.Arguments[ix];
                    var argExp = arg.Expression;
                    var argExpIdent = argExp.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Where(v => v.ValueText != "var").ToImmutableArray();
                    var outVar = argExpIdent.Single().ValueText;
                    toReplaceVariables.Add(outVar, "__p_" + ix);
                }

                var paramsInMethodToNewParamsBuilder = ImmutableDictionary.CreateBuilder<string, SyntaxNode>();
                for (var i = 0; i < paramsInMethod.Length; i++)
                {
                    var oldP = paramsInMethod[i];
                    var newP = "__p_" + i;
                    paramsInMethodToNewParamsBuilder.Add(oldP, SyntaxFactory.IdentifierName(newP));
                }

                var paramsInMethodToNewParams = paramsInMethodToNewParamsBuilder.ToImmutable();

                var variableUsesInCalledMethodBody =
                    calledMethodBody
                        .DescendantNodesAndSelf()
                        .OfType<IdentifierNameSyntax>()
                        .Where(x => paramsInMethodToNewParamsBuilder.ContainsKey(x.Identifier.ValueText))
                        .ToImmutableArray();

                var calledMethodBodyWithNewPs =
                    calledMethodBody
                        .ReplaceSyntax(
                            variableUsesInCalledMethodBody,
                            (old, partialRewrite) => paramsInMethodToNewParams[((IdentifierNameSyntax)old).Identifier.ValueText].WithTriviaFrom(partialRewrite),
                            Enumerable.Empty<SyntaxToken>(),
                            (a, b) => b,
                            Enumerable.Empty<SyntaxTrivia>(),
                            (a, b) => b
                        );

                var retExprs = calledMethodBodyWithNewPs.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>();

                var bodyWithoutRes =
                    calledMethodBodyWithNewPs
                        .ReplaceSyntax(
                            retExprs,
                            (_, old) =>
                            {
                                var oldRet = (ReturnStatementSyntax)old;

                                var oldRetResult = NonNull(oldRet.Expression);

                                var assignmentStatement = SyntaxFactory.ParseStatement(resVar.Identifier + " = " + oldRetResult.ToFullString() + ";");
                                assignmentStatement = assignmentStatement.WithTriviaFrom(oldRet);

                                var withGoto = SyntaxFactory.List(new StatementSyntax[] { assignmentStatement, gotoCallDone });

                                var block = SyntaxFactory.Block(withGoto);

                                return block;
                            },
                            Enumerable.Empty<SyntaxToken>(),
                            (a, b) => b,
                            Enumerable.Empty<SyntaxTrivia>(),
                            (a, b) => b
                        );

                var ifWithVar =
                    toReplaceRet
                        .ReplaceSyntax(
                            new[] { toReplace },
                            (_, old) => resVar.WithTriviaFrom(old),
                            Enumerable.Empty<SyntaxToken>(),
                            (a, b) => b,
                            Enumerable.Empty<SyntaxTrivia>(),
                            (a, b) => b
                        );

                var finalNodes = ImmutableArray.CreateBuilder<SyntaxNode>();

                for (var i = 0; i < paramsInMethod.Length; i++)
                {
                    var oldPDecl = calledMethod.ParameterList.Parameters[i];
                    var oldP = paramsInMethod[i];
                    var newP = paramsInMethodToNewParams[oldP];

                    var type = NonNull(oldPDecl.Type).ToFullString();

                    var initExpression = argsToMethod[i];
                    string initExpressionString;
                    if (initExpression.RefKindKeyword.ValueText == "out")
                    {
                        initExpressionString = "default";
                    }
                    else if (initExpression.RefKindKeyword.ValueText == "in")
                    {
                        initExpressionString = initExpression.Expression.ToFullString();
                    }
                    else
                    {
                        initExpressionString = initExpression.ToFullString();
                    }

                    var statement = SyntaxFactory.ParseStatement(type + " " + newP.ToFullString() + " = (" + initExpressionString + ");");

                    finalNodes.Add(statement);
                }

                finalNodes.AddRange(resVarDecl, bodyWithoutRes, gotoLabel, ifWithVar);

                var replacementBlock = SyntaxFactory.Block(SyntaxFactory.List(finalNodes.ToImmutable()));

                toReplaceWith.Add(toReplaceRet, replacementBlock);
            }

            var replacementMap = toReplaceWith.ToImmutable();

            var ifReplaced =
                toReplaceIn.ReplaceSyntax(
                    replacementMap.Keys,
                    (old, _) =>
                    {
                        var newBlock = replacementMap[(IfStatementSyntax)old];

                        return newBlock;
                    },
                    Enumerable.Empty<SyntaxToken>(),
                    (a, b) => b,
                    Enumerable.Empty<SyntaxTrivia>(),
                    (a, b) => b
                );

            var variableReplacementMap = toReplaceVariables.ToImmutable();

            var tokensMayNeedReplacement =
                ifReplaced
                    .DescendantTokens()
                    .Where(t => variableReplacementMap.ContainsKey(t.ValueText))
                    .Where(t => !replacementMap.Values.Any(v => v.DescendantTokens().Contains(t)))
                    .ToImmutableArray();

            var ret =
                ifReplaced.ReplaceTokens(
                    tokensMayNeedReplacement,
                    (old, partialRewrite) =>
                    {
                        var newTokenStr = variableReplacementMap[old.ValueText];
                        var newToken = SyntaxFactory.ParseToken(newTokenStr);

                        return newToken.WithTriviaFrom(partialRewrite);
                    }
                );

            return ret;
        }

        internal static T InlineTailCalls<T>(T toReplaceIn, TypeDeclarationSyntax referencesTo)
            where T : SyntaxNode
        {
            var ident = referencesTo.Identifier.ValueText;

            var needReplace =
                    toReplaceIn
                        .DescendantNodesAndSelf()
                        .OfType<ReturnStatementSyntax>()
                        .Where(
                            ret =>
                            {
                                if (!(ret.Expression is InvocationExpressionSyntax i))
                                {
                                    return false;
                                }

                                if (i.Expression is MemberAccessExpressionSyntax access)
                                {
                                    if (access.Expression is SimpleNameSyntax name && name.Identifier.ValueText == ident)
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }
                        )
                        .ToImmutableArray();

            var toReplaceWith = ImmutableDictionary.CreateBuilder<ReturnStatementSyntax, (ParameterListSyntax Parameters, BlockSyntax Statements)>();

            foreach (var toReplaceRet in needReplace)
            {
                var toReplace = (InvocationExpressionSyntax)NonNull(toReplaceRet.Expression);

                var calledMethodName = ((MemberAccessExpressionSyntax)toReplace.Expression).Name.Identifier.ValueText;
                var calledMethod = referencesTo.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == calledMethodName);

                var calledMethodBody = NonNull(calledMethod.Body);
                toReplaceWith.Add(toReplaceRet, (calledMethod.ParameterList, calledMethodBody));
            }

            var ret = ReplaceIn(toReplaceIn, toReplaceWith.ToImmutable());

            return ret;
        }

        internal static string RemoveUnusedUsings(Compilation compilation, string source)
        {
            const string UNUSED_USING = "CS8019";

            var existingTree = compilation.SyntaxTrees.ElementAtOrDefault(0);
            var options = existingTree?.Options as CSharpParseOptions;

            if (options == null)
            {
                return source;
            }

            var syntaxTree = CSharpSyntaxTree.ParseText(source, options: options, encoding: Encoding.UTF8);
            var syntax = (CompilationUnitSyntax)syntaxTree.GetRoot();

            var withFile = compilation.AddSyntaxTrees(syntaxTree);
            var model = withFile.GetSemanticModel(syntaxTree);

            var diags = model.GetDiagnostics();
            var relevantDiags = diags.Where(i => i.Id == UNUSED_USING).ToImmutableArray();
            var usingsToRemove = syntax.Usings.Where(u => relevantDiags.Any(d => u.Span.Contains(d.Location.SourceSpan))).ToImmutableArray();

            var updatedSyntax = NonNull(syntax.RemoveNodes(usingsToRemove, SyntaxRemoveOptions.KeepNoTrivia));
            updatedSyntax = updatedSyntax.NormalizeWhitespace();

            return updatedSyntax.ToFullString();
        }

        internal static ImmutableArray<T> SortColumns<T>(ImmutableArray<T> columns, Func<T, int?> getOrder)
        {
            return
                columns.Sort(
                    (a, b) =>
                    {
                        var aCurIx = columns.IndexOf(a);
                        var bCurIx = columns.IndexOf(b);

                        var aOrder = getOrder(a);
                        var bOrder = getOrder(b);

                        // if equal, preserve discovered order
                        if (aOrder == bOrder)
                        {
                            return aCurIx.CompareTo(bCurIx);
                        }

                        // sort nulls to the end
                        if (aOrder == null)
                        {
                            return 1;
                        }

                        if (bOrder == null)
                        {
                            return -1;
                        }

                        return aOrder.Value.CompareTo(bOrder.Value);
                    }
                );
        }

        private static readonly uint HashCodeSeed = GenerateHashCodeSeed();

        private static uint GenerateHashCodeSeed()
        {
            var buff = new byte[4];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(buff);
                return BitConverter.ToUInt32(buff, 0);
            }
        }

        internal static int HashCode<T1, T2, T3, T4, T5, T6>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6)
        {
            // based on .NET's HashCode class (available in netstandard2.1+)

            const uint PRIME1 = 2654435761U;
            const uint PRIME2 = 2246822519U;
            const uint PRIME3 = 3266489917U;
            const uint PRIME4 = 668265263U;

            var h1 = (uint)(t1?.GetHashCode() ?? 0);
            var h2 = (uint)(t2?.GetHashCode() ?? 0);
            var h3 = (uint)(t3?.GetHashCode() ?? 0);
            var h4 = (uint)(t4?.GetHashCode() ?? 0);
            var h5 = (uint)(t5?.GetHashCode() ?? 0);
            var h6 = (uint)(t6?.GetHashCode() ?? 0);

            Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

            v1 = Round(v1, h1);
            v2 = Round(v2, h2);
            v3 = Round(v3, h3);
            v4 = Round(v4, h4);

            uint hash = MixState(v1, v2, v3, v4);
            hash += 24;

            hash = QueueRound(hash, h5);
            hash = QueueRound(hash, h6);

            hash = MixFinal(hash);
            return (int)hash;

            static void Initialize(out uint v1, out uint v2, out uint v3, out uint v4)
            {
                v1 = HashCodeSeed + PRIME1 + PRIME2;
                v2 = HashCodeSeed + PRIME2;
                v3 = HashCodeSeed;
                v4 = HashCodeSeed - PRIME1;
            }

            static uint MixState(uint v1, uint v2, uint v3, uint v4)
            {
                return RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
            }

            static uint RotateLeft(uint value, int offset)
            {
                return (value << offset) | (value >> (32 - offset));
            }

            static uint Round(uint hash, uint input)
            {
                return RotateLeft(hash + input * PRIME2, 13) * PRIME1;
            }

            static uint QueueRound(uint hash, uint queuedValue)
            {
                return RotateLeft(hash + queuedValue * PRIME3, 17) * PRIME4;
            }

            static uint MixFinal(uint hash)
            {
                hash ^= hash >> 15;
                hash *= PRIME2;
                hash ^= hash >> 13;
                hash *= PRIME3;
                hash ^= hash >> 16;
                return hash;
            }
        }

        public static T MakeNonGenericType<T>(T template, string genericParameterName, string concreteTypeName)
            where T: TypeDeclarationSyntax
        {
            // strip out type parameters
            var nonGeneric =
                template
                    .WithConstraintClauses(SyntaxFactory.List<TypeParameterConstraintClauseSyntax>())
                    .WithTypeParameterList(null);

            var concreteTypeNameSyntax = SyntaxFactory.ParseTypeName(concreteTypeName);

            // replace T with the actual enum type
            var mentionsOfT = nonGeneric.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Where(t => t.Identifier.ValueText == genericParameterName).ToList();
            nonGeneric = nonGeneric.ReplaceNodes(mentionsOfT, (_, __) => concreteTypeNameSyntax);

            return (T)nonGeneric;
        }
    }
}
