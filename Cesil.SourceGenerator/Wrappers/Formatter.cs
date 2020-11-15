using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cesil.SourceGenerator
{
    internal sealed class Formatter
    {
        private static readonly ImmutableDictionary<string, Formatter> DefaultLookup = BuildDefaultLookup();

        private static readonly ImmutableHashSet<string> RemoveEnumFormatterMethods =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "CreateTryEnumFormatter",
                    "CreateTryNullableEnumFormatter",
                    "GetFormattingClass",
                    "CreateTryFlagsEnumFormatter",
                    "CreateTryBasicEnumFormatter",
                    "CreateTryNullableFlagsEnumFormatter",
                    "CreateTryNullableBasicEnumFormatter"
                }
            );
        private static readonly ImmutableHashSet<string> RemoveEnumFormatterFields =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "TryEnumFormatter",
                    "TryNullableEnumFormatter"
                }
            );
        private static readonly ImmutableHashSet<string> RemoveEnumFormatterMethodsWhenNotFlags =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "CreateNames",
                    "GetMaxNameLength",
                    "CreateValues",
                    "FormatFlagsEnumImpl"
                }
            );
        private static readonly ImmutableHashSet<string> RemoveEnumFormatterFieldsWhenNotFlags =
            ImmutableHashSet.CreateRange(
                new[]
                {
                    "Names",
                    "Values",
                    "MaxNameLength"
                }
            );
        private static readonly ClassDeclarationSyntax EnumTypeFormatterTemplate = GetEnumTypeFormatterTemplate();

        internal readonly bool IsDefault;
        internal readonly bool DefaultIsMethod;
        internal readonly string? ForDefaultType;
        internal readonly string? DefaultCode;

        internal readonly IMethodSymbol? Method;
        internal readonly ITypeSymbol? TakesType;

        internal Formatter(IMethodSymbol method, ITypeSymbol takesType)
        {
            IsDefault = false;
            DefaultIsMethod = false;
            ForDefaultType = null;
            DefaultCode = null;

            Method = method;
            TakesType = takesType;
        }

        internal Formatter(bool isMethod, string forDefaultType, string defaultCode)
        {
            IsDefault = true;
            DefaultIsMethod = isMethod;
            ForDefaultType = forDefaultType;
            DefaultCode = defaultCode;

            Method = null;
            TakesType = null;
        }

        internal static bool TryGetDefault(SerializerTypes types, ITypeSymbol forType, [NotNullWhen(returnValue: true)] out Formatter? formatter)
        {
            if (forType.TypeKind == TypeKind.Enum)
            {
                return TryCreateEnumDefaultFormatter(types.Framework, forType, out formatter);
            }

            if (forType.TypeKind == TypeKind.Struct && forType is INamedTypeSymbol maybeNullableEnum && maybeNullableEnum.Arity == 1)
            {
                var typeDecl = maybeNullableEnum.ConstructedFrom;
                if (typeDecl.Equals(types.BuiltIn.NullableOfT, SymbolEqualityComparer.Default))
                {
                    var arg = maybeNullableEnum.TypeArguments.Single();
                    if (arg.TypeKind == TypeKind.Enum)
                    {
                        return TryCreateNullableEnumDefaultFormatter(types.Framework, forType, out formatter);
                    }
                }
            }

            var key = forType.ToFullyQualifiedName();

            return DefaultLookup.TryGetValue(key, out formatter);
        }

        private static bool TryCreateNullableEnumDefaultFormatter(FrameworkTypes types, ITypeSymbol forNullableEnum, [NotNullWhen(returnValue: true)] out Formatter? formatter)
        {
            var forEnum = ((INamedTypeSymbol)forNullableEnum).TypeArguments.Single();
            if (!TryCreateEnumDefaultFormatter(types, forEnum, out var nonNullFormatter))
            {
                formatter = null;
                return false;
            }

            var isFlags = IsFlagsEnum(types, forEnum);
            var forEnumFullyQualified = forEnum.ToFullyQualifiedName();
            var forNullableEnumFullyQualified = forNullableEnum.ToFullyQualifiedName();

            // get the formatter method for the nullable enum
            var nonGeneric = MakeNonGenericDefaultTypeFormatter(forEnumFullyQualified);
            var nullableMtd =
                nonGeneric
                    .Members
                    .OfType<MethodDeclarationSyntax>()
                    .Single(m => isFlags ? m.Identifier.ValueText == "TryFormatNullableFlagsEnum" : m.Identifier.ValueText == "TryFormatNullableBasicEnum");

            // get the formatter method for the NON-NULLABLE enum
            var nonNullFormatterParsed =
                    Utils.NonNull(SyntaxFactory.ParseMemberDeclaration(Utils.NonNull(nonNullFormatter.DefaultCode)));
            var nonNullFormatterMtd = nonNullFormatterParsed.DescendantNodesAndSelf().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "__TryFormat");
            var nonNullMtdBody = Utils.NonNull(nonNullFormatterMtd.Body);

            // rewrite things to produce final code
            var replaceRet = nullableMtd.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>().Last();
            var toReplace = ImmutableDictionary.CreateBuilder<ReturnStatementSyntax, (ParameterListSyntax Parameters, BlockSyntax Statements)>();
            toReplace.Add(replaceRet, (nonNullFormatterMtd.ParameterList, nonNullMtdBody));

            bool isMethod;
            SyntaxNode nullableFinal;
            if (nonNullFormatter.DefaultIsMethod)
            {
                // just a method, simple inlining is sufficient
                isMethod = true;

                nullableFinal = ReplaceIn(nullableMtd, toReplace.ToImmutable());
            }
            else
            {
                // whole class, so we need to inline and remove the old method
                isMethod = false;

                var nonNullClass = (ClassDeclarationSyntax)nonNullFormatterParsed;
                var nonNullClassWithoutTryFormat = Utils.NonNull(nonNullClass.RemoveNode(nonNullFormatterMtd, SyntaxRemoveOptions.KeepNoTrivia));

                var newNullableMethod =
                    ReplaceIn(nullableMtd, toReplace.ToImmutable())
                        .WithIdentifier(SyntaxFactory.ParseToken("__TryFormat"))
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));

                nullableFinal = nonNullClassWithoutTryFormat.AddMembers(newNullableMethod);
            }

            nullableFinal = nullableFinal.NormalizeWhitespace();

            var code = nullableFinal.ToFullString();

            formatter = new Formatter(isMethod, forNullableEnumFullyQualified, code);
            return true;
        }

        private static bool IsFlagsEnum(FrameworkTypes types, ITypeSymbol forEnum)
        {
            return forEnum.GetAttributes().Any(i => i.AttributeClass?.Equals(types.FlagsAttribute, SymbolEqualityComparer.Default) ?? false);
        }

        private static bool TryCreateEnumDefaultFormatter(FrameworkTypes types, ITypeSymbol forEnum, [NotNullWhen(returnValue: true)] out Formatter? formatter)
        {
            var isFlags = IsFlagsEnum(types, forEnum);
            var forEnumFullyQualified = forEnum.ToFullyQualifiedName();

            // strip out type parameters
            var nonGeneric = MakeNonGenericDefaultTypeFormatter(forEnumFullyQualified);

            // remove all the stuff we _don't_ need
            var toRemoveMtd =
                nonGeneric
                    .DescendantNodesAndSelf()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(
                        t =>
                       {
                           var val = t.Identifier.ValueText;

                           if (RemoveEnumFormatterMethods.Contains(val))
                           {
                               return true;
                           }

                           if (!isFlags && RemoveEnumFormatterMethodsWhenNotFlags.Contains(val))
                           {
                               return true;
                           }

                           return false;
                       }
                    )
                    .ToList();


            var toRemoveFields =
                nonGeneric
                    .DescendantNodesAndSelf()
                    .OfType<FieldDeclarationSyntax>()
                    .Where(
                        t =>
                            t.Declaration.Variables.Any(
                                x =>
                                {
                                    var val = x.Identifier.ValueText;
                                    if (RemoveEnumFormatterFields.Contains(val))
                                    {
                                        return true;
                                    }

                                    if (!isFlags && RemoveEnumFormatterFieldsWhenNotFlags.Contains(val))
                                    {
                                        return true;
                                    }

                                    return false;
                                }
                            )
                    )
                    .ToList();

            var toRemove = toRemoveMtd.Cast<SyntaxNode>().Concat(toRemoveFields);
            var trimmed = Utils.NonNull(nonGeneric.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepNoTrivia));

            // now grab the right method
            var mtd =
                trimmed
                    .DescendantNodesAndSelf()
                    .OfType<MethodDeclarationSyntax>()
                    .Single(m => isFlags ? m.Identifier.ValueText == "TryFormatFlagsEnum" : m.Identifier.ValueText == "TryFormatBasicEnum");

            // inline anything that needs inlining
            var updatedMtd = InlineTailCalls(mtd, trimmed);

            // construct final class
            var publicFormatMethod =
                updatedMtd
                    .WithIdentifier(SyntaxFactory.ParseToken("__TryFormat"))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));

            var retClass = trimmed.ReplaceNode(mtd, publicFormatMethod);
            retClass = retClass
                .WithIdentifier(SyntaxFactory.ParseToken("__Class"))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));
            retClass = Utils.NonNull(retClass.RemoveNodes(retClass.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText.StartsWith("Try")), SyntaxRemoveOptions.KeepNoTrivia));

            // replace calls to Utils.EnumToULong
            var enumToULongCalls =
                retClass
                    .DescendantNodesAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(i => i.Expression is MemberAccessExpressionSyntax)
                    .Where(i => ((MemberAccessExpressionSyntax)i.Expression).Name.Identifier.ValueText == "EnumToULong")
                    .ToImmutableArray();

            var ulongType = SyntaxFactory.ParseTypeName("ulong");
            retClass =
                retClass.ReplaceNodes(
                    enumToULongCalls,
                    (_, old) =>
                    {
                        var i = old;
                        var ps = i.ArgumentList.Arguments;
                        var cast = SyntaxFactory.CastExpression(ulongType, ps[0].Expression);

                        return cast;
                    }
                );

            // replace calls to Utils.NonNull
            var nonNullCalls =
                retClass
                    .DescendantNodesAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(i => i.Expression is MemberAccessExpressionSyntax)
                    .Where(i => ((MemberAccessExpressionSyntax)i.Expression).Name.Identifier.ValueText == "NonNull")
                    .ToImmutableArray();

            retClass =
                retClass.ReplaceNodes(
                    nonNullCalls,
                    (_, old) =>
                    {
                        var i = old;
                        var ps = i.ArgumentList.Arguments;
                        var val = ps[0].Expression;

                        var eq = SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression, val, SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
                        var throwExp = SyntaxFactory.ParseExpression("throw new InvalidOperationException(\"Expected non-null value, but found null\")");

                        var cond = SyntaxFactory.ConditionalExpression(eq, val, throwExp);
                        var parened = SyntaxFactory.ParenthesizedExpression(cond);

                        return parened;
                    }
                );

            // replace IsFlagsEnum
            var isFlagsEnumCalls =
                retClass
                    .DescendantNodesAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(i => i.Expression is MemberAccessExpressionSyntax)
                    .Where(i => ((MemberAccessExpressionSyntax)i.Expression).Name.Identifier.ValueText == "IsFlagsEnum")
                    .ToImmutableArray();

            var isFlagsEnumReplacement = SyntaxFactory.LiteralExpression(isFlags ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);

            retClass =
                retClass.ReplaceNodes(
                    isFlagsEnumCalls,
                    (_, __) => isFlagsEnumReplacement
                );

            // make FormatFlagsEnumImpl private
            var formatFlagsEnumImpl =
                retClass
                    .Members
                    .OfType<MethodDeclarationSyntax>()
                    .SingleOrDefault(s => s.Identifier.ValueText == "FormatFlagsEnumImpl");

            if (formatFlagsEnumImpl != null)
            {
                var privateFormatFlagsEnumImpl =
                    formatFlagsEnumImpl.WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword)
                        )
                    );

                retClass =
                    retClass.ReplaceNode(
                        formatFlagsEnumImpl,
                        privateFormatFlagsEnumImpl
                    );
            }

            // get the string we're using
            retClass = retClass.NormalizeWhitespace();

            bool isMethod;
            string code;
            if (retClass.Members.Count == 1)
            {
                isMethod = true;
                code = retClass.Members.Single().ToFullString();
            }
            else
            {
                // only include the full class if we're actually using fields or other members
                isMethod = false;
                code = retClass.ToFullString();
            }

            formatter = new Formatter(isMethod, forEnumFullyQualified, code);
            return true;
        }

        private static ClassDeclarationSyntax MakeNonGenericDefaultTypeFormatter(string forEnumFullyQualified)
        {
            // strip out type parameters
            var nonGeneric =
                EnumTypeFormatterTemplate
                    .WithConstraintClauses(SyntaxFactory.List<TypeParameterConstraintClauseSyntax>())
                    .WithTypeParameterList(null);

            var forEnumFullyQualifiedSyntax = SyntaxFactory.ParseTypeName(forEnumFullyQualified);

            // replace T with the actual enum type
            var mentionsOfT = nonGeneric.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Where(t => t.Identifier.ValueText == "T").ToList();
            nonGeneric = nonGeneric.ReplaceNodes(mentionsOfT, (_, __) => forEnumFullyQualifiedSyntax);

            return nonGeneric;
        }

        private static string GetDefaultTypeFormattersSource()
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var cesilDefaults = asm.GetManifestResourceStream("Cesil.SourceGenerator.Resources.DefaultTypeFormatters.cs"))
            using (var reader = new StreamReader(cesilDefaults))
            {
                return reader.ReadToEnd();
            }
        }

        private static ClassDeclarationSyntax GetEnumTypeFormatterTemplate()
        {
            var defaultTypeFormattersCS = GetDefaultTypeFormattersSource();
            var parsedDefaultTypeFormatters = SyntaxFactory.ParseCompilationUnit(defaultTypeFormattersCS);
            return parsedDefaultTypeFormatters.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "DefaultEnumTypeFormatter");
        }

        private static ImmutableDictionary<string, Formatter> BuildDefaultLookup()
        {
            var defaultTypeFormattersCS = GetDefaultTypeFormattersSource();
            var parsedDefaultTypeFormatters = SyntaxFactory.ParseCompilationUnit(defaultTypeFormattersCS);
            var defaultTypeFormatters = parsedDefaultTypeFormatters.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "DefaultTypeFormatters");

            var builder = ImmutableDictionary.CreateBuilder<string, Formatter>();

            // primitives
            Add(builder, typeof(byte).GetTypeInfo(), defaultTypeFormatters, "TryFormatByte");
            Add(builder, typeof(sbyte).GetTypeInfo(), defaultTypeFormatters, "TryFormatSByte");
            Add(builder, typeof(char).GetTypeInfo(), defaultTypeFormatters, "TryFormatChar");
            Add(builder, typeof(short).GetTypeInfo(), defaultTypeFormatters, "TryFormatShort");
            Add(builder, typeof(ushort).GetTypeInfo(), defaultTypeFormatters, "TryFormatUShort");
            Add(builder, typeof(int).GetTypeInfo(), defaultTypeFormatters, "TryFormatInt");
            Add(builder, typeof(uint).GetTypeInfo(), defaultTypeFormatters, "TryFormatUInt");
            Add(builder, typeof(long).GetTypeInfo(), defaultTypeFormatters, "TryFormatLong");
            Add(builder, typeof(ulong).GetTypeInfo(), defaultTypeFormatters, "TryFormatULong");
            Add(builder, typeof(float).GetTypeInfo(), defaultTypeFormatters, "TryFormatFloat");
            Add(builder, typeof(double).GetTypeInfo(), defaultTypeFormatters, "TryFormatDouble");
            Add(builder, typeof(decimal).GetTypeInfo(), defaultTypeFormatters, "TryFormatDecimal");

            // built in structs
            Add(builder, typeof(Index).GetTypeInfo(), defaultTypeFormatters, "TryFormatIndex");
            Add(builder, typeof(Range).GetTypeInfo(), defaultTypeFormatters, "TryFormatRange");
            Add(builder, typeof(Guid).GetTypeInfo(), defaultTypeFormatters, "TryFormatGuid");
            Add(builder, typeof(DateTime).GetTypeInfo(), defaultTypeFormatters, "TryFormatDateTime");
            Add(builder, typeof(DateTimeOffset).GetTypeInfo(), defaultTypeFormatters, "TryFormatDateTimeOffset");
            Add(builder, typeof(TimeSpan).GetTypeInfo(), defaultTypeFormatters, "TryFormatTimeSpan");

            // reference types
            Add(builder, typeof(string).GetTypeInfo(), defaultTypeFormatters, "TryFormatString");
            Add(builder, typeof(Uri).GetTypeInfo(), defaultTypeFormatters, "TryFormatUri");
            Add(builder, typeof(Version).GetTypeInfo(), defaultTypeFormatters, "TryFormatVersion");

            // nullable primitives
            Add(builder, typeof(byte?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableByte");
            Add(builder, typeof(sbyte?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableSByte");
            Add(builder, typeof(char?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableChar");
            Add(builder, typeof(short?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableShort");
            Add(builder, typeof(ushort?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableUShort");
            Add(builder, typeof(int?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableInt");
            Add(builder, typeof(uint?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableUInt");
            Add(builder, typeof(long?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableLong");
            Add(builder, typeof(ulong?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableULong");
            Add(builder, typeof(float?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableFloat");
            Add(builder, typeof(double?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableDouble");
            Add(builder, typeof(decimal?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableDecimal");

            // nullable built in structs
            Add(builder, typeof(Index?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableIndex");
            Add(builder, typeof(Range?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableRange");
            Add(builder, typeof(Guid?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableGuid");
            Add(builder, typeof(DateTime?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableDateTime");
            Add(builder, typeof(DateTimeOffset?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableDateTimeOffset");
            Add(builder, typeof(TimeSpan?).GetTypeInfo(), defaultTypeFormatters, "TryFormatNullableTimeSpan");

            return builder.ToImmutable();

            static void Add(ImmutableDictionary<string, Formatter>.Builder builder, System.Reflection.TypeInfo type, ClassDeclarationSyntax defaultTypeFormattersSyntax, string methodName)
            {
                var methodBody = ExtractMethodBody(defaultTypeFormattersSyntax, methodName);

                string generateTypeName;
                string typeName;
                var underlying = Nullable.GetUnderlyingType(type);
                if (underlying != null)
                {
                    typeName = underlying.FullName + "?";
                    generateTypeName = typeName;
                }
                else
                {
                    typeName = type.FullName;
                    generateTypeName = typeName;
                    if (!type.IsValueType)
                    {
                        generateTypeName += "?";
                    }
                }

                builder.Add(typeName, new Formatter(true, generateTypeName, methodBody));
            }

            static string ExtractMethodBody(ClassDeclarationSyntax defaultTypeFormattersSyntax, string methodName)
            {
                var mtd = defaultTypeFormattersSyntax.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == methodName);

                var updatedMtd = InlineTailCalls(mtd, defaultTypeFormattersSyntax);
                updatedMtd = updatedMtd.NormalizeWhitespace();

                var ret = updatedMtd.ToFullString();

                return ret;
            }
        }

        private static T InlineTailCalls<T>(T toReplaceIn, TypeDeclarationSyntax referencesTo)
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

            var ret = toReplaceIn;

            var toReplaceWith = ImmutableDictionary.CreateBuilder<ReturnStatementSyntax, (ParameterListSyntax Parameters, BlockSyntax Statements)>();

            foreach (var toReplaceRet in needReplace)
            {
                var toReplace = (InvocationExpressionSyntax)Utils.NonNull(toReplaceRet.Expression);

                var calledMethodName = ((MemberAccessExpressionSyntax)toReplace.Expression).Name.Identifier.ValueText;
                var calledMethod = referencesTo.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == calledMethodName);

                var calledMethodBody = Utils.NonNull(calledMethod.Body);
                toReplaceWith.Add(toReplaceRet, (calledMethod.ParameterList, calledMethodBody));
            }

            ret = ReplaceIn(ret, toReplaceWith.ToImmutable());

            return ret;
        }

        private static T ReplaceIn<T>(T toReplaceIn, ImmutableDictionary<ReturnStatementSyntax, (ParameterListSyntax Parameters, BlockSyntax Statements)> replaceWith)
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
    }
}
