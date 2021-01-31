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

        internal static readonly Func<SyntaxToken, SyntaxToken, SyntaxToken> TakeUpdatedToken = (a, b) => b;
        internal static readonly Func<SyntaxTrivia, SyntaxTrivia, SyntaxTrivia> TakeUpdatedTrivia = (a, b) => b;

        internal static bool IsNormalOrByRef(this RefKind refKind)
        => refKind == RefKind.None || refKind == RefKind.Ref;

        // todo: can this be made to handle InternalsVisibleTo?
        internal static bool IsAccessible(this IMethodSymbol mtd, AttributedMembers attrMembers)
        => mtd.DeclaredAccessibility == Accessibility.Public ||
            (attrMembers.CompilingAssembly.Equals(mtd.ContainingAssembly, SymbolEqualityComparer.Default) &&
             mtd.DeclaredAccessibility == Accessibility.Internal);

        internal static bool ShouldInclude(this IPropertySymbol prop, ImmutableArray<AttributeSyntax> attrs)
        => (prop.DeclaredAccessibility == Accessibility.Public && !prop.IsStatic) ||
            !attrs.IsEmpty;

        internal static (bool IsRecord, IMethodSymbol? PrimaryConstructor, ImmutableArray<IPropertySymbol> RecordDeclaredProperties) IsRecord(this INamedTypeSymbol symbol)
        {
            var isRecord = symbol.DeclaringSyntaxReferences.Where(x => x.GetSyntax().ParentOrSelfOfType<RecordDeclarationSyntax>() != null).Any();

            if (!isRecord)
            {
                return (false, null, ImmutableArray<IPropertySymbol>.Empty);
            }

            var cons = symbol.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Constructor).ToImmutableArray();
            var consRefs = cons.Select(s => s.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).ToImmutableArray()).ToImmutableArray();

            var primaryCons = cons.SingleOrDefault(c => c.DeclaredAccessibility == Accessibility.Public && c.DeclaringSyntaxReferences.Any(x => x.GetSyntax() is RecordDeclarationSyntax));

            primaryCons ??= cons.Single(c => c.Parameters.Length == 0);

            var recordDeclaredProperties = GetRecordDeclaredProperties(symbol);

            return (true, primaryCons, recordDeclaredProperties);
        }

        private static ImmutableArray<IPropertySymbol> GetRecordDeclaredProperties(INamedTypeSymbol symbol)
        {
            var ret = ImmutableArray.CreateBuilder<IPropertySymbol>();

            if (symbol.BaseType != null)
            {
                foreach (var prop in GetRecordDeclaredProperties(symbol.BaseType))
                {
                    ret.Add(prop);
                }
            }

            var primaryCons =
                symbol
                    .GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Constructor)
                    .SingleOrDefault(c => c.DeclaredAccessibility == Accessibility.Public && c.DeclaringSyntaxReferences.Any(x => x.GetSyntax() is RecordDeclarationSyntax));

            var declaredProps = symbol.GetMembers().OfType<IPropertySymbol>().ToImmutableArray();

            if (primaryCons != null)
            {
                foreach (var p in primaryCons.Parameters)
                {
                    var correspondingProperty = declaredProps.Single(s => s.Name == p.Name);
                    ret.Add(correspondingProperty);
                }
            }

            return ret.ToImmutable();
        }

        internal static ImmutableArray<ISymbol> GetMembersIncludingInherited(this INamedTypeSymbol symbol)
        {
            var ret = ImmutableArray.CreateBuilder<ISymbol>();

            if (symbol.BaseType != null)
            {
                ret.AddRange(GetMembersIncludingInherited(symbol.BaseType));
            }

            var members = symbol.GetMembers();
            ret.AddRange(members);

            return ret.ToImmutable();
        }

        internal static bool IsFlagsEnum(FrameworkTypes types, ITypeSymbol forEnum)
        {
            var attrs = forEnum.GetAttributes();
            foreach (var i in attrs)
            {
                var attrClass = i.AttributeClass;

                if (types.FlagsAttribute.Equals(attrClass, SymbolEqualityComparer.Default))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string ToFullyQualifiedName(this ITypeSymbol symbol)
        {
            if (symbol.TypeKind == TypeKind.Struct && symbol is INamedTypeSymbol maybeNullableEnum && maybeNullableEnum.Arity == 1)
            {
                var typeDecl = maybeNullableEnum.ConstructedFrom;
                if (typeDecl.SpecialType == SpecialType.System_Nullable_T)
                {
                    var arg = maybeNullableEnum.TypeArguments.Single();
                    return arg.ToFullyQualifiedName() + "?";
                }
            }

            return symbol.SpecialType switch
            {
                SpecialType.System_IntPtr => "System.IntPtr",
                SpecialType.System_UIntPtr => "System.UIntPtr",
                _ => symbol.ToDisplayString(FullyQualifiedFormat)
            };
        }

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

        internal static ImmutableArray<T> GetConstantsWithName<T>(
            AttributedMembers attrMembers, 
            ImmutableArray<AttributeSyntax> attrs, 
            string name, 
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var ret = ImmutableArray<T>.Empty;

            foreach (var attr in attrs)
            {
                if(!attrMembers.AttributeToConstantValues.TryGetValue(attr, out var constValues))
                {
                    continue;
                }

                if(!constValues.TryGetValue(name, out var values))
                {
                    continue;
                }

                foreach (var (expression, trueValue) in values)
                {
                    if (!trueValue.HasValue)
                    {
                        var diag = Diagnostics.CouldNotExtractConstantValue(expression.GetLocation());
                        diags = diags.Add(diag);
                        continue;
                    }

                    if (trueValue.Value is T asT)
                    {
                        ret = ret.Add(asT);
                    }
                    else
                    {
                        var actualType = trueValue.Value?.GetType().GetTypeInfo();
                        var diag = Diagnostics.UnexpectedConstantValueType(expression.GetLocation(), new[] { typeof(T).GetTypeInfo() }, actualType);
                        diags = diags.Add(diag);
                        continue;
                    }
                }
            }

            return ret;
        }

        internal static (ImmutableArray<T1> First, ImmutableArray<T2> Second) GetConstantsWithName<T1, T2>(
            AttributedMembers attrMembers,
            ImmutableArray<AttributeSyntax> attrs,
            string name, 
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var ret1 = ImmutableArray<T1>.Empty;
            var ret2 = ImmutableArray<T2>.Empty;

            foreach (var attr in attrs)
            {
                if (!attrMembers.AttributeToConstantValues.TryGetValue(attr, out var constValues))
                {
                    continue;
                }

                if (!constValues.TryGetValue(name, out var values))
                {
                    continue;
                }

                foreach (var (expression, trueValue)  in values)
                {
                    if (!trueValue.HasValue)
                    {
                        var diag = Diagnostics.CouldNotExtractConstantValue(expression.GetLocation());
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
                        var actualType = trueValue.Value?.GetType().GetTypeInfo();
                        var diag = Diagnostics.UnexpectedConstantValueType(expression.GetLocation(), new[] { typeof(T1).GetTypeInfo(), typeof(T2).GetTypeInfo() }, actualType);
                        diags = diags.Add(diag);
                        continue;
                    }
                }
            }

            return (ret1, ret2);
        }

        internal static int? GetOrderFromAttributes(
            AttributedMembers attrMembers,
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
                if(!attrMembers.AttributeSyntaxToAttributeType.TryGetValue(attr, out var type))
                {
                    continue;
                }

                if (cesilMemberAttr.Equals(type, SymbolEqualityComparer.Default))
                {
                    var value = GetConstantsWithName<int>(attrMembers, ImmutableArray.Create(attr), "Order", ref diags);
                    foreach (var val in value)
                    {
                        values.Add(val);
                    }
                }
                else if (dataMemberAttr != null && dataMemberAttr.Equals(type, SymbolEqualityComparer.Default))
                {
                    var value = GetConstantsWithName<int>(attrMembers, ImmutableArray.Create(attr), "Order", ref diags);
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
            AttributedMembers attrMembers,
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
            var types = GetTypeConstantWithName(attrMembers, attrs, typeNameProperty, ref diags);
            if (types.Length > 1)
            {
                var diag = multipleTypeDefinitionDiagnostic(location);
                diags = diags.Add(diag);

                return null;
            }

            var type = types.SingleOrDefault();

            var methods = GetConstantsWithName<string>(attrMembers, attrs, methodNameProperty, ref diags);
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

        internal static ImmutableArray<INamedTypeSymbol> GetTypeConstantWithName(
            AttributedMembers attrMembers,
            ImmutableArray<AttributeSyntax> attrs, 
            string name, 
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var ret = ImmutableArray<INamedTypeSymbol>.Empty;

            foreach (var attr in attrs)
            {
                if (!attrMembers.AttributeToConstantValues.TryGetValue(attr, out var constValues))
                {
                    continue;
                }

                if (!constValues.TryGetValue(name, out var values))
                {
                    continue;
                }

                foreach (var (expression, value) in values)
                {
                    if(!value.HasValue || !(value.Value is INamedTypeSymbol namedType))
                    {
                        var diag = Diagnostics.CouldNotExtractConstantValue(expression.GetLocation());
                        diags = diags.Add(diag);
                        continue;
                    }

                    ret = ret.Add(namedType);
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

        internal static string? GetNameFromAttributes(
            AttributedMembers attrMembers,
            Location? location, 
            ImmutableArray<AttributeSyntax> attrs, 
            ref ImmutableArray<Diagnostic> diags
        )
        {
            var names = GetConstantsWithName<string>(attrMembers, attrs, "Name", ref diags);

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
            var nodesToReplaceBuilder = ImmutableDictionary.CreateBuilder<ReturnStatementSyntax, BlockSyntax>();

            var nextParameterIndex =
                NonNullValue(
                    toReplaceIn
                        .DescendantTokens()
                        .Select(
                            x =>
                            {
                                var valText = x.ValueText;
                                if (!valText.StartsWith("__parameter_"))
                                {
                                    return default(int?);
                                }

                                var tail = valText.Substring("__parameter_".Length);
                                return int.Parse(tail);
                            }
                        )
                        .Where(x => x != null)
                        .Concat(new int?[] { -1 })
                        .Max()
                ) + 1;

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

                    var newVar = "__parameter_" + nextParameterIndex;
                    nextParameterIndex++;

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

                nodesToReplaceBuilder.Add(toReplaceRet, block);
            }

            var nodesToReplace = nodesToReplaceBuilder.ToImmutable();

            var ret = toReplaceIn.ReplaceNodes(nodesToReplace.Keys, (old, _) => nodesToReplace[old]);

            return NonNull(ret);
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

            var nextParameterIndex =
                NonNullValue(
                    toReplaceIn
                        .DescendantTokens()
                        .Select(
                            x =>
                            {
                                var valText = x.ValueText;
                                if (!valText.StartsWith("__parameter_"))
                                {
                                    return default(int?);
                                }

                                var tail = valText.Substring("__parameter_".Length);
                                return int.Parse(tail);
                            }
                        )
                        .Where(x => x != null)
                        .Concat(new int?[] { -1 })
                        .Max()
                ) + 1;

            var nextDoneIndex =
                NonNullValue(
                    toReplaceIn
                        .DescendantTokens()
                        .Select(
                            x =>
                            {
                                var valText = x.ValueText;
                                if (!valText.StartsWith("__callDone_"))
                                {
                                    return default(int?);
                                }

                                var tail = valText.Substring("__callDone_".Length);
                                return int.Parse(tail);
                            }
                        )
                        .Where(x => x != null)
                        .Concat(new int?[] { -1 })
                        .Max()
                ) + 1;

            var toReplaceWith = ImmutableDictionary.CreateBuilder<IfStatementSyntax, BlockSyntax>();
            var toReplaceVariables = ImmutableDictionary.CreateBuilder<string, string>();

            var labelName = SyntaxFactory.IdentifierName("__callDone_" + nextDoneIndex);

            foreach (var toReplaceRet in needReplace)
            {
                var toReplace = (InvocationExpressionSyntax)NonNull(toReplaceRet.Condition);

                var calledMethodName = ((MemberAccessExpressionSyntax)toReplace.Expression).Name.Identifier.ValueText;
                var calledMethod = referencesTo.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == calledMethodName);
                var calledMethodBody = NonNull(calledMethod.Body);

                var resVar = SyntaxFactory.IdentifierName("__resultOf_" + calledMethodName);
                var resVarDecl = SyntaxFactory.ParseStatement("System.Boolean " + resVar.Identifier + ";");

                var gotoCallDone = SyntaxFactory.GotoStatement(SyntaxKind.GotoStatement, labelName);
                var gotoLabel = SyntaxFactory.ParseStatement(labelName.Identifier.ValueText + ":");

                var paramsInMethod = calledMethod.ParameterList.Parameters.Select(p => p.Identifier.ValueText).ToImmutableArray();
                var argsToMethod = toReplace.ArgumentList.Arguments.Select(a => a).ToImmutableArray();

                var outParamIndexes = calledMethod.ParameterList.Parameters.Select((a, ix) => (Argument: a, Index: ix)).Where(t => t.Argument.Modifiers.Any(m => m.ValueText == "out")).Select(t => t.Index).ToImmutableArray();

                var paramsInMethodToNewParamsBuilder = ImmutableDictionary.CreateBuilder<string, SyntaxNode>();
                for (var i = 0; i < paramsInMethod.Length; i++)
                {
                    var oldP = paramsInMethod[i];

                    var newP = "__parameter_" + nextParameterIndex;
                    nextParameterIndex++;

                    paramsInMethodToNewParamsBuilder.Add(oldP, SyntaxFactory.IdentifierName(newP));
                }

                var paramsInMethodToNewParams = paramsInMethodToNewParamsBuilder.ToImmutable();

                foreach (var ix in outParamIndexes)
                {
                    var arg = toReplace.ArgumentList.Arguments[ix];
                    var pForArg = paramsInMethod[ix];
                    var argExp = arg.Expression;
                    var argExpIdent = argExp.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken)).Where(v => v.ValueText != "var").ToImmutableArray();
                    var outVar = argExpIdent.Single().ValueText;

                    var mapsTo = paramsInMethodToNewParams[pForArg];

                    toReplaceVariables.Add(outVar, mapsTo.ToFullString());
                }

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
                            TakeUpdatedToken,
                            Enumerable.Empty<SyntaxTrivia>(),
                            TakeUpdatedTrivia
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
                            TakeUpdatedToken,
                            Enumerable.Empty<SyntaxTrivia>(),
                            TakeUpdatedTrivia
                        );

                var ifWithVar =
                    toReplaceRet
                        .ReplaceSyntax(
                            new[] { toReplace },
                            (_, old) => resVar.WithTriviaFrom(old),
                            Enumerable.Empty<SyntaxToken>(),
                            TakeUpdatedToken,
                            Enumerable.Empty<SyntaxTrivia>(),
                            TakeUpdatedTrivia
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
                    TakeUpdatedToken,
                    Enumerable.Empty<SyntaxTrivia>(),
                    TakeUpdatedTrivia
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

            // recurse if we made any changes to handle new references entered
            if (!ret.Equals(toReplaceIn))
            {
                return InlineIfInnerCalls(ret, referencesTo);
            }

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

            // do another pass, incase anything with inlined itself has relevant tail calls
            if (!ret.Equals(toReplaceIn))
            {
                return InlineTailCalls(ret, referencesTo);
            }

            return ret;
        }

        internal static string RemoveUnusedUsings(Compilation compilation, string source)
        {
            const string UNUSED_USING = "CS8019";

            var existingTree = compilation.SyntaxTrees.ElementAt(0);
            var options = NonNull(existingTree.Options as CSharpParseOptions);

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
            where T : TypeDeclarationSyntax
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
